using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;
using TagLib.Ape;

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

	public class Inventory : INotifyCreated, IDeathActorInitModifier, IResolveOrder, INotifyPickUpItem, INotifyKilled
	{
		public readonly InventoryInfo Info;
		readonly Actor inventoryActor;
		readonly List<Item> items = new List<Item>();
		public readonly ItemCache ItemCache;

		Item[] autoAdd;
		INotifyInventory[] inventoryNotifiers = Array.Empty<INotifyInventory>();

		public IEnumerable<Item> Items => items.ToArray();
		public string Name => Info.Name;

		public Inventory(Actor self, InventoryInit inventoryInit, InventoryInfo inventoryInfo)
		{
			inventoryActor = self;
			autoAdd = inventoryInit?.Items;
			Info = inventoryInfo;
			ItemCache = self.World.WorldActor.Trait<ItemCache>();
			if (Info.InitItems != null)
				foreach (var name in Info.InitItems)
				{
					TryAdd(self, ItemCache.AddItem(self.World, name));
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

		public bool TryRemove(Actor self, Item item)
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
			if (order.OrderString == "TryAddItem" && order.TargetString == Name)
			{
				TryAdd(self, self.World.WorldActor.Trait<ItemCache>().GetItem(order.ExtraData));
			}
		}

		public bool PrepareForPickUpItem()
		{
			return true;
		}

		public bool Picking()
		{
			return true;
		}

		public void OnPickUpItem(Actor item)
		{
			if (item.IsInWorld && !item.IsDead)
			{
				// this item actor might not in the item cache, try to add it
				ItemCache.TryAddItem(item);
				inventoryActor.World.AddFrameEndTask(w => w.Remove(item));
				TryAdd(inventoryActor, item.Trait<Item>());
			}
		}

		public void Killed(Actor self, AttackInfo e)
		{
			var items = Items.Where(i => i.EquipmentSlot == null || !i.EquipmentSlot.KeepItemInSlotWhenKilled);
			var deathPos = self.CenterPosition;
			var deathFace = self.TraitOrDefault<IFacing>()?.Facing ?? WAngle.Zero;
			inventoryActor.World.AddFrameEndTask(w =>
			{
				foreach (var item in items)
				{
					w.Add(item.ItemActor);
					var position = item.ItemActor.TraitOrDefault<IPositionable>();
					var facing = item.ItemActor.TraitOrDefault<IFacing>();
					if (position != null)
					{
						position.SetPosition(item.ItemActor, deathPos, true);
						item.ItemActor.QueueActivity(new FallDown(item.ItemActor, deathPos, 50));
					}

					if (facing != null)
					{
						facing.Facing = deathFace + new WAngle(self.World.SharedRandom.Next(0, 1023));
					}
				}
			});
		}
	}
}
