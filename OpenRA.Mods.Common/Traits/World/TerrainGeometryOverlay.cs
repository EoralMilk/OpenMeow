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

using System.Collections;
using System.Collections.Generic;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Commands;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[TraitLocation(SystemActors.World | SystemActors.EditorWorld)]
	[Desc("Renders a debug overlay showing the terrain cells. Attach this to the world actor.")]
	public class TerrainGeometryOverlayInfo : TraitInfo<TerrainGeometryOverlay> { }

	public class TerrainGeometryOverlay : IRenderAnnotations, IWorldLoaded, IChatCommand
	{
		const string CommandName = "terrain-geometry";

		[TranslationReference]
		const string CommandDescription = "description-terrain-geometry-overlay";

		public bool Enabled;

		void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr)
		{
			var console = w.WorldActor.TraitOrDefault<ChatCommands>();
			var help = w.WorldActor.TraitOrDefault<HelpCommand>();

			if (console == null || help == null)
				return;

			console.RegisterCommand(CommandName, this);
			help.RegisterHelp(CommandName, CommandDescription);
		}

		void IChatCommand.InvokeCommand(string name, string arg)
		{
			if (name == CommandName)
				Enabled ^= true;
		}

		IEnumerable<IRenderable> IRenderAnnotations.RenderAnnotations(Actor self, WorldRenderer wr)
		{
			if (!Enabled)
				yield break;

			var map = wr.World.Map;
			var mapMaxHeight = map.Grid.MaximumTerrainHeight * MapGrid.MapHeightStep;
			var mouseCell = wr.Viewport.ViewToWorld(Viewport.LastMousePos).ToMPos(wr.World.Map);

			foreach (var uv in wr.Viewport.AllVisibleCells.CandidateMapCoords)
			{
				if (!map.CellInfos.Contains(uv) || self.World.ShroudObscures(uv))
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
					var startColor = Color.FromAhsv((float)cellCorner[i].Z / mapMaxHeight, 1, 1);
					var endColor = Color.FromAhsv((float)cellCorner[i + 1].Z / mapMaxHeight, 1, 1);

					yield return new LineAnnotationRenderable(cellCorner[i], cellCorner[i + 1], width, startColor, endColor);
				}
			}

			// Projected cell coordinates for the current cell
			var projectedCorners = map.Grid.Ramps[0].Corners;
			foreach (var puv in map.ProjectedCellsCovering(mouseCell))
			{
				var pos = map.CenterOfCell(((MPos)puv).ToCPos(map));
				for (var i = 0; i < 4; i++)
				{
					var j = (i + 1) % 4;
					var start = pos + projectedCorners[i] - new WVec(0, 0, pos.Z);
					var end = pos + projectedCorners[j] - new WVec(0, 0, pos.Z);
					yield return new LineAnnotationRenderable(start, end, 3, Color.Navy);
				}
			}
		}

		bool IRenderAnnotations.SpatiallyPartitionable => false;
	}
}
