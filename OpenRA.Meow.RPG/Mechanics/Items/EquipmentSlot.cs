using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Meow.RPG.Mechanics
{
	public interface INotifyEquip
	{
		bool CanEquip(Actor self, Item item);
		void Equipped(Actor self, Item item);
		bool CanUnequip(Actor self, Item item);
		void Unequipped(Actor self, Item item);
	}

	public class EquipmentSlotsInit : ActorInit, ISingleInstanceInit
	{
		/// <summary>
		/// key: SlotType, Value: Item
		/// </summary>
		public readonly Dictionary<string, List<Item>> Items = new Dictionary<string, List<Item>>();

		public void AddItem(string type, Item item)
		{
			if (Items.ContainsKey(type))
				Items[type].Add(item);
			else
			{
				Items[type] = new List<Item>
				{
					item
				};
			}
		}

		public override MiniYaml Save()
		{
			throw new NotImplementedException();
		}
	}

	public class EquipmentSlotInfo : TraitInfo
	{
		[FieldLoader.Require]
		public readonly string Name = null;

		[Desc("The items type name of this equipment slot can equip.")]
		public readonly string SlotType = "Item";

		public readonly string InitEquipment = null;

		public override object Create(ActorInitializer init)
		{
			return new EquipmentSlot(this, init.GetOrDefault<EquipmentSlotsInit>(), init.Self);
		}
	}

	public class EquipmentSlot : INotifyCreated, INotifyInventory, IDeathActorInitModifier, IResolveOrder
	{
		readonly EquipmentSlotInfo info;

		List<Item> autoEquip;
		Inventory inventory;
		INotifyEquip[] equipNotifiers = Array.Empty<INotifyEquip>();

		public Item Item { get; private set; }

		public string SlotType => info.SlotType;
		public string Name => info.Name;

		readonly Actor slotActor;
		public Actor SlotOwnerActor => slotActor;

		public EquipmentSlot(EquipmentSlotInfo info, EquipmentSlotsInit equipmentSlotsInit, Actor self)
		{
			this.info = info;
			slotActor = self;
			equipmentSlotsInit?.Items.TryGetValue(this.info.SlotType, out autoEquip);
		}

		void INotifyCreated.Created(Actor self)
		{
			inventory = self.TraitOrDefault<Inventory>();

			if (info.InitEquipment != null && inventory.Info.InitItems.Contains(info.InitEquipment))
			{
				TryEquip(self, inventory.Items.Where(i => i.ItemActor.Info.Name == info.InitEquipment).First(), false);
			}
			else
			{
				throw new Exception("inventory has no InitEquipment: " + info.InitEquipment + " , the InitEquipment should be Item Actor Name not Item Name");
			}

			equipNotifiers = self.TraitsImplementing<INotifyEquip>().ToArray();

			if (self.TraitsImplementing<EquipmentSlot>().Where(slot => slot.Name == Name).ToArray().Length > 1)
				throw new Exception("The Name of EquipmentSlot should be unique in one actor");

			if (autoEquip == null)
				return;

			foreach (var equip in autoEquip)
			{
				if (TryEquip(self, equip, false))
					break;
			}

			autoEquip = null;
		}

		bool INotifyInventory.TryAdd(Actor self, Item item)
		{
			return true;
		}

		void INotifyInventory.Added(Actor self, Item item)
		{
		}

		bool INotifyInventory.TryRemove(Actor self, Item item)
		{
			// no same item in slot, ok and skip
			// same item in slot, but can unequip, ok
			return Item != item || TryUnequip(self);
		}

		void INotifyInventory.Removed(Actor self, Item item)
		{
		}

		public void ModifyDeathActorInit(Actor self, TypeDictionary init)
		{
			if (Item == null)
				return;

			if (!init.Contains<EquipmentSlotsInit>())
				init.Add(new EquipmentSlotsInit());

			init.GetOrDefault<EquipmentSlotsInit>()?.AddItem(info.SlotType, Item);
		}

		public bool CanEquip(Actor self, Item item, bool canFromSlot = true, bool canReplace = false)
		{
			if (Item == item)
				return true;

			if (Item != null && !canReplace)
				return false;

			if (item.Type != info.SlotType || (item.EquipmentSlot != null && !canFromSlot))
				return false;

			if (inventory == null || !inventory.Items.Contains(item))
				return false;

			if (equipNotifiers.Any(notifyEquip => !notifyEquip.CanEquip(self, item)))
				return false;

			if (Item != null && !CanUnequip(self))
				return false;

			return true;
		}

		public bool CanUnequip(Actor self)
		{
			if (Item == null)
				return true;

			if (equipNotifiers.Any(notifyEquip => !notifyEquip.CanUnequip(self, Item)))
				return false;

			return true;
		}

		public bool TryEquip(Actor self, Item item, bool canFromSlot = true)
		{
			if (!CanEquip(self, item, canFromSlot))
				return false;

			if (Item == item)
				return true;

			if (Item != null && !TryUnequip(self))
				return false;

			Item = item;
			Item.EquipmentSlot = this;

			SlotOwnerActor.World.AddFrameEndTask(w =>
			{
				Item.EquipingEffect(slotActor);
			});

			foreach (var notifyEquip in equipNotifiers)
				notifyEquip.Equipped(self, item);

			return true;
		}

		public bool TryUnequip(Actor self)
		{
			if (!CanUnequip(self))
				return false;

			if (Item == null)
				return true;

			var item = Item;

			SlotOwnerActor.World.AddFrameEndTask(w =>
			{
				item.UnequipingEffect(slotActor);
			});

			Item.EquipmentSlot = null;
			Item = null;

			foreach (var notifyEquip in equipNotifiers)
				notifyEquip.Unequipped(self, item);

			return true;
		}

		public void ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString == "TryEquip" && order.TargetString == Name)
			{
				TryEquip(self, ItemCache.GetItem(order.ExtraData), false);
			}
			else if (order.OrderString == "TryUnequip" && order.TargetString == Name)
			{
				TryUnequip(self);
			}
		}
	}
}
