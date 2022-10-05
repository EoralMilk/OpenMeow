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
using System.Reflection;
using OpenRA.Traits;

namespace OpenRA.Graphics
{
	public sealed class TerrainSpriteLayer : IDisposable
	{
		public struct LayerCell
		{
			public MapVertex[] Vertices;
			public bool IgnoreTint;
			public bool Draw;

			public void Clear()
			{
				Vertices = null;

				Draw = false;
			}
		}

		static readonly int[] CornerVertexMap6 = { 0, 1, 2, 2, 3, 0 };
		static readonly int[] CornerVertexMap = { 1, 0, 4, 0, 2, 4, 1, 3, 0, 0, 3, 2 };

		public readonly BlendMode BlendMode;

		readonly Sheet[] sheets;
		readonly Sprite emptySprite;

		readonly LayerCell[] cells;

		readonly HashSet<int> dirtyRows = new HashSet<int>();
		readonly bool restrictToBounds;

		readonly WorldRenderer worldRenderer;
		readonly Map map;

		readonly PaletteReference[] palettes;
		static readonly MapVertex NoVertex = new MapVertex(0);

		public TerrainSpriteLayer(World world, WorldRenderer wr, Sprite emptySprite, BlendMode blendMode, bool restrictToBounds)
		{
			worldRenderer = wr;
			this.restrictToBounds = restrictToBounds;
			this.emptySprite = emptySprite;
			sheets = new Sheet[MapRenderer.SheetCount];
			BlendMode = blendMode;

			map = world.Map;

			cells = new LayerCell[map.MapSize.X * map.MapSize.Y];

			for (int i = 0; i < cells.Length; i++)
			{
				cells[i].Clear();
			}

			palettes = new PaletteReference[map.MapSize.X * map.MapSize.Y];

			wr.PaletteInvalidated += UpdatePaletteIndices;

			if (wr.TerrainLighting != null)
			{
				wr.TerrainLighting.CellChanged += UpdateTint;
			}
		}

		void UpdatePaletteIndices()
		{
			for (int i = 0; i < cells.Length; i++)
			{
				var p = palettes[i]?.TextureIndex ?? 0;
				for (int vi = 0; vi < cells[i].Vertices.Length; vi++)
				{
					var v = cells[i].Vertices[vi];
					cells[i].Vertices[vi] = cells[i].Vertices[vi].ChangePal(p);
				}
			}

			for (var row = 0; row < map.MapSize.Y; row++)
				dirtyRows.Add(row);
		}

		public void Clear(CPos cell)
		{
			Update(cell, null, null, 1f, 0f, true, -1, true);
		}

		public void Update(CPos cell, ISpriteSequence sequence, PaletteReference palette, int frame, bool additional = false, bool rotation = true)
		{
			Update(cell, sequence.GetSprite(frame), palette, sequence.Scale, sequence.GetAlpha(frame), sequence.IgnoreWorldTint, sequence.ZOffset, additional: additional, rotation: rotation);
		}

		public void Update(CPos cell, Sprite sprite, PaletteReference palette, float scale = 1f, float alpha = 1f, bool ignoreTint = false, int zOffset = -1, bool additional = false, bool rotation = true)
		{
			WPos wPos = WPos.Zero;
			if (sprite != null)
			{
				wPos = map.CenterOfCell(cell);
				wPos = new WPos(wPos.X, wPos.Y, map.MiniHeightOfCell(cell));
			}

			Update(cell.ToMPos(map.Grid.Type), sprite, palette, wPos, scale, alpha, ignoreTint, zOffset, additional, rotation: rotation);
		}

		readonly float[] weights = new float[]
		{
					1,
					0f,
					0f,
					0f,
					0f,
		};

		public void ModifyTint(MPos uv, float3 colorOffset)
		{
			var ci = uv.V * map.MapSize.X + uv.U;
			for (int vi = 0; vi < cells[ci].Vertices.Length; vi++)
			{
				var v = cells[ci].Vertices[vi];
				cells[ci].Vertices[vi] = new MapVertex(v.X, v.Y, v.Z, v.S, v.T, v.U, v.V, v.P, v.C, (weights[CornerVertexMap[vi % 12]] * colorOffset), v.A, v.TX, v.TY, v.TZ, v.BX, v.BY, v.BZ, v.NX, v.NY, v.NZ, v.TU, v.TV, v.DrawType);
			}

			dirtyRows.Add(uv.V);
		}

		void UpdateTint(MPos uv)
		{
			return;
			//var offset = rowStride * uv.V + MaxVerticesPerMesh * uv.U;
			//if (ignoreTint[offset])
			//{
			//	for (var i = 0; i < MaxVerticesPerMesh; i++)
			//	{
			//		var v = vertices[offset + i];
			//		vertices[offset + i] = new MapVertex(v.X, v.Y, v.Z, v.S, v.T, v.U, v.V, v.P, v.C, v.A * float3.Ones, v.A, v.NX, v.NY, v.NZ, v.FNX, v.FNY, v.FNZ, v.TU, v.TV, v.DrawType);
			//	}

			//	return;
			//}

			//// Allow the terrain tint to vary linearly across the cell to smooth out the staircase effect
			//// This is done by sampling the lighting the corners of the sprite, even though those pixels are
			//// transparent for isometric tiles
			//var tl = worldRenderer.TerrainLighting;
			//var pos = map.CenterOfCell(uv.ToCPos(map));
			//var step = map.Grid.Type == MapGridType.RectangularIsometric ? 724 : 512;
			//var weights6 = new[]
			//{
			//	tl.TintAt(pos + new WVec(-step, -step, 0)),
			//	tl.TintAt(pos + new WVec(step, -step, 0)),
			//	tl.TintAt(pos + new WVec(step, step, 0)),
			//	tl.TintAt(pos + new WVec(-step, step, 0))
			//};

			//var weights = new[]
			//{
			//	tl.TintAt(pos),
			//	tl.TintAt(pos + new WVec(0, -724, 0)),
			//	tl.TintAt(pos + new WVec(0, 724, 0)),
			//	tl.TintAt(pos + new WVec(-724, 0, 0)),
			//	tl.TintAt(pos + new WVec(724, 0, 0))
			//};

			//// Apply tint directly to the underlying vertices
			//// This saves us from having to re-query the sprite information, which has not changed
			//for (var i = 0; i < MaxVerticesPerMesh; i++)
			//{
			//	var v = vertices[offset + i];
			//	var color = verticesColor[offset + i];
			//	if (color == float3.Ones)
			//	{
			//		vertices[offset + i] = new MapVertex(v.X, v.Y, v.Z, v.S, v.T, v.U, v.V, v.P, v.C, v.A * weights6[CornerVertexMap6[i % 6]], v.A, v.NX, v.NY, v.NZ, v.FNX, v.FNY, v.FNZ, v.TU, v.TV, v.DrawType);
			//	}
			//	else
			//	{
			//		vertices[offset + i] = new MapVertex(v.X, v.Y, v.Z, v.S, v.T, v.U, v.V, v.P, v.C, v.A * weights[CornerVertexMap[i % 12]], v.A, v.NX, v.NY, v.NZ, v.FNX, v.FNY, v.FNZ, v.TU, v.TV, v.DrawType);
			//	}
			//}

			//dirtyRows.Add(uv.V);
		}

		int GetOrAddSheetIndex(Sheet sheet)
		{
			if (sheet == null)
				return 0;

			for (var i = 0; i < sheets.Length; i++)
			{
				if (sheets[i] == sheet)
					return i;

				if (sheets[i] == null)
				{
					sheets[i] = sheet;
					return i;
				}
			}

			throw new InvalidDataException("Sheet overflow");
		}

		readonly float3 shroudColor = float3.Zero;
		public void Update(MPos uv, Sprite sprite, PaletteReference palette, in WPos pos, float scale, float alpha, bool ignoreTint, int zOffset = -1, bool additional = false, bool rotation = true)
		{
			var cellIndex = uv.V * map.MapSize.X + uv.U;
			if (alpha == 0)
			{
				cells[cellIndex].Clear();

				dirtyRows.Add(uv.V);
				return;
			}

			int2 samplers;
			if (sprite != null)
			{
				if (sprite.BlendMode != BlendMode && sprite.BlendMode != BlendMode.None)
				{
					throw new InvalidDataException("Attempted to add sprite with a different blend mode");
				}

				samplers = new int2(GetOrAddSheetIndex(sprite.Sheet), GetOrAddSheetIndex((sprite as SpriteWithSecondaryData)?.SecondarySheet));

				// PERF: Remove useless palette assignments for RGBA sprites
				// HACK: This is working around the limitation that palettes are defined on traits rather than on sequences,
				// and can be removed once this has been fixed
				if (sprite.Channel == TextureChannel.RGBA && !(palette?.HasColorShift ?? false))
					palette = null;
			}
			else
			{
				sprite = emptySprite;
				samplers = int2.Zero;
			}

			// The vertex buffer does not have geometry for cells outside the map
			if (!map.Tiles.Contains(uv))
				return;

			var cellinfo = map.CellInfos[uv];

			// switch (spriteMeshType)
			// {
			// 	case SpriteMeshType.Plane:
			// 		Util.FastCreatePlane(vertices, pos, viewOffset, sprite, samplers, palette?.TextureIndex ?? 0, scale, alpha * float3.Ones, alpha, offset);
			// 		break;
			// 	case SpriteMeshType.Card:
			// 		Util.FastCreateCard(vertices, pos, viewOffset, sprite, samplers, palette?.TextureIndex ?? 0, scale, alpha * float3.Ones, alpha, offset);
			// 		break;
			// 	case SpriteMeshType.Board:
			// 		Util.FastCreateBoard(vertices, pos, viewOffset, sprite, samplers, palette?.TextureIndex ?? 0, scale, alpha * float3.Ones, alpha, offset);
			// 		break;
			// 	default: throw new Exception("not valid SpriteMeshType for terrain");
			// }
			var viewOffset = Game.Renderer.World3DRenderer.InverseCameraFrontMeterPerWDist * (zOffset - 15);

			if (additional)
			{
				// var spriteMeshType = sprite.SpriteMeshType;
				cells[cellIndex].Vertices = Util.FastCreateTilePlane(map.TerrainVertices[cellinfo.M].TBN, pos, viewOffset, sprite, samplers, palette?.TextureIndex ?? 0, scale, float3.Zero, ignoreTint ? -alpha : alpha);
				cells[cellIndex].Draw = true;
				cells[cellIndex].IgnoreTint = ignoreTint;

			}
			else
			{
				cells[cellIndex].Vertices = Util.FastCreateTile(
																map, cellinfo,
																float3.Zero,
																float3.Zero,
																float3.Zero,
																float3.Zero,
																float3.Zero,
																cellinfo.Type,
																sprite, samplers, palette?.TextureIndex ?? 0, viewOffset, ignoreTint ? -alpha : alpha, rotation);

				cells[cellIndex].Draw = true;
				cells[cellIndex].IgnoreTint = ignoreTint;
			}

			palettes[uv.V * map.MapSize.X + uv.U] = palette;

			if (worldRenderer.TerrainLighting != null)
			{
				UpdateTint(uv);
			}

			dirtyRows.Add(uv.V);
		}

		public void Draw(Viewport viewport, bool reverse = false)
		{
			var visiblecells = restrictToBounds ? viewport.VisibleCellsInsideBounds : viewport.AllVisibleCells;

			// Only draw the rows that are visible.
			int2 tp = new int2(Math.Clamp(visiblecells.CandidateMapCoords.TopLeft.U, 0, map.MapSize.X - 1), Math.Clamp(visiblecells.CandidateMapCoords.TopLeft.V, 0, map.MapSize.Y - 1));
			int2 br = new int2(Math.Clamp(visiblecells.CandidateMapCoords.BottomRight.U + 1, 0, map.MapSize.X - 1), Math.Clamp(visiblecells.CandidateMapCoords.BottomRight.V + 1, 0, map.MapSize.Y - 1));

			Game.Renderer.Flush();

			Game.Renderer.MapRenderer.SetSheets(sheets, BlendMode);

			if (reverse)
				for (int y = br.Y; y >= tp.Y; y--)
				{
					int cellStart = y * map.MapSize.X + tp.X;
					int cellEnd = y * map.MapSize.X + br.X;

					Game.Renderer.MapRenderer.DrawCells(cells, cellStart, cellEnd);
				}
			else
				for (int y = tp.Y; y <= br.Y; y++)
				{
					int cellStart = y * map.MapSize.X + tp.X;
					int cellEnd = y * map.MapSize.X + br.X;

					Game.Renderer.MapRenderer.DrawCells(cells, cellStart, cellEnd);
				}
		}

		public void Dispose()
		{
			worldRenderer.PaletteInvalidated -= UpdatePaletteIndices;
			if (worldRenderer.TerrainLighting != null)
				worldRenderer.TerrainLighting.CellChanged -= UpdateTint;
		}
	}
}
