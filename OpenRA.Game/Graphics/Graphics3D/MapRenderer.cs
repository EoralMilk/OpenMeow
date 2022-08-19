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
	public class MapRenderer
	{
		public const int SheetCount = 4;
		static readonly string[] SheetIndexToTextureName = Exts.MakeArray(SheetCount, i => $"Texture{i}");

		readonly Renderer renderer;
		public readonly IShader Shader;

		public MapRenderer(Renderer renderer, IShader shader)
		{
			this.renderer = renderer;
			this.Shader = shader;
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

		public void SetTextures(World world)
		{
			Shader.SetFloat("WaterUVOffset", (float)(Game.LocalTick % 400) / 400);
			Shader.SetFloat("GrassUVOffset", (MathF.Sin((float)Game.LocalTick / 6) + 1) / 177);

			foreach (var kv in world.MapTextureCache.Textures)
			{
				Shader.SetTexture(kv.Key, kv.Value);
			}

			Shader.SetTexture("Caustics", world.MapTextureCache.Caustics[Math.Min((Game.LocalTick % 93) / 3, world.MapTextureCache.Caustics.Length - 1)]);

		}

		// PERF: methods that throw won't be inlined by the JIT, so extract a static helper for use on hot paths
		static void ThrowSheetOverflow(string paramName)
		{
			throw new ArgumentException($"MapRenderer only supports {SheetCount} simultaneous textures", paramName);
		}

		public void SetPalette(ITexture palette, ITexture colorShifts)
		{
			Shader.SetTexture("Palette", palette);
			Shader.SetTexture("ColorShifts", colorShifts);
		}

		public void SetCameraParams(in World3DRenderer w3dr, bool sunCamera)
		{
			Shader.SetCommonParaments(w3dr, sunCamera);
		}

		public void SetRenderShroud(bool flag)
		{
			Shader.SetBool("RenderShroud", flag);
			Shader.SetVec("CameraInvFront",
				Game.Renderer.World3DRenderer.InverseCameraFront.x,
				Game.Renderer.World3DRenderer.InverseCameraFront.y,
				Game.Renderer.World3DRenderer.InverseCameraFront.z);
		}

		public void SetShadowParams()
		{
			if (Game.Renderer.World3DRenderer != null)
			{
				Game.Renderer.SetShadowParams(Shader, Game.Renderer.World3DRenderer);
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
