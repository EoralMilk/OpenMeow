#region Copyright & License Information
/*
 * Copyright 2007-2022 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Terrain;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Mods.Common.Widgets
{
	public sealed class EditorTerrainMaskBrush : IEditorBrush
	{
		readonly MaskBrush brush;
		readonly WorldRenderer worldRenderer;
		readonly World world;

		readonly EditorViewportControllerWidget editorWidget;
		readonly EditorActionManager editorActionManager;
		readonly EditorCursorLayer editorCursor;
		readonly Func<int[]> getActiveLayers;
		readonly int cursorToken;

		bool painting;
		WPos lastPaintPos;

		public EditorTerrainMaskBrush(Func<int[]> getActiveLayers, EditorViewportControllerWidget editorWidget, MaskBrush brush, WorldRenderer wr)
		{
			this.getActiveLayers = getActiveLayers;
			this.brush = brush;
			this.editorWidget = editorWidget;
			worldRenderer = wr;
			world = wr.World;

			editorActionManager = world.WorldActor.Trait<EditorActionManager>();
			editorCursor = world.WorldActor.Trait<EditorCursorLayer>();

			worldRenderer = wr;
			world = wr.World;

			cursorToken = editorCursor.SetTerrainBrush(wr, brush);
		}

		public bool HandleMouseInput(MouseInput mi)
		{
			// Exclusively uses left and right mouse buttons, but nothing else
			if (mi.Button != MouseButton.Left && mi.Button != MouseButton.Right)
				return false;

			if (mi.Button == MouseButton.Right)
			{
				if (mi.Event == MouseInputEvent.Up)
				{
					editorWidget.ClearBrush();
					return true;
				}

				return false;
			}

			if (mi.Button == MouseButton.Left)
			{
				if (mi.Event == MouseInputEvent.Down)
					painting = true;
				else if (mi.Event == MouseInputEvent.Up)
					painting = false;
			}

			if (!painting)
				return true;

			if (mi.Event != MouseInputEvent.Down && mi.Event != MouseInputEvent.Move)
				return true;

			if (editorCursor.CurrentToken != cursorToken)
				return false;

			var cell = worldRenderer.Viewport.ViewToWorld(mi.Location);
			var pos = worldRenderer.Viewport.ViewToWorldPos(mi.Location, cell);

			var isMoving = mi.Event == MouseInputEvent.Move;

			if (mi.Modifiers.HasModifier(Modifiers.Shift))
			{
				PaintCell(world.Map.CenterOfCell(cell), isMoving, mi.Modifiers.HasModifier(Modifiers.Ctrl));
			}
			else
				PaintCell(pos, isMoving, mi.Modifiers.HasModifier(Modifiers.Ctrl));

			return true;
		}

		void PaintCell(WPos pos, bool isMoving, bool erase)
		{
			if (lastPaintPos == pos && !isMoving)
				return;
			lastPaintPos = pos;
			editorActionManager.Add(new PaintMaskEditorAction(brush, world.Map, pos, getActiveLayers(), erase));
		}

		public void Tick() { }

		public void Dispose()
		{
			editorCursor.Clear(cursorToken);
		}
	}

	class PaintMaskEditorAction : IEditorAction
	{
		public string Text { get; }

		readonly MaskBrush brush;
		readonly Map map;
		readonly WPos pos;
		readonly bool erase;
		readonly int[] activeLayers;

		public PaintMaskEditorAction(MaskBrush brush, Map map, WPos pos, int[] activeLayers, bool erase)
		{
			this.brush = brush;
			this.map = map;
			this.pos = pos;
			this.activeLayers = activeLayers;
			this.erase = erase;
			Text = $"Paint with {brush.Name} at " + pos;
		}

		public void Execute()
		{
			Do();
		}

		public void Do()
		{
			foreach (var layer in activeLayers)
			{
				TerrainRenderBlock.PaintAt(map, brush, pos, brush.DefaultSize, layer, erase ? -255 : 255);
			}
		}

		public void Undo()
		{
			// TerrainRenderBlock.PaintAt(map, brush, pos, brush.DefaultSize, 0, 255);
		}
	}

}
