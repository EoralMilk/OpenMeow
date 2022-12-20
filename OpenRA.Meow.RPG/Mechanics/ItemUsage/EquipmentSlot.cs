using System;
using System.Collections.Generic;
using System.Linq;
using GlmSharp;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Trait3D;
using OpenRA.Primitives;
using OpenRA.Traits;
using TagLib.Ape;

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

	public class EquipmentSlotInfo : ConditionalTraitInfo
	{
		[FieldLoader.Require]
		public readonly string Name = null;

		[Desc("The items type name of this equipment slot can equip.")]
		public readonly string SlotType = "Item";

		public readonly string InitEquipment = null;

		public readonly bool KeepItemInSlotWhenKilled = false;

		public readonly string EquipmentSkeleton;

		public readonly string EquipmentBone;

		public override object Create(ActorInitializer init)
		{
			return new EquipmentSlot(this, init.GetOrDefault<EquipmentSlotsInit>(), init.Self);
		}
	}

	public class EquipmentSlot : ConditionalTrait<EquipmentSlotInfo>,INotifyCreated, INotifyInventory, IDeathActorInitModifier, IResolveOrder,
		ITick
	{
		readonly EquipmentSlotInfo info;

		List<Item> autoEquip;
		Inventory inventory;
		INotifyEquip[] equipNotifiers = Array.Empty<INotifyEquip>();
		INotifyConsumeItem[] consumeNotifiers = Array.Empty<INotifyConsumeItem>();
		public RenderMeshes RenderMeshes { get; private set; }
		public WithSkeleton SkeletonBind { get; private set; }
		public int BoneId { get; private set; }
		public Func<mat4> SlotGetRenderMatrix { get; private set; }

		public Item Item { get; private set; }

		public string SlotType => info.SlotType;
		public string Name => info.Name;

		readonly Actor slotActor;
		public Actor SlotOwnerActor => slotActor;

		public bool KeepItemInSlotWhenKilled => info.KeepItemInSlotWhenKilled;

		public readonly ItemCache ItemCache;

		public EquipmentSlot(EquipmentSlotInfo info, EquipmentSlotsInit equipmentSlotsInit, Actor self)
			: base(info)
		{
			this.info = info;
			slotActor = self;
			ItemCache = self.World.WorldActor.Trait<ItemCache>();
			equipmentSlotsInit?.Items.TryGetValue(this.info.SlotType, out autoEquip);
		}

		void INotifyCreated.Created(Actor self)
		{
			inventory = self.TraitOrDefault<Inventory>();
			RenderMeshes = self.TraitOrDefault<RenderMeshes>();
			equipNotifiers = self.TraitsImplementing<INotifyEquip>().ToArray();
			consumeNotifiers = self.TraitsImplementing<INotifyConsumeItem>().ToArray();

			if (info.EquipmentSkeleton != null)
			{
				if (info.EquipmentBone == null)
					throw new Exception("EquipmentBone can not be null if we use EquipmentSkeleton");

				SkeletonBind = self.TraitsImplementing<WithSkeleton>().Single(w => w.Info.Name == info.EquipmentSkeleton);
				if (SkeletonBind == null)
					throw new Exception("Can not find EquipmentSkeleton");

				BoneId = SkeletonBind.GetBoneId(info.EquipmentBone);
				SlotGetRenderMatrix = () => SkeletonBind.GetRenderMatrixFromBoneId(BoneId);
			}
			else
			{
				SkeletonBind = null;
				BoneId = -1;
			}

			if (info.InitEquipment != null && inventory.Info.InitItems.Contains(info.InitEquipment))
			{
				TryEquip(self, inventory.Items.Where(i => i.ItemActor.Info.Name == info.InitEquipment).First(), false);
			}

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

		void ToggleEquipBoneUpdate(bool update)
		{
			if (BoneId != -1 && SlotGetRenderMatrix != null)
			{
				SkeletonBind.SetBoneRenderUpdate(BoneId, update);
				SkeletonBind.Skeleton.TempUpdateRenderSingle(BoneId);
			}
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

			if (item.ItemActor == null || item.ItemActor.IsDead)
				return false;

			if (Item != null && !canReplace)
				return false;

			if (IsTraitDisabled)
				return false;

			if (item.Type != info.SlotType || (item.EquipmentSlot != null && !canFromSlot))
				return false;

			if (inventory == null || !inventory.Items.Contains(item))
				return false;

			if (equipNotifiers.Any(notifyEquip => !notifyEquip.CanEquip(self, item)))
				return false;

			if (item is ConsumableItem && consumeNotifiers.Any(notify => !notify.CanConsume(item)))
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

		public bool TryEquip(Actor self, Item item, bool canFromSlot = true, bool canReplace = false)
		{
			if (!CanEquip(self, item, canFromSlot, canReplace))
				return false;

			if (Item == item)
				return true;

			if (Item != null && !TryUnequip(self))
				return false;

			Item = item;
			Item.EquipmentSlot = this;

			ToggleEquipmentRender(true);

			Item.EquipingEffect(slotActor, this);

			foreach (var notifyEquip in equipNotifiers)
				notifyEquip.Equipped(self, item);

			return true;
		}

		public void ToggleEquipmentRender(bool toggle)
		{
			ToggleEquipBoneUpdate(toggle);
		}

		public bool TryUnequip(Actor self)
		{
			if (!CanUnequip(self))
				return false;

			if (Item == null)
				return true;

			ToggleEquipmentRender(false);

			var item = Item;

			item.UnequipingEffect(slotActor, this);

			Item.EquipmentSlot = null;
			Item = null;

			foreach (var notifyEquip in equipNotifiers)
				notifyEquip.Unequipped(self, item);

			return true;
		}

		public void RemoveItem(Actor self)
		{
			if (Item == null)
				return;
			Item.EquipmentSlot = null;
			Item = null;
		}


		public void ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString == "TryEquip" && order.TargetString == Name && ItemCache.HasItem(order.ExtraData))
			{
				TryEquip(self, self.World.WorldActor.Trait<ItemCache>().GetItem(order.ExtraData), false);
			}
			else if (order.OrderString == "TryEquipForce" && order.TargetString == Name && ItemCache.HasItem(order.ExtraData))
			{
				TryEquip(self, self.World.WorldActor.Trait<ItemCache>().GetItem(order.ExtraData), true, true);
			}
			else if (order.OrderString == "TryUnequip" && order.TargetString == Name)
			{
				TryUnequip(self);
			}
		}

		protected override void TraitEnabled(Actor self)
		{
		}

		protected override void TraitDisabled(Actor self)
		{
			TryUnequip(self);
		}

		public void Tick(Actor self)
		{
			if (Item != null)
				Item.TickItem();
		}
	}
}
