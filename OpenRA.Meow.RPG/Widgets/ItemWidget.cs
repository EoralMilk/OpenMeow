using System;
using OpenRA.Graphics;
using OpenRA.Meow.RPG.Mechanics;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Traits;
using OpenRA.Widgets;

namespace OpenRA.Meow.RPG.Widgets
{
	public sealed class ItemWidget : ContainerWidget
	{
		readonly int rowWidth;
		readonly int textX;
		readonly int textWidth;
		readonly int statsY;

		readonly Animation thumbnail;

		readonly InventoryWidget inventoryWidget;
		readonly Item item;
		readonly Actor actor;
		public readonly Skin Skin;
		public ItemWidget(InventoryWidget inventoryWidget, Actor actor, Item item, WorldRenderer worldRenderer, Skin skin)
		{
			this.inventoryWidget = inventoryWidget;
			this.actor = actor;
			this.item = item;

			Skin = skin;

			rowWidth = Skin.InventoryWidth - Skin.ScrollbarWidth - Skin.SpacingSmall * 2;
			textX = Skin.InventoryThumbnailSizeX + Skin.SpacingLarge;
			textWidth = rowWidth - Skin.InventoryThumbnailSizeX - Skin.SpacingSmall;
			statsY = Skin.InventoryThumbnailSizeY - Skin.SpacingLarge - Skin.InventoryLabelHeight;

			Bounds = new Rectangle(Skin.SpacingSmall, 0, rowWidth, Skin.InventoryThumbnailSizeY);

			AddChild(
				new ThumbnailWidget(item, worldRenderer, Skin) {
					Bounds = new Rectangle(0, 0, Skin.InventoryThumbnailSizeX, Skin.InventoryThumbnailSizeY) }
			);

			AddChild(
				new LabelWidget
				{
					Text = item.Name,
					Bounds = new Rectangle(textX, Skin.SpacingLarge, textWidth, Skin.InventoryLabelHeight),
					Font = Skin.InGameUiFont,
					FontsForScale = Skin.Fontsmall,
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
				//item.EquipmentSlot.TryUnequip(actor);

				if (item.EquipmentSlot != null)
				{
					var order = new Order("TryUnequip", actor, false)
					{
						TargetString = item.EquipmentSlot.Name
					};
					actor.World.IssueOrder(order);
				}
				else
					inventoryWidget.TryEquip(item);
			}
			else if (mouseInput.Button == MouseButton.Right)
				inventoryWidget.TryTransfer(item);

			return true;
		}
	}
}
