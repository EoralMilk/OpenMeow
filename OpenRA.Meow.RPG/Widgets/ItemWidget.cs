using System;
using OpenRA.Graphics;
using OpenRA.Meow.RPG.Mechanics;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Meow.RPG.Widgets
{
	public sealed class ItemWidget : ContainerWidget
	{
		const int RowWidth = Skin.InventoryWidth - Skin.ScrollbarWidth - Skin.SpacingSmall * 2;
		const int TextX = Skin.InventoryThumbnailSizeX + Skin.SpacingLarge;
		const int TextWidth = ItemWidget.RowWidth - Skin.InventoryThumbnailSizeX - Skin.SpacingLarge;
		const int StatsY = Skin.InventoryThumbnailSizeY - Skin.SpacingLarge - Skin.InventoryLabelHeight;

		readonly Animation thumbnail;

		readonly InventoryWidget inventoryWidget;
		readonly Item item;
		readonly Actor actor;
		public ItemWidget(InventoryWidget inventoryWidget, Actor actor, Item item, WorldRenderer worldRenderer)
		{
			this.inventoryWidget = inventoryWidget;
			this.actor = actor;
			this.item = item;
			Bounds = new Rectangle(Skin.SpacingSmall, 0, ItemWidget.RowWidth, Skin.InventoryThumbnailSizeY);

			AddChild(
				new ThumbnailWidget(item, worldRenderer) { Bounds = new Rectangle(0, 0, Skin.InventoryThumbnailSizeX, Skin.InventoryThumbnailSizeY) }
			);

			AddChild(
				new LabelWidget
				{
					Text = item.Name,
					Bounds = new Rectangle(TextX, Skin.SpacingLarge, TextWidth, Skin.InventoryLabelHeight),
					Font = Skin.InGameUiFont
				}
			);

		}

		public override bool HandleMouseInput(MouseInput mouseInput)
		{
			if (mouseInput.Button != MouseButton.Left && mouseInput.Button != MouseButton.Right)
				return false;

			// ReSharper disable once ConvertIfStatementToSwitchStatement
			if (mouseInput.Button == MouseButton.Left)
			{
				if (item.EquipmentSlot != null)
					item.EquipmentSlot.TryUnequip(actor);
				else
					inventoryWidget.TryEquip(item);
			}
			else if (mouseInput.Button == MouseButton.Right)
				inventoryWidget.TryTransfer(item);

			return true;
		}
	}
}
