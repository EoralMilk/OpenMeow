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
using OpenRA.Graphics;
using OpenRA.Primitives;

namespace OpenRA.Mods.Common.Graphics
{
	public class DebugCircleRenderable : IRenderable, IFinalizedRenderable
	{
		readonly WPos pos;
		readonly int zOffset;
		readonly int radius;
		readonly int drawSegments;
		public const int CircleSegments = 256;
		static readonly WVec[] FacingOffsets = Exts.MakeArray(CircleSegments, i => new WVec(0, -1024, 0).Rotate(WRot.FromFacing(i * 256 / CircleSegments)));
		readonly WDist width;
		readonly Color color;
		readonly Color color2;

		readonly BlendMode blendMode;
		public BlendMode BlendMode => blendMode;

		public DebugCircleRenderable(WPos pos, int zOffset, int radius, WDist width, Color color, Color color2, BlendMode blendMode, int drawSegments = CircleSegments - 1)
		{
			this.radius = radius;
			this.pos = pos;
			this.zOffset = zOffset;
			this.width = width;
			this.color = color;
			this.color2 = color2;
			this.blendMode = blendMode;
			this.drawSegments = Math.Min(drawSegments + 1, CircleSegments);
		}

		public WPos Pos => pos;
		public int ZOffset => zOffset;
		public bool IsDecoration => true;

		public IRenderable WithZOffset(int newOffset) { return new DebugCircleRenderable(pos, zOffset, radius, width, color, color2, blendMode, drawSegments); }
		public IRenderable OffsetBy(in WVec vec) { return new DebugCircleRenderable(pos + vec, zOffset, radius, width, color, color2, blendMode, drawSegments); }
		public IRenderable AsDecoration() { return this; }

		public IFinalizedRenderable PrepareRender(WorldRenderer wr) { return this; }
		public void Render(WorldRenderer wr)
		{
			var screenWidth = wr.RenderVector(new WVec(width, WDist.Zero, WDist.Zero))[0];

			var r = radius;
			var a = pos + r * FacingOffsets[CircleSegments - 1] / 1024;
			for (var i = 0; i < CircleSegments; i++)
			{
				var b = pos + r * FacingOffsets[i] / 1024;
				Game.Renderer.WorldRgbaColorRenderer.DrawWorldLine(a, b, screenWidth, i < drawSegments ? color : color2, blendMode: blendMode);
				a = b;
			}
		}

		public void RenderDebugGeometry(WorldRenderer wr) { }
		public Rectangle ScreenBounds(WorldRenderer wr) { return Rectangle.Empty; }
	}
}
