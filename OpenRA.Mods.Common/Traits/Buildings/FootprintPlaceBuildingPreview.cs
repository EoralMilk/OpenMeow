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

using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Mods.Common.Orders;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Creates a building placement preview showing only the building footprint.")]
	public class FootprintPlaceBuildingPreviewInfo : TraitInfo<FootprintPlaceBuildingPreview>, IPlaceBuildingPreviewGeneratorInfo
	{
		[Desc("Specifically specify the sequence it uses.")]
		public readonly string ValidPlaceSequence = null;

		[Desc("Specifically specify the sequence it uses.")]
		public readonly string InvalidPlaceSequence = null;

		[PaletteReference]
		[Desc("Palette to use for rendering the placement sprite.")]
		public readonly string Palette = TileSet.TerrainPaletteInternalName;

		[Desc("Custom opacity to apply to the placement sprite.")]
		public readonly float FootprintAlpha = 1f;

		[Desc("Custom opacity to apply to the line-build placement sprite.")]
		public readonly float LineBuildFootprintAlpha = 1f;

		[Desc("Render terrain geometry when place building.")]
		public readonly bool RenderTerrainGeometry = true;

		protected virtual IPlaceBuildingPreview CreatePreview(WorldRenderer wr, ActorInfo ai, TypeDictionary init)
		{
			return new FootprintPlaceBuildingPreviewPreview(wr, ai, this);
		}

		IPlaceBuildingPreview IPlaceBuildingPreviewGeneratorInfo.CreatePreview(WorldRenderer wr, ActorInfo ai, TypeDictionary init)
		{
			return CreatePreview(wr, ai, init);
		}
	}

	public class FootprintPlaceBuildingPreview { }

	public class FootprintPlaceBuildingPreviewPreview : IPlaceBuildingPreview
	{
		protected readonly ActorInfo ActorInfo;
		protected readonly WVec CenterOffset;
		readonly FootprintPlaceBuildingPreviewInfo info;
		readonly IPlaceBuildingDecorationInfo[] decorations;
		readonly int2 topLeftScreenOffset;
		readonly int validZOffset, blockedZOffset;
		readonly Sprite validTile, blockedTile;
		readonly float validAlpha, blockedAlpha;

		public FootprintPlaceBuildingPreviewPreview(WorldRenderer wr, ActorInfo ai, FootprintPlaceBuildingPreviewInfo info)
		{
			ActorInfo = ai;
			this.info = info;
			decorations = ActorInfo.TraitInfos<IPlaceBuildingDecorationInfo>().ToArray();

			var world = wr.World;
			CenterOffset = ActorInfo.TraitInfo<BuildingInfo>().CenterOffset(world);
			topLeftScreenOffset = -wr.ScreenPxOffset(CenterOffset);
			var tileset = world.Map.Tileset.ToLowerInvariant();

			if (info.ValidPlaceSequence != null)
			{
				var validSequence = world.Map.Rules.Sequences.GetSequence("overlay", info.ValidPlaceSequence);
				validTile = validSequence.GetSprite(0);
				validAlpha = validSequence.GetAlpha(0);
				validZOffset = validSequence.ZOffset;
			}
			else if (world.Map.Rules.Sequences.HasSequence("overlay", $"build-valid-{tileset}"))
			{
				var validSequence = world.Map.Rules.Sequences.GetSequence("overlay", $"build-valid-{tileset}");
				validTile = validSequence.GetSprite(0);
				validAlpha = validSequence.GetAlpha(0);
				validZOffset = validSequence.ZOffset;
			}
			else
			{
				var validSequence = world.Map.Rules.Sequences.GetSequence("overlay", "build-valid");
				validTile = validSequence.GetSprite(0);
				validAlpha = validSequence.GetAlpha(0);
				validZOffset = validSequence.ZOffset;
			}

			if (info.InvalidPlaceSequence != null)
			{
				var blockedSequence = world.Map.Rules.Sequences.GetSequence("overlay", info.InvalidPlaceSequence);
				blockedTile = blockedSequence.GetSprite(0);
				blockedAlpha = blockedSequence.GetAlpha(0);
				blockedZOffset = blockedSequence.ZOffset;
			}
			else
			{
				var blockedSequence = world.Map.Rules.Sequences.GetSequence("overlay", "build-invalid");
				blockedTile = blockedSequence.GetSprite(0);
				blockedAlpha = blockedSequence.GetAlpha(0);
				blockedZOffset = blockedSequence.ZOffset;
			}
		}

		protected virtual void TickInner() { }

		protected virtual IEnumerable<IRenderable> RenderFootprint(WorldRenderer wr, CPos topLeft, Dictionary<CPos, PlaceBuildingCellType> footprint,
			PlaceBuildingCellType filter = PlaceBuildingCellType.Invalid | PlaceBuildingCellType.Valid | PlaceBuildingCellType.LineBuild)
		{
			var palette = wr.Palette(info.Palette);
			var topLeftPos = wr.World.Map.CenterOfCell(topLeft);
			foreach (var c in footprint)
			{
				if ((c.Value & filter) == 0)
					continue;

				var tile = (c.Value & PlaceBuildingCellType.Invalid) != 0 ? blockedTile : validTile;
				var sequenceAlpha = (c.Value & PlaceBuildingCellType.Invalid) != 0 ? blockedAlpha : validAlpha;
				var pos = wr.World.Map.CenterOfCell(c.Key);
				var offset = new WVec(0, 0, topLeftPos.Z - pos.Z);
				var zoffset = (c.Value & PlaceBuildingCellType.Invalid) != 0 ? blockedZOffset : validZOffset;
				var traitAlpha = (c.Value & PlaceBuildingCellType.LineBuild) != 0 ? info.LineBuildFootprintAlpha : info.FootprintAlpha;
				yield return new SpriteRenderable(tile, pos, offset, zoffset, palette, 1f, sequenceAlpha * traitAlpha, float3.Ones, TintModifiers.IgnoreWorldTint, true);
			}
		}

		protected virtual IEnumerable<IRenderable> RenderAnnotations(WorldRenderer wr, CPos topLeft)
		{
			var centerPosition = wr.World.Map.CenterOfCell(topLeft) + CenterOffset;
			foreach (var d in decorations)
				foreach (var r in d.RenderAnnotations(wr, wr.World, ActorInfo, centerPosition))
					yield return r;

			if (!info.RenderTerrainGeometry)
				yield break;

			var map = wr.World.Map;
			var mapMaxHeight = map.Grid.MaximumTerrainHeight * MapGrid.MapHeightStep;
			var mouseCell = wr.Viewport.ViewToWorld(Viewport.LastMousePos).ToMPos(wr.World.Map);

			foreach (var uv in wr.Viewport.AllVisibleCells.CandidateMapCoords)
			{
				if (!map.CellInfos.Contains(uv) || wr.World.ShroudObscures(uv))
					continue;

				var cellinfo = map.CellInfos[uv];
				var cellCorner = new WPos[5] {
					map.TerrainVertices[cellinfo.T].LogicPos,
					map.TerrainVertices[cellinfo.R].LogicPos,
					map.TerrainVertices[cellinfo.B].LogicPos,
					map.TerrainVertices[cellinfo.L].LogicPos,
					map.TerrainVertices[cellinfo.T].LogicPos};
				var width = uv == mouseCell ? 3 : 1;

				// Colors change between points, so render separately
				for (var i = 0; i < cellCorner.Length - 1; i++)
				{
					var startColor = Color.FromAhsv(128, (float)cellCorner[i].Z / mapMaxHeight, 1, 1);
					var endColor = Color.FromAhsv(128, (float)cellCorner[i + 1].Z / mapMaxHeight, 1, 1);

					yield return new LineAnnotationRenderable(cellCorner[i], cellCorner[i + 1], width, startColor, endColor);
				}
			}
		}

		protected virtual IEnumerable<IRenderable> RenderInner(WorldRenderer wr, CPos topLeft, Dictionary<CPos, PlaceBuildingCellType> footprint)
		{
			return RenderFootprint(wr, topLeft, footprint);
		}

		IEnumerable<IRenderable> IPlaceBuildingPreview.Render(WorldRenderer wr, CPos topLeft, Dictionary<CPos, PlaceBuildingCellType> footprint)
		{
			return RenderInner(wr, topLeft, footprint);
		}

		IEnumerable<IRenderable> IPlaceBuildingPreview.RenderAnnotations(WorldRenderer wr, CPos topLeft)
		{
			return RenderAnnotations(wr, topLeft);
		}

		void IPlaceBuildingPreview.Tick() { TickInner(); }

		int2 IPlaceBuildingPreview.TopLeftScreenOffset => topLeftScreenOffset;
	}
}
