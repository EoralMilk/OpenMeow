using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Meow.RPG.Mechanics;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Meow.RPG.Widgets
{
	public sealed class EquipmentSlotsWidget : ShadowScrollContainerWidget
	{
		readonly WorldRenderer worldRenderer;
		readonly Dictionary<EquipmentSlot, SlotItemWidget> slotWidgets = new Dictionary<EquipmentSlot, SlotItemWidget>();

		Actor thisActor;
		readonly World world;
		public EquipmentSlotsWidget(World world,WorldRenderer worldRenderer, Skin skin)
			: base(skin)
		{
			this.world = world;
			this.worldRenderer = worldRenderer;
			BottomSpacing = Skin.SpacingSmall;
			IsVisible = () => thisActor != null && slotWidgets.Count > 0 && world.LocalPlayer == thisActor.Owner;
		}

		public void UpdateActor(Actor actor)
		{
			if (actor == thisActor)
				return;

			SlotItemWidget.ForcusSlot = null;

			if (thisActor != null)
			{
				foreach (var w in slotWidgets.Values)
					RemoveChild(w);
				slotWidgets.Clear();
			}

			thisActor = actor;

			if (thisActor == null)
				return;

			var slots = thisActor.TraitsImplementing<EquipmentSlot>().ToArray();

			if (slots == null || slots.Length == 0)
				return;

			var y = Skin.SpacingSmall;

			foreach (var slot in slots)
			{
				var slotWidget = new SlotItemWidget(thisActor, slot, slot.Item, worldRenderer, Skin);
				slotWidget.IsVisible = IsVisible;
				slotWidgets.Add(slot, slotWidget);
				AddChild(slotWidget);

				slotWidget.Bounds.Y = y;
				y += slotWidget.Bounds.Height + Skin.SpacingSmall;
			}
		}

		public override void Tick()
		{
			if (thisActor == null)
			{
				return;
			}

			if (SlotItemWidget.ForcusSlot != null && SlotItemWidget.ForcusSlot.IsTraitDisabled)
			{
				SlotItemWidget.ForcusSlot = null;
			}

			foreach (var (slot, itemWidget) in slotWidgets)
			{
				itemWidget.SetItem(slot.Item);
			}
		}

		public override bool HandleMouseInput(MouseInput mouseInput)
		{
			return base.HandleMouseInput(mouseInput) || EventBounds.Contains(mouseInput.Location);
		}
	}
}
