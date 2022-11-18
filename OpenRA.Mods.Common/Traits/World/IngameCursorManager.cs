using System;
using System.Collections.Generic;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	public class IngameCursorManagerInfo : TraitInfo
	{
		public override object Create(ActorInitializer init) { return new IngameCursorManager(init.World, this); }
	}

	public class IngameCursorManager
	{
		public IngameCursorManager(World world, IngameCursorManagerInfo IngameCursorManagerInfo)
		{

		}

		public string CurrentCursor = null;
		public bool CanSelect = true;
	}
}
