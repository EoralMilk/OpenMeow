using System;
using System.Collections.Generic;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	class KilledWhenPlaceBuildingInfo : TraitInfo
	{
		public override object Create(ActorInitializer init) { return new KilledWhenPlaceBuilding(init.Self, this); }
	}

	class KilledWhenPlaceBuilding
	{
		readonly KilledWhenPlaceBuildingInfo info;
		readonly Actor self;

		public KilledWhenPlaceBuilding(Actor self, KilledWhenPlaceBuildingInfo info)
		{
			this.self = self;
			this.info = info;
		}

	}
}
