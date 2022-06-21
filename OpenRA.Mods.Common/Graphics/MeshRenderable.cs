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
	public class MeshRenderable : IRenderable, IModifyableRenderable
	{
		readonly IEnumerable<MeshInstance> meshes;
		readonly WPos pos;
		readonly int zOffset;

		readonly Color remap;
		readonly float scale;
		readonly float alpha;
		readonly float3 tint;
		readonly TintModifiers tintModifiers;

		public MeshRenderable(
			IEnumerable<MeshInstance> meshes, WPos pos, int zOffset, in Color remap, float scale)
			: this(meshes, pos, zOffset, remap, scale, 1f, float3.Ones, TintModifiers.None)
		{ }

		public MeshRenderable(
			IEnumerable<MeshInstance> meshes, WPos pos, int zOffset, in Color remap, float scale,
			float alpha, in float3 tint, TintModifiers tintModifiers)
		{
			this.meshes = meshes;
			this.pos = pos;
			this.zOffset = zOffset;
			this.remap = remap;
			this.scale = scale;
			this.alpha = alpha;
			this.tint = tint;
			this.tintModifiers = tintModifiers;
		}

		public WPos Pos => pos;
		public int ZOffset => zOffset;
		public bool IsDecoration => false;

		public float Alpha => alpha;
		public float3 Tint => tint;
		public TintModifiers TintModifiers => tintModifiers;
		public IRenderable WithZOffset(int newOffset)
		{
			return new MeshRenderable(meshes, pos, newOffset, remap, scale, alpha, tint, tintModifiers);
		}

		public IRenderable OffsetBy(in WVec vec)
		{
			return new MeshRenderable(meshes, pos + vec, zOffset, remap, scale, alpha, tint, tintModifiers);
		}

		public IRenderable AsDecoration() { return this; }

		public IModifyableRenderable WithAlpha(float newAlpha)
		{
			return new MeshRenderable(meshes, pos, zOffset, remap, scale, newAlpha, tint, tintModifiers);
		}

		public IModifyableRenderable WithTint(in float3 newTint, TintModifiers newTintModifiers)
		{
			return new MeshRenderable(meshes, pos, zOffset, remap, scale, alpha, newTint, newTintModifiers);
		}

		public IFinalizedRenderable PrepareRender(WorldRenderer wr)
		{
			var renderable = this;

			var t = renderable.tint;
			if (wr.TerrainLighting != null && (renderable.tintModifiers & TintModifiers.IgnoreWorldTint) == 0)
				t *= wr.TerrainLighting.TintAt(renderable.pos);

			// Shader interprets negative alpha as a flag to use the tint colour directly instead of multiplying the sprite colour
			var a = renderable.alpha;
			if ((renderable.tintModifiers & TintModifiers.ReplaceColor) != 0)
				a *= -1;

			var draw = renderable.meshes.Where(v => v.IsVisible());

			var map = wr.World.Map;

			Game.Renderer.World3DRenderer.AddInstancesToDraw(renderable.pos, zOffset, draw, renderable.scale, t, a, remap);

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
