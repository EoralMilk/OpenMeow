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
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Mods.Common.Traits.Trait3D;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{

	public class AttachedArmamentInfo : ArmamentInfo, Requires<WithSkeletonInfo>
	{
		public readonly string SkeletonToUse = null;
		public readonly string[] FromBonePose = Array.Empty<string>();

		public override object Create(ActorInitializer init) { return new AttachedArmament(init.Self, this); }

	}

	public class AttachedArmament : Armament, IUpdateWithSkeleton
	{
		readonly WithSkeleton withSkeleton;
		TurretAttachment turret;
		AttackBase attack;
		IFacing facing;
		BodyOrientation coords;
		INotifyBurstComplete[] notifyBurstComplete;
		INotifyAttack[] notifyAttacks;

		int conditionToken = Actor.InvalidConditionToken;

		IEnumerable<int> rangeModifiers;
		IEnumerable<int> reloadModifiers;
		IEnumerable<int> damageModifiers;
		IEnumerable<int> inaccuracyModifiers;

		int ticksSinceLastShot;
		int currentBarrel;
		readonly bool hasFacingTolerance;
		readonly List<(int Ticks, int Burst, Action<int> Func)> delayedActions = new List<(int, int, Action<int>)>();

		readonly int[] boneIds;
		public AttachedArmament(Actor self, AttachedArmamentInfo info)
			: base(self, info, true)
		{
			withSkeleton = self.TraitsImplementing<WithSkeleton>().Single(w => w.Info.Name == info.SkeletonToUse);
			if (withSkeleton == null)
				throw new Exception(self.Info.Name + " Armament Can not find skeleton " + info.SkeletonToUse);

			var barrels = new List<Barrel>();

			if (info.FromBonePose.Length > 0 && info.SkeletonToUse != null)
			{
				boneIds = new int[info.FromBonePose.Length];
				for (var i = 0; i < info.FromBonePose.Length; i++)
				{
					boneIds[i] = withSkeleton.GetBoneId(info.FromBonePose[i]);
					if (boneIds[i] == -1)
					{
						throw new Exception(self.Info.Name + " can't find bone " + info.FromBonePose[i] + " from current skeleton");
					}

					barrels.Add(new Barrel
					{
						BoneId = boneIds[i],
						Offset = WVec.Zero,
						Yaw = info.LocalYaw.Length > i ? info.LocalYaw[i] : WAngle.Zero
					});
				}
			}

			if (barrels.Count == 0)
				throw new Exception("AttachedArmament must have at least one bone to calculate muzzle");

			barrelCount = barrels.Count;

			Barrels = barrels.ToArray();
		}

		protected override void Created(Actor self)
		{
			turret = self.TraitsImplementing<TurretAttachment>().FirstOrDefault(t => t.Name == Info.Turret);
			attack = self.TraitsImplementing<AttackBase>().First();
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

		protected override void Tick(Actor self)
		{
			// keep empty
		}

		void TickEarly(Actor self)
		{
			// We need to disable conditions if IsTraitDisabled is true, so we have to update conditions before the return below.
			UpdateCondition(self);

			if (IsTraitDisabled)
				return;

			if (ticksSinceLastShot < Weapon.ReloadDelay)
				++ticksSinceLastShot;

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

		protected new void ScheduleDelayedAction(int t, int b, Action<int> a)
		{
			if (t > 0)
				delayedActions.Add((t, b, a));
			else
				a(b);
		}

		protected override void FireBarrel(Actor self, IFacing facing, in Target target, Barrel barrel)
		{
			foreach (var na in notifyAttacks)
				na.PreparingAttack(self, target, this, barrel);

			Func<WPos> muzzlePosition = () => CalculateMuzzleWPos(self, barrel);
			Func<WAngle> muzzleFacing = () => CalculateMuzzleOrientation(self, barrel).Yaw;
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
				GuidedTarget = target
			};

			// Lambdas can't use 'in' variables, so capture a copy for later
			var delayedTarget = target;
			ScheduleDelayedAction(Info.FireDelay, Burst, (burst) =>
			{
				if (args.Weapon.Projectile != null)
				{
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

		// Note: facing is only used by the legacy positioning code
		// The world coordinate model uses Actor.Orientation
		public override Barrel CheckFire(Actor self, IFacing facing, in Target target)
		{
			if (hasFacingTolerance && facing != null)
			{
				delayedFaceAngle = facing.Facing;
			}

			if (turret != null)
			{
				turret.FacingTarget(delayedTarget, attack.GetTargetPosition(self.CenterPosition, delayedTarget));
			}
			else
			{
				withSkeleton.CallForUpdate();
			}

			delayedTarget = target;
			delayedIFacing = facing;
			needDelayedCheckFire = true;
			lastIsReloading = IsReloading;
			lastTraitPaused = IsTraitPaused;
			return delayedBarrel;
		}

		bool lastIsReloading;
		bool lastTraitPaused;

		protected bool CanFire(Actor self, in Target target)
		{
			if (lastIsReloading || lastTraitPaused)
				return false;

			if (turret != null && !turret.FacingWithInTolerance(Info.FacingTolerance))
				return false;

			if ((!target.IsInRange(self.CenterPosition, MaxRange()))
				|| (Weapon.MinRange != WDist.Zero && target.IsInRange(self.CenterPosition, Weapon.MinRange)))
				return false;

			if (!Weapon.IsValidAgainst(target, self.World, self))
				return false;

			if (hasFacingTolerance && facing != null)
			{
				var delta = target.CenterPosition - self.CenterPosition;
				return Util.FacingWithinTolerance(delayedFaceAngle, delta.Yaw, Info.FacingTolerance);
			}

			return true;
		}

		public void UpdateEarly(Actor self)
		{
		}

		[Sync]
		bool needDelayedCheckFire = false;
		[Sync]
		WAngle delayedFaceAngle;
		[Sync]
		Target delayedTarget;
		[Sync]
		bool canFire;
		IFacing delayedIFacing;
		Barrel delayedBarrel = null;
		public void UpdateLate(Actor self)
		{
			if (!needDelayedCheckFire)
			{
				delayedBarrel = null;
			}
			else
			{
				needDelayedCheckFire = false;

				if (!CanFire(self, delayedTarget))
				{
					delayedBarrel = null;
					canFire = false;
				}
				else
				{
					canFire = true;

					if (ticksSinceLastShot >= Weapon.ReloadDelay)
						Burst = Weapon.Burst;

					ticksSinceLastShot = 0;

					// If Weapon.Burst == 1, cycle through all LocalOffsets, otherwise use the offset corresponding to current Burst
					currentBarrel %= barrelCount;
					delayedBarrel = Weapon.Burst == 1 ? Barrels[currentBarrel] : Barrels[Burst % Barrels.Length];
					currentBarrel++;

					FireBarrel(self, delayedIFacing, delayedTarget, delayedBarrel);

					UpdateBurst(self, delayedTarget);
				}
			}

			TickEarly(self);
		}

		[Sync]
		int calcount = 0;
		[Sync]
		WPos syncFirePos;
		[Sync]
		WRot syncFireRot;
		protected override WPos CalculateMuzzleWPos(Actor self, Barrel b)
		{
			calcount++;
			syncFirePos = withSkeleton.GetWPosFromBoneId(b.BoneId);
			return syncFirePos;
		}

		protected override WRot CalculateMuzzleOrientation(Actor self, Barrel b)
		{
			syncFireRot = withSkeleton.GetWRotFromBoneId(b.BoneId);
			return syncFireRot;
		}
	}
}
