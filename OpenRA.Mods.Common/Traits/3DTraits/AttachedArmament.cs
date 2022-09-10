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
using OpenRA.Mods.Common.Projectiles;
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

	public class AttachedArmament : Armament
	{
		readonly WithSkeleton withSkeleton;
		TurretAttachment turret;

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
			base.Created(self);
			turret = self.TraitsImplementing<TurretAttachment>().FirstOrDefault(t => t.Name == Info.Turret);
		}

		protected override bool CanFire(Actor self, in Target target)
		{
			if (IsReloading || IsTraitPaused)
				return false;

			withSkeleton.CallForUpdate(boneIds[currentBarrel % boneIds.Length]);

			if (turret != null && !turret.FacingWithInTolerance(Info.FacingTolerance))
				return false;

			if ((!target.IsInRange(self.CenterPosition, MaxRange()))
				|| (Weapon.MinRange != WDist.Zero && target.IsInRange(self.CenterPosition, Weapon.MinRange)))
				return false;

			if (!Weapon.IsValidAgainst(target, self.World, self))
				return false;

			if (turret == null && hasFacingTolerance && facing != null)
			{
				var delta = target.CenterPosition - self.CenterPosition;
				return Util.FacingWithinTolerance(facing.Facing, delta.Yaw + Info.FiringAngle, Info.FacingTolerance);
			}

			return true;
		}

		protected override WPos CalculateMuzzleWPos(Actor self, Barrel b)
		{
			return withSkeleton.GetWPosFromBoneId(b.BoneId);
		}

		protected override WRot CalculateMuzzleOrientation(Actor self, Barrel b)
		{
			return withSkeleton.GetWRotFromBoneId(b.BoneId);
		}

	}
}
