﻿using System;
using System.Collections.Generic;
using System.Linq;
using GlmSharp;
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

	public class EquipmentSlot : ConditionalTrait<EquipmentSlotInfo>,INotifyCreated, INotifyInventory, IDeathActorInitModifier, IResolveOrder
	{
		readonly EquipmentSlotInfo info;

		List<Item> autoEquip;
		Inventory inventory;
		INotifyEquip[] equipNotifiers = Array.Empty<INotifyEquip>();
		RenderMeshes renderMeshes;
		WithSkeleton withSkeleton;
		int boneId = -1;
		Func<mat4> slotGetRenderMatrix;

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
			renderMeshes = self.TraitOrDefault<RenderMeshes>();
			equipNotifiers = self.TraitsImplementing<INotifyEquip>().ToArray();

			if (info.EquipmentSkeleton != null)
			{
				if (info.EquipmentBone == null)
					throw new Exception("EquipmentBone can not be null if we use EquipmentSkeleton");

				withSkeleton = self.TraitsImplementing<WithSkeleton>().Single(w => w.Info.Name == info.EquipmentSkeleton);
				if (withSkeleton == null)
					throw new Exception("Can not find EquipmentSkeleton");

				boneId = withSkeleton.GetBoneId(info.EquipmentBone);
				slotGetRenderMatrix = () => withSkeleton.GetRenderMatrixFromBoneId(boneId);
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
			if (boneId != -1 && slotGetRenderMatrix != null)
			{
				withSkeleton.SetBoneRenderUpdate(boneId, update);
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

			Item.EquipingEffect(slotActor);

			foreach (var notifyEquip in equipNotifiers)
				notifyEquip.Equipped(self, item);

			return true;
		}

		public void ToggleEquipmentRender(bool toggle)
		{
			if (Item != null)
			{
				if (toggle == false)
				{
					if (renderMeshes != null && slotGetRenderMatrix != null)
					{
						var itemMeshes = Item.ItemActor.TraitsImplementing<ItemMesh>();
						foreach (var im in itemMeshes)
						{
							im.MeshInstance.Matrix = null;
							renderMeshes.Remove(im.MeshInstance);
						}
					}
				}
				else
				{
					if (renderMeshes != null && slotGetRenderMatrix != null)
					{
						var itemMeshes = Item.ItemActor.TraitsImplementing<ItemMesh>();
						foreach (var im in itemMeshes)
						{
							im.MeshInstance.Matrix = slotGetRenderMatrix;
							renderMeshes.Add(im.MeshInstance);
						}
					}
				}

				ToggleEquipBoneUpdate(toggle);

			}
			else
			{
				ToggleEquipBoneUpdate(false);
			}
		}

		public bool TryUnequip(Actor self)
		{
			if (!CanUnequip(self))
				return false;

			if (Item == null)
				return true;

			ToggleEquipmentRender(false);

			var item = Item;

			item.UnequipingEffect(slotActor);

			Item.EquipmentSlot = null;
			Item = null;

			foreach (var notifyEquip in equipNotifiers)
				notifyEquip.Unequipped(self, item);

			return true;
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
	}
}
