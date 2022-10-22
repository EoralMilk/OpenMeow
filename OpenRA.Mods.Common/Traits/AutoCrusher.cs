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
	class AutoCrusherInfo : PausableConditionalTraitInfo, Requires<IMoveInfo>
	{
		[Desc("Maximum range to scan for targets.")]
		public readonly WDist ScanRadius = WDist.FromCells(5);

		[Desc("Ticks to wait until scan for targets.")]
		public readonly int MinimumScanTimeInterval = 10;

		[Desc("Ticks to wait until scan for targets.")]
		public readonly int MaximumScanTimeInterval = 15;

		[Desc("Player relationships the owner of the actor needs to get targeted")]
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
			var mobile = self.TraitOrDefault<Mobile>();
			if (mobile != null)
				crushes = mobile.Info.LocomotorInfo.Crushes;
			else
			{
				var aircraft = self.TraitOrDefault<Aircraft>();
				if (aircraft != null)
				{
					crushes = aircraft.Info.Crushes;
					isAircraft = true;
				}
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
				.Where(a => a != self && !a.IsDead && a.IsInWorld &&
				self.Location != a.Location && a.IsAtGroundLevel() &&
				Info.TargetRelationships.HasRelationship(self.Owner.RelationshipWith(a.Owner)) &&
				a.TraitsImplementing<ICrushable>().Any(c => c.CrushableBy(a, self, crushes)))
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
