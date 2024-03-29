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
		readonly Func<int> getAlpha;
		readonly Func<float> getSize;

		readonly int cursorToken;

		bool painting;
		WPos lastPaintPos;
		PaintMaskEditorAction action;
		public EditorTerrainMaskBrush(Func<int[]> getActiveLayers, Func<int> getAlpha, Func<float> getSize, EditorViewportControllerWidget editorWidget, MaskBrush brush, WorldRenderer wr)
		{
			this.getActiveLayers = getActiveLayers;
			this.getAlpha = getAlpha;
			this.getSize = getSize;
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
			if (mi.Modifiers.HasModifier(Modifiers.Shift))
				editorCursor.DrawBrushCell = true;
			else
				editorCursor.DrawBrushCell = false;

			// Exclusively uses left and right mouse buttons, but nothing else
			if (mi.Button != MouseButton.Left && mi.Button != MouseButton.Right)
				return false;

			if (mi.Button == MouseButton.Right)
			{
				if (mi.Event == MouseInputEvent.Up)
				{
					if (painting && action != null)
					{
						action.Undo();
						action = null;
						painting = false;
					}

					editorCursor.DrawBrushCell = false;
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
			{
				action = null;
				return true;
			}

			if (mi.Event != MouseInputEvent.Down && mi.Event != MouseInputEvent.Move)
				return true;

			if (editorCursor.CurrentToken != cursorToken)
				return false;

			var cell = worldRenderer.Viewport.ViewToWorld(mi.Location);
			var pos = worldRenderer.Viewport.ViewToWorldPos(mi.Location, cell);

			var isMoving = mi.Event == MouseInputEvent.Move;

			if (mi.Modifiers.HasModifier(Modifiers.Shift))
			{
				PaintCell(cell, world.Map.CenterOfCell(cell), isMoving, mi.Modifiers.HasModifier(Modifiers.Ctrl), mi.Modifiers.HasModifier(Modifiers.Alt));
			}
			else
				PaintCell(cell, pos, isMoving, mi.Modifiers.HasModifier(Modifiers.Ctrl), mi.Modifiers.HasModifier(Modifiers.Alt));

			return true;
		}

		void PaintCell(in CPos cell, in WPos pos, bool isMoving, bool erase, bool eraseUpper)
		{
			if (action == null)
			{
				action = new PaintMaskEditorAction(editorCursor, brush, world.Map,
					pos,
					getActiveLayers(),
					(int)(getSize() * brush.DefaultSize),
					getAlpha(),
					erase, eraseUpper);
				editorActionManager.Add(action);
			}

			if (lastPaintPos == pos && !isMoving)
				return;
			lastPaintPos = pos;
			action.UpdatePos(cell, pos);
		}

		public void Tick() { }

		public void Dispose()
		{
			editorCursor.DrawBrushCell = false;
			editorCursor.Clear(cursorToken);
		}
	}

	class PaintMaskEditorAction : IEditorAction
	{
		public static int EditorDrawOrderKey = 0;
		public string Text { get; }

		readonly EditorCursorLayer editorCursor;
		readonly MaskBrush brush;
		readonly Map map;
		readonly WPos pos;
		readonly bool eraseUpper;
		readonly int size;
		readonly int intensity;
		readonly bool erase;
		readonly int[] activeLayers;
		readonly int drawKey;

		/// <summary>
		/// before type, after type
		/// </summary>
		readonly Dictionary<CPos, (int, int)> typeDraws = new Dictionary<CPos, (int, int)>();
		readonly List<(WPos, int, int, int)> draws = new List<(WPos, int, int, int)>();
		public PaintMaskEditorAction(EditorCursorLayer editorCursor, MaskBrush brush, Map map, WPos pos, int[] activeLayers, int size, int intensity, bool erase, bool eraseUpper)
		{
			this.editorCursor = editorCursor;
			this.brush = brush;
			this.map = map;
			this.pos = pos;
			this.activeLayers = activeLayers;
			this.eraseUpper = eraseUpper;
			this.size = size;
			this.erase = erase;
			this.intensity = erase ? -intensity : intensity;
			Text = $"Paint with {brush.Name} at " + pos;
			drawKey = EditorDrawOrderKey++;
		}

		public void UpdatePos(CPos cell, WPos pos)
		{
			if (editorCursor.CellType >= 0)
			{
				foreach (var c in map.FindTilesInCircle(cell, (size - 200) / 2048, true))
				{
					if (map.CellInfos.Contains(c) && !typeDraws.ContainsKey(c))
					{
						typeDraws.Add(c, (map.CellInfos[c].TerrainType, editorCursor.CellType));
						map.CellInfos[c].TerrainType = (byte)editorCursor.CellType;
					}
				}
			}

			if (intensity == 0)
				return;

			int top = 9;
			foreach (var layer in activeLayers)
			{
				if (layer < top)
					top = layer;
				draws.Add((pos, size, layer, intensity));
				TerrainRenderBlock.PaintAt(map, brush, pos, size, layer, intensity, drawKey);
			}

			if (eraseUpper)
			{
				for (int i = top - 1; i >= 0; i--)
				{
					draws.Add((pos, size, i,  -intensity));
					TerrainRenderBlock.PaintAt(map, brush, pos, size, i, -intensity, drawKey);
				}
			}
		}

		public void Execute()
		{
			//Do();
		}

		public void Do()
		{
			foreach (var draw in typeDraws)
			{
				if (map.CellInfos.Contains(draw.Key))
					map.CellInfos[draw.Key].TerrainType = (byte)draw.Value.Item2;
			}

			if (intensity == 0)
				return;

			foreach (var draw in draws)
			{
				TerrainRenderBlock.PaintAt(map, brush, draw.Item1, draw.Item2, draw.Item3, draw.Item4, drawKey);
			}
		}

		public void Undo()
		{
			foreach (var draw in typeDraws)
			{
				if (map.CellInfos.Contains(draw.Key))
					map.CellInfos[draw.Key].TerrainType = (byte)draw.Value.Item1;
			}

			if (intensity == 0)
				return;

			// TerrainRenderBlock.PaintAt(map, brush, pos, brush.DefaultSize, 0, 255);
			foreach (var block in map.TerrainBlocks)
			{
				block.UndoEditorDraw(drawKey);
			}
		}
	}

}
