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
using System.Linq;
using OpenRA.GameRules;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Effects;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Support;
using OpenRA.Traits;
using TrueSync;

namespace OpenRA.Mods.Common.Projectiles
{
	public class VectorBoosterInfo : CorporealProjectileInfo, IProjectileInfo, IRulesetLoaded<WeaponInfo>
	{

		public readonly float RotationSpeed = 15.0f;

		public readonly WDist ProximityRange = new WDist(256);

		public readonly bool ProximitySnapping = false;

		public readonly bool LostTarget = false;

		public readonly bool DetectTargetOnCurve = false;
		public readonly WDist DetectTargetBeforeDist = WDist.Zero;

		[Desc("Inaccuracy override when successfully locked onto target. Defaults to Inaccuracy if negative.")]
		public readonly WDist LockOnInaccuracy = new WDist(-1);

		[Desc("Inaccuracy value in Vertical space.")]
		public readonly bool UseLockOnVerticalInaccuracy = false;

		[Desc("Probability of locking onto and following target.")]
		public readonly int LockOnProbability = 100;

		[Desc("Up to how many times does this VectorBooster bounce when touching ground without hitting a target.",
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

		[Desc("sub weapon count.")]
		public readonly int ScatCount = 5;

		[WeaponReference]
		[Desc("Weapon fire when projectile die.")]
		public readonly string ScatWeapon = null;

		public WeaponInfo ScatWeaponInfo { get; private set; }

		void IRulesetLoaded<WeaponInfo>.RulesetLoaded(Ruleset rules, WeaponInfo info)
		{
			if (ScatWeapon != null)
			{
				WeaponInfo scatWeapon;

				if (!rules.Weapons.TryGetValue(ScatWeapon.ToLowerInvariant(), out scatWeapon))
					throw new YamlException("Weapons Ruleset does not contain an entry '{0}'".F(ScatWeapon.ToLowerInvariant()));
				ScatWeaponInfo = scatWeapon;
			}
		}

		public virtual IProjectile Create(ProjectileArgs args) { return new VectorBooster(this, args); }
	}

	public class VectorBooster : CorporealProjectile, IProjectile, ISync
	{
		readonly bool lockOn;

		readonly VectorBoosterInfo info;
		readonly ProjectileArgs args;

		readonly WAngle facing;
		readonly WAngle angle;
		readonly FP speed;

		readonly WeaponInfo scatWeapon;

		readonly WVec offset = WVec.Zero;

		[Sync]
		WPos pos, lastPos, target, source;

		int liveTicks;
		readonly int lifetime;

		public Actor SourceActor { get { return args.SourceActor; } }
		protected Actor blocker;

		TSQuaternion currentFacing, desireFacing;
		TSVector sourcePos, currentPos, targetPos;
		FP rotationSpeed;
		readonly int proximityRange;
		readonly long detectTargetBeforeDistSquare;

		public VectorBooster(VectorBoosterInfo info, ProjectileArgs args)
			:base(info, args)
		{
			this.info = info;
			this.args = args;
			pos = args.Source;
			source = args.Source;
			lastPos = pos;
			detectTargetBeforeDistSquare = info.DetectTargetBeforeDist.Length * info.DetectTargetBeforeDist.Length;

			var world = args.SourceActor.World;

			if (info.UsingScat)
			{
				scatWeapon = info.ScatWeaponInfo;
			}

			if (args.Rotation == TSQuaternion.identity && info.LaunchAngle.Length > 1)
				angle = new WAngle(world.SharedRandom.Next(info.LaunchAngle[0].Angle, info.LaunchAngle[1].Angle));
			else
				angle = info.LaunchAngle[0];

			if (info.Speed.Length > 1)
				speed = world.SharedRandom.Next(info.Speed[0].Length, info.Speed[1].Length);
			else
				speed = info.Speed[0].Length;

			if ((int)speed > info.ProximityRange.Length)
				proximityRange = (int)speed + 2;
			else
				proximityRange = info.ProximityRange.Length;

			speed /= Game.Renderer.World3DRenderer.WPosPerMeter;

			if (args.GuidedTarget.Actor != null && args.GuidedTarget.Actor.IsInWorld && !args.GuidedTarget.Actor.IsDead)
			{
				if (world.SharedRandom.Next(100) <= info.LockOnProbability)
					lockOn = true;
			}

			if (lockOn)
			{
				if (info.LockOnInaccuracy.Length > 0)
				{
					var maxInaccuracyOffset = Util.GetProjectileInaccuracy(info.LockOnInaccuracy.Length, info.InaccuracyType, args);
					offset = WVec.FromPDF(world.SharedRandom, 2, info.UseLockOnVerticalInaccuracy) * maxInaccuracyOffset / 1024;
				}

				target = args.Weapon.TargetActorCenter ? args.GuidedTarget.CenterPosition + offset : args.GuidedTarget.Positions.PositionClosestTo(args.Source) + offset;
			}
			else
			{
				if (info.Inaccuracy.Length > 0)
				{
					var maxInaccuracyOffset = Util.GetProjectileInaccuracy(info.Inaccuracy.Length, info.InaccuracyType, args);
					offset = WVec.FromPDF(world.SharedRandom, 2, info.UseVerticalInaccuracy) * maxInaccuracyOffset / 1024;
				}

				target = args.PassiveTarget + offset;
			}

			if (info.AirburstAltitude > WDist.Zero)
				target += new WVec(WDist.Zero, WDist.Zero, info.AirburstAltitude);

			facing = (target - pos).Yaw;

			rotationSpeed = FP.FromFloat(info.RotationSpeed);

			sourcePos = Game.Renderer.World3DRenderer.Get3DPositionFromWPos(source);
			currentPos = sourcePos;
			targetPos = Game.Renderer.World3DRenderer.Get3DPositionFromWPos(target);
			if (args.Rotation == TSQuaternion.identity)
				currentFacing = new WRot(new WAngle(0), angle, facing).ToQuat();
			else
				currentFacing = args.Rotation;
			desireFacing = TSQuaternion.FromToRotation(TSVector.forward, targetPos - sourcePos);
			//facingVec = new WVec(0, -1, 0).Rotate(new WRot(new WAngle(0), angle, facing));

			lifetime = info.LifeTime.Length == 2
					? world.SharedRandom.Next(info.LifeTime[0], info.LifeTime[1])
					: info.LifeTime[0];
			liveTicks = 0;
		}

		public void Tick(World world)
		{
			RenderTick(world, pos);
			SelfTick(world);
		}

		protected virtual void SelfTick(World world)
		{
			lastPos = pos;
			currentPos += currentFacing * TSVector.forward * speed;
			pos = Game.Renderer.World3DRenderer.GetWPosFromTSVector(currentPos);

			if (lockOn && args.GuidedTarget.Actor.IsInWorld)
			{
				if (!args.GuidedTarget.Actor.IsDead)
				{
					target = args.Weapon.TargetActorCenter ? args.GuidedTarget.CenterPosition + offset : args.GuidedTarget.Positions.PositionClosestTo(args.Source) + offset;
					targetPos = Game.Renderer.World3DRenderer.Get3DPositionFromWPos(target);
				}

				desireFacing = TSQuaternion.FromToRotation(TSVector.forward, targetPos - currentPos);
				currentFacing = TSQuaternion.RotateTowards(currentFacing, desireFacing, rotationSpeed);
			}
			else if (!lockOn && !info.LostTarget)
			{
				desireFacing = TSQuaternion.FromToRotation(TSVector.forward, targetPos - currentPos);
				currentFacing = TSQuaternion.RotateTowards(currentFacing, desireFacing, rotationSpeed);
			}

			if (ShouldExplode(world))
			{
				Explode(world);
				explode = true;
			}

			liveTicks++;
		}

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

			//if (info.AlwaysDetectTarget)
			//{
			//	if (AnyValidTargetsInRadius(world, pos, info.Width, args.SourceActor, true))
			//		return true;
			//}

			if ((info.DetectTargetOnCurve && (pos - source).LengthSquared > detectTargetBeforeDistSquare) || info.AlwaysDetectTarget)
			{
				// check target at PassiveTargetPos
				if (FirstValidTargetsOnLine(world, lastPos, pos, info.Width, args.SourceActor, true, args.GuidedTarget.Actor, out var hitpos, out blocker))
				{
					pos = hitpos;
					return true;
				}
			}

			var distToTarget = (pos - target).Length;

			if (distToTarget < proximityRange)
			{
				if (info.ProximitySnapping)
					pos = target;
				return true;
			}

			if (info.UsingScat)
			{
				if (distToTarget < info.ScatDist.Length)
					return true;
			}

			var dat = world.Map.DistanceAboveTerrain(pos).Length;

			if (dat < info.ExplodeUnderThisAltitude.Length || (distToTarget < MathF.Abs(info.ExplodeUnderThisAltitude.Length) && dat < 0))
				return true;

			return false;
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
					Rotation = currentFacing,
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
				var activeShapes = victim.TraitsImplementing<HitShape>().Where(Exts.IsTraitEnabled);
				if (activeShapes.Any(i => i.DistanceFromEdge(victim, pos).Length <= 0))
					return true;
			}

			return false;
		}
	}
}
