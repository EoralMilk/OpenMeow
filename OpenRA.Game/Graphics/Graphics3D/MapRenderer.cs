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
using GlmSharp;
using OpenRA.Primitives;

namespace OpenRA.Graphics
{
	public class MapRenderer : Renderer.IBatchRenderer
	{
		public const int SheetCount = 8;
		static readonly string[] SheetIndexToTextureName = Exts.MakeArray(SheetCount, i => $"Texture{i}");

		readonly Renderer renderer;
		readonly IShader shader;

		readonly Vertex[] vertices;
		readonly Sheet[] sheets = new Sheet[SheetCount];

		BlendMode currentBlend = BlendMode.Alpha;
		int nv = 0;
		int ns = 0;
		readonly int maxVerticesPerMesh = 12;
		public MapRenderer(Renderer renderer, IShader shader)
		{
			this.renderer = renderer;
			this.shader = shader;
			vertices = new Vertex[renderer.TempBufferSize];
		}

		public void Flush(BlendMode blendMode = BlendMode.None)
		{
			if (nv > 0)
			{
				for (var i = 0; i < ns; i++)
				{
					shader.SetTexture(SheetIndexToTextureName[i], sheets[i].GetTexture());
					sheets[i] = null;
				}

				renderer.Context.SetBlendMode(blendMode != BlendMode.None ? blendMode : currentBlend);
				shader.PrepareRender();
				renderer.DrawBatch(shader, vertices, nv, PrimitiveType.TriangleList);
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

		public void DrawVertexBuffer(IVertexBuffer buffer, int start, int length, PrimitiveType type, IEnumerable<Sheet> sheets, BlendMode blendMode)
		{
			var i = 0;
			foreach (var s in sheets)
			{
				if (i >= SheetCount)
					ThrowSheetOverflow(nameof(sheets));

				if (s != null)
					shader.SetTexture(SheetIndexToTextureName[i++], s.GetTexture());
			}

			renderer.Context.SetBlendMode(blendMode);
			shader.PrepareRender();
			renderer.DrawBatch(shader, buffer, start, length, type);
			renderer.Context.SetBlendMode(BlendMode.None);
		}

		// PERF: methods that throw won't be inlined by the JIT, so extract a static helper for use on hot paths
		static void ThrowSheetOverflow(string paramName)
		{
			throw new ArgumentException($"MapRenderer only supports {SheetCount} simultaneous textures", paramName);
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
			shader.SetTexture("Palette", palette);
			shader.SetTexture("ColorShifts", colorShifts);
		}

		public void SetCameraParams(in World3DRenderer w3dr, bool sunCamera)
		{
			shader.SetCommonParaments(w3dr, sunCamera);
		}

		public void SetRenderShroud(bool flag)
		{
			shader.SetBool("RenderShroud", flag);
		}

		public void SetShadowParams()
		{
			if (Game.Renderer.World3DRenderer != null)
			{
				Game.Renderer.SetShadowParams(shader, Game.Renderer.World3DRenderer);
				Game.Renderer.SetLightParams(shader, Game.Renderer.World3DRenderer);
			}
		}

		public void SetDepthPreview(bool enabled, float contrast, float offset)
		{
			shader.SetBool("EnableDepthPreview", enabled);
			shader.SetVec("DepthPreviewParams", contrast, offset);
		}

		public void SetAntialiasingPixelsPerTexel(float pxPerTx)
		{
			shader.SetVec("AntialiasPixelsPerTexel", pxPerTx);
		}
	}
}
