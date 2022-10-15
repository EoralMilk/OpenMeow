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
using GlmSharp;
using OpenRA.GameRules;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Effects;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Support;
using OpenRA.Traits;
using TagLib.Matroska;
using TrueSync;

namespace OpenRA.Mods.Common.Projectiles
{
	public class VectorBoosterInfo : CorporealProjectileInfo, IProjectileInfo, IRulesetLoaded<WeaponInfo>
	{

		public readonly BitSet<TargetableType> LockOnTargetTypes = new BitSet<TargetableType>("Vehicle", "Air", "Building", "Defense");
		public readonly BitSet<TargetableType> InvalidLockOnTargetTypes = new BitSet<TargetableType>("MissileUnLock");

		[Desc("Projectile acceleration when propulsion activated.")]
		public readonly WDist Acceleration = new WDist(5);

		public readonly WDist MaxSpeed = new WDist(512);
		public readonly WDist MinSpeed = new WDist(64);

		public readonly WAngle HorizonRotationRate = new WAngle(15);
		public readonly WAngle VerticalRotationRate = new WAngle(15);

		public readonly int LockOnDelay = 5;

		public readonly int IgnitionDelay = 5;

		public readonly int JetDelay = -1;

		public readonly WDist ProximityRange = new WDist(256);

		public readonly bool ProximitySnapping = false;

		public readonly bool ExplodeWhenLostTarget = false;

		public readonly bool KeepDirectionWhenLostTarget = true;

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
		int speed;

		readonly WeaponInfo scatWeapon;

		readonly WVec offset = WVec.Zero;

		[Sync]
		WPos pos, lastPos, target, source;
		[Sync]
		int hFacing;
		[Sync]
		int vFacing;

		int liveTicks;
		readonly int jetDelay, trailDelay;
		readonly int lifetime;

		public Actor SourceActor { get { return args.SourceActor; } }
		protected Actor blocker;

		WVec tDir;
		WVec velocity;

		int proximityRange;
		readonly long detectTargetBeforeDistSquare;

		public VectorBooster(VectorBoosterInfo info, ProjectileArgs args)
			: base(info, args)
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

			if (args.Matrix == TSMatrix4x4.Identity && info.LaunchAngle.Length > 1)
				angle = new WAngle(world.SharedRandom.Next(info.LaunchAngle[0].Angle, info.LaunchAngle[1].Angle));
			else
				angle = info.LaunchAngle[0];

			if (info.Speed.Length > 1)
				speed = world.SharedRandom.Next(info.Speed[0].Length, info.Speed[1].Length);
			else
				speed = info.Speed[0].Length;

			proximityRange = info.ProximityRange.Length;

			if (args.GuidedTarget.Actor != null && args.GuidedTarget.Actor.IsInWorld && !args.GuidedTarget.Actor.IsDead)
			{
				var targetTypes = args.GuidedTarget.Actor.GetEnabledTargetTypes();
				if (info.LockOnTargetTypes.Overlaps(targetTypes) &&
					!info.InvalidLockOnTargetTypes.Overlaps(targetTypes) &&
					world.SharedRandom.Next(100) <= info.LockOnProbability)
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

			if (args.Matrix == TSMatrix4x4.Identity)
			{
				vFacing = (sbyte)(angle.Angle >> 2);
				hFacing = args.Facing.Facing;

				velocity = new WVec(0, -speed, 0)
					.Rotate(new WRot(WAngle.FromFacing(vFacing), WAngle.Zero, WAngle.Zero))
					.Rotate(new WRot(WAngle.Zero, WAngle.Zero, WAngle.FromFacing(hFacing)));
			}
			else
			{
				var dir = World3DCoordinate.TSVec3ToWPos(Transformation.MatWithOutScale(args.Matrix) * (front.normalized)) - args.Source;
				hFacing = dir.Yaw.Facing;
				var hLength = new WVec(dir.X, dir.Y, 0).Length;
				vFacing = new WVec(-dir.Z, -hLength, 0).Yaw.Facing;

				velocity = dir * speed / dir.Length;
			}

			tDir = target - pos;

			if (info.JetDelay < 0)
				jetDelay = info.IgnitionDelay;
			else
				jetDelay = info.JetDelay;

			if (info.TrailDelay < 0)
				trailDelay = info.IgnitionDelay;
			else
				trailDelay = info.TrailDelay;

			lifetime = info.LifeTime.Length == 2
					? world.SharedRandom.Next(info.LifeTime[0], info.LifeTime[1])
					: info.LifeTime[0];
			liveTicks = 0;

			renderJet = false;
			renderTrail = false;
		}

		public void Tick(World world)
		{
			RenderTick(world, pos);
			SelfTick(world);
		}

		DebugLineRenderable DebugDrawLine(WPos pos, TSQuaternion q, Color color)
		{
			var start = World3DCoordinate.Float3toVec3(Game.Renderer.WorldRgbaColorRenderer.Render3DPosition(pos));
			var end = World3DCoordinate.TSVec3ToVec3(q * (front * 5)) + start;

			return new DebugLineRenderable(pos, 0,
				World3DCoordinate.Vec3toFloat3(start),
				World3DCoordinate.Vec3toFloat3(end),
				new WDist(64), color, BlendMode.None);
		}

		//protected override IEnumerable<IRenderable> RenderSelf(WorldRenderer wr)
		//{
		//	yield return DebugDrawLine(pos, initFacing, Color.Coral);

		//	yield return DebugDrawLine(pos, desireHorizonFacing, Color.Azure);
		//	yield return DebugDrawLine(pos, currentFacing, Color.Green);
		//}

		protected virtual void SelfTick(World world)
		{
			lastPos = pos;

			if (liveTicks > jetDelay)
				renderJet = true;
			if (liveTicks > trailDelay)
				renderTrail = true;

			if (lockOn && liveTicks >= info.LockOnDelay && args.GuidedTarget.Actor.IsInWorld)
			{
				if (!args.GuidedTarget.Actor.IsDead)
				{
					target = args.Weapon.TargetActorCenter ? args.GuidedTarget.CenterPosition + offset : args.GuidedTarget.Positions.PositionClosestTo(args.Source) + offset;
				}
			}

			// lost target
			if (lockOn && (!args.GuidedTarget.Actor.IsInWorld || args.GuidedTarget.Actor.IsDead))
			{
				if (info.ExplodeWhenLostTarget)
				{
					Explode(world);
					explode = true;
					return;
				}
				else if (!info.KeepDirectionWhenLostTarget)
					tDir = target - pos;
			}
			else
				tDir = target - pos;

			if (liveTicks >= info.IgnitionDelay)
			{
				var desiredHFacing = tDir.Yaw.Facing;
				var hLength = tDir.HorizontalLength;
				var desiredVFacing = new WVec(-tDir.Z, -hLength, 0).Yaw.Facing;

				hFacing = Util.TickFacing(hFacing, desiredHFacing, info.HorizonRotationRate.Angle);
				vFacing = Util.TickFacing(vFacing, desiredVFacing, info.VerticalRotationRate.Angle);

				// pass target
				if (WVec.Dot(velocity, tDir) < 0)
				{
					speed -= info.Acceleration.Length;
					speed = speed <= 0 ? info.MinSpeed.Length : speed;
				}
				else
				{
					speed += info.Acceleration.Length;
					speed = speed >= info.MaxSpeed.Length ? info.MaxSpeed.Length : speed;
				}
			}

			velocity = new WVec(0, -1024 * speed, 0)
				.Rotate(new WRot(WAngle.FromFacing(vFacing), WAngle.Zero, WAngle.Zero))
				.Rotate(new WRot(WAngle.Zero, WAngle.Zero, WAngle.FromFacing(hFacing)))
				/ 1024;

			pos += velocity;

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

			var distToTarget = tDir.Length;

			if (speed > proximityRange * 2)
				proximityRange = speed / 2;
			else
				proximityRange = info.ProximityRange.Length;

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
					Matrix = GetMatrix(),
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
