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
using System.Numerics;
using OpenRA.Primitives;

namespace OpenRA.Graphics
{
	public class SpriteRenderable : IPalettedRenderable, IModifyableRenderable, IFinalizedRenderable
	{
		public static readonly IEnumerable<IRenderable> None = Array.Empty<IRenderable>();

		public readonly Sprite Sprite;
		readonly WPos pos;

		/// <summary>
		/// using for nml board sprite
		/// </summary>
		readonly WVec? nmlDir;
		readonly WVec offset;
		readonly int zOffset;
		readonly PaletteReference palette;
		readonly float scale;
		readonly WAngle rotation = WAngle.Zero;
		readonly float3 tint;
		readonly TintModifiers tintModifiers;
		readonly float alpha;
		readonly bool isDecoration;
		readonly BlendMode blendMode;
		public BlendMode BlendMode => blendMode;

		public SpriteRenderable(Sprite sprite, WPos pos, WVec offset, int zOffset,
			PaletteReference palette,
			float scale, float alpha, float3 tint, TintModifiers tintModifiers,
			bool isDecoration, WAngle rotation, bool forceAlphaBlend = false, WVec? nmlDir = null)
		{
			this.Sprite = sprite;
			this.pos = pos;
			this.offset = offset;
			this.zOffset = zOffset;
			this.palette = palette;
			this.scale = scale;
			this.rotation = rotation;
			this.tint = tint;
			this.isDecoration = isDecoration;
			this.tintModifiers = tintModifiers;
			this.alpha = alpha;
			this.nmlDir = nmlDir;
			if (forceAlphaBlend || (sprite.BlendMode == BlendMode.None && alpha < 1f))
			{
				sprite.ChangeBlendMode(BlendMode.Alpha);
			}

			blendMode = sprite.BlendMode;

			// PERF: Remove useless palette assignments for RGBA sprites
			// HACK: This is working around the fact that palettes are defined on traits rather than sequences
			// and can be removed once this has been fixed
			if (sprite.Channel == TextureChannel.RGBA && !(palette?.HasColorShift ?? false))
				this.palette = null;
		}

		public SpriteRenderable(Sprite sprite, WPos pos, WVec offset, int zOffset, PaletteReference palette, float scale, float alpha,
			float3 tint, TintModifiers tintModifiers, bool isDecoration)
			: this(sprite, pos, offset, zOffset, palette, scale, alpha, tint, tintModifiers, isDecoration, WAngle.Zero) { }

		public WPos Pos => pos + offset;
		public WVec Offset => offset;
		public PaletteReference Palette => palette;
		public int ZOffset => zOffset;
		public bool IsDecoration => isDecoration;

		public float Alpha => alpha;
		public float3 Tint => tint;
		public TintModifiers TintModifiers => tintModifiers;

		public IPalettedRenderable WithPalette(PaletteReference newPalette)
		{
			return new SpriteRenderable(Sprite, pos, offset, zOffset, newPalette, scale, alpha, tint, tintModifiers, isDecoration, rotation, BlendMode == BlendMode.Alpha, nmlDir);
		}

		public IRenderable WithZOffset(int newOffset)
		{
			return new SpriteRenderable(Sprite, pos, offset, newOffset, palette, scale, alpha, tint, tintModifiers, isDecoration, rotation, BlendMode == BlendMode.Alpha, nmlDir);
		}

		public IRenderable OffsetBy(in WVec vec)
		{
			return new SpriteRenderable(Sprite, pos + vec, offset, zOffset, palette, scale, alpha, tint, tintModifiers, isDecoration, rotation, BlendMode == BlendMode.Alpha, nmlDir);
		}

		public IRenderable AsDecoration()
		{
			return new SpriteRenderable(Sprite, pos, offset, zOffset, palette, scale, alpha, tint, tintModifiers, true, rotation, BlendMode == BlendMode.Alpha, nmlDir);
		}

		public IModifyableRenderable WithAlpha(float newAlpha)
		{
			return new SpriteRenderable(Sprite, pos, offset, zOffset, palette, scale, newAlpha, tint, tintModifiers, isDecoration, rotation, BlendMode == BlendMode.Alpha, nmlDir);
		}

		public IModifyableRenderable WithTint(in float3 newTint, TintModifiers newTintModifiers)
		{
			return new SpriteRenderable(Sprite, pos, offset, zOffset, palette, scale, alpha, newTint, newTintModifiers, isDecoration, rotation, BlendMode == BlendMode.Alpha, nmlDir);
		}

		float3 ScreenPosition(WorldRenderer wr)
		{
			var s = 0.5f * scale * Sprite.Size;
			return wr.Screen3DPxPosition(pos) + wr.ScreenPxOffset(offset) - new float3((int)s.X, (int)s.Y, s.Z);
		}

		public IFinalizedRenderable PrepareRender(WorldRenderer wr) { return this; }
		public void Render(WorldRenderer wr)
		{
			var viewOffset = Game.Renderer.World3DRenderer.InverseCameraFrontMeterPerWDist * (zOffset + 1);
			var t = alpha * tint;
			var a = alpha;

			// this sprite should use MapRenderer to render
			if (Sprite.SpriteMeshType == SpriteMeshType.TerrainCovering || Sprite.SpriteMeshType == SpriteMeshType.TileActorNoStretch)
			{
				Game.Renderer.MapRenderer.DrawTileAdditonSprite(Sprite, palette, Pos, viewOffset, scale, t, a, wr.World.Map);
				return;
			}

			var wsr = Game.Renderer.WorldSpriteRenderer;

			if (Sprite.SpriteMeshType != SpriteMeshType.TileOverlay &&
				Sprite.SpriteMeshType != SpriteMeshType.TileOverlayNoStretch &&
				wr.TerrainLighting != null &&
				(tintModifiers & TintModifiers.IgnoreWorldTint) == 0)
				t *= wr.TerrainLighting.TintAt(pos);

			// Shader interprets negative alpha as a flag to use the tint colour directly instead of multiplying the sprite colour
			if (((Sprite.SpriteMeshType == SpriteMeshType.TileOverlay || Sprite.SpriteMeshType == SpriteMeshType.TileOverlayNoStretch) &&
				(tintModifiers & TintModifiers.IgnoreWorldTint) != 0) ||
				(!(Sprite.SpriteMeshType == SpriteMeshType.TileOverlay || Sprite.SpriteMeshType == SpriteMeshType.TileOverlayNoStretch) &&
				(tintModifiers & TintModifiers.ReplaceColor) != 0))
				a *= -1;

			wsr.DrawSprite(Sprite, palette, ScreenPosition(wr), scale, t, a);

			switch (Sprite.SpriteMeshType)
			{
				case SpriteMeshType.Plane:
					wsr.DrawPlaneSprite(Sprite, palette, Pos, viewOffset, scale, t, a);
					break;
				case SpriteMeshType.Card:
					wsr.DrawCardSprite(Sprite, palette, Pos, viewOffset, scale, t, a);
					break;
				case SpriteMeshType.Board:
					if (nmlDir == null)
						wsr.DrawBoardSprite(Sprite, palette, Pos, viewOffset, scale, t, a);
					else
					{
						// An easy vector to find which is perpendicular vector to forwardStep, with 0 Z component
						var leftVector = new Vector3(0, 0, 1);
						if (nmlDir.Value.X != 0 || nmlDir.Value.Y != 0)
						{
							leftVector = Vector3.Normalize(World3DCoordinate.WPosToVec3(new WPos(nmlDir.Value.Y, -nmlDir.Value.X, 0)));
						}

						var upVector = Vector3.Normalize(Vector3.Cross(World3DCoordinate.WVecToVec3(nmlDir.Value), leftVector));
						wsr.DrawNmlDirBoardSprite(Sprite, palette, Pos, viewOffset, leftVector, upVector, scale, t, a);
					}

					break;
				case SpriteMeshType.FloatBoard:
					wsr.DrawFloatBoardSprite(Sprite, palette, Pos, viewOffset, scale, t, a);
					break;
				case SpriteMeshType.TileOverlay:
					wsr.DrawTileOverlaySprite(Sprite, palette, Pos, viewOffset, scale, t, a, wr.World.Map);
					break;
				case SpriteMeshType.TileOverlayNoStretch:
					wsr.DrawTileOverlaySprite(Sprite, palette, Pos, viewOffset, scale, t, a, wr.World.Map);
					break;
				case SpriteMeshType.Cell:
					wsr.DrawCellSprite(Sprite, palette, Pos, viewOffset, scale, t, a, wr.World.Map);
					break;
			}
		}

		public void RenderDebugGeometry(WorldRenderer wr)
		{
			var pos = ScreenPosition(wr) + Sprite.Offset;
			var bpos = ScreenPosition(wr);
			var tl = wr.Viewport.WorldToViewPx(pos);
			var br = wr.Viewport.WorldToViewPx(pos + Sprite.Size);
			var ca = wr.Viewport.WorldToViewPx(pos + Sprite.Size / 2);
			var cb = wr.Viewport.WorldToViewPx(bpos + Sprite.Size / 2);
			Game.Renderer.RgbaColorRenderer.DrawRect(tl, br, 1, Color.Red);
			Game.Renderer.RgbaColorRenderer.DrawRect(tl, br, 1, Color.Red);
			Game.Renderer.RgbaColorRenderer.DrawScreenLine(ca, cb, 2, Color.Azure);
			Game.Renderer.RgbaColorRenderer.DrawRect(cb - new int2(1, 1), cb + new int2(1, 1), 1, Color.BlueViolet);
		}

		public Rectangle ScreenBounds(WorldRenderer wr)
		{
			var screenOffset = ScreenPosition(wr) + Sprite.Offset;
			return Util.BoundingRectangle(screenOffset, Sprite.Size, rotation.RendererRadians());
		}
	}
}
