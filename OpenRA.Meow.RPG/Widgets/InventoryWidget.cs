﻿using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Meow.RPG.Mechanics;
using OpenRA.Widgets;

namespace OpenRA.Meow.RPG.Widgets
{
	public sealed class InventoryWidget : ShadowScrollContainerWidget
	{
		readonly WorldRenderer worldRenderer;
		readonly Dictionary<Item, ItemWidget> itemWidgets = new Dictionary<Item, ItemWidget>();

		public Actor InventoryActor;
		public Inventory Inventory;
		public Actor OtherInventoryActor;
		public Inventory OtherInventory;
		readonly World world;
		public InventoryWidget(World world,WorldRenderer worldRenderer)
		{
			this.world = world;
			this.worldRenderer = worldRenderer;

			BottomSpacing = Skin.SpacingSmall;
		}

		public override void Tick()
		{
			foreach (var (item, itemWidget) in itemWidgets.ToArray())
			{
				if (Inventory != null && Inventory.Items.Contains(item) && Render)
					continue;

				itemWidgets.Remove(item);
				RemoveChild(itemWidget);
			}

			if (!Render)
				return;

			if (Inventory != null && InventoryActor != null)
			{
				foreach (var item in Inventory.Items)
				{
					if (itemWidgets.ContainsKey(item))
						continue;

					var itemWidget = new ItemWidget(this, InventoryActor, item, worldRenderer);
					itemWidgets.Add(item, itemWidget);
					AddChild(itemWidget);
				}
			}

			var y = Skin.SpacingSmall;

			foreach (var itemWidget in itemWidgets.Values)
			{
				itemWidget.Bounds.Y = y;
				y += itemWidget.Bounds.Height + Skin.SpacingSmall;
			}
		}

		public void TryTransfer(Item item)
		{
			if (OtherInventoryActor == null)
				return;

			OtherInventory?.TryAdd(OtherInventoryActor, item);
		}

		public void TryEquip(Item item)
		{
			if (InventoryActor == null)
				return;

			foreach (var equipmentSlot in InventoryActor.TraitsImplementing<EquipmentSlot>())
			{
				if (equipmentSlot.TryEquip(InventoryActor, item))
					break;
			}
		}
	}
}