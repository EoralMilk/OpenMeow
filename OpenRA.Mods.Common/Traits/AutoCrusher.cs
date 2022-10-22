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

using System.Linq;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	class AutoCrusherInfo : PausableConditionalTraitInfo
	{
		[Desc("Maximum scan range for AutoCrusher.")]
		public readonly WDist ScanRadius = WDist.FromCells(5);

		[Desc("Ticks to wait until next AutoCrusher: attempt.")]
		public readonly int MinimumScanTimeInterval = 10;

		[Desc("Ticks to wait until next AutoCrusher: attempt.")]
		public readonly int MaximumScanTimeInterval = 15;

		[Desc("Relationships between actor's and target's owner needed for AutoCrusher.")]
		public readonly PlayerRelationship TargetRelationships = PlayerRelationship.Ally | PlayerRelationship.Neutral | PlayerRelationship.Enemy;

		public override object Create(ActorInitializer init) { return new AutoCrusher(this); }
	}

	class AutoCrusher : PausableConditionalTrait<AutoCrusherInfo>, INotifyIdle
	{
		BitSet<CrushClass> crushes;
		bool isAircraft;
		int nextScanTime;
		IResolveOrder move;

		public AutoCrusher(AutoCrusherInfo info)
			: base(info) { }

		protected override void Created(Actor self)
		{
			if (self.Info.HasTraitInfo<MobileInfo>())
				crushes = self.Info.TraitInfos<MobileInfo>().First().LocomotorInfo.Crushes;
			else if (self.Info.HasTraitInfo<AircraftInfo>())
			{
				crushes = self.Info.TraitInfos<AircraftInfo>().First().Crushes;
				isAircraft = true;
			}

			nextScanTime = self.World.SharedRandom.Next(Info.MinimumScanTimeInterval, Info.MaximumScanTimeInterval);

			move = self.Trait<IMove>() as IResolveOrder;

			base.Created(self);
		}

		void INotifyIdle.TickIdle(Actor self)
		{
			if (nextScanTime-- > 0)
				return;

			var crushableActor = self.World.FindActorsInCircle(self.CenterPosition, Info.ScanRadius)
				.Where(a => a != self && !a.IsDead && a.IsInWorld && Info.TargetRelationships.HasRelationship(self.Owner.RelationshipWith(a.Owner)) && a.IsAtGroundLevel() && a.TraitsImplementing<ICrushable>().Any(c => c.CrushableBy(a, self, crushes)))
				.ClosestTo(self); // TODO: Make it use shortest pathfinding distance instead

			if (crushableActor == null)
				return;

			if (isAircraft)
				move.ResolveOrder(self, new Order("Land", self, Target.FromCell(self.World, crushableActor.Location), false));
			else
				move.ResolveOrder(self, new Order("Move", self, Target.FromCell(self.World, crushableActor.Location), false));

			nextScanTime = self.World.SharedRandom.Next(Info.MinimumScanTimeInterval, Info.MaximumScanTimeInterval);
		}
	}
}
