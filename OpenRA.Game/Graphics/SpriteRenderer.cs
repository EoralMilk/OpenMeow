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
using System.Collections;
using System.Collections.Generic;
using GlmSharp;
using OpenRA.Primitives;
using OpenRA.Primitives.FixPoint;

namespace OpenRA.Graphics
{
	public class SpriteRenderer : Renderer.IBatchRenderer
	{
		public const int SheetCount = 8;
		static readonly string[] SheetIndexToTextureName = Exts.MakeArray(SheetCount, i => $"Texture{i}");

		readonly Renderer renderer;
		public readonly IShader Shader;

		readonly Vertex[] vertices;
		readonly Sheet[] sheets = new Sheet[SheetCount];

		BlendMode currentBlend = BlendMode.Alpha;
		int nv = 0;
		int ns = 0;
		readonly int maxVerticesPerMesh = 12;
		public SpriteRenderer(Renderer renderer, IShader shader)
		{
			this.renderer = renderer;
			this.Shader = shader;
			vertices = new Vertex[renderer.TempBufferSize];
		}

		public void Flush(BlendMode blendMode = BlendMode.None)
		{
			if (nv > 0)
			{
				for (var i = 0; i < ns; i++)
				{
					Shader.SetTexture(SheetIndexToTextureName[i], sheets[i].GetTexture());
					sheets[i] = null;
				}

				renderer.Context.SetBlendMode(blendMode != BlendMode.None ? blendMode : currentBlend);
				Shader.PrepareRender();
				renderer.DrawBatch(Shader, vertices, nv, PrimitiveType.TriangleList);
				renderer.Context.SetBlendMode(BlendMode.None);

				nv = 0;
				ns = 0;
			}
		}

		int2 SetRenderStateForSprite(Sprite s)
		{
			renderer.CurrentBatchRenderer = this;

			if (s.BlendMode != currentBlend || nv + renderer.MaxVerticesPerMesh > renderer.TempBufferSize)
				Flush();

			currentBlend = s.BlendMode;

			// Check if the sheet (or secondary data sheet) have already been mapped
			var sheet = s.Sheet;
			var sheetIndex = 0;
			for (; sheetIndex < ns; sheetIndex++)
				if (sheets[sheetIndex] == sheet)
					break;

			var secondarySheetIndex = 0;
			var ss = s as SpriteWithSecondaryData;
			if (ss != null)
			{
				var secondarySheet = ss.SecondarySheet;
				for (; secondarySheetIndex < ns; secondarySheetIndex++)
					if (sheets[secondarySheetIndex] == secondarySheet)
						break;

				// If neither sheet has been mapped both index values will be set to ns.
				// This is fine if they both reference the same texture, but if they don't
				// we must increment the secondary sheet index to the next free sampler.
				if (secondarySheetIndex == sheetIndex && secondarySheet != sheet)
					secondarySheetIndex++;
			}

			// Make sure that we have enough free samplers to map both if needed, otherwise flush
			if (Math.Max(sheetIndex, secondarySheetIndex) >= sheets.Length)
			{
				Flush();
				sheetIndex = 0;
				secondarySheetIndex = ss != null && ss.SecondarySheet != sheet ? 1 : 0;
			}

			if (sheetIndex >= ns)
			{
				sheets[sheetIndex] = sheet;
				ns++;
			}

			if (secondarySheetIndex >= ns && ss != null)
			{
				sheets[secondarySheetIndex] = ss.SecondarySheet;
				ns++;
			}

			return new int2(sheetIndex, secondarySheetIndex);
		}

		float ResolveTextureIndex(Sprite s, PaletteReference pal)
		{
			if (pal == null)
				return 0;

			// PERF: Remove useless palette assignments for RGBA sprites
			// HACK: This is working around the limitation that palettes are defined on traits rather than on sequences,
			// and can be removed once this has been fixed
			if (s.Channel == TextureChannel.RGBA && !pal.HasColorShift)
				return 0;

			return pal.TextureIndex;
		}

		internal void DrawSprite(Sprite s, float paletteTextureIndex, in float3 location, in float3 scale, float rotation = 0f)
		{
			var samplers = SetRenderStateForSprite(s);
			Util.FastCreateQuad(vertices, location + scale * s.Offset, s, samplers, paletteTextureIndex, nv, scale * s.Size, float3.Ones,
								1f, rotation);
			nv += 6;
		}

		internal void DrawSprite(Sprite s, float paletteTextureIndex, in float3 location, float scale, float rotation = 0f)
		{
			var samplers = SetRenderStateForSprite(s);
			Util.FastCreateQuad(vertices, location + scale * s.Offset, s, samplers, paletteTextureIndex, nv, scale * s.Size, float3.Ones,
								1f, rotation);
			nv += 6;
		}

		public void DrawSprite(Sprite s, PaletteReference pal, in float3 location, float scale = 1f, float rotation = 0f)
		{
			DrawSprite(s, ResolveTextureIndex(s, pal), location, scale, rotation);
		}

		internal void DrawSprite(Sprite s, float paletteTextureIndex, in float3 location, float scale, in float3 tint, float alpha,
			float rotation = 0f)
		{
			var samplers = SetRenderStateForSprite(s);
			Util.FastCreateQuad(vertices, location + scale * s.Offset, s, samplers, paletteTextureIndex, nv, scale * s.Size, tint, alpha,
								rotation);
			nv += 6;
		}

		// old sprite renderable use this
		public void DrawSprite(Sprite s, PaletteReference pal, in float3 location, float scale, in float3 tint, float alpha,
			float rotation = 0f)
		{
			DrawSprite(s, ResolveTextureIndex(s, pal), location, scale, tint, alpha, rotation);
		}

		// draw sprite with wpos
		public void DrawCardSprite(Sprite s, float pal, in WPos wPos, in vec3 viewOffset, float scale, in float3 tint, float alpha, float rotation = 0f)
		{
			var samplers = SetRenderStateForSprite(s);
			nv += Util.FastCreateCard(vertices, wPos, viewOffset, s, samplers, pal, scale, tint, alpha, nv, rotation);
		}

		public void DrawCardSprite(Sprite s, PaletteReference pal, in WPos wPos, in vec3 viewOffset, float scale, in float3 tint, float alpha, float rotation = 0f)
		{
			var samplers = SetRenderStateForSprite(s);
			nv += Util.FastCreateCard(vertices, wPos, viewOffset, s, samplers, ResolveTextureIndex(s, pal), scale, tint, alpha, nv, rotation);
		}

		public void DrawPlaneSprite(Sprite s, PaletteReference pal, in WPos wPos, in  vec3 viewOffset, float scale, in float3 tint, float alpha, float rotation = 0f)
		{
			var samplers = SetRenderStateForSprite(s);
			Util.FastCreatePlane(vertices, wPos, viewOffset, s, samplers, ResolveTextureIndex(s, pal), scale, tint, alpha, nv, rotation);
			nv += 6;
		}

		public void DrawFloatBoardSprite(Sprite s, PaletteReference pal, in WPos wPos, in vec3 viewOffset, float scale, in float3 tint, float alpha, float rotation = 0f)
		{
			var samplers = SetRenderStateForSprite(s);
			nv += Util.FastCreateFloatBoard(vertices, wPos, viewOffset, s, samplers, ResolveTextureIndex(s, pal), scale, tint, alpha, nv, rotation);
		}

		public void DrawBoardSprite(Sprite s, PaletteReference pal, in WPos wPos, in vec3 viewOffset, float scale, in float3 tint, float alpha, float rotation = 0f)
		{
			var samplers = SetRenderStateForSprite(s);
			Util.FastCreateBoard(vertices, wPos, viewOffset, s, samplers, ResolveTextureIndex(s, pal), scale, tint, alpha, nv, rotation);
			nv += 6;
		}

		public void DrawTileOverlaySprite(Sprite s, PaletteReference pal, in WPos wPos, in vec3 viewOffset, float scale, in float3 tint, float alpha, Map map, float rotation = 0f)
		{
			var samplers = SetRenderStateForSprite(s);

			var vv = Util.FastCreateTileOverlay(wPos, World3DCoordinate.Vec3toFloat3(viewOffset),
				s, samplers, ResolveTextureIndex(s, pal),
				scale, tint, alpha, map);

			if (nv + vv.Length >= vertices.Length)
				Flush();

			Array.Copy(vv, 0, vertices, nv, vv.Length);

			nv += vv.Length;
		}

		public void DrawCellSprite(Sprite s, PaletteReference pal, in WPos wPos, in vec3 viewOffset, float scale, in float3 tint, float alpha, Map map, float rotation = 0f)
		{
			var cPos = map.CellContaining(wPos);
			if (!map.Contains(cPos))
				return;

			var samplers = SetRenderStateForSprite(s);
			var vv = Util.FastCreateCell(cPos, World3DCoordinate.Vec3toFloat3(viewOffset),
				s, samplers, ResolveTextureIndex(s, pal),
				scale, tint, alpha, map);

			if (nv + vv.Length >= vertices.Length)
				Flush();

			Array.Copy(vv, 0, vertices, nv, vv.Length);

			nv += vv.Length;
		}

		internal void DrawSprite(Sprite s, float paletteTextureIndex, in float3 a, in float3 b, in float3 c, in float3 d, in float3 tint, float alpha)
		{
			var samplers = SetRenderStateForSprite(s);
			Util.FastCreateQuad(vertices, a, b, c, d, s, samplers, paletteTextureIndex, tint, alpha, nv);
			nv += 6;
		}

		public void DrawVertexBuffer(IVertexBuffer buffer, int start, int length, PrimitiveType type, IEnumerable<Sheet> sheets, BlendMode blendMode)
		{
			var i = 0;
			foreach (var s in sheets)
			{
				if (i >= SheetCount)
					ThrowSheetOverflow(nameof(sheets));

				if (s != null)
					Shader.SetTexture(SheetIndexToTextureName[i++], s.GetTexture());
			}

			renderer.Context.SetBlendMode(blendMode);
			Shader.PrepareRender();
			renderer.DrawBatch(Shader, buffer, start, length, type);
			renderer.Context.SetBlendMode(BlendMode.None);
		}

		// PERF: methods that throw won't be inlined by the JIT, so extract a static helper for use on hot paths
		static void ThrowSheetOverflow(string paramName)
		{
			throw new ArgumentException($"SpriteRenderer only supports {SheetCount} simultaneous textures", paramName);
		}

		// For RGBAColorRenderer
		internal void DrawRGBAVertices(Vertex[] v, BlendMode blendMode)
		{
			renderer.CurrentBatchRenderer = this;

			if (currentBlend != blendMode || nv + v.Length > renderer.TempBufferSize)
				Flush();

			currentBlend = blendMode;
			Array.Copy(v, 0, vertices, nv, v.Length);
			nv += v.Length;
		}

		public void SetPalette(ITexture palette, ITexture colorShifts)
		{
			Shader.SetTexture("Palette", palette);
			Shader.SetTexture("ColorShifts", colorShifts);
		}

		public void SetViewportParams(Size sheetSize, int downscale, float depthMargin, int2 scroll)
		{
			// Calculate the scale (r1) and offset (r2) that convert from OpenRA viewport pixels
			// to OpenGL normalized device coordinates (NDC). OpenGL expects coordinates to vary from [-1, 1],
			// so we rescale viewport pixels to the range [0, 2] using r1 then subtract 1 using r2.
			var width = 2f / (downscale * sheetSize.Width);
			var height = 2f / (downscale * sheetSize.Height);

			// Depth is more complicated:
			// * The OpenGL z axis is inverted (negative is closer) relative to OpenRA (positive is closer).
			// * We want to avoid clipping pixels that are behind the nominal z == y plane at the
			//   top of the map, or above the nominal z == y plane at the bottom of the map.
			//   We therefore expand the depth range by an extra margin that is calculated based on
			//   the maximum expected world height (see Renderer.InitializeDepthBuffer).
			// * Sprites can specify an additional per-pixel depth offset map, which is applied in the
			//   fragment shader. The fragment shader operates in OpenGL window coordinates, not NDC,
			//   with a depth range [0, 1] corresponding to the NDC [-1, 1]. We must therefore multiply the
			//   sprite channel value [0, 1] by 255 to find the pixel depth offset, then by our depth scale
			//   to find the equivalent NDC offset, then divide by 2 to find the window coordinate offset.
			// * If depthMargin == 0 (which indicates per-pixel depth testing is disabled) sprites that
			//   extend beyond the top of bottom edges of the screen may be pushed outside [-1, 1] and
			//   culled by the GPU. We avoid this by forcing everything into the z = 0 plane.
			var depth = depthMargin != 0f ? 2f / (downscale * (sheetSize.Height + depthMargin)) : 0;

			//shader.SetVec("DepthTextureScale", 128 * depth);
			Shader.SetVec("Scroll", scroll.X, scroll.Y, depthMargin != 0f ? scroll.Y : 0);
			Shader.SetVec("r1", width, height, -depth);
			Shader.SetVec("r2", -1, -1, depthMargin != 0f ? 1 : 0);
			Shader.SetBool("hasCamera", false);
			Shader.SetBool("renderScreen", false);
		}

		public void SetCameraParams()
		{
			if (Game.Renderer.World3DRenderer != null)
			{
				Shader.SetBool("hasCamera", true);
				Shader.SetBool("renderScreen", false);
				Shader.SetMatrix("projection", Game.Renderer.World3DRenderer.Projection.Values1D);
				Shader.SetMatrix("view", Game.Renderer.World3DRenderer.View.Values1D);
				//shader.SetVec("viewPos", Game.Renderer.Standalone3DRenderer.CameraPos.x, Game.Renderer.Standalone3DRenderer.CameraPos.y, Game.Renderer.Standalone3DRenderer.CameraPos.z);
			}
		}

		public void SetShadowParams()
		{
			if (Game.Renderer.World3DRenderer != null)
			{
				Game.Renderer.SetShadowParams(Shader, Game.Renderer.World3DRenderer, Game.Settings.Graphics.CombinedShadowType);
				Game.Renderer.SetLightParams(Shader, Game.Renderer.World3DRenderer);
			}
		}

		public void SetDepthPreview(bool enabled, float contrast, float offset)
		{
			Shader.SetBool("EnableDepthPreview", enabled);
			Shader.SetVec("DepthPreviewParams", contrast, offset);
		}

		public void SetAntialiasingPixelsPerTexel(float pxPerTx)
		{
			Shader.SetVec("AntialiasPixelsPerTexel", pxPerTx);
		}
	}
}
