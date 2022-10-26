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

		public Actor Actor { get; private set; }
		readonly World world;
		public bool ActiveToggle = true;

		public EquipmentSlotsWidget(World world,WorldRenderer worldRenderer, Skin skin)
			: base(skin)
		{
			this.world = world;
			this.worldRenderer = worldRenderer;
			BottomSpacing = Skin.SpacingSmall;
			IsVisible = () => ActiveToggle && Actor != null && slotWidgets.Count > 0 && world.LocalPlayer == Actor.Owner;
		}

		public void UpdateActor(Actor actor)
		{
			if (actor == Actor)
				return;

			SlotItemWidget.ForcusSlot = null;

			if (Actor != null)
			{
				foreach (var w in slotWidgets.Values)
					RemoveChild(w);
				slotWidgets.Clear();
			}

			Actor = actor;

			if (Actor == null)
				return;

			var slots = Actor.TraitsImplementing<EquipmentSlot>().ToArray();

			if (slots == null || slots.Length == 0)
				return;

			var y = Skin.SpacingSmall;

			foreach (var slot in slots)
			{
				var slotWidget = new SlotItemWidget(Actor, slot, slot.Item, worldRenderer, Skin);
				slotWidget.IsVisible = IsVisible;
				slotWidgets.Add(slot, slotWidget);
				AddChild(slotWidget);

				slotWidget.Bounds.Y = y;
				y += slotWidget.Bounds.Height + Skin.SpacingSmall;
			}
		}

		public override void Tick()
		{
			if (Actor == null)
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
