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

using OpenRA.Graphics;
using OpenRA.Primitives;

namespace OpenRA.Mods.Common.Graphics
{
	public class DebugLineRenderable : IRenderable, IFinalizedRenderable
	{
		readonly WPos pos;
		readonly int zOffset;
		readonly float3 start;
		readonly float3 end;
		readonly WDist width;
		readonly Color color;
		readonly BlendMode blendMode;
		public BlendMode BlendMode => blendMode;

		public DebugLineRenderable(WPos pos, int zOffset, float3 start, float3 end, WDist width, Color color, BlendMode blendMode)
		{
			this.start = start;
			this.end = end;
			this.pos = pos;
			this.zOffset = zOffset;
			this.width = width;
			this.color = color;
			this.blendMode = blendMode;
		}

		public WPos Pos => pos;
		public int ZOffset => zOffset;
		public bool IsDecoration => true;

		public IRenderable WithZOffset(int newOffset) { return new DebugLineRenderable(pos, zOffset, start, end, width, color, blendMode); }
		public IRenderable OffsetBy(in WVec vec) { return new DebugLineRenderable(pos + vec, zOffset, start, end, width, color, blendMode); }
		public IRenderable AsDecoration() { return this; }

		public IFinalizedRenderable PrepareRender(WorldRenderer wr) { return this; }
		public void Render(WorldRenderer wr)
		{
			var screenWidth = wr.RenderVector(new WVec(width, WDist.Zero, WDist.Zero))[0];
			Game.Renderer.WorldRgbaColorRenderer.DrawWorldLine(start, end, screenWidth, color, blendMode: blendMode);
		}

		public void RenderDebugGeometry(WorldRenderer wr) { }
		public Rectangle ScreenBounds(WorldRenderer wr) { return Rectangle.Empty; }
	}
}
