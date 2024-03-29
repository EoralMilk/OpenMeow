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
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Mods.Common.Terrain;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	public enum EditorCursorType { None, Actor, TerrainTemplate, Resource, Brush }

	[TraitLocation(SystemActors.EditorWorld)]
	[Desc("Required for the map editor to work. Attach this to the world actor.")]
	public class EditorCursorLayerInfo : TraitInfo, Requires<EditorActorLayerInfo>, Requires<ITiledTerrainRendererInfo>
	{
		public readonly WAngle PreviewFacing = new WAngle(384);

		public override object Create(ActorInitializer init) { return new EditorCursorLayer(init.Self, this); }
	}

	public class EditorCursorLayer : IWorldLoaded, ITickRender, IRenderAboveShroud, IRenderAnnotations
	{
		readonly EditorCursorLayerInfo info;
		readonly EditorActorLayer editorLayer;
		readonly ITiledTerrainRenderer terrainRenderer;
		readonly World world;
		IResourceRenderer[] resourceRenderers;

		public int CurrentToken { get; private set; }
		public EditorCursorType Type { get; private set; }
		public EditorActorPreview Actor { get; private set; }
		CPos actorLocation;
		SubCell actorSubCell;
		WVec actorCenterOffset;
		bool actorSharesCell;

		public MaskBrush Brush { get; private set; }
		public int CellType;
		public Func<float> GetBrushSize;
		public Func<int> GetBrushAlpha;
		public bool DrawBrushCell = false;

		IRenderable[] brushRenderables;
		WPos brushPos;

		public TerrainTemplateInfo TerrainTemplate { get; private set; }
		public string ResourceType { get; private set; }
		CPos terrainOrResourceCell;
		bool terrainOrResourceDirty;
		readonly List<IRenderable> terrainOrResourcePreview = new List<IRenderable>();

		public EditorCursorLayer(Actor self, EditorCursorLayerInfo info)
		{
			this.info = info;
			world = self.World;
			editorLayer = self.Trait<EditorActorLayer>();
			terrainRenderer = self.Trait<ITiledTerrainRenderer>();

			Type = EditorCursorType.None;
		}

		void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr)
		{
			resourceRenderers = w.WorldActor.TraitsImplementing<IResourceRenderer>().ToArray();
		}

		void ITickRender.TickRender(WorldRenderer wr, Actor self)
		{
			if (wr.World.Type != WorldType.Editor)
				return;

			if (Type == EditorCursorType.Brush)
			{
				var cell = wr.Viewport.ViewToWorld(Viewport.LastMousePos);
				var pos = wr.Viewport.ViewToWorldPos(Viewport.LastMousePos, cell);

				if (terrainOrResourceCell != cell || brushPos != pos)
				{
					terrainOrResourceCell = cell;

					if (DrawBrushCell)
						brushPos = wr.World.Map.CenterOfCell(cell);
					else
						brushPos = pos;

					var normal = World3DCoordinate.TSVec3ToWVec(wr.World.Map.NormalOfTerrain(pos));

					brushRenderables = new IRenderable[2];
					brushRenderables[0] = new DebugLineRenderable(brushPos,
						15,
						World3DCoordinate.WPosToFloat3(brushPos),
						World3DCoordinate.WPosToFloat3(brushPos + normal * 4),
						new WDist(64), Color.OrangeRed, BlendMode.Alpha
						);

					brushRenderables[1] = new DebugCircleRenderable(brushPos,
						10, GetBrushSize == null ? Brush.DefaultSize / 2 : (int)(GetBrushSize() * Brush.DefaultSize / 2),
						new WDist(64), Color.OrangeRed, Color.Aqua, BlendMode.Alpha, GetBrushAlpha == null ? 255 : GetBrushAlpha());
				}
			}
			else if (Type == EditorCursorType.TerrainTemplate || Type == EditorCursorType.Resource)
			{
				var cell = wr.Viewport.ViewToWorld(Viewport.LastMousePos);
				if (terrainOrResourceCell != cell || terrainOrResourceDirty)
				{
					terrainOrResourceCell = cell;
					terrainOrResourceDirty = false;
					terrainOrResourcePreview.Clear();

					var pos = world.Map.CenterOfCell(cell);
					if (Type == EditorCursorType.TerrainTemplate)
						terrainOrResourcePreview.AddRange(terrainRenderer.RenderPreview(wr, TerrainTemplate, pos));
					else
						terrainOrResourcePreview.AddRange(resourceRenderers.SelectMany(r => r.RenderPreview(wr, ResourceType, pos)));
				}
			}
			else if (Type == EditorCursorType.Actor)
			{
				// Offset mouse position by the center offset (in world pixels)
				var worldPx = wr.Viewport.ViewToWorldPx(Viewport.LastMousePos) - wr.ScreenPxOffset(actorCenterOffset);
				var cell = wr.Viewport.ViewToWorld(wr.Viewport.WorldToViewPx(worldPx));
				var subCell = actorSharesCell ? editorLayer.FreeSubCellAt(cell) : SubCell.Invalid;
				var updated = false;
				if (actorLocation != cell)
				{
					actorLocation = cell;
					Actor.ReplaceInit(new LocationInit(cell));
					updated = true;
				}

				if (actorSubCell != subCell)
				{
					actorSubCell = subCell;

					if (Actor.RemoveInits<SubCellInit>() > 0)
						updated = true;

					var subcell = world.Map.Tiles.Contains(cell) ? editorLayer.FreeSubCellAt(cell) : SubCell.Invalid;
					if (subcell != SubCell.Invalid)
					{
						Actor.AddInit(new SubCellInit(subcell));
						updated = true;
					}
				}

				if (updated)
					Actor = new EditorActorPreview(wr, null, Actor.Export(), Actor.Owner);
			}
		}

		static readonly IEnumerable<IRenderable> NoRenderables = Enumerable.Empty<IRenderable>();
		IEnumerable<IRenderable> IRenderAboveShroud.RenderAboveShroud(Actor self, WorldRenderer wr)
		{
			if (wr.World.Type != WorldType.Editor)
				return NoRenderables;

			if (Type == EditorCursorType.Brush && brushRenderables != null)
			{
				return brushRenderables;
			}

			if (Type == EditorCursorType.TerrainTemplate || Type == EditorCursorType.Resource)
				return terrainOrResourcePreview;

			if (Type == EditorCursorType.Actor)
				return Actor.Render().OrderBy(WorldRenderer.RenderableZPositionComparisonKey);

			return NoRenderables;
		}

		bool IRenderAboveShroud.SpatiallyPartitionable => false;

		public IEnumerable<IRenderable> RenderAnnotations(Actor self, WorldRenderer wr)
		{
			if (wr.World.Type != WorldType.Editor)
				return NoRenderables;

			if (Type == EditorCursorType.Brush && DrawBrushCell)
			{
				var map = wr.World.Map;
				if (!map.CellInfos.Contains(terrainOrResourceCell))
					return NoRenderables;

				var cells = wr.World.Map.FindTilesInCircle(terrainOrResourceCell, (int)(GetBrushSize() * Brush.DefaultSize - 200) / 2048, true);

				IRenderable[] lines = new IRenderable[4 * cells.Count()];
				int idx = 0;
				foreach (var cc in cells)
				{
					var mapMaxHeight = map.Grid.MaximumTerrainHeight * MapGrid.MapHeightStep;
					var cellinfo = map.CellInfos[cc];
					var cellCorner = new WPos[5] {
					map.TerrainVertices[cellinfo.T].LogicPos,
					map.TerrainVertices[cellinfo.R].LogicPos,
					map.TerrainVertices[cellinfo.B].LogicPos,
					map.TerrainVertices[cellinfo.L].LogicPos,
					map.TerrainVertices[cellinfo.T].LogicPos};

					// Colors change between points, so render separately
					for (var i = 0; i < cellCorner.Length - 1; i++)
					{
						var startColor = Color.FromAhsv((float)cellCorner[i].Z / mapMaxHeight, 1, 1);
						var endColor = Color.FromAhsv((float)cellCorner[i + 1].Z / mapMaxHeight, 1, 1);

						lines[idx * 4 + i] = new LineAnnotationRenderable(cellCorner[i], cellCorner[i + 1], 3, startColor, endColor);
					}

					idx++;
				}

				return lines;
			}

			return Type == EditorCursorType.Actor ? Actor.RenderAnnotations() : NoRenderables;
		}

		bool IRenderAnnotations.SpatiallyPartitionable => false;

		public int SetActor(WorldRenderer wr, ActorInfo actor, PlayerReference owner)
		{
			var ios = actor.TraitInfoOrDefault<IOccupySpaceInfo>();
			var buildingInfo = ios as BuildingInfo;
			actorCenterOffset = buildingInfo?.CenterOffset(world) ?? WVec.Zero;
			actorSharesCell = ios != null && ios.SharesCell;
			actorSubCell = SubCell.Invalid;

			// Enforce first entry of ValidOwnerNames as owner if the actor has RequiresSpecificOwners
			var ownerName = owner.Name;
			var specificOwnerInfo = actor.TraitInfoOrDefault<RequiresSpecificOwnersInfo>();
			if (specificOwnerInfo != null && !specificOwnerInfo.ValidOwnerNames.Contains(ownerName))
				ownerName = specificOwnerInfo.ValidOwnerNames.First();

			var reference = new ActorReference(actor.Name)
			{
				new OwnerInit(ownerName),
				new FactionInit(owner.Faction)
			};

			var worldPx = wr.Viewport.ViewToWorldPx(Viewport.LastMousePos) - wr.ScreenPxOffset(actorCenterOffset);
			var cell = wr.Viewport.ViewToWorld(wr.Viewport.WorldToViewPx(worldPx));

			reference.Add(new LocationInit(cell));
			if (ios != null && ios.SharesCell)
			{
				actorSubCell = editorLayer.FreeSubCellAt(cell);
				if (actorSubCell != SubCell.Invalid)
					reference.Add(new SubCellInit(actorSubCell));
			}

			if (actor.HasTraitInfo<IFacingInfo>())
				reference.Add(new FacingInit(info.PreviewFacing));

			Type = EditorCursorType.Actor;
			Actor = new EditorActorPreview(wr, null, reference, owner);
			TerrainTemplate = null;
			ResourceType = null;

			return ++CurrentToken;
		}

		public int SetTerrainTemplate(WorldRenderer wr, TerrainTemplateInfo template)
		{
			terrainOrResourceCell = wr.Viewport.ViewToWorld(wr.Viewport.WorldToViewPx(Viewport.LastMousePos));

			Type = EditorCursorType.TerrainTemplate;
			TerrainTemplate = template;
			Actor = null;
			ResourceType = null;
			terrainOrResourceDirty = true;

			return ++CurrentToken;
		}

		public int SetResource(WorldRenderer wr, string resourceType)
		{
			terrainOrResourceCell = wr.Viewport.ViewToWorld(wr.Viewport.WorldToViewPx(Viewport.LastMousePos));

			Type = EditorCursorType.Resource;
			ResourceType = resourceType;
			Actor = null;
			TerrainTemplate = null;
			terrainOrResourceDirty = true;

			return ++CurrentToken;
		}

		public int SetTerrainBrush(WorldRenderer wr, MaskBrush brush)
		{
			var screenPos = wr.Viewport.WorldToViewPx(Viewport.LastMousePos);
			terrainOrResourceCell = wr.Viewport.ViewToWorld(screenPos);

			Type = EditorCursorType.Brush;
			brushPos = wr.Viewport.ViewToWorldPos(screenPos, terrainOrResourceCell);
			Brush = brush;
			Actor = null;
			ResourceType = null;
			terrainOrResourceDirty = true;

			return ++CurrentToken;
		}

		public void Clear(int token)
		{
			if (token != CurrentToken)
				return;

			Type = EditorCursorType.None;
			Actor = null;
			TerrainTemplate = null;
			ResourceType = null;
		}
	}
}
