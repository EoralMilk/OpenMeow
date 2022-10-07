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
using System.Runtime.CompilerServices;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Terrain;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Widgets;
using static OpenRA.Mods.Common.Traits.LocomotorInfo;

namespace OpenRA.Mods.Common.Widgets
{
	public class TerrainBrushPreviewWidget : Widget
	{
		public Func<float> GetScale = () => 1f;

		readonly ITiledTerrainRenderer terrainRenderer;
		readonly WorldRenderer worldRenderer;

		MaskBrush brush;
		Rectangle bounds;

		public MaskBrush Brush
		{
			get => brush;

			set
			{
				brush = value;
				if (brush == null)
					return;

				bounds = new Rectangle(0, 0, brush.TextureSize.X, brush.TextureSize.Y);
			}
		}

		[ObjectCreator.UseCtor]
		public TerrainBrushPreviewWidget(WorldRenderer worldRenderer, World world)
		{
			this.worldRenderer = worldRenderer;
			terrainRenderer = world.WorldActor.TraitOrDefault<ITiledTerrainRenderer>();
			if (terrainRenderer == null)
				throw new YamlException("TerrainTemplatePreviewWidget requires a tile-based terrain renderer.");
		}

		protected TerrainBrushPreviewWidget(TerrainBrushPreviewWidget other)
			: base(other)
		{
			worldRenderer = other.worldRenderer;
			terrainRenderer = other.terrainRenderer;
			Brush = other.Brush;
			GetScale = other.GetScale;
		}

		public override Widget Clone() { return new TerrainBrushPreviewWidget(this); }

		public override void Draw()
		{
			if (brush == null)
				return;

			var scale = GetScale();
			var sb = new Rectangle((int)(scale * bounds.X), (int)(scale * bounds.Y), (int)(scale * bounds.Width), (int)(scale * bounds.Height));
			var origin = RenderOrigin + new int2((RenderBounds.Size.Width - sb.Width) / 2 - sb.X, (RenderBounds.Size.Height - sb.Height) / 2 - sb.Y);

			var r = new UITextureArrayRenderable(brush.Map.TextureCache.BrushTextureArray, brush.TextureIndex, WPos.Zero, origin, brush.TextureSize, 0, BlendMode.Alpha, scale);
			r.PrepareRender(worldRenderer).Render(worldRenderer);
		}
	}
}
