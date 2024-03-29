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
using OpenRA.Effects;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Graphics
{
	public sealed class WorldRenderer : IDisposable
	{
		public static readonly Func<IRenderable, int> RenderableZPositionComparisonKey =
			r => r.Pos.Y + r.Pos.Z + r.ZOffset;

		public readonly Size TileSize;
		public readonly int TileScale;
		public readonly World World;
		public Viewport Viewport { get; }
		public readonly ITerrainLighting TerrainLighting;

		public event Action PaletteInvalidated = null;

		readonly HashSet<Actor> onScreenActors = new HashSet<Actor>();
		readonly HardwarePalette palette = new HardwarePalette();
		readonly Dictionary<string, PaletteReference> palettes = new Dictionary<string, PaletteReference>();
		public readonly IRenderTerrain TerrainRenderer;
		readonly Lazy<DebugVisualizations> debugVis;
		readonly Func<string, PaletteReference> createPaletteReference;
		readonly bool enableDepthBuffer;

		/// <summary>
		/// none blendmode renderables
		/// </summary>
		readonly List<IFinalizedRenderable> preparedNoneBlendRenderables = new List<IFinalizedRenderable>();

		/// <summary>
		/// alpha blend, should ordered by distance from camera
		/// </summary>
		readonly List<IFinalizedRenderable> preparedRenderables = new List<IFinalizedRenderable>();

		readonly List<IFinalizedRenderable> preparedMapAdditonRenderables = new List<IFinalizedRenderable>();

		/// <summary>
		/// other blendmode renderables
		/// </summary>
		readonly List<IFinalizedRenderable> preparedBlendRenderables = new List<IFinalizedRenderable>();

		/// <summary>
		/// screen twist renderables
		/// </summary>
		readonly List<IFinalizedRenderable> preparedTwistRenderables = new List<IFinalizedRenderable>();

		readonly List<IFinalizedRenderable> preparedOverlayRenderables = new List<IFinalizedRenderable>();
		readonly List<IFinalizedRenderable> preparedAnnotationRenderables = new List<IFinalizedRenderable>();

		readonly List<IRenderable> renderablesBuffer = new List<IRenderable>();

		internal WorldRenderer(ModData modData, World world)
		{
			World = world;

			TileSize = World.Map.Grid.TileSize;
			TileScale = World.Map.Grid.Type == MapGridType.RectangularIsometric ? 1448 : 1024;
			Viewport = new Viewport(this, world.Map);

			createPaletteReference = CreatePaletteReference;

			var mapGrid = modData.Manifest.Get<MapGrid>();
			enableDepthBuffer = mapGrid.EnableDepthBuffer;

			foreach (var pal in world.TraitDict.ActorsWithTrait<ILoadsPalettes>())
				pal.Trait.LoadPalettes(this);

			foreach (var p in world.Players)
				UpdatePalettesForPlayer(p.InternalName, p.Color, false);

			palette.Initialize();

			TerrainLighting = world.WorldActor.TraitOrDefault<ITerrainLighting>();
			TerrainRenderer = world.WorldActor.TraitOrDefault<IRenderTerrain>();

			debugVis = Exts.Lazy(() => world.WorldActor.TraitOrDefault<DebugVisualizations>());

			RefreshTextures();

			World.Map.CreateRenderBlocks(this);
			World.Map.InitTerrainBlockMask();
			World.Map.UpdateTerrainBlockTexture(Viewport, true);
			World.Map.DrawMaskWithTSMapInfo(this);
			Console.WriteLine("Updating Mask");
			World.Map.UpdateTerrainBlockMask(Viewport, true);
			Console.WriteLine("Updating Texture");
			World.Map.UpdateTerrainBlockTexture(Viewport, true);
			Console.WriteLine("Completed");
		}

		public void RefreshTextures()
		{
			OrderedSkeleton.BoneAnimTexture?.Dispose();
			OrderedSkeleton.BoneAnimTexture = Game.Renderer.CreateInfoTexture(new Primitives.Size(SkeletonAsset.AnimTextureWidth, SkeletonAsset.AnimTextureHeight));
			World.MeshCache?.RefreshAllTextures();
		}

		public void DisposeTextures()
		{
			OrderedSkeleton.BoneAnimTexture?.Dispose();
			OrderedSkeleton.BoneAnimTexture = null;
			World.MapTextureCache?.DisposeAllTextures();
		}

		public void UpdatePalettesForPlayer(string internalName, Color color, bool replaceExisting)
		{
			foreach (var pal in World.WorldActor.TraitsImplementing<ILoadsPlayerPalettes>())
				pal.LoadPlayerPalettes(this, internalName, color, replaceExisting);
		}

		PaletteReference CreatePaletteReference(string name)
		{
			var pal = palette.GetPalette(name);
			return new PaletteReference(name, palette.GetPaletteIndex(name), pal, palette);
		}

		public PaletteReference Palette(string name)
		{
			// HACK: This is working around the fact that palettes are defined on traits rather than sequences
			// and can be removed once this has been fixed.
			return name == null ? null : palettes.GetOrAdd(name, createPaletteReference);
		}

		public void AddPalette(string name, ImmutablePalette pal, bool allowModifiers = false, bool allowOverwrite = false)
		{
			if (allowOverwrite && palette.Contains(name))
				ReplacePalette(name, pal);
			else
			{
				var oldHeight = palette.Height;
				palette.AddPalette(name, pal, allowModifiers);

				if (oldHeight != palette.Height)
					PaletteInvalidated?.Invoke();
			}
		}

		public void ReplacePalette(string name, IPalette pal)
		{
			palette.ReplacePalette(name, pal);

			// Update cached PlayerReference if one exists
			if (palettes.ContainsKey(name))
				palettes[name].Palette = pal;
		}

		public void SetPaletteColorShift(string name, float hueOffset, float satOffset, float minHue, float maxHue)
		{
			palette.SetColorShift(name, hueOffset, satOffset, minHue, maxHue);
		}

		// PERF: Avoid LINQ.
		long lastUpdate = 0;
		void GenerateRenderables()
		{
			if (Math.Abs(Game.RunTime - lastUpdate) > 33)
			{
				OrderedSkeleton.AnimTransformDataIndex = 0;

				foreach (var a in onScreenActors)
				{
					a.UpdateSkeletonDrawInfo();
				}

				World.SkeletonCache.UpdateAllSkeletonTexture();

				lastUpdate = Game.RunTime;
			}

			foreach (var actor in onScreenActors)
				renderablesBuffer.AddRange(actor.Render(this));

			renderablesBuffer.AddRange(World.WorldActor.Render(this));

			if (World.RenderPlayer != null)
				renderablesBuffer.AddRange(World.RenderPlayer.PlayerActor.Render(this));

			if (World.OrderGenerator != null)
				renderablesBuffer.AddRange(World.OrderGenerator.Render(this, World));

			// Unpartitioned effects
			foreach (var e in World.UnpartitionedEffects)
				renderablesBuffer.AddRange(e.Render(this));

			// Partitioned, currently on-screen effects
			foreach (var e in World.ScreenMap.RenderableEffectsInBox(Viewport.TopLeft, Viewport.BottomRight))
				renderablesBuffer.AddRange(e.Render(this));

			// Renderables must be ordered using a stable sorting algorithm to avoid flickering artefacts
			// don't sort it now, we only need to sort the alpha renderable
			foreach (var renderable in renderablesBuffer.OrderBy(RenderableZPositionComparisonKey))
			{
				if (renderable is SpriteRenderable && (renderable as SpriteRenderable).Sprite.SpriteMeshType == SpriteMeshType.TerrainCovering)
					preparedMapAdditonRenderables.Add(renderable.PrepareRender(this));
				else
				{
					if (renderable.BlendMode == BlendMode.None)
						preparedNoneBlendRenderables.Add(renderable.PrepareRender(this));
					else if (renderable.BlendMode == BlendMode.Alpha)
						preparedRenderables.Add(renderable.PrepareRender(this)); // add alpha renderable to sort
					else if (renderable.BlendMode == BlendMode.Twist)
						preparedTwistRenderables.Add(renderable.PrepareRender(this));
					else
						preparedBlendRenderables.Add(renderable.PrepareRender(this));
				}
			}

			// PERF: Reuse collection to avoid allocations.
			renderablesBuffer.Clear();
		}

		// PERF: Avoid LINQ.
		void GenerateOverlayRenderables()
		{
			World.ApplyToActorsWithTrait<IRenderAboveShroud>((actor, trait) =>
			{
				if (!actor.IsInWorld || actor.Disposed || (trait.SpatiallyPartitionable && !onScreenActors.Contains(actor)))
					return;

				foreach (var renderable in trait.RenderAboveShroud(actor, this))
					preparedOverlayRenderables.Add(renderable.PrepareRender(this));
			});

			foreach (var a in World.Selection.Actors)
			{
				if (!a.IsInWorld || a.Disposed)
					continue;

				foreach (var t in a.TraitsImplementing<IRenderAboveShroudWhenSelected>())
				{
					if (t.SpatiallyPartitionable && !onScreenActors.Contains(a))
						continue;

					foreach (var renderable in t.RenderAboveShroud(a, this))
						preparedOverlayRenderables.Add(renderable.PrepareRender(this));
				}
			}

			foreach (var e in World.Effects)
			{
				if (!(e is IEffectAboveShroud ea))
					continue;

				foreach (var renderable in ea.RenderAboveShroud(this))
					preparedOverlayRenderables.Add(renderable.PrepareRender(this));
			}

			if (World.OrderGenerator != null)
				foreach (var renderable in World.OrderGenerator.RenderAboveShroud(this, World))
					preparedOverlayRenderables.Add(renderable.PrepareRender(this));
		}

		// PERF: Avoid LINQ.
		void GenerateAnnotationRenderables()
		{
			World.ApplyToActorsWithTrait<IRenderAnnotations>((actor, trait) =>
			{
				if (!actor.IsInWorld || actor.Disposed || (trait.SpatiallyPartitionable && !onScreenActors.Contains(actor)))
					return;

				foreach (var renderAnnotation in trait.RenderAnnotations(actor, this))
					preparedAnnotationRenderables.Add(renderAnnotation.PrepareRender(this));
			});

			foreach (var a in World.Selection.Actors)
			{
				if (!a.IsInWorld || a.Disposed)
					continue;

				foreach (var t in a.TraitsImplementing<IRenderAnnotationsWhenSelected>())
				{
					if (t.SpatiallyPartitionable && !onScreenActors.Contains(a))
						continue;

					foreach (var renderAnnotation in t.RenderAnnotations(a, this))
						preparedAnnotationRenderables.Add(renderAnnotation.PrepareRender(this));
				}
			}

			foreach (var e in World.Effects)
			{
				if (!(e is IEffectAnnotation ea))
					continue;

				foreach (var renderAnnotation in ea.RenderAnnotation(this))
					preparedAnnotationRenderables.Add(renderAnnotation.PrepareRender(this));
			}

			if (World.OrderGenerator != null)
				foreach (var renderAnnotation in World.OrderGenerator.RenderAnnotations(this, World))
					preparedAnnotationRenderables.Add(renderAnnotation.PrepareRender(this));
		}

		public void PrepareRenderables()
		{
			if (World.WorldActor.Disposed)
				return;

			RefreshPalette();

			// PERF: Reuse collection to avoid allocations.
			onScreenActors.UnionWith(World.ScreenMap.RenderableActorsInBox(Viewport.TopLeft, Viewport.BottomRight));

			GenerateRenderables();
			GenerateOverlayRenderables();
			GenerateAnnotationRenderables();

			onScreenActors.Clear();
		}

		IShader[] shadersNeedTerrainLight;
		public void UpdateTerrainLightToShader()
		{
			var map = World.Map;
			var cells = Viewport.VisibleCellsInsideBounds;
			if (shadersNeedTerrainLight == null)
			{
				shadersNeedTerrainLight = new IShader[3];
				shadersNeedTerrainLight[0] = Game.Renderer.MapRenderer.Shader;
				shadersNeedTerrainLight[1] = Game.Renderer.WorldSpriteRenderer.Shader;
				shadersNeedTerrainLight[2] = TerrainRenderBlock.TerrainShader;
			}

			var tlcell = cells.CandidateMapCoords.TopLeft.ToCPos(map);
			var brcell = cells.CandidateMapCoords.BottomRight.ToCPos(map);
			TerrainLighting.LightSourcesToMapShader(map.CenterOfCell(tlcell), map.CenterOfCell(brcell), shadersNeedTerrainLight);
		}

		public void Draw()
		{
			if (World.WorldActor.Disposed)
				return;

			Game.Renderer.TwistTime = (float)(Game.LocalTick % 200) / 200;
			if (Game.Renderer.TwistTime > 0.5f)
			{
				Game.Renderer.TwistTime = 2.0f - Game.Renderer.TwistTime * 2.0f;
			}
			else
			{
				Game.Renderer.TwistTime = Game.Renderer.TwistTime * 2.0f;
			}

			Game.Renderer.TwistMul = float2.Lerp(0.25f, 0.75f, Game.Renderer.TwistTime) * 0.03f;

			Game.Renderer.TwistScreenSize = new float2(Game.Renderer.NativeResolution.Width + 1f, Game.Renderer.NativeResolution.Height + 1f) / 40f;

			debugVis.Value?.UpdateDepthBuffer();

			// var bounds = Viewport.GetScissorBounds(World.Type != WorldType.Editor);
			// Game.Renderer.EnableScissor(bounds);
			Game.Renderer.EnableDepthWrite(true);
			Game.Renderer.EnableDepthBuffer();

			World.Map.SetTerrainShaderParams(Game.Renderer.World3DRenderer, false);
			UpdateTerrainLightToShader();

			Game.Renderer.SetFaceCull(FaceCullFunc.Back);

			TerrainRenderer?.RenderTerrain(this, Viewport);
			Game.Renderer.MapRenderer.Flush(BlendMode.Alpha);

			for (var i = 0; i < preparedMapAdditonRenderables.Count; i++)
			{
				preparedMapAdditonRenderables[i].Render(this);
			}

			Game.Renderer.MapRenderer.Flush(BlendMode.Alpha);
			Game.Renderer.EnableDepthWrite(true);

			Game.Renderer.SetFaceCull(FaceCullFunc.None);

			Game.Renderer.Flush();

			// none blend
			for (var i = 0; i < preparedNoneBlendRenderables.Count; i++)
			{
				preparedNoneBlendRenderables[i].Render(this);
			}

			Game.Renderer.WorldSpriteRenderer.Flush(BlendMode.None);

			Game.Renderer.Draw3DMeshesInstance(this, false, MeshDrawType.Actor);

			// draw twist mesh
			Game.Renderer.Draw3DMeshesInstance(this, false, MeshDrawType.Twist);

			Game.Renderer.Context.DisableCullFace();

			// alpha blend
			for (var i = 0; i < preparedRenderables.Count; i++)
			{
				preparedRenderables[i].Render(this);
			}

			Game.Renderer.Flush();

			// disable depth write to render other blend mode
			Game.Renderer.EnableDepthWrite(false);

			// render effect meshes
			Game.Renderer.Draw3DMeshesInstance(this, false, MeshDrawType.Effect);

			// other blend
			for (var i = 0; i < preparedBlendRenderables.Count; i++)
			{
				preparedBlendRenderables[i].Render(this);
			}

			Game.Renderer.Flush();

			// twist
			Game.Renderer.WorldSpriteRenderer.Shader.SetBool("RemappingTwist", true);

			for (var i = 0; i < preparedTwistRenderables.Count; i++)
			{
				preparedTwistRenderables[i].Render(this);
			}

			Game.Renderer.WorldSpriteRenderer.Flush(BlendMode.Additive);
			Game.Renderer.Flush();
			Game.Renderer.WorldSpriteRenderer.Shader.SetBool("RemappingTwist", false);
			// end twist

			Game.Renderer.DisableDepthTest();

			World.ApplyToActorsWithTrait<IRenderAboveWorld>((actor, trait) =>
			{
				if (actor.IsInWorld && !actor.Disposed)
					trait.RenderAboveWorld(actor, this);
			});

			Game.Renderer.Flush();

			Game.Renderer.EnableDepthBuffer();
			Game.Renderer.EnableDepthWrite(true);
			if (World.Type != WorldType.Editor)
			{
				Game.Renderer.MapRenderer.SetRenderShroud(true);
				Game.Renderer.SetFaceCull(FaceCullFunc.Back);

				World.ApplyToActorsWithTrait<IRenderShroud>((actor, trait) => trait.RenderShroud(this));

				Game.Renderer.SetFaceCull(FaceCullFunc.None);
				Game.Renderer.MapRenderer.SetRenderShroud(false);
			}

			// Game.Renderer.DisableScissor();

			// HACK: Keep old grouping behaviour
			var groupedOverlayRenderables = preparedOverlayRenderables.GroupBy(prs => prs.GetType());
			foreach (var g in groupedOverlayRenderables)
				foreach (var r in g)
					r.Render(this);

			Game.Renderer.Flush();
		}

		public void DrawAnnotations()
		{
			Game.Renderer.EnableAntialiasingFilter();
			for (var i = 0; i < preparedAnnotationRenderables.Count; i++)
				preparedAnnotationRenderables[i].Render(this);
			Game.Renderer.DisableAntialiasingFilter();

			// Engine debugging overlays
			if (debugVis.Value != null && debugVis.Value.RenderGeometry)
			{
				for (var i = 0; i < preparedNoneBlendRenderables.Count; i++)
					preparedNoneBlendRenderables[i].RenderDebugGeometry(this);
				for (var i = 0; i < preparedRenderables.Count; i++)
					preparedRenderables[i].RenderDebugGeometry(this);
				for (var i = 0; i < preparedBlendRenderables.Count; i++)
					preparedBlendRenderables[i].RenderDebugGeometry(this);
				for (var i = 0; i < preparedTwistRenderables.Count; i++)
					preparedTwistRenderables[i].RenderDebugGeometry(this);

				for (var i = 0; i < preparedOverlayRenderables.Count; i++)
					preparedOverlayRenderables[i].RenderDebugGeometry(this);

				for (var i = 0; i < preparedAnnotationRenderables.Count; i++)
					preparedAnnotationRenderables[i].RenderDebugGeometry(this);
			}

			if (debugVis.Value != null && debugVis.Value.ScreenMap)
			{
				foreach (var r in World.ScreenMap.RenderBounds(World.RenderPlayer))
				{
					var tl = Viewport.WorldToViewPx(new float2(r.Left, r.Top));
					var br = Viewport.WorldToViewPx(new float2(r.Right, r.Bottom));
					Game.Renderer.RgbaColorRenderer.DrawRect(tl, br, 1, Color.MediumSpringGreen);
				}

				foreach (var b in World.ScreenMap.MouseBounds(World.RenderPlayer))
				{
					var points = new float2[b.Vertices.Length];
					for (var index = 0; index < b.Vertices.Length; index++)
					{
						var vertex = b.Vertices[index];
						points[index] = Viewport.WorldToViewPx(vertex).ToFloat2();
					}

					Game.Renderer.RgbaColorRenderer.DrawPolygon(points, 1, Color.OrangeRed);
				}
			}

			Game.Renderer.Flush();

			preparedNoneBlendRenderables.Clear();
			preparedRenderables.Clear();
			preparedMapAdditonRenderables.Clear();
			preparedBlendRenderables.Clear();
			preparedTwistRenderables.Clear();

			preparedOverlayRenderables.Clear();
			preparedAnnotationRenderables.Clear();
		}

		public void RefreshPalette()
		{
			palette.ApplyModifiers(World.WorldActor.TraitsImplementing<IPaletteModifier>());
			Game.Renderer.SetPalette(palette);
		}

		// Conversion between world and screen coordinates
		public float2 ScreenPosition(WPos pos)
		{
			return new float2((float)TileSize.Width * pos.X / TileScale, (float)TileSize.Height * (pos.Y - 1.73205f * pos.Z) / TileScale);
		}

		public float3 Render3DPosition(WPos pos)
		{
			return new float3(-(float)pos.X / World3DCoordinate.WDistPerMeter, (float)pos.Y / World3DCoordinate.WDistPerMeter, (float)pos.Z / World3DCoordinate.WDistPerMeter);
		}

		public float3 Screen3DPosition(WPos pos)
		{
			// The projection from world coordinates to screen coordinates has
			// a non-obvious relationship between the y and z coordinates:
			// * A flat surface with constant y (e.g. a vertical wall) in world coordinates
			//   transforms into a flat surface with constant z (depth) in screen coordinates.
			// * Increasing the world y coordinate increases screen y and z coordinates equally.
			// * Increases the world z coordinate decreases screen y but doesn't change screen z.
			var z = pos.Y * (float)TileSize.Height / TileScale;
			return new float3((float)TileSize.Width * pos.X / TileScale, (float)TileSize.Height * (pos.Y - 1.73205f * pos.Z) / TileScale, z);
		}

		public int2 ScreenPxPosition(WPos pos)
		{
			// Round to nearest pixel
			var px = ScreenPosition(pos);
			return new int2((int)Math.Round(px.X), (int)Math.Round(px.Y));
		}

		public float3 Screen3DPxPosition(WPos pos)
		{
			// Round to nearest pixel
			var px = Screen3DPosition(pos);
			return new float3((float)Math.Round(px.X), (float)Math.Round(px.Y), px.Z);
		}

		// For scaling vectors to pixel sizes in the model renderer
		public float3 ScreenVectorComponents(in WVec vec)
		{
			return new float3(
				(float)TileSize.Width * vec.X / TileScale,
				(float)TileSize.Height * (vec.Y - vec.Z) / TileScale,
				(float)TileSize.Height * vec.Z / TileScale);
		}

		public int2 ScreenPxOffset(in WVec vec)
		{
			// Round to nearest pixel
			var xyz = ScreenVectorComponents(vec);
			return new int2((int)Math.Round(xyz.X), (int)Math.Round(xyz.Y));
		}

		public float3 RenderVectorComponents(in WVec vec)
		{
			return new float3(-(float)vec.X / World3DCoordinate.WDistPerMeter, (float)vec.Y / World3DCoordinate.WDistPerMeter, (float)vec.Z / World3DCoordinate.WDistPerMeter);
		}

		// For scaling vectors to pixel sizes in the model renderer
		public float[] RenderVector(in WVec vec)
		{
			var xyz = RenderVectorComponents(vec);
			return new[] { xyz.X, xyz.Y, xyz.Z, 1f };
		}

		/// <summary>
		/// Returns a position in the world that is projected to the given screen position.
		/// There are many possible world positions, and the returned value chooses the value with no elevation.
		/// </summary>
		public WPos ProjectedPosition(int2 screenPx)
		{
			return new WPos(TileScale * screenPx.X / TileSize.Width, TileScale * screenPx.Y / TileSize.Height, 0);
		}

		public float3 ProjectedPositionFloat(float2 screenPx)
		{
			return new float3(TileScale * screenPx.X / TileSize.Width, TileScale * screenPx.Y / TileSize.Height, 0);
		}

		public void Dispose()
		{
			DisposeTextures();
			// HACK: Disposing the world from here violates ownership
			// but the WorldRenderer lifetime matches the disposal
			// behavior we want for the world, and the root object setup
			// is so horrible that doing it properly would be a giant mess.

			World.Map.DisposeRenderBlocks();
			World.Dispose();

			palette.Dispose();
		}
	}
}
