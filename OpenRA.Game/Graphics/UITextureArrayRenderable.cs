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

using OpenRA.Primitives;

namespace OpenRA.Graphics
{
	public class UITextureArrayRenderable : IRenderable, IFinalizedRenderable
	{
		readonly ITexture textureArray;
		readonly int index;
		readonly int2 screenPos;
		readonly float2 size;
		readonly WPos effectiveWorldPos;
		readonly int zOffset;
		readonly float scale;
		readonly float alpha;
		readonly BlendMode blendMode;
		public BlendMode BlendMode => blendMode;

		public UITextureArrayRenderable(ITexture textureArray, int index, WPos effectiveWorldPos, int2 screenPos, float2 size, int zOffset, BlendMode blendMode, float scale = 1f, float alpha = 1f)
		{
			this.textureArray = textureArray;
			this.index = index;
			this.screenPos = screenPos;
			this.size = size;
			this.effectiveWorldPos = effectiveWorldPos;
			this.zOffset = zOffset;
			this.scale = scale;
			this.alpha = alpha;
			this.blendMode = blendMode;
		}

		// Does not exist in the world, so a world positions don't make sense
		public WPos Pos => effectiveWorldPos;
		public bool IsDecoration => true;
		public int ZOffset => zOffset;

		public IRenderable WithZOffset(int newOffset) { return this; }
		public IRenderable OffsetBy(in WVec vec) { return this; }
		public IRenderable AsDecoration() { return this; }

		public IFinalizedRenderable PrepareRender(WorldRenderer wr) { return this; }
		public void Render(WorldRenderer wr)
		{
			Game.Renderer.UITextureRenderer.DrawQuadForTexArray(textureArray, index, screenPos, scale * size, blendMode);
		}

		public void RenderDebugGeometry(WorldRenderer wr)
		{
			//var offset = screenPos + sprite.Offset.XY;
			//Game.Renderer.RgbaColorRenderer.DrawRect(offset, offset + sprite.Size.XY, 1, Color.Red);
		}

		public Rectangle ScreenBounds(WorldRenderer wr)
		{
			var offset = screenPos;
			return Util.BoundingRectangle(offset, scale * new float3(textureArray.Size.Width, textureArray.Size.Height, 0), 0);
		}
	}
}
