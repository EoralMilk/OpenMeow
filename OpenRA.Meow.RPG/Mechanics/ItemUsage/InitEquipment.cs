using OpenRA.GameRules;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits.Trait3D;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Warheads;
using OpenRA.Traits;
using System.Collections.Generic;
using System.Linq;
using System;
using TagLib.Riff;
using System.Xml.Linq;

namespace OpenRA.Meow.RPG.Mechanics
{
	public class InitEquipmentInfo : TraitInfo, Requires<EquipmentSlotInfo>, Requires<InventoryInfo>
	{
		public readonly string EquipmentSlotName = null;
		public readonly int InitEquipChance = 100;
		[FieldLoader.Require]
		public readonly Dictionary<string, int> ItemPool;

		public override object Create(ActorInitializer init)
		{
			return new InitEquipment(this, init.Self);
		}

		public WeaponInfo WeaponInfo { get; private set; }
	}

	public class InitEquipment: INotifyCreated
	{
		public readonly InitEquipmentInfo Info;
		public readonly Item ItemChosen;
		public readonly EquipmentSlot slot;
		public InitEquipment(InitEquipmentInfo info, Actor self)
		{
			Info = info;
			if (info.InitEquipChance < self.World.SharedRandom.Next(100))
				return;
			if (Info.ItemPool == null || Info.ItemPool.Count == 0)
				return;

			int sumWeight = 0;

			foreach (var kv in Info.ItemPool)
			{
				sumWeight += kv.Value;
			}

			if (sumWeight <= 0)
				return;
			var inventory = self.Trait<Inventory>();
			slot = null;

			if (!string.IsNullOrEmpty(info.EquipmentSlotName))
				slot = self.TraitsImplementing<EquipmentSlot>().Single(s => s.Name == info.EquipmentSlotName);

			var itemCache = self.World.WorldActor.Trait<ItemCache>();

			var value = self.World.SharedRandom.Next(0, sumWeight);
			foreach (var kv in info.ItemPool)
			{
				value -= kv.Value;
				if (value <= 0)
				{
					ItemChosen = inventory.ItemCache.AddItem(self.World, kv.Key);
					inventory.TryAdd(self, ItemChosen);

					return;
				}
			}
		}

		public void Created(Actor self)
		{
			if (ItemChosen != null)
				slot?.TryEquip(self, ItemChosen);
		}
	}
}
