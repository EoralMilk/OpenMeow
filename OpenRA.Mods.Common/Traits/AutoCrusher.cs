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

		public override object Create(ActorInitializer init) { return new AutoCrusher(this); }
	}

	class AutoCrusher : PausableConditionalTrait<AutoCrusherInfo>, INotifyIdle
	{
		BitSet<CrushClass> crushes;
		int nextScanTime;
		IResolveOrder move;

		public AutoCrusher(AutoCrusherInfo info)
			: base(info) { }

		protected override void Created(Actor self)
		{
			if (self.Info.HasTraitInfo<MobileInfo>())
				crushes = self.Info.TraitInfos<MobileInfo>().First().LocomotorInfo.Crushes;
			else if (self.Info.HasTraitInfo<AircraftInfo>())
				crushes = self.Info.TraitInfos<AircraftInfo>().First().Crushes;

			nextScanTime = self.World.SharedRandom.Next(Info.MinimumScanTimeInterval, Info.MaximumScanTimeInterval);

			move = self.Trait<IMove>() as IResolveOrder;

			base.Created(self);
		}

		void INotifyIdle.TickIdle(Actor self)
		{
			if (nextScanTime-- > 0)
				return;

			var actors = self.World.FindActorsInCircle(self.CenterPosition, Info.ScanRadius);

			foreach (var a in actors)
			{
				if (a.TraitsImplementing<ICrushable>().Any(c => c.CrushableBy(a, self, crushes)))
				{
					move.ResolveOrder(self, new Order("Move", self, Target.FromCell(self.World, a.Location), false));
					break;
				}
			}

			nextScanTime = self.World.SharedRandom.Next(Info.MinimumScanTimeInterval, Info.MaximumScanTimeInterval);
		}
	}
}
