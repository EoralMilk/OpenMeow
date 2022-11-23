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
using OpenRA.Mods.Common.Projectiles;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Mods.Common.Traits.Trait3D;
using OpenRA.Primitives;
using OpenRA.Traits;
using TrueSync;

namespace OpenRA.Mods.Common.Traits
{
	public class Barrel
	{
		public int BoneId;
		public WVec Offset;
		public WAngle Yaw;
	}

	[Desc("Allows you to attach weapons to the unit (use @IdentifierSuffix for > 1)")]
	public class ArmamentInfo : PausableConditionalTraitInfo, Requires<AttackBaseInfo>
	{
		public readonly string Name = "primary";

		[Desc("If this armament will show in actor info panel.")]
		public readonly bool ShowInActorInfo = true;

		[WeaponReference]
		[FieldLoader.Require]
		[Desc("Has to be defined in weapons.yaml as well.")]
		public readonly string Weapon = null;

		[Desc("The number of bursts fired per shot.")]
		public readonly int BurstsPerFire = 1;

		[Desc("Exhaust all burst per attack.")]
		public readonly bool ExhaustAllBurst = false;

		[Desc("Exhaust all burst per attack whit out Facing check.")]
		public readonly bool NotCheckFacingToExhaustAllBurst = false;

		[Desc("Exhaust all burst per attack whit out Range check.")]
		public readonly bool NotCheckRangeToExhaustAllBurst = false;

		[Desc("Which turret (if present) should this armament be assigned to.")]
		public readonly string Turret = "primary";

		[Desc("Time (in frames) until the weapon can fire again.")]
		public readonly int FireDelay = 0;

		[Desc("Muzzle position relative to turret or body, (forward, right, up) triples.",
			"If weapon Burst = 1, it cycles through all listed offsets, otherwise the offset corresponding to current burst is used.")]
		public readonly WVec[] LocalOffset = Array.Empty<WVec>();

		[Desc("Muzzle yaw relative to turret or body.")]
		public readonly WAngle[] LocalYaw = Array.Empty<WAngle>();

		[Desc("Move the turret backwards when firing.")]
		public readonly WDist Recoil = WDist.Zero;

		[Desc("Recoil recovery per-frame")]
		public readonly WDist RecoilRecovery = new WDist(9);

		[SequenceReference]
		[Desc("Muzzle flash sequence to render")]
		public readonly string MuzzleSequence = null;

		[PaletteReference]
		[Desc("Palette to render Muzzle flash sequence in")]
		public readonly string MuzzlePalette = "effect";

		[GrantedConditionReference]
		[Desc("Condition to grant while reloading.")]
		public readonly string ReloadingCondition = null;

		[Desc("Tolerance for attack angle. Range [0, 512], 512 covers 360 degrees. 1023 Means to use attack trait value. Only influence self fire checking.")]
		public readonly WAngle FacingTolerance = new WAngle(512);

		[Desc("Whether to calculate target movement to correct trajectory.")]
		public readonly bool CalculateTargetMoving = true;

		[Desc("The angle relative to the actor's orientation used to fire the weapon from.")]
		public readonly WAngle FiringAngle = WAngle.Zero;

		[Desc("Whether this armament can use bindage.")]
		public readonly bool UseBlindage = false;

		public readonly WDist BlindageDetectWidth = new WDist(1024);
		public WeaponInfo WeaponInfo { get; private set; }
		public WDist ModifiedRange { get; private set; }

		public readonly PlayerRelationship TargetRelationships = PlayerRelationship.Enemy;
		public readonly PlayerRelationship ForceTargetRelationships = PlayerRelationship.Enemy | PlayerRelationship.Neutral | PlayerRelationship.Ally;

		// TODO: instead of having multiple Armaments and unique AttackBase,
		// an actor should be able to have multiple AttackBases with
		// a single corresponding Armament each
		[CursorReference]
		[Desc("Cursor to display when hovering over a valid target.")]
		public readonly string Cursor = "attack";

		// TODO: same as above
		[CursorReference]
		[Desc("Cursor to display when hovering over a valid target that is outside of range.")]
		public readonly string OutsideRangeCursor = "attackoutsiderange";

		[Desc("Ammo the weapon consumes per shot.")]
		public readonly int AmmoUsage = 1;

		public override object Create(ActorInitializer init) { return new Armament(init.Self, this); }

		public override void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			var weaponToLower = Weapon.ToLowerInvariant();
			if (!rules.Weapons.TryGetValue(weaponToLower, out var weaponInfo))
				throw new YamlException($"Weapons Ruleset does not contain an entry '{weaponToLower}'");

			WeaponInfo = weaponInfo;
			ModifiedRange = new WDist(Util.ApplyPercentageModifiers(
				WeaponInfo.Range.Length,
				ai.TraitInfos<IRangeModifierInfo>().Select(m => m.GetRangeModifierDefault())));

			if (BurstsPerFire <= 0)
				throw new YamlException("BurstsPerFire in Armament has to be greater than 0");

			if (FacingTolerance.Angle > 512)
				throw new YamlException("Facing tolerance must be in range of [0, 512], 512 covers 360 degrees.");

			var expectedLength = WeaponInfo.Burst % BurstsPerFire > 0 ? WeaponInfo.Burst / BurstsPerFire : WeaponInfo.Burst / BurstsPerFire - 1;

			if (WeaponInfo.Burst > 1 && WeaponInfo.BurstDelays.Length > 1 && expectedLength != WeaponInfo.BurstDelays.Length)
				throw new YamlException($"Weapon '{weaponToLower}' has an invalid number of BurstDelays, must be single entry or the ceil of \"Weapon.Burst / Armament.BurstsPerFire - 1\".");

			base.RulesetLoaded(rules, ai);
		}
	}

	public class Armament : PausableConditionalTrait<ArmamentInfo>, ITick
	{
		public WeaponInfo Weapon { get; protected set; }

		public Barrel[] Barrels { get; protected set; }

		readonly Actor self;
		protected IFacing facing;
		protected ITurreted turret;
		protected BodyOrientation coords;
		INotifyBurstComplete[] notifyBurstComplete;
		INotifyAttack[] notifyAttacks;

		int conditionToken = Actor.InvalidConditionToken;

		IEnumerable<int> rangeModifiers;
		IEnumerable<int> reloadModifiers;
		IEnumerable<int> damageModifiers;
		IEnumerable<int> inaccuracyModifiers;

		int ticksSinceLastShot;
		protected int currentBarrel;
		protected int barrelCount;
		readonly bool hasFacingTolerance;
		readonly int[] boneIds;
		readonly List<(int Ticks, int Burst, Action<int> Func)> delayedActions = new List<(int, int, Action<int>)>();

		public WDist Recoil;
		public int FireDelay { get; protected set; }
		public int Burst { get; protected set; }

		public bool IgnoreAirborne = false;

		public Func<int> OverrideFireDelay = () => -1;

		public Armament(Actor self, ArmamentInfo info, bool replaceBarrel = false)
			: base(info)
		{
			this.self = self;

			hasFacingTolerance = info.FacingTolerance.Angle != 512;

			Weapon = info.WeaponInfo;
			Burst = Weapon.Burst;

			if (!replaceBarrel)
			{
				var barrels = new List<Barrel>();

				for (var i = 0; i < info.LocalOffset.Length; i++)
				{
					barrels.Add(new Barrel
					{
						BoneId = -1,
						Offset = info.LocalOffset[i],
						Yaw = info.LocalYaw.Length > i ? info.LocalYaw[i] : WAngle.Zero
					});
				}

				if (barrels.Count == 0)
					barrels.Add(new Barrel { BoneId = -1, Offset = WVec.Zero, Yaw = WAngle.Zero });

				barrelCount = barrels.Count;

				Barrels = barrels.ToArray();
			}
		}

		public virtual bool IsValidForArmamentChoose(in Target target, World world)
		{
			return true;
		}

		public virtual bool WeaponIsValidAgainst(in Target target, World world, Actor firedBy)
		{
			return Weapon.IsValidAgainst(target, world, firedBy, IgnoreAirborne);
		}

		public virtual WDist MaxRange()
		{
			return new WDist(Util.ApplyPercentageModifiers(Weapon.Range.Length, rangeModifiers.ToArray()));
		}

		protected override void Created(Actor self)
		{
			turret = self.TraitsImplementing<ITurreted>().FirstOrDefault(t => t.Name == Info.Turret);
			coords = self.Trait<BodyOrientation>();
			notifyBurstComplete = self.TraitsImplementing<INotifyBurstComplete>().ToArray();
			notifyAttacks = self.TraitsImplementing<INotifyAttack>().ToArray();
			rangeModifiers = self.TraitsImplementing<IRangeModifier>().ToArray().Select(m => m.GetRangeModifier());
			reloadModifiers = self.TraitsImplementing<IReloadModifier>().ToArray().Select(m => m.GetReloadModifier());
			damageModifiers = self.TraitsImplementing<IFirepowerModifier>().ToArray().Select(m => m.GetFirepowerModifier());
			inaccuracyModifiers = self.TraitsImplementing<IInaccuracyModifier>().ToArray().Select(m => m.GetInaccuracyModifier());
			facing = self.TraitOrDefault<IFacing>();
			base.Created(self);
		}

		void UpdateCondition(Actor self)
		{
			if (string.IsNullOrEmpty(Info.ReloadingCondition))
				return;

			var enabled = !IsTraitDisabled && IsReloading;

			if (enabled && conditionToken == Actor.InvalidConditionToken)
				conditionToken = self.GrantCondition(Info.ReloadingCondition);
			else if (!enabled && conditionToken != Actor.InvalidConditionToken)
				conditionToken = self.RevokeCondition(conditionToken);
		}

		Target persistentTarget = Target.Invalid;
		protected virtual void Tick(Actor self)
		{
			// We need to disable conditions if IsTraitDisabled is true, so we have to update conditions before the return below.
			UpdateCondition(self);

			if (IsTraitDisabled)
			{
				persistentTarget = Target.Invalid;
				return;
			}

			if (ticksSinceLastShot < Weapon.ReloadDelay)
				++ticksSinceLastShot;
			else
				persistentTarget = Target.Invalid;

			if (persistentTarget != Target.Invalid && Info.ExhaustAllBurst)
				CheckFire(self, facing, persistentTarget);

			if (FireDelay > 0)
				--FireDelay;

			Recoil = new WDist(Math.Max(0, Recoil.Length - Info.RecoilRecovery.Length));

			for (var i = 0; i < delayedActions.Count; i++)
			{
				var x = delayedActions[i];
				if (--x.Ticks <= 0)
					x.Func(x.Burst);
				delayedActions[i] = x;
			}

			delayedActions.RemoveAll(a => a.Ticks <= 0);
		}

		void ITick.Tick(Actor self)
		{
			// Split into a protected method to allow subclassing
			Tick(self);
		}

		protected void ScheduleDelayedAction(int t, int b, Action<int> a)
		{
			if (t > 0)
				delayedActions.Add((t, b, a));
			else
				a(b);
		}

		public virtual WVec AimTargetOn(Actor self, in WPos firePos, in Target target)
		{
			if (!Info.CalculateTargetMoving)
				return WVec.Zero;

			var offset = WVec.Zero;

			// target the lead
			if (target.Actor != null && target.Actor != self && !target.Actor.IsDead && target.Actor.IsInWorld)
			{
				var move = target.Actor.TraitOrDefault<IMove>();
				if (move != null)
				{
					int projSpeed = 0;
					if (Weapon.Projectile is BulletInfo)
					{
						var bullet = Weapon.Projectile as BulletInfo;
						var speeds = bullet.Speed;
						if (speeds.Length == 1)
							projSpeed = speeds[0].Length;
						else
						{
							projSpeed = (speeds[0].Length + speeds[1].Length) / 2;
						}

						// A very imprecise calculation
						if (bullet.Acceleration.Length != 0)
							projSpeed = (projSpeed + bullet.MaxSpeed.Length) / 2;
					}
					else if (Weapon.Projectile is RailgunInfo)
					{
						projSpeed = (Weapon.Projectile as RailgunInfo).Speed.Length;
					}
					else if (Weapon.Projectile is BlastWaveInfo)
					{
						var speeds = (Weapon.Projectile as BlastWaveInfo).Speed;
						if (speeds.Length == 1)
							projSpeed = speeds[0].Length;
						else
						{
							projSpeed = (speeds[0].Length + speeds[1].Length) / 2;
						}
					}

					if (projSpeed == 0)
						return offset;

					var dist = firePos - target.CenterPosition;
					var moveLensqr = move.CurrentVelocity.LengthSquared;
					if (moveLensqr == 0)
						return offset;

					var distLensqr = dist.LengthSquared;
					var coss = WVec.Dot(dist, move.CurrentVelocity);
					var a = moveLensqr - projSpeed * projSpeed;
					var b = -(2 * coss);
					var c = distLensqr;
					var delta = (b * b) - (4 * a * c);

					if (delta >= 0 && a != 0)
					{
						var t = (-b + (int)Exts.ISqrt(delta)) / (2 * a);
						var t2 = (-b - (int)Exts.ISqrt(delta)) / (2 * a);
						if (t < t2)
							t = t2;

						if (t > 0)
						{
							offset = new WVec((int)(t * move.CurrentVelocity.X), (int)(t * move.CurrentVelocity.Y), (int)(t * move.CurrentVelocity.Z));
							target.SetOffset(offset);

							return offset;
						}
					}
				}

			}

			return offset;
		}

		protected virtual bool CanFire(Actor self, in Target target)
		{
			if (IsReloading || IsTraitPaused)
				return false;

			var keepFireWithOutFacingCheck = Info.ExhaustAllBurst && persistentTarget.Type != TargetType.Invalid && Info.NotCheckFacingToExhaustAllBurst;
			var keepFireWithOutRangeCheck = Info.ExhaustAllBurst && persistentTarget.Type != TargetType.Invalid && Info.NotCheckRangeToExhaustAllBurst;

			if (!keepFireWithOutFacingCheck && (turret != null && !turret.HasAchievedDesiredFacing))
				return false;

			if (!keepFireWithOutRangeCheck &&
				((!target.IsInRange(self.CenterPosition, MaxRange())) ||
				(Weapon.MinRange != WDist.Zero && target.IsInRange(self.CenterPosition, Weapon.MinRange))))
				return false;

			if (!WeaponIsValidAgainst(target, self.World, self))
				return false;

			if (turret == null && hasFacingTolerance && facing != null)
			{
				var delta = target.CenterPosition - self.CenterPosition;
				return Util.FacingWithinTolerance(facing.Facing, delta.Yaw + Info.FiringAngle, Info.FacingTolerance) || keepFireWithOutFacingCheck;
			}

			return true;
		}

		public virtual bool TargetInFiringArc(in Target target)
		{
			if (hasFacingTolerance && facing != null)
			{
				var delta = target.CenterPosition - self.CenterPosition;
				return Util.FacingWithinTolerance(facing.Facing, delta.Yaw + Info.FiringAngle, Info.FacingTolerance);
			}

			if (hasFacingTolerance && facing == null)
				return false;
			return true;
		}

		// Note: facing is only used by the legacy positioning code
		// The world coordinate model uses Actor.Orientation
		Barrel barrel;
		public virtual Barrel CheckFire(Actor self, IFacing facing, in Target target)
		{
			if (!CanFire(self, in target))
				return null;

			if (Info.ExhaustAllBurst)
				persistentTarget = target;

			if (ticksSinceLastShot >= Weapon.ReloadDelay)
				Burst = Weapon.Burst;

			ticksSinceLastShot = 0;

			for (var i = 0; i < Info.BurstsPerFire && Burst > 0; i++)
			{
				// If Weapon.Burst == 1, cycle through all LocalOffsets, otherwise use the offset corresponding to current Burst
				currentBarrel %= barrelCount;
				barrel = Weapon.Burst == 1 ? Barrels[currentBarrel] : Barrels[Burst % Barrels.Length];
				currentBarrel++;

				FireBarrel(self, facing, target, barrel);
				Burst--;
			}

			AfterFire(self, target);

			return barrel;
		}

		protected virtual TSMatrix4x4 GetProjectileMatrix(Actor self, Barrel b)
		{
			return TSMatrix4x4.Identity;
		}

		protected virtual void FireBarrel(Actor self, IFacing facing, in Target target, Barrel barrel)
		{
			foreach (var na in notifyAttacks)
				na.PreparingAttack(self, target, this, barrel);

			Func<WPos> muzzlePosition = () => MuzzleWPos(self, barrel);
			Func<WAngle> muzzleFacing = () => MuzzleOrientation(self, barrel).Yaw;
			var muzzleOrientation = WRot.FromYaw(muzzleFacing());
			var passiveTarget = Weapon.TargetActorCenter ? target.CenterPosition : target.Positions.PositionClosestTo(muzzlePosition());
			var initialOffset = Weapon.FirstBurstTargetOffset;
			if (initialOffset != WVec.Zero)
			{
				// We want this to match Armament.LocalOffset, so we need to convert it to forward, right, up
				initialOffset = new WVec(initialOffset.Y, -initialOffset.X, initialOffset.Z);
				passiveTarget += initialOffset.Rotate(muzzleOrientation);
			}

			var followingOffset = Weapon.FollowingBurstTargetOffset;
			if (followingOffset != WVec.Zero)
			{
				// We want this to match Armament.LocalOffset, so we need to convert it to forward, right, up
				followingOffset = new WVec(followingOffset.Y, -followingOffset.X, followingOffset.Z);
				passiveTarget += ((Weapon.Burst - Burst) * followingOffset).Rotate(muzzleOrientation);
			}

			var args = new ProjectileArgs
			{
				Weapon = Weapon,
				Facing = muzzleFacing(),
				CurrentMuzzleFacing = muzzleFacing,

				DamageModifiers = damageModifiers.ToArray(),

				InaccuracyModifiers = inaccuracyModifiers.ToArray(),

				RangeModifiers = rangeModifiers.ToArray(),

				Source = muzzlePosition(),
				CurrentSource = muzzlePosition,
				SourceActor = self,
				PassiveTarget = passiveTarget,
				GuidedTarget = target,
				Matrix = GetProjectileMatrix(self, barrel),
			};

			if (Info.UseBlindage)
			{
				var toend = (passiveTarget - args.Source);
				var end = args.Source + toend * Info.BlindageDetectWidth.Length * 2 / toend.Length;

				foreach (Actor a in self.World.FindBlockingActorsOnLine(args.Source, end, Info.BlindageDetectWidth))
				{
					if (a.TraitsImplementing<IBlocksProjectiles>().Any(b => b.IsBindage))
						args.IgnoredActors.Add(a);
				}
			}

			// Lambdas can't use 'in' variables, so capture a copy for later
			var delayedTarget = target;
			ScheduleDelayedAction(OverrideFireDelay() >= 0 ? OverrideFireDelay() : Info.FireDelay, Burst, (burst) =>
			{
				if (args.Weapon.Projectile != null)
				{
					var targetOffset = AimTargetOn(self, args.Source, delayedTarget);
					args.PassiveTarget += targetOffset;
					var projectile = args.Weapon.Projectile.Create(args);
					if (projectile != null)
						self.World.Add(projectile);

					if (args.Weapon.Report != null && args.Weapon.Report.Length > 0)
						Game.Sound.Play(SoundType.World, args.Weapon.Report, self.World, self.CenterPosition);

					if (burst == args.Weapon.Burst && args.Weapon.StartBurstReport != null && args.Weapon.StartBurstReport.Length > 0)
						Game.Sound.Play(SoundType.World, args.Weapon.StartBurstReport, self.World, self.CenterPosition);

					foreach (var na in notifyAttacks)
						na.Attacking(self, delayedTarget, this, barrel);

					Recoil = Info.Recoil;
				}
			});
		}

		protected virtual void AfterFire(Actor self, in Target target)
		{
			if (Burst > 0)
			{
				if (Weapon.BurstDelays.Length == 1)
					FireDelay = Weapon.BurstDelays[0];
				else
					FireDelay = Weapon.BurstDelays[(Weapon.Burst - Burst) / Info.BurstsPerFire - 1];
			}
			else
			{
				persistentTarget = Target.Invalid;
				var modifiers = reloadModifiers.ToArray();
				FireDelay = Util.ApplyPercentageModifiers(Weapon.ReloadDelay, modifiers);
				Burst = Weapon.Burst;

				if (Weapon.AfterFireSound != null && Weapon.AfterFireSound.Length > 0)
					ScheduleDelayedAction(Weapon.AfterFireSoundDelay, Burst, (burst) => Game.Sound.Play(SoundType.World, Weapon.AfterFireSound, self.World, self.CenterPosition));

				foreach (var nbc in notifyBurstComplete)
					nbc.FiredBurst(self, target, this);
			}
		}

		public virtual bool IsReloading => FireDelay > 0 || IsTraitDisabled;

		public WPos MuzzleWPos(Actor self, Barrel b)
		{
			return CalculateMuzzleWPos(self, b);
		}

		public WPos MuzzleWPos()
		{
			return CalculateMuzzleWPos(self, Weapon.Burst == 1 ? Barrels[currentBarrel % Barrels.Length] : Barrels[Burst % Barrels.Length]);
		}

		protected virtual WPos CalculateMuzzleWPos(Actor self, Barrel b)
		{
			// Weapon offset in turret coordinates
			var localOffset = b.Offset + new WVec(-Recoil, WDist.Zero, WDist.Zero);

			// Turret coordinates to body coordinates
			var bodyOrientation = coords.QuantizeOrientation(self.Orientation);
			if (turret != null)
			{
				localOffset = localOffset.Rotate(turret.WorldOrientation) + turret.Offset.Rotate(bodyOrientation);
				return self.CenterPosition + coords.LocalToWorld(localOffset);
			}
			else
			{
				localOffset = localOffset.Rotate(bodyOrientation);

				return self.CenterPosition + coords.LocalToWorld(localOffset);
			}
		}

		public WRot MuzzleOrientation(Actor self, Barrel b)
		{
			return CalculateMuzzleOrientation(self, b);
		}

		protected virtual WRot CalculateMuzzleOrientation(Actor self, Barrel b)
		{
			return WRot.FromYaw(b.Yaw).Rotate(turret?.WorldOrientation ?? self.Orientation);
		}

		public Actor Actor => self;
	}
}
