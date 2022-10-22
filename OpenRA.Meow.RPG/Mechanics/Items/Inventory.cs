using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Meow.RPG.Mechanics
{
	public interface INotifyInventory
	{
		bool TryAdd(Actor self, Item item);
		void Added(Actor self, Item item);
		bool TryRemove(Actor self, Item item);
		void Removed(Actor self, Item item);
	}

	public class InventoryInit : ActorInit, ISingleInstanceInit
	{
		public readonly Item[] Items;

		public InventoryInit(Item[] items)
		{
			this.Items = items;
		}

		public override MiniYaml Save()
		{
			throw new NotImplementedException();
		}
	}

	public class InventoryInfo : TraitInfo
	{
		public readonly string Name = "none";

		public readonly string ThumbnailImage = "inventroy";

		[SequenceReference]
		[Desc("The sequence name that defines the item thumbnail sprites.")]
		public readonly string ThumbnailSequence = "none";

		[Desc("init item actor names")]
		public readonly string[] InitItems = null;

		public override object Create(ActorInitializer init)
		{
			return new Inventory(init.Self, init.GetOrDefault<InventoryInit>(), this);
		}
	}

	public class Inventory : INotifyCreated, IDeathActorInitModifier, IResolveOrder
	{
		public readonly InventoryInfo Info;
		readonly Actor inventoryActor;
		readonly List<Item> items = new List<Item>();

		Item[] autoAdd;
		INotifyInventory[] inventoryNotifiers = Array.Empty<INotifyInventory>();

		public IEnumerable<Item> Items => items.ToArray();

		public Inventory(Actor self, InventoryInit inventoryInit, InventoryInfo inventoryInfo)
		{
			inventoryActor = self;
			autoAdd = inventoryInit?.Items;
			Info = inventoryInfo;

			if (Info.InitItems != null)
				foreach (var name in Info.InitItems)
				{
					var a = self.World.CreateActor(false, name, new TypeDictionary());
					var item = a.TraitOrDefault<Item>();
					if (item != null)
						TryAdd(self, item);
					else
						throw new Exception("The actor: " + name + " does not have Item trait");
				}
		}

		public void ModifyDeathActorInit(Actor self, TypeDictionary init)
		{
			init.Add(new InventoryInit(items.ToArray()));
		}

		void INotifyCreated.Created(Actor self)
		{
			inventoryNotifiers = self.TraitsImplementing<INotifyInventory>().ToArray();

			if (autoAdd == null)
				return;

			foreach (var item in autoAdd)
				TryAdd(self, item);

			autoAdd = null;
		}

		public bool TryAdd(Actor self, Item item)
		{
			if (items.Contains(item))
				return true;

			if (inventoryNotifiers.Any(notifyInventory => !notifyInventory.TryAdd(self, item)))
				return false;

			if (item.Inventory != null && !item.Inventory.TryRemove(item.Inventory.inventoryActor, item))
				return false;

			items.Add(item);
			item.Inventory = this;

			foreach (var notifyInventory in inventoryNotifiers)
				notifyInventory.Added(self, item);

			return true;
		}

		bool TryRemove(Actor self, Item item)
		{
			if (!items.Contains(item))
				return false;

			if (inventoryNotifiers.Any(notifyInventory => !notifyInventory.TryRemove(self, item)))
				return false;

			items.Remove(item);
			item.Inventory = null;

			foreach (var notifyInventory in this.inventoryNotifiers)
				notifyInventory.Removed(self, item);

			return true;
		}

		public void ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString == "TryAddItem")
			{
				if (order.Target.Actor == null || order.Target.Actor.IsDead)
					return;
				var item = order.Target.Actor.TraitOrDefault<Item>();
				if (item == null)
					throw new Exception(order.Target.Actor.Info + " is not an Item Actor");

				TryAdd(self, item);
			}
		}
	}
}
