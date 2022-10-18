#region Copyright & License Information
/*
 * Copyright 2007-2020 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Orders
{
	class PowerDownOrderGenerator : OrderGenerator
	{
		int2 dragStartMousePos;
		int2 dragEndMousePos;
		bool isDragging;

		public override IEnumerable<Order> Order(World world, CPos cell, int2 worldPixel, MouseInput mi)
		{
			if ((mi.Button == MouseButton.Left && mi.Event == MouseInputEvent.Down) || (mi.Button == MouseButton.Right && mi.Event == MouseInputEvent.Up) || (mi.Button == MouseButton.Left && mi.Event == MouseInputEvent.Up) || (mi.Button == MouseButton.Left && mi.Event == MouseInputEvent.Move))
				return OrderInner(world, cell, worldPixel, mi);

			return Enumerable.Empty<Order>();
		}

		protected override IEnumerable<Order> OrderInner(World world, CPos cell, int2 worldPixel, MouseInput mi)
		{
			if (mi.Button == MouseButton.Right)
				world.CancelInputMode();

			return OrderInner(world, mi, worldPixel);
		}

		protected IEnumerable<Order> OrderInner(World world, MouseInput mi, int2 worldPixel)
		{
			if (mi.Button != MouseButton.Left)
				yield break;

			dragEndMousePos = worldPixel;

			if (mi.Event == MouseInputEvent.Down)
			{
				if (!isDragging)
				{
					isDragging = true;
					dragStartMousePos = worldPixel;
				}

				yield break;
			}

			if (mi.Event == MouseInputEvent.Move)
				yield break;

			// Use "isDragging" here to avoid mis-dragging when player use hot key to switch mode.
			if (isDragging && mi.Event == MouseInputEvent.Up)
			{
				var actors = SelectToggleConditionActorsInBoxWithDeadzone(world, dragStartMousePos, dragEndMousePos, mi.Modifiers).ToArray();

				isDragging = false;

				if (!actors.Any())
					yield break;

				yield return new Order("PowerDown", null, false, groupedActors: actors);
			}
		}

		protected override IEnumerable<IRenderable> RenderAnnotations(WorldRenderer wr, World world)
		{
			var lastMousePos = wr.Viewport.ViewToWorldPx(Viewport.LastMousePos);
			if (isDragging && (lastMousePos - dragStartMousePos).Length > Game.Settings.Game.SelectionDeadzone)
			{
				var diag1 = wr.ProjectedPosition(lastMousePos);
				var diag2 = wr.ProjectedPosition(dragStartMousePos);
				var modifiers = Game.GetModifierKeys();

				/* Following code do two things:
				// 1. Draw health bar for every units/buildings can be power-down inside the box.
				// 2. Draw highlight box for each unit/building that can be power-down inside the box.
				// 3. Draw text that power changed.
				*/
				var actors = SelectToggleConditionActorsInBoxWithDeadzone(world, dragStartMousePos, lastMousePos, modifiers, true);
				int powerChanged = 0;
				var toggleConditions = actors.Any() ? actors.First().Trait<ToggleConditionOnOrder>().IsEnabled() : modifiers == Modifiers.Ctrl;
				var font = Game.Renderer.Fonts["Bold"];

				// Draw the rectangle box dragged by mouse.
				yield return new RectangleAnnotationRenderable(diag1, diag2, diag1, 2, Color.Orange);

				if (modifiers == Modifiers.Ctrl)
					yield return new TextAnnotationRenderable(font, wr.ProjectedPosition(lastMousePos + new int2(-30, 8)), 0, Color.Red, "off");
				else if (modifiers == Modifiers.Alt)
					yield return new TextAnnotationRenderable(font, wr.ProjectedPosition(lastMousePos + new int2(-30, 8)), 0, Color.Gold, "on");

				// Render actors
				foreach (var actor in actors)
				{
					var isometricSelectable = actor.TraitsImplementing<IsometricSelectable>();
					if (isometricSelectable.Any())
					{
						var bounds = isometricSelectable.First().DecorationBounds(actor, wr);
						yield return new IsometricSelectionBoxAnnotationRenderable(actor, bounds, Color.Orange);
						yield return new IsometricSelectionBarsAnnotationRenderable(actor, bounds, true, false);
					}

					// Get abulote power cost for text rendering. Please don't use "RequiresCondition: !disabled" in YAML or
					// it won't work correctly.
					powerChanged += actor.TraitsImplementing<Power>().Where(t => !t.IsTraitDisabled).Sum(p => p.Info.Amount);
				}

				if (powerChanged != 0)
				{
					if (toggleConditions)
						yield return new TextAnnotationRenderable(font, wr.ProjectedPosition(lastMousePos + new int2(70, -8)), 0, Color.Red, powerChanged.ToString());
					else
						yield return new TextAnnotationRenderable(font, wr.ProjectedPosition(lastMousePos + new int2(60, -8)), 0, Color.Gold, (0 - powerChanged).ToString());
				}
			}

			yield break;
		}

		protected IEnumerable<Actor> SelectToggleConditionActorsInBoxWithDeadzone(World world, int2 a, int2 b, Modifiers modifiers, bool forRendering = false)
		{
			// Because the "WorldInteractionControllerWidget" can show detailed unit's information when mouse over,
			// so we can just leave it alone when render under cursor actor. No needs to render it twice.
			var isDeadzone = true;
			if ((a - b).Length <= Game.Settings.Game.SelectionDeadzone)
			{
				if (forRendering)
					return Enumerable.Empty<Actor>();
				else
					isDeadzone = false;
			}

			IEnumerable<Actor> allActors;

			if (isDeadzone)
			{
				// "x.AppearsFriendlyTo(world.LocalPlayer.PlayerActor)" only select local player and allied units.
				// "x.Owner == world.LocalPlayer" only select local player units which is from local player and allied,
				// when used with the line above.
				allActors = world.ScreenMap.ActorsInMouseBox(a, b)
					.Select(x => x.Actor)
					.Where(x => x.AppearsFriendlyTo(world.LocalPlayer.PlayerActor) && x.Owner == world.LocalPlayer && !world.FogObscures(x)
						&& x.TraitsImplementing<ToggleConditionOnOrder>().Any(IsValidTrait)).ToList();

				if (!allActors.Any())
					return allActors;
			}
			else
			{
				allActors = world.ScreenMap.ActorsAtMouse(b)
					.Select(x => x.Actor)
					.Where(x => x.AppearsFriendlyTo(world.LocalPlayer.PlayerActor) && x.Owner == world.LocalPlayer && !world.FogObscures(x)
						&& x.TraitsImplementing<ToggleConditionOnOrder>().Any(IsValidTrait));
				return allActors;
			}

			if (forRendering)
				allActors = allActors.Where(x => x.TraitOrDefault<ISelectionDecorations>() != null);

			/* Modifiers for Powerdown Mode
			// Default: generally turn on/off with smart selection.
			// Ctrl: Only turn off.
			// Alt: Only turn on.
			*/
			if (modifiers == Modifiers.Ctrl)
				return allActors = allActors.Where(x => !x.Trait<ToggleConditionOnOrder>().IsEnabled());
			else if (modifiers == Modifiers.Alt)
				return allActors = allActors.Where(x => x.Trait<ToggleConditionOnOrder>().IsEnabled());

			// Default modifier:
			else
			{
				/* Smart Selection Of Buildings: at first, check power-down status of things inside,
				// then either select those who are not power-down or select all whose power-down status are actived.
				*/
				if (!allActors.All(x => x.Trait<ToggleConditionOnOrder>().IsEnabled()))
					return allActors.Where(x => !x.Trait<ToggleConditionOnOrder>().IsEnabled());
				return allActors;
			}
		}

		protected override string GetCursor(World world, CPos cell, int2 worldPixel, MouseInput mi)
		{
			// "x.Info.HasTraitInfo<ISelectableInfo>()" avoids selecting some special actors like "camera" and "mutiplayer starting point".
			var underCursor = world.ScreenMap.ActorsAtMouse(worldPixel)
					.Select(x => x.Actor)
					.Where(x => x.Info.HasTraitInfo<ISelectableInfo>() && !world.FogObscures(x));

			// ONLY when the mouse is over an enemy/allied/powerdown-blocked and selectable actor, the cursor will change to "powerdown-blocked",
			// which means cursor is "powerdown" when no normal actors under the mouse.
			if (!underCursor.Any())
				return "powerdown";
			else
			{
				var actor = underCursor.First();
				if (actor.AppearsFriendlyTo(world.LocalPlayer.PlayerActor) && actor.Owner == world.LocalPlayer
						&& actor.TraitsImplementing<ToggleConditionOnOrder>().Any(IsValidTrait))
					return "powerdown";
				else
					return "powerdown-blocked";
			}
		}

		protected bool IsValidTrait(ToggleConditionOnOrder t)
		{
			return !t.IsTraitDisabled && !t.IsTraitPaused;
		}

		protected override IEnumerable<IRenderable> Render(WorldRenderer wr, World world) { yield break; }
		protected override IEnumerable<IRenderable> RenderAboveShroud(WorldRenderer wr, World world) { yield break; }
	}
}
