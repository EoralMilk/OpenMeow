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
	[Desc("Renders a debug overlay showing the terrain cells type. Attach this to the world actor.")]
	public class CellTypeOverlayInfo : TraitInfo {

		[PaletteReference]
		[Desc("Palette to use for rendering the placement sprite.")]
		public readonly string Palette = TileSet.TerrainPaletteInternalName;

		[Desc("Custom opacity to apply to the placement sprite.")]
		public readonly float FootprintAlpha = 0.5f;

		[Desc("Sequence image where the selection overlay types are defined.")]
		public readonly string Image = "editor-overlay";

		[SequenceReference(nameof(Image))]
		[Desc("Sequence to use for the copy overlay.")]
		public readonly string Sequence = "copy";

		public override object Create(ActorInitializer init) { return new CellTypeOverlay(this); }
	}

	public class CellTypeOverlay : IRenderAboveShroud, IWorldLoaded
	{
		public bool Enabled;
		CellTypeOverlayInfo info;
		Map map;
		TerrainTypeInfo[] allTypes;
		Color[] typeColors;
		Sprite tileSprite;
		float tileAlpha;
		PaletteReference palette;

		public CellTypeOverlay(CellTypeOverlayInfo info)
		{
			this.info = info;
			Enabled = false;
		}

		void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr)
		{
			map = w.Map;
			var seq = map.Rules.Sequences.GetSequence(info.Image, info.Sequence);
			tileSprite = seq.GetSprite(0);
			tileAlpha = seq.GetAlpha(0);

			allTypes = map.Rules.TerrainInfo.TerrainTypes;
			typeColors = new Color[allTypes.Length];
			for (int i = 0; i < allTypes.Length; i++)
			{
				typeColors[i] = Color.FromAhsv((float)i / allTypes.Length, 1.0f, 1.0f);
			}

			palette = wr.Palette(info.Palette);
		}

		IEnumerable<IRenderable> IRenderAboveShroud.RenderAboveShroud(Actor self, WorldRenderer wr)
		{
			if (!Enabled)
				yield break;

			var map = wr.World.Map;

			foreach (var uv in wr.Viewport.AllVisibleCells.CandidateMapCoords)
			{
				if (!map.CellInfos.Contains(uv) || self.World.ShroudObscures(uv))
					continue;

				var cellinfo = map.CellInfos[uv];

				yield return new SpriteRenderable(tileSprite, wr.World.Map.CenterOfCell(uv),
							WVec.Zero, 0, palette, 1f, tileAlpha * info.FootprintAlpha, Color.ToFloat3(typeColors[cellinfo.TerrainType]), TintModifiers.IgnoreWorldTint, true);
			}
		}

		bool IRenderAboveShroud.SpatiallyPartitionable => false;
	}
}
