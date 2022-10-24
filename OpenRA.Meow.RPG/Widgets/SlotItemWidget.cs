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
		readonly int rowWidth;
		readonly int textX;
		readonly int textWidth;
		readonly int thumbnailX = 3;
		readonly int thumbnailY = 3;

		readonly Actor actor;
		Item item;
		readonly WorldRenderer worldRenderer;
		ThumbnailWidget thumbnailWidget;
		LabelWidget labelWidget;
		readonly LabelWidget emptyLabel;
		readonly EquipmentSlot slot;

		public SlotItemWidget(Actor actor, EquipmentSlot equipmentSlot, Item item, WorldRenderer worldRenderer, Skin skin)
			: base(skin)
		{
			this.actor = actor;
			this.worldRenderer = worldRenderer;
			slot = equipmentSlot;
			rowWidth = Skin.InventoryWidth - Skin.ScrollbarWidth - Skin.SpacingSmall * 2;
			textX = thumbnailX + Skin.InventoryThumbnailSizeX + Skin.SpacingSmall;
			textWidth = rowWidth - textX - 2;

			Bounds = new Rectangle(Skin.SpacingSmall, 0, rowWidth, Skin.InventoryThumbnailSizeY + 6);
			Render = true;

			emptyLabel = new LabelWidget
			{
				Text = slot.Name,
				Bounds = new Rectangle(Skin.SpacingLarge, Skin.SpacingLarge, rowWidth - Skin.SpacingLarge * 2, Skin.InventoryLabelHeight),
				Font = Skin.InGameUiFont,
				FontsForScale = Skin.Fontsmall,
				GetColor = () => slot != null && !slot.IsTraitDisabled ? emptyLabel.TextColor : Color.DarkGray,
				Align = TextAlign.Center,
			};

			AddChild(emptyLabel);

			SetItem(item);
			ShadowSkin = () => slot != null && !slot.IsTraitDisabled ? Skin.BrightShadowSkin : Skin.DarkShadowSkin;
		}

		public void SetItem(Item item)
		{
			if (this.item == item)
				return;

			RemoveChildren();

			this.item = item;

			if (item == null)
			{
				AddChild(emptyLabel);
				return;
			}

			thumbnailWidget = new ThumbnailWidget(item, worldRenderer, Skin)
			{
				Bounds = new Rectangle(thumbnailX, thumbnailY, Skin.InventoryThumbnailSizeX, Skin.InventoryThumbnailSizeY)
			};

			labelWidget = new LabelWidget
			{
				Text = item.Name,
				Bounds = new Rectangle(textX, thumbnailY + Skin.SpacingLarge, textWidth, Skin.InventoryLabelHeight),
				Font = Skin.InGameUiFont,
				FontsForScale = Skin.Fontsmall,
			};

			AddChild(thumbnailWidget);

			AddChild(labelWidget);
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
