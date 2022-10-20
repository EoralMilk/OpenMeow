using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Meow.RPG.Mechanics.Global
{
	public class ItemsManagerInfo : TraitInfo
	{
		public override object Create(ActorInitializer init)
		{
			return new ItemsManager(init.World, this);
		}
	}

	public class ItemsManager : INotifyCreated
	{
		readonly World _world;
		readonly HashSet<string> itemsActor = new HashSet<string>();
		public ItemsManager(World world, ItemsManagerInfo itemsManagerInfo)
		{
			_world = world;
		}

		void INotifyCreated.Created(Actor self)
		{

		}

	}
}
