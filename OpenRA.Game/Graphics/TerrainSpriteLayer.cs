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
using System.Security.Cryptography;
using OpenRA.Primitives;
using System.Xml.Linq;
using OpenRA.Traits;
using StbImageSharp;

namespace OpenRA.Graphics
{
	public sealed class TerrainSpriteLayer : IDisposable
	{
		static readonly int[] CornerVertexMap6 = { 0, 1, 2, 2, 3, 0 };
		static readonly int[] CornerVertexMap = { 1, 0, 4, 0, 2, 4, 1, 3, 0, 0, 3, 2 };

		public readonly BlendMode BlendMode;

		readonly Sheet[] sheets;
		readonly Sprite emptySprite;

		readonly IVertexBuffer<MapVertex> vertexBuffer;
		readonly MapVertex[] vertices;
		readonly float3[] verticesColor;

		readonly bool[] ignoreTint;
		readonly HashSet<int> dirtyRows = new HashSet<int>();
		readonly int rowStride;
		readonly bool restrictToBounds;

		readonly WorldRenderer worldRenderer;
		readonly Map map;

		readonly PaletteReference[] palettes;
		readonly MapVertex noVertex;

		public TerrainSpriteLayer(World world, WorldRenderer wr, Sprite emptySprite, BlendMode blendMode, bool restrictToBounds)
		{
			worldRenderer = wr;
			this.restrictToBounds = restrictToBounds;
			this.emptySprite = emptySprite;
			sheets = new Sheet[MapRenderer.SheetCount];
			BlendMode = blendMode;

			map = world.Map;

			rowStride = Game.Renderer.MaxVerticesPerMesh * map.MapSize.X;
			noVertex = new MapVertex(0);

			vertices = new MapVertex[rowStride * map.MapSize.Y];

			verticesColor = new float3[rowStride * map.MapSize.Y];

			for (int i = 0; i < verticesColor.Length; i++)
			{
				verticesColor[i] = float3.Ones;
			}

			palettes = new PaletteReference[map.MapSize.X * map.MapSize.Y];
			vertexBuffer = Game.Renderer.Context.CreateVertexBuffer<MapVertex>(vertices.Length);

			wr.PaletteInvalidated += UpdatePaletteIndices;

			if (wr.TerrainLighting != null)
			{
				ignoreTint = new bool[rowStride * map.MapSize.Y];
				wr.TerrainLighting.CellChanged += UpdateTint;
			}
		}

		void UpdatePaletteIndices()
		{
			for (var i = 0; i < vertices.Length; i++)
			{
				var v = vertices[i];
				var p = palettes[i / Game.Renderer.MaxVerticesPerMesh]?.TextureIndex ?? 0;
				//vertices[i] = new MapVertex(v.X, v.Y, v.Z, v.S, v.T, v.U, v.V, p, v.C, v.R, v.G, v.B, v.A, v.NX, v.NY, v.NZ, v.FNX, v.FNY, v.FNZ, v.TU, v.TV, v.DrawType);
				vertices[i] = vertices[i].ChangePal(p);
			}

			for (var row = 0; row < map.MapSize.Y; row++)
				dirtyRows.Add(row);
		}

		public void Clear(CPos cell)
		{
			Update(cell, null, null, 1f, 1f, true);
		}

		public void Update(CPos cell, ISpriteSequence sequence, PaletteReference palette, int frame, bool additional = false)
		{
			Update(cell, sequence.GetSprite(frame), palette, sequence.Scale, sequence.GetAlpha(frame), sequence.IgnoreWorldTint, sequence.ZOffset, additional: additional);
		}

		public void Update(CPos cell, Sprite sprite, PaletteReference palette, float scale = 1f, float alpha = 1f, bool ignoreTint = false, int zOffset = -1, bool additional = false)
		{
			WPos wPos = WPos.Zero;
			if (sprite != null)
			{
				wPos = map.CenterOfCell(cell) - new WVec(0, 0, map.Grid.Ramps[map.Ramp[cell]].CenterHeightOffset);
			}

			Update(cell.ToMPos(map.Grid.Type), sprite, palette, wPos, scale, alpha, ignoreTint, zOffset, additional, false);
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
			var offset = rowStride * uv.V + Game.Renderer.MaxVerticesPerMesh * uv.U;
			for (var i = 0; i < Game.Renderer.MaxVerticesPerMesh; i++)
			{
				var v = vertices[offset + i];
				var color = verticesColor[offset + i];
				vertices[offset + i] = new MapVertex(v.X, v.Y, v.Z, v.S, v.T, v.U, v.V, v.P, v.C, (weights[CornerVertexMap[i % 12]] * colorOffset), v.A, v.TX, v.TY, v.TZ, v.BX, v.BY, v.BZ, v.NX, v.NY, v.NZ, v.TU, v.TV, v.DrawType);
			}

			dirtyRows.Add(uv.V);
		}

		void UpdateTint(MPos uv)
		{
			return;
			//var offset = rowStride * uv.V + Game.Renderer.MaxVerticesPerMesh * uv.U;
			//if (ignoreTint[offset])
			//{
			//	for (var i = 0; i < Game.Renderer.MaxVerticesPerMesh; i++)
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
			//for (var i = 0; i < Game.Renderer.MaxVerticesPerMesh; i++)
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
		public void Update(MPos uv, Sprite sprite, PaletteReference palette, in WPos pos, float scale, float alpha, bool ignoreTint, int zOffset = -1, bool additional = false, bool shroud = false)
		{
			if (alpha == 0)
			{
				var nv = rowStride * uv.V + Game.Renderer.MaxVerticesPerMesh * uv.U;
				vertices[nv] = noVertex;
				vertices[nv + 1] = noVertex;
				vertices[nv + 2] = noVertex;
				vertices[nv + 3] = noVertex;
				vertices[nv + 4] = noVertex;
				vertices[nv + 5] = noVertex;
				vertices[nv + 6] = noVertex;
				vertices[nv + 7] = noVertex;
				vertices[nv + 8] = noVertex;
				vertices[nv + 9] = noVertex;
				vertices[nv + 10] = noVertex;
				vertices[nv + 11] = noVertex;
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

			var offset = rowStride * uv.V + Game.Renderer.MaxVerticesPerMesh * uv.U;
			var cellinfo = map.CellInfos[uv];

			//switch (spriteMeshType)
			//{
			//	case SpriteMeshType.Plane:
			//		Util.FastCreatePlane(vertices, pos, viewOffset, sprite, samplers, palette?.TextureIndex ?? 0, scale, alpha * float3.Ones, alpha, offset);
			//		break;
			//	case SpriteMeshType.Card:
			//		Util.FastCreateCard(vertices, pos, viewOffset, sprite, samplers, palette?.TextureIndex ?? 0, scale, alpha * float3.Ones, alpha, offset);
			//		break;
			//	case SpriteMeshType.Board:
			//		Util.FastCreateBoard(vertices, pos, viewOffset, sprite, samplers, palette?.TextureIndex ?? 0, scale, alpha * float3.Ones, alpha, offset);
			//		break;
			//	default: throw new Exception("not valid SpriteMeshType for terrain");
			//}
			var viewOffset = Game.Renderer.World3DRenderer.InverseCameraFrontMeterPerWPos * (zOffset - 15);

			if (additional)
			{
				//var spriteMeshType = sprite.SpriteMeshType;

				Util.FastCreateTilePlane(vertices, map.VertexTBN[cellinfo.M], pos, viewOffset, sprite, samplers, palette?.TextureIndex ?? 0, scale, float3.Zero, ignoreTint ? -alpha : alpha, offset);
			}
			else
			{
				if (shroud)
				{
					Util.FastCreateTile(vertices, verticesColor,
												map, cellinfo,
												float3.Zero,
												float3.Zero,
												float3.Zero,
												float3.Zero,
												float3.Zero,
												cellinfo.Type,
												sprite, samplers, palette?.TextureIndex ?? 0, viewOffset, ignoreTint ? -alpha : alpha, offset, false);
				}
				else
				{
					Util.FastCreateTile(vertices, verticesColor,
												map, cellinfo,
												float3.Zero,
												float3.Zero,
												float3.Zero,
												float3.Zero,
												float3.Zero,
												cellinfo.Type,
												sprite, samplers, palette?.TextureIndex ?? 0, viewOffset, ignoreTint ? -alpha : alpha, offset, true);
				}
			}

			palettes[uv.V * map.MapSize.X + uv.U] = palette;

			if (worldRenderer.TerrainLighting != null)
			{
				this.ignoreTint[offset] = ignoreTint;
				UpdateTint(uv);
			}

			dirtyRows.Add(uv.V);
		}

		public void LightShader(Viewport viewport)
		{
			var cells = restrictToBounds ? viewport.VisibleCellsInsideBounds : viewport.AllVisibleCells;

			var tlcell = cells.CandidateMapCoords.TopLeft.ToCPos(map);
			var brcell = cells.CandidateMapCoords.BottomRight.ToCPos(map);
			worldRenderer.TerrainLighting.LightSourcesToMapShader(map.CenterOfCell(tlcell), map.CenterOfCell(brcell), Game.Renderer.MapRenderer.Shader);
		}

		int firstRow, lastRow;
		public void Draw(Viewport viewport, bool stop = true)
		{
			//if (stop)
			//	return;

			var cells = restrictToBounds ? viewport.VisibleCellsInsideBounds : viewport.AllVisibleCells;

			// Only draw the rows that are visible.
			firstRow = cells.CandidateMapCoords.TopLeft.V.Clamp(0, map.MapSize.Y);
			lastRow = (cells.CandidateMapCoords.BottomRight.V + 1).Clamp(firstRow, map.MapSize.Y);

			Game.Renderer.Flush();

			// Flush any visible changes to the GPU
			for (var row = firstRow; row <= lastRow; row++)
			{
				if (!dirtyRows.Remove(row))
					continue;

				var rowOffset = rowStride * row;
				vertexBuffer.SetData(vertices, rowOffset, rowOffset, rowStride);
			}

			//if (stop)
			//	return;
			Game.Renderer.MapRenderer.DrawVertexBuffer(
				vertexBuffer, rowStride * firstRow, rowStride * (lastRow - firstRow),
				PrimitiveType.TriangleList, sheets, BlendMode);

			Game.Renderer.Flush();
		}

		public void DrawAgain()
		{
			Game.Renderer.MapRenderer.DrawVertexBuffer(
				vertexBuffer, rowStride * firstRow, rowStride * (lastRow - firstRow),
				PrimitiveType.TriangleList, sheets, BlendMode);

			Game.Renderer.Flush();
		}

		public void Dispose()
		{
			worldRenderer.PaletteInvalidated -= UpdatePaletteIndices;
			if (worldRenderer.TerrainLighting != null)
				worldRenderer.TerrainLighting.CellChanged -= UpdateTint;

			vertexBuffer.Dispose();
		}
	}
}
