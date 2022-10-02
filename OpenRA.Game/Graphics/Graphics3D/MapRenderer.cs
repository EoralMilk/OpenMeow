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
using OpenRA.Primitives.FixPoint;
using static OpenRA.Graphics.TerrainSpriteLayer;

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
					//sheets[i] = null;
				}

				renderer.Context.SetBlendMode(blendMode != BlendMode.None ? blendMode : currentBlend);
				Shader.PrepareRender();
				renderer.DrawMapBatch(Shader, vertices, nv, PrimitiveType.TriangleList);
				renderer.Context.SetBlendMode(BlendMode.None);

				nv = 0;
				ns = 0;
			}
		}

		public void SetSheets(Sheet[] sheets, BlendMode blendMode = BlendMode.None)
		{
			int si = 0;
			if (currentBlend != blendMode)
			{
				Flush();
				currentBlend = blendMode;
			}

			for (int i = 0; i < sheets.Length; i++)
			{
				if (i >= SheetCount)
					ThrowSheetOverflow(nameof(sheets));

				if (sheets[i] != null)
				{
					if (this.sheets[si] != null && sheets[i] != this.sheets[si])
						Flush();
					//Shader.SetTexture(SheetIndexToTextureName[si], sheets[i].GetTexture());
					this.sheets[si] = sheets[i];
					si++;
				}
			}

			ns = Math.Max(si, ns);
		}

		int2 SetRenderStateForSprite(Sprite s)
		{
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

		public void DrawVertices(in MapVertex[] inVertices, int start, int length, bool renderAllVert)
		{
			if (nv + length >= vertices.Length)
				Flush();
			if (renderAllVert)
			{
				Array.Copy(inVertices, start, vertices, nv, length);

				nv += length;
			}
			else
			{
				var end = start + length;
				for (int i = start; i < end; i++)
				{
					if (inVertices[i].A != 0)
					{
						vertices[nv] = inVertices[i];
						nv++;
					}
				}
			}
		}

		public void DrawCells(in LayerCell[] cells, int start, int end)
		{
			for (int i = start; i < end; i++)
			{
				if (!cells[i].Draw)
					continue;

				if (nv + cells[i].Vertices.Length >= vertices.Length)
					Flush();

				Array.Copy(cells[i].Vertices, 0, vertices, nv, cells[i].Vertices.Length);
				nv += cells[i].Vertices.Length;
			}
		}

		public void DrawTileAdditonSprite(Sprite s, PaletteReference pal, in WPos wPos, in vec3 viewOffset, float scale, in float3 tint, float alpha, Map map, float rotation = 0f)
		{
			var samplers = SetRenderStateForSprite(s);

			var vv = Util.FastCreateTileActor(wPos, World3DCoordinate.Vec3toFloat3(viewOffset),
				s, samplers, ResolveTextureIndex(s, pal),
				scale, tint, alpha, map);

			if (nv + vv.Length >= vertices.Length)
				Flush();

			Array.Copy(vv, 0, vertices, nv, vv.Length);

			nv += vv.Length;
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
							world.MapTextureCache.Textures[key].Item2.GetTexture());
					}

					Shader.SetTexture(MapTextureCache.TN_Caustics, world.MapTextureCache.CausticsTextures[Math.Min((Game.LocalTick % 93) / 3, world.MapTextureCache.CausticsTextures.Length - 1)].GetTexture());
					break;
				case UsageType.Smudge:

					foreach (var key in world.MapTextureCache.SmudgeTexturesSet)
					{
						Shader.SetTexture(world.MapTextureCache.Textures[key].Item1,
							world.MapTextureCache.Textures[key].Item2.GetTexture());
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
			Shader.SetVec("CameraInvFront",
				Game.Renderer.World3DRenderer.InverseCameraFront.x,
				Game.Renderer.World3DRenderer.InverseCameraFront.y,
				Game.Renderer.World3DRenderer.InverseCameraFront.z);
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
				Game.Renderer.SetShadowParams(Shader, Game.Renderer.World3DRenderer, Game.Settings.Graphics.TerrainShadowType);
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
