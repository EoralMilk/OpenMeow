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

using GlmSharp;
using OpenRA.Graphics;
using OpenRA.Primitives;

namespace OpenRA.Mods.Common.Graphics
{
	public class SpriteBeamRenderable : IRenderable, IFinalizedRenderable
	{
		public readonly Sprite Sprite;
		readonly PaletteReference palette;

		readonly float alpha;
		readonly WPos pos;
		readonly int zOffset;
		readonly WPos target;
		readonly WDist width;
		readonly Color color;
		readonly BlendMode blendMode;
		public BlendMode BlendMode => blendMode;

		public SpriteBeamRenderable(Sprite sprite, PaletteReference palette, float spriteAlpha, WPos pos, int zOffset, WPos target, WDist width, Color color, BlendMode blendMode)
		{
			Sprite = sprite;
			this.palette = palette;
			this.alpha = spriteAlpha;
			this.pos = pos;
			this.zOffset = zOffset;
			this.target = target;
			this.width = width;
			this.color = color;
			this.blendMode = blendMode;
		}

		public WPos Pos => pos;
		public int ZOffset => zOffset;
		public bool IsDecoration => true;

		public IRenderable WithZOffset(int newOffset) { return new SpriteBeamRenderable(Sprite, palette, alpha, pos, zOffset, target, width, color, blendMode); }
		public IRenderable OffsetBy(in WVec vec) { return new SpriteBeamRenderable(Sprite, palette, alpha, pos + vec, zOffset, target, width, color, blendMode); }
		public IRenderable AsDecoration() { return this; }

		public IFinalizedRenderable PrepareRender(WorldRenderer wr) { return this; }
		public void Render(WorldRenderer wr)
		{
			var viewoffset = World3DCoordinate.Vec3toFloat3(Game.Renderer.World3DRenderer.InverseCameraFrontMeterPerWDist * (zOffset + 1));
			var start = World3DCoordinate.WPosToFloat3(pos) + viewoffset;
			var end = World3DCoordinate.WPosToFloat3(target) + viewoffset;
			var widthOffset = CalSpriteDir(start, end, (float)width.Length / World3DCoordinate.WDistPerMeter);

			float3 leftTop = end - widthOffset;
			float3 rightTop = end + widthOffset;
			float3 leftBottom = start - widthOffset;
			float3 rightBottom = start + widthOffset;

			if (BlendMode == BlendMode.Alpha)
				Game.Renderer.WorldSpriteRenderer.DrawDirectionSprite(Sprite, palette,
				leftTop, rightTop, leftBottom, rightBottom,
				new float3(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f), color.A / 255.0f * alpha, new float3(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f), color.A / 255.0f * alpha);
			else
				Game.Renderer.WorldSpriteRenderer.DrawDirectionSprite(Sprite, palette,
				leftTop, rightTop, leftBottom, rightBottom,
				alpha * new float3(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f), color.A / 255.0f * alpha,
				alpha * new float3(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f), color.A / 255.0f * alpha);
		}

		public void RenderDebugGeometry(WorldRenderer wr) { }
		public Rectangle ScreenBounds(WorldRenderer wr) { return Rectangle.Empty; }

		public static float3 CalSpriteDir(float3 start, float3 end, float width)
		{
			var dir = World3DCoordinate.Float3toVec3(end - start).Normalized;
			var cam = Game.Renderer.World3DRenderer.InverseCameraFront;
			vec3 cross;
			if (dir == cam)
				cross = Game.Renderer.World3DRenderer.CameraUp;
			else
				cross = vec3.Cross(cam, dir).Normalized;
			var widthOffset = World3DCoordinate.Vec3toFloat3(cross * (width / 2));

			return widthOffset;
		}
	}
}
