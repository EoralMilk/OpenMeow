#region Copyright & License Information
/*
 * Copyright 2007-2022 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using OpenRA.GameRules;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Projectiles
{
	[Desc("Projectile that travels in a straight line or arc.")]
	public class BulletInfo : CorporealProjectileInfo, IProjectileInfo, IRulesetLoaded<WeaponInfo>
	{
		public readonly bool InstantDropToGround = false;

		public readonly float DetectTargetFromLength = 1f;

		public readonly WDist DetectTargetBeforeDist = WDist.Zero;

		[Desc("Up to how many times does this bullet bounce when touching ground without hitting a target.",
			"0 implies exploding on contact with the originally targeted position.")]
		public readonly int BounceCount = 0;

		[Desc("Modify distance of each bounce by this percentage of previous distance.")]
		public readonly int BounceRangeModifier = 60;

		[Desc("Sound to play when the projectile hits the ground, but not the target.")]
		public readonly string BounceSound = null;

		[Desc("Terrain where the projectile explodes instead of bouncing.")]
		public readonly HashSet<string> InvalidBounceTerrain = new HashSet<string>();

		[Desc("Trigger the explosion if the projectile touches an actor thats owner has these player relationships.")]
		public readonly PlayerRelationship ValidBounceBlockerRelationships = PlayerRelationship.Enemy | PlayerRelationship.Neutral;

		[Desc("A projectile will not bounce when it is below the terrain by this depth.")]
		public readonly WDist BounceMaxDepth = new WDist(-336);

		[Desc("Altitude above terrain below which to explode. Zero effectively deactivates airburst.")]
		public readonly WDist AirburstAltitude = WDist.Zero;

		[Desc("Will the projectile split?")]
		public bool UsingScat = false;

		[Desc("How far from the target the projectile will split.")]
		public readonly WDist ScatDist = new WDist(2048);

		[Desc("What percentage of the trajectory will the projectile split over.")]
		public readonly float ScatFrom = 1f;

		[Desc("sub weapon count.")]
		public readonly int ScatCount = 5;

		[WeaponReference]
		[Desc("Weapon fire when projectile die.")]
		public readonly string ScatWeapon = null;

		[WeaponReference]
		[Desc("Weapon impact when projectile bounce.")]
		public readonly string BounceWeapon = null;
		public WeaponInfo ScatWeaponInfo { get; private set; }
		public WeaponInfo BounceWeaponInfo { get; private set; }

		void IRulesetLoaded<WeaponInfo>.RulesetLoaded(Ruleset rules, WeaponInfo info)
		{
			if (ScatWeapon != null)
			{
				if (!rules.Weapons.TryGetValue(ScatWeapon.ToLowerInvariant(), out var scatWeapon))
					throw new YamlException("Weapons Ruleset does not contain an entry '{0}'".F(ScatWeapon.ToLowerInvariant()));
				ScatWeaponInfo = scatWeapon;
			}

			if (BounceWeapon != null)
			{
				if (!rules.Weapons.TryGetValue(BounceWeapon.ToLowerInvariant(), out var bounceWeapon))
					throw new YamlException("Weapons Ruleset does not contain an entry '{0}'".F(BounceWeapon.ToLowerInvariant()));
				BounceWeaponInfo = bounceWeapon;
			}
		}

		public virtual IProjectile Create(ProjectileArgs args) { return new Bullet(this, args); }
	}

	public class Bullet : CorporealProjectile, IProjectile, ISync
	{
		readonly BulletInfo info;
		readonly ProjectileArgs args;
		readonly World world;

		readonly WAngle facing;
		readonly WAngle angle;
		readonly WDist speed;

		readonly WeaponInfo scatWeapon;
		readonly WeaponInfo bonceWeapon;

		readonly WVec offset = WVec.Zero;

		[Sync]
		WPos pos, lastPos, target, source;

		int length, scatlength;
		int ticks, liveTicks;
		readonly int lifetime;
		int remainingBounces;

		public Actor SourceActor { get { return args.SourceActor; } }
		protected Actor blocker;
		readonly int detectTargetBeforeDistSquare;

		public Bullet(BulletInfo info, ProjectileArgs args)
			: base(info, args)
		{
			this.info = info;
			this.args = args;
			pos = args.Source;
			source = args.Source;
			lastPos = pos;
			detectTargetBeforeDistSquare = info.DetectTargetBeforeDist.Length * info.DetectTargetBeforeDist.Length;
			world = args.SourceActor.World;

			if (info.UsingScat)
			{
				scatWeapon = info.ScatWeaponInfo;
			}

			if (info.BounceCount > 0 && info.BounceWeaponInfo != null)
				bonceWeapon = info.BounceWeaponInfo;

			if (info.LaunchAngle.Length > 1)
				angle = new WAngle(world.SharedRandom.Next(info.LaunchAngle[0].Angle, info.LaunchAngle[1].Angle));
			else
				angle = info.LaunchAngle[0];

			if (info.Speed.Length > 1)
				speed = new WDist(world.SharedRandom.Next(info.Speed[0].Length, info.Speed[1].Length));
			else
				speed = info.Speed[0];

			if (info.InstantDropToGround)
			{
				target = args.Source;
				target = new WPos(target.X, target.Y, world.Map.HeightOfTerrain(target));
			}
			else
			{
				target = args.PassiveTarget;
			}

			if (info.Inaccuracy.Length > 0)
			{
				var maxInaccuracyOffset = Util.GetProjectileInaccuracy(info.Inaccuracy.Length, info.InaccuracyType, args);
				offset = WVec.FromPDF(world.SharedRandom, 2, info.UseVerticalInaccuracy) * maxInaccuracyOffset / 1024;
			}

			target += offset;

			if (info.AirburstAltitude > WDist.Zero)
				target += new WVec(WDist.Zero, WDist.Zero, info.AirburstAltitude);

			facing = (target - pos).Yaw;
			length = Math.Max((target - pos).Length / speed.Length, 1);
			if (info.UsingScat)
			{
				scatlength = Math.Max(((target - pos).Length - info.ScatDist.Length) / speed.Length, 1);
			}

			remainingBounces = info.BounceCount;
			lifetime = info.LifeTime.Length == 2
					? world.SharedRandom.Next(info.LifeTime[0], info.LifeTime[1])
					: info.LifeTime[0];
			liveTicks = 0;
		}

		protected override WAngle GetEffectiveFacing()
		{
			var at = (float)ticks / (length - 1);
			var attitude = angle.Tan() * (1 - 2 * at) / (4 * 1024);

			var u = (facing.Angle % 512) / 512f;
			var scale = 2048 * u * (1 - u);

			var effective = (int)(facing.Angle < 512
				? facing.Angle - scale * attitude
				: facing.Angle + scale * attitude);

			return new WAngle(effective);
		}

		public void Tick(World world)
		{
			RenderTick(world, pos);
			SelfTick(world);
		}

		protected virtual void SelfTick(World world)
		{
			lastPos = pos;
			bounceHeight = bounceHeight < pos.Z ? pos.Z : bounceHeight;
			pos = WPos.LerpQuadratic(source, target, angle, ticks, length);

			if (ShouldExplode(world))
			{
				Explode(world);
				explode = true;
			}

			liveTicks++;
			if (bouncingTick > 0)
				bouncingTick--;
		}

		int bouncingTick = 0;
		int bounceHeight = 0;
		protected virtual bool ShouldExplode(World world)
		{
			if (lifetime > 0 && liveTicks > lifetime)
			{
				return true;
			}

			if (info.Blockable && BlocksProjectiles.AnyBlockingActorsBetween(world, args.SourceActor.Owner, lastPos, pos, info.Width, out var blockedPos, out blocker, args))
			{
				pos = blockedPos;
				return true;
			}

			var flightLengthReached = ticks++ >= length;
			var checkLengthReached = (ticks >= (length * info.DetectTargetFromLength) && (pos - source).LengthSquared > detectTargetBeforeDistSquare);
			var shouldBounce = remainingBounces > 0;
			var dat = world.Map.DistanceAboveTerrain(pos).Length;

			if (flightLengthReached || checkLengthReached || remainingBounces < info.BounceCount || info.AlwaysDetectTarget)
			{
				// check target at PassiveTargetPos
				if (FirstValidTargetsOnLine(world, lastPos, pos, info.Width, args.SourceActor, true, args.GuidedTarget.Actor, out var hitpos, out blocker))
				{
					pos = hitpos;
					return true;
				}
			}

			if (shouldBounce && dat <= 0 && bouncingTick <= 0)
			{
				var cell = world.Map.CellContaining(pos);
				if (!world.Map.Contains(cell))
					return true;

				if (info.InvalidBounceTerrain.Contains(world.Map.GetTerrainInfo(cell).Type))
					return true;

				if (AnyValidTargetsInRadius(world, pos, info.Width, args.SourceActor, true))
					return true;

				var ph = world.Map.HeightOfTerrain(pos);

				// rebounce
				if (bounceHeight > ph)
				{
					/*pos = pos - new WVec(0, 0, dat);*/
					target += (pos - source) * info.BounceRangeModifier / 100;
					target = new WPos(target.X, target.Y, (bounceHeight - ph) * info.BounceRangeModifier / 100 + ph);
				}
				else
				{
					target += (source - pos) * info.BounceRangeModifier / 100;
					target = new WPos(target.X, target.Y, pos.Z * info.BounceRangeModifier / 100);
				}

				length = Math.Max((target - pos).Length / speed.Length, 1);
				if (info.UsingScat)
				{
					scatlength = Math.Max(((target - pos).Length - info.ScatDist.Length) / speed.Length, 1);
				}

				if (bonceWeapon != null)
				{
					var warheadArgs = new WarheadArgs(args)
					{
						ImpactOrientation = new WRot(WAngle.Zero, Util.GetVerticalAngle(lastPos, pos), args.Facing),
						ImpactPosition = pos,
						Blocker = blocker,
					};
					bonceWeapon.Impact(Target.FromPos(pos), warheadArgs);
				}

				ticks = 0;
				source = pos;
				Game.Sound.Play(SoundType.World, info.BounceSound, source);
				remainingBounces--;
				bouncingTick = 2;
				bounceHeight = ph;
			}

			if (info.UsingScat)
			{
				if (ticks >= length * info.ScatFrom && !shouldBounce)
					return true;

				if (ticks >= scatlength && !shouldBounce)
					return true;
			}

			// Flight length reached / exceeded
			if (flightLengthReached && !info.OnlyHitToExplode && !shouldBounce)
				return true;

			// Driving into cell with higher height level
			if (!shouldBounce)
			{
				if (dat < info.ExplodeUnderThisAltitude.Length)
				{
					FIndGroundHitPos();
					return true;
				}

				if (flightLengthReached && dat <= 1)
				{
					FIndGroundHitPos();
					return true;
				}
			}

			return false;
		}

		void FIndGroundHitPos()
		{
			var posToLastPos = lastPos - pos;
			if (posToLastPos.Z <= 0)
				return;

			var th = world.Map.HeightOfTerrain(pos);
			if (th > pos.Z && th < lastPos.Z)
				pos += posToLastPos * (th - pos.Z) / (posToLastPos.Z);
		}

		protected virtual void Explode(World world)
		{
			RenderExplode(world, pos);

			world.AddFrameEndTask(w => w.Remove(this));

			var warheadArgs = new WarheadArgs(args)
			{
				ImpactOrientation = new WRot(WAngle.Zero, Util.GetVerticalAngle(lastPos, pos), args.Facing),
				ImpactPosition = pos,
				Blocker = blocker,
			};

			if (info.UsingScat)
			{
				var pArgs = new ProjectileArgs
				{
					Weapon = scatWeapon,
					Facing = (target - pos).Yaw,
					CurrentMuzzleFacing = () => (target - pos).Yaw,

					DamageModifiers = !SourceActor.IsDead ? SourceActor.TraitsImplementing<IFirepowerModifier>().ToArray().Select(m => m.GetFirepowerModifier()).ToArray() : Array.Empty<int>(),

					InaccuracyModifiers = !SourceActor.IsDead ? SourceActor.TraitsImplementing<IInaccuracyModifier>().ToArray().Select(m => m.GetInaccuracyModifier()).ToArray() : Array.Empty<int>(),

					RangeModifiers = !SourceActor.IsDead ? SourceActor.TraitsImplementing<IRangeModifier>().ToArray().Select(m => m.GetRangeModifier()).ToArray() : Array.Empty<int>(),

					Source = pos,
					CurrentSource = () => pos,
					SourceActor = SourceActor,
					PassiveTarget = target,
					GuidedTarget = args.GuidedTarget
				};

				if (pArgs.Weapon.Projectile != null)
				{
					for (var i = -1; i < info.ScatCount; i++)
					{
						var projectile = scatWeapon.Projectile.Create(pArgs);
						world.AddFrameEndTask(w => w.Add(projectile));
					}
				}
			}

			args.Weapon.Impact(Target.FromPos(pos), warheadArgs);
		}

		bool AnyValidTargetsInRadius(World world, WPos pos, WDist radius, Actor firedBy, bool checkTargetType)
		{
			foreach (var victim in world.FindActorsOnCircle(pos, radius))
			{
				if (checkTargetType && !Target.FromActor(victim).IsValidFor(firedBy))
					continue;

				if (!(args.GuidedTarget.Actor != null && args.GuidedTarget.Actor == victim) && !info.ValidBounceBlockerRelationships.HasRelationship(firedBy.Owner.RelationshipWith(victim.Owner)))
					continue;

				// If the impact position is within any actor's HitShape, we have a direct hit
				// PERF: Avoid using TraitsImplementing<HitShape> that needs to find the actor in the trait dictionary.
				foreach (var targetPos in victim.EnabledTargetablePositions)
					if (targetPos is HitShape h)
						if (h.DistanceFromEdge(victim, pos).Length <= 0)
							return true;
			}

			return false;
		}
	}
}
