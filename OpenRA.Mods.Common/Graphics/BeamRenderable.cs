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
	public enum BeamRenderableShape { Cylindrical, Flat }
	public class BeamRenderable : IRenderable, IFinalizedRenderable
	{
		readonly WPos pos;
		readonly int zOffset;
		readonly WVec length;
		readonly BeamRenderableShape shape;
		readonly WDist width;
		readonly Color color;
		readonly BlendMode blendMode;

		public BeamRenderable(WPos pos, int zOffset, in WVec length, BeamRenderableShape shape, WDist width, Color color, BlendMode blendMode)
		{
			this.pos = pos;
			this.zOffset = zOffset;
			this.length = length;
			this.shape = shape;
			this.width = width;
			this.color = color;
			this.blendMode = blendMode;
		}

		public WPos Pos => pos;
		public int ZOffset => zOffset;
		public bool IsDecoration => true;

		public IRenderable WithZOffset(int newOffset) { return new BeamRenderable(pos, zOffset, length, shape, width, color, blendMode); }
		public IRenderable OffsetBy(in WVec vec) { return new BeamRenderable(pos + vec, zOffset, length, shape, width, color, blendMode); }
		public IRenderable AsDecoration() { return this; }

		public IFinalizedRenderable PrepareRender(WorldRenderer wr) { return this; }
		public void Render(WorldRenderer wr)
		{
			var vecLength = length.Length;
			if (vecLength == 0)
				return;

			if (shape == BeamRenderableShape.Flat)
			{
				var delta = length * width.Length / (2 * vecLength);
				var corner = new WVec(-delta.Y, delta.X, delta.Z);
				var a = wr.Render3DPosition(pos - corner);
				var b = wr.Render3DPosition(pos + corner);
				var c = wr.Render3DPosition(pos + corner + length);
				var d = wr.Render3DPosition(pos - corner + length);
				Game.Renderer.WorldRgbaColorRenderer.FillWorldRect(a, b, c, d, color, blendMode: blendMode);
			}
			else
			{
				var start = wr.Render3DPosition(pos);
				var end = wr.Render3DPosition(pos + length);
				var screenWidth = wr.RenderVector(new WVec(width, WDist.Zero, WDist.Zero))[0];
				Game.Renderer.WorldRgbaColorRenderer.DrawWorldLine(start, end, screenWidth, color, blendMode: blendMode);
			}
		}

		public void RenderDebugGeometry(WorldRenderer wr) { }
		public Rectangle ScreenBounds(WorldRenderer wr) { return Rectangle.Empty; }
	}
}
