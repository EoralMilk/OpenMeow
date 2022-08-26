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
using System.Xml.Linq;
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
		readonly MapVertex[] vertices;
		readonly Sheet[] sheets = new Sheet[SheetCount];

		BlendMode currentBlend = BlendMode.Alpha;
		int nv = 0;
		int ns = 0;

		public MapRenderer(Renderer renderer, IShader shader)
		{
			this.renderer = renderer;
			this.Shader = shader;
			vertices = new MapVertex[renderer.TempBufferSize];

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
				renderer.DrawMapBatch(Shader, vertices, nv, PrimitiveType.TriangleList);
				renderer.Context.SetBlendMode(BlendMode.None);

				nv = 0;
				ns = 0;
			}
		}

		public void SetSheets(IEnumerable<Sheet> sheets)
		{
			var si = 0;

			foreach (var s in sheets)
			{
				if (si >= SheetCount)
					ThrowSheetOverflow(nameof(sheets));

				if (s != null)
					Shader.SetTexture(SheetIndexToTextureName[si++], s.GetTexture());
			}
		}

		public void DrawOverlay(in MapVertex[] overlayVertices, float alpha)
		{
			if (nv + overlayVertices.Length >= vertices.Length)
				Flush();

			for (int i = 0; i < overlayVertices.Length; i++)
			{
				vertices[nv + i] = overlayVertices[i].ChangeAlpha(alpha);
			}

			nv += overlayVertices.Length;
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

		public void SetTextures(World world, UsageType type = UsageType.Terrain)
		{

			switch (type)
			{
				case UsageType.Terrain:
					Shader.SetFloat("WaterUVOffset", (float)(Game.LocalTick % 400) / 400);
					Shader.SetFloat("GrassUVOffset", (MathF.Sin((float)Game.LocalTick / 6) + 1) / 177);

					foreach (var key in world.MapTextureCache.TerrainTexturesSet)
					{
						Shader.SetTexture(world.MapTextureCache.Textures[key].Item1,
							world.MapTextureCache.Textures[key].Item2);
					}

					Shader.SetTexture(MapTextureCache.TN_Caustics, world.MapTextureCache.CausticsTextures[Math.Min((Game.LocalTick % 93) / 3, world.MapTextureCache.CausticsTextures.Length - 1)]);
					break;
				case UsageType.Smudge:

					foreach (var key in world.MapTextureCache.SmudgeTexturesSet)
					{
						Shader.SetTexture(world.MapTextureCache.Textures[key].Item1,
							world.MapTextureCache.Textures[key].Item2);
					}

					break;
			}

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
