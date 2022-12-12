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
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Primitives;

namespace OpenRA.Mods.Common.Graphics
{
	public class ModelRenderable : IPalettedRenderable, IModifyableRenderable
	{
		readonly IEnumerable<ModelAnimation> models;
		readonly WPos pos;
		readonly int zOffset;
		readonly WRot camera;
		public readonly float LightScale = 0.4f;
		public readonly float SpecularScale = 0.13f;
		public readonly float AmbientScale = 1.12f;
		readonly float[] lightAmbientColor;
		readonly float[] lightDiffuseColor;
		readonly PaletteReference palette;
		readonly PaletteReference normalsPalette;
		readonly PaletteReference shadowPalette;
		readonly float scale;
		readonly float alpha;
		readonly float3 tint;
		readonly TintModifiers tintModifiers;
		public BlendMode BlendMode => BlendMode.None;
		public ModelRenderable(
			IEnumerable<ModelAnimation> models, WPos pos, int zOffset, in WRot camera, float scale,
			float[] lightAmbientColor, float[] lightDiffuseColor, float lightScale, float ambientScale, float specularScale,
			PaletteReference color, PaletteReference normals, PaletteReference shadow)
			: this(models, pos, zOffset, camera, scale, lightAmbientColor, lightDiffuseColor, lightScale, ambientScale, specularScale,
				color, normals, shadow, 1f,
				float3.Ones, TintModifiers.None) { }

		public ModelRenderable(
			IEnumerable<ModelAnimation> models, WPos pos, int zOffset, in WRot camera, float scale, float[] lightAmbientColor, float[] lightDiffuseColor,
			float lightScale, float ambientScale, float specularScale,
			PaletteReference color, PaletteReference normals, PaletteReference shadow,
			float alpha, in float3 tint, TintModifiers tintModifiers)
		{
			this.models = models;
			this.pos = pos;
			this.zOffset = zOffset;
			this.scale = scale;
			this.camera = camera;
			LightScale = lightScale;
			AmbientScale = ambientScale;
			SpecularScale = specularScale;
			this.lightAmbientColor = lightAmbientColor;
			this.lightDiffuseColor = lightDiffuseColor;
			palette = color;
			normalsPalette = normals;
			shadowPalette = shadow;
			this.alpha = alpha;
			this.tint = tint;
			this.tintModifiers = tintModifiers;
		}

		public WPos Pos => pos;
		public PaletteReference Palette => palette;
		public int ZOffset => zOffset;
		public bool IsDecoration => false;

		public float Alpha => alpha;
		public float3 Tint => tint;
		public TintModifiers TintModifiers => tintModifiers;

		public IPalettedRenderable WithPalette(PaletteReference newPalette)
		{
			return new ModelRenderable(
				models, pos, zOffset, camera, scale,
				lightAmbientColor, lightDiffuseColor,	LightScale, AmbientScale, SpecularScale, 
				newPalette, normalsPalette, shadowPalette, alpha, tint, tintModifiers);
		}

		public IRenderable WithZOffset(int newOffset)
		{
			return new ModelRenderable(
				models, pos, newOffset, camera, scale,
				lightAmbientColor, lightDiffuseColor, LightScale, AmbientScale, SpecularScale,
				palette, normalsPalette, shadowPalette, alpha, tint, tintModifiers);
		}

		public IRenderable OffsetBy(in WVec vec)
		{
			return new ModelRenderable(
				models, pos + vec, zOffset, camera, scale,
				lightAmbientColor, lightDiffuseColor, LightScale, AmbientScale, SpecularScale,
				palette, normalsPalette, shadowPalette, alpha, tint, tintModifiers);
		}

		public IRenderable AsDecoration() { return this; }

		public IModifyableRenderable WithAlpha(float newAlpha)
		{
			return new ModelRenderable(
				models, pos, zOffset, camera, scale,
				lightAmbientColor, lightDiffuseColor, LightScale, AmbientScale, SpecularScale,
				palette, normalsPalette, shadowPalette, newAlpha, tint, tintModifiers);
		}

		public IModifyableRenderable WithTint(in float3 newTint, TintModifiers newTintModifiers)
		{
			return new ModelRenderable(
				models, pos, zOffset, camera, scale,
				lightAmbientColor, lightDiffuseColor, LightScale, AmbientScale, SpecularScale,
				palette, normalsPalette, shadowPalette, alpha, newTint, newTintModifiers);
		}

		public IFinalizedRenderable PrepareRender(WorldRenderer wr)
		{
			var model = this;
			var wrsr = Game.Renderer.WorldRgbaSpriteRenderer;
			var t = model.tint;
			if (wr.TerrainLighting != null && (model.tintModifiers & TintModifiers.IgnoreWorldTint) == 0)
				t *= wr.TerrainLighting.NoGlobalLightTintAt(model.pos);

			// Shader interprets negative alpha as a flag to use the tint colour directly instead of multiplying the sprite colour
			var a = model.alpha;
			if ((model.tintModifiers & TintModifiers.ReplaceColor) != 0)
				a *= -1;

			var draw = model.models.Where(v => v.IsVisible);

			var map = wr.World.Map;
			var groundOrientation = map.TerrainOrientation(map.CellContaining(model.pos));

			var viewOffset = Game.Renderer.World3DRenderer.InverseCameraFrontMeterPerWDist * zOffset;

			Game.Renderer.WorldVxlRenderer.CreateRenderInstance(
				wr, model.pos, viewOffset, draw, model.scale,
				LightScale, AmbientScale, SpecularScale,
				t, a,
				model.palette, model.normalsPalette);

			return new FinalizedModelRenderable(wr, this);
		}

		class FinalizedModelRenderable : IFinalizedRenderable
		{
			readonly ModelRenderable model;
			public BlendMode BlendMode => BlendMode.Alpha;

			public FinalizedModelRenderable(WorldRenderer wr, ModelRenderable model)
			{
				this.model = model;
			}

			public void Render(WorldRenderer wr)
			{
			}

			public void RenderDebugGeometry(WorldRenderer wr)
			{
				//var groundPos = model.pos - new WVec(0, 0, wr.World.Map.DistanceAboveTerrain(model.pos).Length);
				//var groundZ = wr.World.Map.Grid.TileSize.Height * (groundPos.Z - model.pos.Z) / 1024f;
				//var pxOrigin = wr.Render3DPosition(model.pos);
				//var shadowOrigin = pxOrigin - groundZ * (new float2(renderProxy.ShadowDirection, 1));

				//// Draw sprite rect
				//var offset = pxOrigin + renderProxy.Sprite.Offset - 0.5f * renderProxy.Sprite.Size;
				//var tl = wr.Viewport.WorldToViewPx(offset.XY);
				//var br = wr.Viewport.WorldToViewPx((offset + renderProxy.Sprite.Size).XY);
				//Game.Renderer.RgbaColorRenderer.DrawRect(tl, br, 1, Color.Red);

				//// Draw transformed shadow sprite rect
				//var c = Color.Purple;
				//var psb = renderProxy.ProjectedShadowBounds;

				//Game.Renderer.RgbaColorRenderer.DrawPolygon(new float2[]
				//{
				//	wr.Viewport.WorldToViewPx(shadowOrigin + psb[1]),
				//	wr.Viewport.WorldToViewPx(shadowOrigin + psb[3]),
				//	wr.Viewport.WorldToViewPx(shadowOrigin + psb[0]),
				//	wr.Viewport.WorldToViewPx(shadowOrigin + psb[2])
				//}, 1, c);

				//// Draw bounding box
				//var draw = model.models.Where(v => v.IsVisible);
				//var scaleTransform = OpenRA.Graphics.Util.ScaleMatrix(model.scale, model.scale, model.scale);
				//var cameraTransform = OpenRA.Graphics.Util.MakeFloatMatrix(model.camera.AsMatrix());

				//foreach (var v in draw)
				//{
				//	var bounds = v.Model.Bounds(v.FrameFunc());
				//	var rotation = OpenRA.Graphics.Util.MakeFloatMatrix(v.RotationFunc().AsMatrix());
				//	var worldTransform = OpenRA.Graphics.Util.MatrixMultiply(scaleTransform, rotation);

				//	var pxPos = pxOrigin + wr.ScreenVectorComponents(v.OffsetFunc());
				//	var screenTransform = OpenRA.Graphics.Util.MatrixMultiply(cameraTransform, worldTransform);
				//	DrawBoundsBox(wr, pxPos, screenTransform, bounds, 1, Color.Yellow);
				//}
			}

			static readonly uint[] CornerXIndex = new uint[] { 0, 0, 0, 0, 3, 3, 3, 3 };
			static readonly uint[] CornerYIndex = new uint[] { 1, 1, 4, 4, 1, 1, 4, 4 };
			static readonly uint[] CornerZIndex = new uint[] { 2, 5, 2, 5, 2, 5, 2, 5 };
			static void DrawBoundsBox(WorldRenderer wr, in float3 pxPos, float[] transform, float[] bounds, float width, Color c)
			{
				var cr = Game.Renderer.RgbaColorRenderer;
				var corners = new float2[8];
				for (var i = 0; i < 8; i++)
				{
					var vec = new[] { bounds[CornerXIndex[i]], bounds[CornerYIndex[i]], bounds[CornerZIndex[i]], 1 };
					var screen = OpenRA.Graphics.Util.MatrixVectorMultiply(transform, vec);
					corners[i] = wr.Viewport.WorldToViewPx(pxPos + new float3(screen[0], screen[1], screen[2]));
				}

				// Front face
				cr.DrawPolygon(new[] { corners[0], corners[1], corners[3], corners[2] }, width, c);

				// Back face
				cr.DrawPolygon(new[] { corners[4], corners[5], corners[7], corners[6] }, width, c);

				// Horizontal edges
				cr.DrawScreenLine(corners[0], corners[4], width, c);
				cr.DrawScreenLine(corners[1], corners[5], width, c);
				cr.DrawScreenLine(corners[2], corners[6], width, c);
				cr.DrawScreenLine(corners[3], corners[7], width, c);
			}

			public Rectangle ScreenBounds(WorldRenderer wr)
			{
				return Screen3DBounds(wr).Bounds;
			}

			(Rectangle Bounds, float2 Z) Screen3DBounds(WorldRenderer wr)
			{
				var pxOrigin = wr.ScreenPosition(model.pos);
				var draw = model.models.Where(v => v.IsVisible);
				var scaleTransform = OpenRA.Graphics.Util.ScaleMatrix(model.scale, model.scale, model.scale);
				var cameraTransform = OpenRA.Graphics.Util.MakeFloatMatrix(model.camera.AsMatrix());

				var minX = float.MaxValue;
				var minY = float.MaxValue;
				var minZ = float.MaxValue;
				var maxX = float.MinValue;
				var maxY = float.MinValue;
				var maxZ = float.MinValue;

				foreach (var v in draw)
				{
					var bounds = v.Model.Bounds(v.FrameFunc());
					var rotation = OpenRA.Graphics.Util.MakeFloatMatrix(v.RotationFunc().AsMatrix());
					var worldTransform = OpenRA.Graphics.Util.MatrixMultiply(scaleTransform, rotation);

					var pxPos = pxOrigin + wr.ScreenVectorComponents(v.OffsetFunc());
					var screenTransform = OpenRA.Graphics.Util.MatrixMultiply(cameraTransform, worldTransform);

					for (var i = 0; i < 8; i++)
					{
						var vec = new float[] { bounds[CornerXIndex[i]], bounds[CornerYIndex[i]], bounds[CornerZIndex[i]], 1 };
						var screen = OpenRA.Graphics.Util.MatrixVectorMultiply(screenTransform, vec);
						minX = Math.Min(minX, pxPos.X + screen[0]);
						minY = Math.Min(minY, pxPos.Y + screen[1]);
						minZ = Math.Min(minZ, pxPos.Z + screen[2]);
						maxX = Math.Max(maxX, pxPos.X + screen[0]);
						maxY = Math.Max(maxY, pxPos.Y + screen[1]);
						maxZ = Math.Max(minZ, pxPos.Z + screen[2]);
					}
				}

				return (Rectangle.FromLTRB((int)minX, (int)minY, (int)maxX, (int)maxY), new float2(minZ, maxZ));
			}
		}
	}
}
