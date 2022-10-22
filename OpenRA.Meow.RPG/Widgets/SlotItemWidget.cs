using System;
using OpenRA.Graphics;
using OpenRA.Meow.RPG.Mechanics;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Traits;
using OpenRA.Widgets;

namespace OpenRA.Meow.RPG.Widgets
{
	public sealed class SlotItemWidget : ShadowContainerWidget
	{
		const int RowWidth = Skin.InventoryWidth - Skin.ScrollbarWidth - Skin.SpacingSmall * 2;
		const int TextX = Skin.InventoryThumbnailSizeX + Skin.SpacingLarge;
		const int TextWidth = RowWidth - Skin.InventoryThumbnailSizeX - Skin.SpacingSmall;
		const int StatsY = Skin.InventoryThumbnailSizeY - Skin.SpacingLarge - Skin.InventoryLabelHeight;

		readonly Actor actor;
		Item item;
		readonly WorldRenderer worldRenderer;
		public SlotItemWidget(Actor actor, Item item, WorldRenderer worldRenderer)
		{
			this.actor = actor;
			this.item = item;
			this.worldRenderer = worldRenderer;
			Bounds = new Rectangle(Skin.SpacingSmall, 0, RowWidth, Skin.InventoryThumbnailSizeY);
			Render = true;
			SetItem(item);
		}

		public void SetItem(Item item)
		{
			if (this.item == item)
				return;

			if (this.item != null)
				RemoveChildren();

			this.item = item;

			if (item == null)
				return;

			AddChild(
					new ThumbnailWidget(item, worldRenderer)
					{
						Bounds = new Rectangle(0, 0, Skin.InventoryThumbnailSizeX, Skin.InventoryThumbnailSizeY)
					}
				);

			AddChild(
				new LabelWidget
				{
					Text = item.Name,
					Bounds = new Rectangle(TextX, Skin.SpacingLarge, TextWidth, Skin.InventoryLabelHeight),
					Font = Skin.InGameUiFontSmall
				}
			);
		}

		public override bool HandleMouseInput(MouseInput mouseInput)
		{
			if (item == null || (mouseInput.Button != MouseButton.Left && mouseInput.Button != MouseButton.Right))
				return false;

			if (mouseInput.Button == MouseButton.Right)
			{
				var order = new Order("TryUnequip", actor, false)
				{
					TargetString = item.EquipmentSlot.Name
				};
				actor.World.IssueOrder(order);
			}

			return true;
		}
	}
}
