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
		const int TextX = Skin.InventoryThumbnailSizeX + Skin.SpacingSmall;
		const int TextWidth = RowWidth - TextX - 2;
		const int StatsY = Skin.InventoryThumbnailSizeY - Skin.SpacingLarge - Skin.InventoryLabelHeight;

		readonly Actor actor;
		Item item;
		readonly WorldRenderer worldRenderer;
		ThumbnailWidget thumbnailWidget;
		LabelWidget labelWidget;
		readonly EquipmentSlot slot;
		public SlotItemWidget(Actor actor, EquipmentSlot equipmentSlot, Item item, WorldRenderer worldRenderer)
		{
			this.actor = actor;
			this.worldRenderer = worldRenderer;
			slot = equipmentSlot;
			Bounds = new Rectangle(Skin.SpacingSmall, 0, RowWidth, Skin.InventoryThumbnailSizeY + 6);
			Render = true;

			labelWidget = new LabelWidget
			{
				Text = slot.Name,
				Bounds = new Rectangle(Skin.SpacingLarge, Skin.SpacingLarge, RowWidth - Skin.SpacingLarge * 2, Skin.InventoryLabelHeight),
				Font = Skin.InGameUiFont,
				FontsForScale = Skin.Fontsmall,
				Align = TextAlign.Center,
			};

			AddChild(labelWidget);

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
				labelWidget = new LabelWidget
				{
					Text = slot.Name,
					Bounds = new Rectangle(Skin.SpacingLarge, Skin.SpacingLarge, RowWidth - Skin.SpacingLarge * 2, Skin.InventoryLabelHeight),
					Font = Skin.InGameUiFont,
					FontsForScale = Skin.Fontsmall,
					Align = TextAlign.Center,
				};

				AddChild(labelWidget);
				return;
			}

			thumbnailWidget = new ThumbnailWidget(item, worldRenderer)
			{
				Bounds = new Rectangle(3, 3, Skin.InventoryThumbnailSizeX, Skin.InventoryThumbnailSizeY)
			};

			labelWidget = new LabelWidget
			{
				Text = item.Name,
				Bounds = new Rectangle(TextX, Skin.SpacingLarge, TextWidth, Skin.InventoryLabelHeight),
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
