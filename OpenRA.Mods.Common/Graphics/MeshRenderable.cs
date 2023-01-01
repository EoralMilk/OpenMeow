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
using OpenRA.Mods.Common.Traits.Trait3D;
using OpenRA.Primitives;

namespace OpenRA.Mods.Common.Graphics
{
	public class MeshRenderable : IRenderable, IModifyableRenderable
	{
		readonly IEnumerable<MeshInstance> meshes;
		readonly WPos pos;
		readonly int zOffset;
		readonly bool twist;
		readonly Color remap;
		readonly float scale;
		readonly float alpha;
		readonly float3 tint;
		readonly TintModifiers tintModifiers;
		public readonly RenderMeshes RenderMeshes;
		public BlendMode BlendMode => BlendMode.None;

		public MeshRenderable(
			IEnumerable<MeshInstance> meshes, WPos pos, int zOffset, in Color remap, float scale, RenderMeshes renderMeshes, bool twist)
			: this(meshes, pos, zOffset, remap, scale, 1f, float3.Ones, TintModifiers.None, renderMeshes, twist)
		{ }

		public MeshRenderable(
			IEnumerable<MeshInstance> meshes, WPos pos, int zOffset, in Color remap, float scale,
			float alpha, in float3 tint, TintModifiers tintModifiers, RenderMeshes renderMeshes, bool twist)
		{
			this.meshes = meshes;
			this.pos = pos;
			this.zOffset = zOffset;
			this.remap = remap;
			this.scale = scale;
			this.alpha = alpha;
			this.tint = tint;
			this.tintModifiers = tintModifiers;
			this.RenderMeshes = renderMeshes;
			this.twist = twist;
		}

		public WPos Pos => pos;
		public int ZOffset => zOffset;
		public bool IsDecoration => false;

		public float Alpha => alpha;
		public float3 Tint => tint;
		public TintModifiers TintModifiers => tintModifiers;
		public IRenderable WithZOffset(int newOffset)
		{
			return new MeshRenderable(meshes, pos, newOffset, remap, scale, alpha, tint, tintModifiers, RenderMeshes, twist);
		}

		public IRenderable OffsetBy(in WVec vec)
		{
			return new MeshRenderable(meshes, pos + vec, zOffset, remap, scale, alpha, tint, tintModifiers, RenderMeshes, twist);
		}

		public IRenderable AsDecoration() { return this; }

		public IModifyableRenderable WithAlpha(float newAlpha)
		{
			return new MeshRenderable(meshes, pos, zOffset, remap, scale, newAlpha, tint, tintModifiers, RenderMeshes, twist);
		}

		public IModifyableRenderable WithTint(in float3 newTint, TintModifiers newTintModifiers)
		{
			return new MeshRenderable(meshes, pos, zOffset, remap, scale, alpha, newTint, newTintModifiers, RenderMeshes, twist);
		}

		public IFinalizedRenderable PrepareRender(WorldRenderer wr)
		{
			var renderable = this;
			// RenderMeshes.CallWhenInSceen();
			var t = renderable.tint;
			if (wr.TerrainLighting != null && (renderable.tintModifiers & TintModifiers.IgnoreWorldTint) == 0)
				t *= wr.TerrainLighting.NoGlobalLightTintAt(renderable.pos);

			// Shader interprets negative alpha as a flag to use the tint colour directly instead of multiplying the sprite colour
			var a = renderable.alpha;
			if ((renderable.tintModifiers & TintModifiers.ReplaceColor) != 0)
				a *= -1;

			var draw = renderable.meshes.Where(v => v.IsVisible());

			var map = wr.World.Map;

			Game.Renderer.World3DRenderer.AddMeshInstancesToDraw(zOffset, draw, renderable.scale, t, a, remap, twist);

			return new FinalizedMeshRenderable(wr, this);
		}

		class FinalizedMeshRenderable : IFinalizedRenderable
		{
			readonly MeshRenderable mesh;
			public BlendMode BlendMode => BlendMode.Alpha;

			public FinalizedMeshRenderable(WorldRenderer wr, MeshRenderable mesh)
			{
				this.mesh = mesh;
			}

			public void Render(WorldRenderer wr)
			{
				// var renderable = mesh;
				// mesh.RenderMeshes.CallWhenInSceen();
				// var t = renderable.tint;
				// if (wr.TerrainLighting != null && (renderable.tintModifiers & TintModifiers.IgnoreWorldTint) == 0)
				// 	t *= wr.TerrainLighting.TintAt(renderable.pos);

				// // Shader interprets negative alpha as a flag to use the tint colour directly instead of multiplying the sprite colour
				// var a = renderable.alpha;
				// if ((renderable.tintModifiers & TintModifiers.ReplaceColor) != 0)
				// 	a *= -1;

				// var draw = renderable.meshes.Where(v => v.IsVisible());

				// var map = wr.World.Map;

				// Game.Renderer.World3DRenderer.AddInstancesToDraw(mesh.zOffset, draw, renderable.scale, t, a, mesh.remap);
			}

			public void RenderDebugGeometry(WorldRenderer wr)
			{

			}

			static readonly uint[] CornerXIndex = new uint[] { 0, 0, 0, 0, 3, 3, 3, 3 };
			static readonly uint[] CornerYIndex = new uint[] { 1, 1, 4, 4, 1, 1, 4, 4 };
			static readonly uint[] CornerZIndex = new uint[] { 2, 5, 2, 5, 2, 5, 2, 5 };

			public Rectangle ScreenBounds(WorldRenderer wr)
			{
				return Screen3DBounds(wr).Bounds;
			}

			(Rectangle Bounds, float2 Z) Screen3DBounds(WorldRenderer wr)
			{
				//var pxOrigin = wr.ScreenPosition(mesh.pos);
				//var draw = mesh.meshes.Where(v => v.IsVisible);
				var minX = float.MaxValue;
				var minY = float.MaxValue;
				var minZ = float.MaxValue;
				var maxX = float.MinValue;
				var maxY = float.MinValue;
				var maxZ = float.MinValue;

				return (Rectangle.FromLTRB((int)minX, (int)minY, (int)maxX, (int)maxY), new float2(minZ, maxZ));
			}
		}
	}
}
