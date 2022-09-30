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
using System.Threading;
using OpenRA.FileFormats;
using OpenRA.Graphics;
using OpenRA.Primitives;
using OpenRA.Support;

namespace OpenRA
{
	public sealed class Renderer : IDisposable
	{
		enum RenderType { None, World, UI }

		public World3DRenderer World3DRenderer { get; private set; }
		public SpriteRenderer WorldSpriteRenderer { get; private set; }
		public RgbaSpriteRenderer WorldRgbaSpriteRenderer { get; private set; }
		public RgbaColorRenderer WorldRgbaColorRenderer { get; private set; }
		public MapRenderer MapRenderer { get; private set; }
		public VxlRenderer WorldVxlRenderer { get; private set; }
		public RgbaColorRenderer RgbaColorRenderer { get; private set; }
		public SpriteRenderer SpriteRenderer { get; private set; }
		public RgbaSpriteRenderer RgbaSpriteRenderer { get; private set; }
		public ScreenRenderer ScreenRenderer { get; private set; }

		public bool WindowHasInputFocus => Window.HasInputFocus;
		public bool WindowIsSuspended => Window.IsSuspended;

		public IReadOnlyDictionary<string, SpriteFont> Fonts;

		internal IPlatformWindow Window { get; }
		internal IGraphicsContext Context { get; }

		internal int SheetSize { get; }
		internal int TempBufferSize { get; }

		readonly IVertexBuffer<Vertex> tempBuffer;
		readonly IVertexBuffer<MapVertex> tempMapBuffer;

		readonly Stack<Rectangle> scissorState = new Stack<Rectangle>();

		IFrameBuffer screenBuffer;
		Sprite screenSprite;

		Size worldBufferSize;
		IFrameBuffer worldBuffer;
		public IFrameBuffer WorldBuffer => worldBuffer;
		ITexture worldTexture;

		IFrameBuffer worldShadowBuffer;
		ITexture worldShadowDepthTexture;

		int worldDownscaleFactor = 1;
		Size lastMaximumViewportSize;
		Size lastWorldViewportSize;

		public Size WorldFrameBufferSize => worldTexture.Size;
		public int WorldDownscaleFactor => worldDownscaleFactor;

		SheetBuilder fontSheetBuilder;
		readonly IPlatform platform;

		float depthMargin;

		Size lastBufferSize = new Size(-1, -1);

		Rectangle lastWorldViewport = Rectangle.Empty;
		ITexture currentPaletteTexture;
		IBatchRenderer currentBatchRenderer;
		RenderType renderType = RenderType.None;

		Dictionary<string, IShader> orderedMeshShaders;
		Dictionary<string, IOrderedMesh> orderedMeshes;

		public readonly int MaxVerticesPerMesh = 12;
		public Renderer(IPlatform platform, GraphicSettings graphicSettings)
		{
			this.platform = platform;
			var resolution = GetResolution(graphicSettings);

			orderedMeshShaders = new Dictionary<string, IShader>();
			orderedMeshes = new Dictionary<string, IOrderedMesh>();

			Window = platform.CreateWindow(new Size(resolution.Width, resolution.Height),
				graphicSettings.Mode, graphicSettings.UIScale, graphicSettings.BatchSize,
				graphicSettings.VideoDisplay, graphicSettings.GLProfile, !graphicSettings.DisableLegacyGL);

			Context = Window.Context;

			TempBufferSize = graphicSettings.BatchSize;
			SheetSize = graphicSettings.SheetSize;

			WorldSpriteRenderer = new SpriteRenderer(this, Context.CreateUnsharedShader<CombinedShaderBindings>());
			WorldRgbaSpriteRenderer = new RgbaSpriteRenderer(WorldSpriteRenderer);
			WorldRgbaColorRenderer = new RgbaColorRenderer(WorldSpriteRenderer);
			MapRenderer = new MapRenderer(this, Context.CreateUnsharedShader<MapShaderBindings>());
			WorldVxlRenderer = new VxlRenderer(this);
			SpriteRenderer = new SpriteRenderer(this, Context.CreateUnsharedShader<CombinedShaderBindings>());
			RgbaSpriteRenderer = new RgbaSpriteRenderer(SpriteRenderer);
			RgbaColorRenderer = new RgbaColorRenderer(SpriteRenderer);
			ScreenRenderer = new ScreenRenderer(this, Context.CreateUnsharedShader<ScreenShaderBindings>());
			tempBuffer = Context.CreateVertexBuffer<Vertex>(TempBufferSize);
			tempMapBuffer = Context.CreateVertexBuffer<MapVertex>(TempBufferSize);
		}

		static Size GetResolution(GraphicSettings graphicsSettings)
		{
			var size = (graphicsSettings.Mode == WindowMode.Windowed)
				? graphicsSettings.WindowedSize
				: graphicsSettings.FullscreenSize;
			return new Size(size.X, size.Y);
		}

		public void SetUIScale(float scale)
		{
			Window.SetScaleModifier(scale);
		}

		public void InitializeFonts(ModData modData)
		{
			if (Fonts != null)
				foreach (var font in Fonts.Values)
					font.Dispose();
			using (new PerfTimer("SpriteFonts"))
			{
				fontSheetBuilder?.Dispose();
				fontSheetBuilder = new SheetBuilder(SheetType.BGRA, 512);
				Fonts = modData.Manifest.Get<Fonts>().FontList.ToDictionary(x => x.Key,
					x => new SpriteFont(x.Value.Font, modData.DefaultFileSystem.Open(x.Value.Font).ReadAllBytes(),
										x.Value.Size, x.Value.Ascender, Window.EffectiveWindowScale, fontSheetBuilder));
			}

			Window.OnWindowScaleChanged += (oldNative, oldEffective, newNative, newEffective) =>
			{
				Game.RunAfterTick(() =>
				{
					// Recalculate downscaling factor for the new window scale
					SetMaximumViewportSize(lastMaximumViewportSize);

					ChromeProvider.SetDPIScale(newEffective);

					foreach (var f in Fonts)
						f.Value.SetScale(newEffective);
				});
			};
		}

		public void InitializeDepthBuffer(MapGrid mapGrid)
		{
			// The depth buffer needs to be initialized with enough range to cover:
			//  - the height of the screen
			//  - the z-offset of tiles from MaxTerrainHeight below the bottom of the screen (pushed into view)
			//  - additional z-offset from actors on top of MaxTerrainHeight terrain
			//  - a small margin so that tiles rendered partially above the top edge of the screen aren't pushed behind the clip plane
			// We need an offset of mapGrid.MaximumTerrainHeight * mapGrid.TileSize.Height / 2 to cover the terrain height
			// and choose to use mapGrid.MaximumTerrainHeight * mapGrid.TileSize.Height / 4 for each of the actor and top-edge cases
			depthMargin = mapGrid == null || !mapGrid.EnableDepthBuffer ? 0 : mapGrid.TileSize.Height * mapGrid.MaximumTerrainHeight;
		}

		public void InitializeWorld3DRenderer(MapGrid mapGrid)
		{
			World3DRenderer = new World3DRenderer(this, mapGrid);
			WorldRgbaColorRenderer.UpdateWorldRenderOffset(World3DRenderer);
		}

		float2 screenUISize;
		void BeginFrame()
		{
			Context.Clear();

			var surfaceSize = Window.SurfaceSize;
			var surfaceBufferSize = surfaceSize.NextPowerOf2();

			if (screenSprite == null || screenSprite.Sheet.Size != surfaceBufferSize)
			{
				Console.WriteLine("ScreenBuffer Refresh");
				screenBuffer?.Dispose();

				// Render the screen into a frame buffer to simplify reading back screenshots
				screenBuffer = Context.CreateFrameBuffer(surfaceBufferSize, Color.FromArgb(0x00, 0, 0, 0));
			}

			if (screenSprite == null || surfaceSize.Width != screenSprite.Bounds.Width || -surfaceSize.Height != screenSprite.Bounds.Height)
			{
				Console.WriteLine("screenSheet Refresh");
				var screenSheet = new Sheet(SheetType.BGRA, screenBuffer.Texture);

				// Flip sprite in Y to match OpenGL's bottom-left origin
				var screenBounds = Rectangle.FromLTRB(0, surfaceSize.Height, surfaceSize.Width, 0);

				screenUISize = new float2(screenBounds.Size.Width, screenBounds.Size.Height);
				screenSprite = new Sprite(screenSheet, screenBounds, TextureChannel.RGBA);
			}

			// In HiDPI windows we follow Apple's convention of defining window coordinates as for standard resolution windows
			// but to have a higher resolution backing surface with more than 1 texture pixel per viewport pixel.
			// We must convert the surface buffer size to a viewport size - in general this is NOT just the window size
			// rounded to the next power of two, as the NextPowerOf2 calculation is done in the surface pixel coordinates
			var scale = Window.EffectiveWindowScale;
			var bufferSize = new Size((int)(surfaceBufferSize.Width / scale), (int)(surfaceBufferSize.Height / scale));
			if (lastBufferSize != bufferSize)
			{
				SpriteRenderer.SetViewportParams(bufferSize, 1, 0f, int2.Zero);
				lastBufferSize = bufferSize;
			}
		}

		public void SetMaximumViewportSize(Size size)
		{
			// Aim to render the world into a framebuffer at 1:1 scaling which is then up/downscaled using a custom
			// filter to provide crisp scaling and avoid rendering glitches when the depth buffer is used and samples don't match.
			// This approach does not scale well to large sizes, first saturating GPU fill rate and then crashing when
			// reaching the framebuffer size limits (typically 16k). We therefore clamp the maximum framebuffer size to
			// twice the window surface size, which strikes a reasonable balance between rendering quality and performance.
			// Mods that use the depth buffer must instead limit their artwork resolution or maximum zoom-out levels.
			if (depthMargin == 0)
			{
				var surfaceSize = Window.SurfaceSize;
				worldBufferSize = new Size(Math.Min(size.Width, 2 * surfaceSize.Width), Math.Min(size.Height, 2 * surfaceSize.Height)).NextPowerOf2();
			}
			else
				worldBufferSize = size.NextPowerOf2();

			if (worldTexture == null || worldTexture.Size != worldBufferSize)
			{
				worldBuffer?.Dispose();
				worldShadowBuffer?.Dispose();

				worldShadowBuffer = Context.CreateDepthFrameBuffer(new Size(2048, 2048));
				worldShadowDepthTexture = worldShadowBuffer.DepthTexture;

				// If enableWorldFrameBufferDownscale and the world is more than twice the size of the final output size do we allow it to be downsampled!
				worldBuffer = Context.CreateFrameBuffer(worldBufferSize);

				// Pixel art scaling mode is a customized bilinear sampling
				worldBuffer.Texture.ScaleFilter = TextureScaleFilter.Linear;
				worldTexture = worldBuffer.Texture;

				// Invalidate cached state to force a shader update
				lastWorldViewport = Rectangle.Empty;
			}

			lastMaximumViewportSize = size;
		}

		public void BeginWorld(Rectangle worldViewport, WorldRenderer worldRenderer)
		{
			if (renderType != RenderType.None)
				throw new InvalidOperationException($"BeginWorld called with renderType = {renderType}, expected RenderType.None.");

			BeginFrame();

			if (worldTexture == null)
				throw new InvalidOperationException($"BeginWorld called before SetMaximumViewportSize has been set.");

			MapRenderer.SetShadowParams();

			WorldSpriteRenderer.SetCameraParams();
			WorldSpriteRenderer.SetShadowParams();

			if (lastWorldViewport != worldViewport)
			{
				WorldVxlRenderer.SetViewportParams();

				lastWorldViewport = worldViewport;
			}

			worldRenderer.World.Map.UpdateTerrainBlockMask(worldRenderer.Viewport);
			worldRenderer.World.Map.UpdateTerrainBlockTexture(worldRenderer.Viewport);

			renderType = RenderType.World;
		}

		bool enable3DDepthPreview;
		float2 depthPreview3dParams;

		public void SetDepthPreview(bool enabled, float contrast, float offset)
		{
			enable3DDepthPreview = enabled;
			depthPreview3dParams = new float2(contrast, offset);
		}

		// call by world renderer before draw 3d meshes
		public void UpdateShadowBuffer(WorldRenderer wr)
		{
			worldShadowBuffer.Bind();
			Context.EnableDepthBuffer(DepthFunc.LessEqual);
			Game.Renderer.EnableDepthWrite(true);
			Draw3DMeshesInstance(wr, true);

			// MapRenderer.SetCameraParams(World3DRenderer, true);
			// wr.TerrainRenderer?.RenderTerrainEarly(wr, wr.Viewport);

			Context.DisableDepthBuffer();
			worldShadowBuffer.Unbind();
			worldBuffer.Bind();
			MapRenderer.SetCameraParams(World3DRenderer, false);
		}

		public void Flush3DMeshesInstance(WorldRenderer wr)
		{
			// vxl
			foreach (var orderedMesh in orderedMeshes)
			{
				orderedMesh.Value.Flush();
			}

			// mesh
			wr.World.MeshCache.FlushInstances();
		}

		public void SetLightParams(IShader shader, World3DRenderer w3dr)
		{
			shader.SetVec("dirLight.direction", w3dr.SunDir.x, w3dr.SunDir.y, w3dr.SunDir.z);
			shader.SetVec("dirLight.ambient", w3dr.AmbientColor.X, w3dr.AmbientColor.Y, w3dr.AmbientColor.Z);
			shader.SetVec("dirLight.diffuse", w3dr.SunColor.X, w3dr.SunColor.Y, w3dr.SunColor.Z);
			shader.SetVec("dirLight.specular", w3dr.SunSpecularColor.X, w3dr.SunSpecularColor.Y, w3dr.SunSpecularColor.Z);
		}

		public void SetShadowParams(IShader shader, World3DRenderer w3dr)
		{
			shader.SetTexture("ShadowDepthTexture", worldShadowDepthTexture);
			var sunVP = w3dr.SunProjection * w3dr.SunView;
			var invCameraVP = (w3dr.Projection * w3dr.View).Inverse;
			shader.SetMatrix("SunVP", sunVP.Values1D);
			shader.SetMatrix("InvCameraVP", invCameraVP.Values1D);
			shader.SetFloat("ShadowBias", w3dr.FrameShadowBias);
			shader.SetFloat("AmbientIntencity", w3dr.AmbientIntencity);
			shader.SetVec("ViewPort", worldBufferSize.Width, worldBufferSize.Height);
		}

		public void Draw3DMeshesInstance(WorldRenderer wr, bool sunCamera)
		{
			// update common shader uniform param
			foreach (var shader in orderedMeshShaders)
			{
				shader.Value.SetCommonParaments(World3DRenderer, sunCamera);
				shader.Value.SetBool("EnableDepthPreview", enable3DDepthPreview);
				shader.Value.SetVec("DepthPreviewParams", depthPreview3dParams.X, depthPreview3dParams.Y);
				if (!sunCamera)
				{
					SetShadowParams(shader.Value, World3DRenderer);
				}
			}

			// vxl
			foreach (var orderedMesh in orderedMeshes)
			{
				orderedMesh.Value.DrawInstances(sunCamera);
			}

			// mesh
			wr.World.MeshCache.DrawInstances(sunCamera);
		}

		public void RenderInstance(int start, int numVertices, int numInstance, bool elemented = false)
		{
			Context.DrawInstances(PrimitiveType.TriangleList, start, numVertices, numInstance, elemented);
			PerfHistory.Increment("batches", 1);
		}

		public void EndWorld()
		{
			if (renderType == RenderType.World)
			{
				// Complete world rendering
				Flush();
				worldBuffer.Unbind();
				ScreenRenderer.DrawScreen(worldBuffer.Texture);
			}

			renderType = RenderType.World;
		}

		bool toRenderUI = true;

		public void TurnOnUIRender()
		{
			toRenderUI = true;
		}

		public void ToggleUIRender()
		{
			toRenderUI = !toRenderUI;
		}

		public void BeginUI()
		{
			if (renderType == RenderType.World)
			{
				// Render the world buffer into the UI buffer
				screenBuffer.Bind();

				SpriteRenderer.SetAntialiasingPixelsPerTexel(0);
			}
			else
			{
				// World rendering was skipped
				BeginFrame();
				screenBuffer.Bind();
			}

			renderType = RenderType.UI;
		}

		public void SetPalette(HardwarePalette palette)
		{
			// Note: palette.Texture and palette.ColorShifts are updated at the same time
			// so we only need to check one of the two to know whether we must update the textures
			if (palette.Texture == currentPaletteTexture)
				return;

			Flush();
			currentPaletteTexture = palette.Texture;

			SpriteRenderer.SetPalette(currentPaletteTexture, palette.ColorShifts);
			WorldSpriteRenderer.SetPalette(currentPaletteTexture, palette.ColorShifts);
			MapRenderer.SetPalette(currentPaletteTexture, palette.ColorShifts);
			WorldVxlRenderer.SetPalette(currentPaletteTexture);
		}

		public void EndFrame(IInputHandler inputHandler)
		{
			if (renderType != RenderType.UI)
				throw new InvalidOperationException($"EndFrame called with renderType = {renderType}, expected RenderType.UI.");

			Flush();
			screenBuffer.Unbind();

			// Render the compositor buffers to the screen
			// HACK / PERF: Fudge the coordinates to cover the actual window while keeping the buffer viewport parameters
			// This saves us two redundant (and expensive) SetViewportParams each frame
			if (toRenderUI)
			{
				RgbaSpriteRenderer.DrawSprite(screenSprite, new float3(0, lastBufferSize.Height, 0), new float3(lastBufferSize.Width / screenSprite.Size.X, -lastBufferSize.Height / screenSprite.Size.Y, 1f));
				Flush();

				//ScreenRenderer.DrawUIScreen(screenBuffer.Texture,
				//	new float2(0, 1.0f + screenBuffer.Texture.Size.Height / screenUISize.Y),
				//	new float2(screenBuffer.Texture.Size.Width / screenUISize.X,
				//	-screenBuffer.Texture.Size.Height / screenUISize.Y),
				//	BlendMode.Alpha);
			}

			Window.PumpInput(inputHandler);
			Context.Present();

			renderType = RenderType.None;
		}

		public void DrawBatch(IShader shader, Vertex[] vertices, int numVertices, PrimitiveType type)
		{
			tempBuffer.SetData(vertices, numVertices);
			DrawBatch(shader, tempBuffer, 0, numVertices, type);
		}

		public void DrawMapBatch(IShader shader, MapVertex[] vertices, int numVertices, PrimitiveType type)
		{
			tempMapBuffer.SetData(vertices, numVertices);
			DrawBatch(shader, tempMapBuffer, 0, numVertices, type);
		}

		public void DrawBatch(IShader shader, IVertexBuffer vertices,
			int firstVertex, int numVertices, PrimitiveType type, bool enableDepthTest = false)
		{
			vertices.Bind();

			// Future notice: using ARB_vertex_array_object makes all the gl calls in LayoutAttributes obsolete.
			shader.LayoutAttributes();

			Context.DrawPrimitives(type, firstVertex, numVertices);
			PerfHistory.Increment("batches", 1);
		}

		public void Flush()
		{
			CurrentBatchRenderer = null;
		}

		public Size Resolution => Window.EffectiveWindowSize;
		public Size NativeResolution => Window.NativeWindowSize;
		public float WindowScale => Window.EffectiveWindowScale;
		public float NativeWindowScale => Window.NativeWindowScale;
		public GLProfile GLProfile => Window.GLProfile;
		public GLProfile[] SupportedGLProfiles => Window.SupportedGLProfiles;

		public interface IBatchRenderer { void Flush(BlendMode blendMode = BlendMode.None); }

		public IBatchRenderer CurrentBatchRenderer
		{
			get => currentBatchRenderer;

			set
			{
				if (currentBatchRenderer == value)
					return;
				currentBatchRenderer?.Flush(BlendMode.None);
				currentBatchRenderer = value;
			}
		}

		public IVertexBuffer<T> CreateVertexBuffer<T>(int length)
			where T : struct
		{
			return Context.CreateVertexBuffer<T>(length);
		}

		public IShader GetOrCreateShader<T>(string typeName)
			where T : IShaderBindings
		{
			if (orderedMeshShaders.ContainsKey(typeName))
				return orderedMeshShaders[typeName];
			else
			{
				// ʵ������context��Ҳά����һ���ֵ��ֹ�ظ�������ͬ���͵�shader
				// ����������������Ҫ��¼���е�shader
				orderedMeshShaders.Add(typeName, Context.CreateShader<T>());
				return orderedMeshShaders[typeName];
			}
		}

		public IOrderedMesh UpdateOrderedMeshes(string typeName,in IOrderedMesh iom)
		{
			if (orderedMeshes.ContainsKey(typeName))
				return orderedMeshes[typeName];
			else
			{
				orderedMeshes.Add(typeName, iom);
				return orderedMeshes[typeName];
			}
		}

		public ITexture CreateInfoTexture(Size size)
		{
			return Context.CreateInfoTexture(size);
		}

		public void EnableScissor(Rectangle rect)
		{
			// Must remain inside the current scissor rect
			if (scissorState.Count > 0)
				rect = Rectangle.Intersect(rect, scissorState.Peek());

			Flush();

			if (renderType == RenderType.World)
			{
				var r = Rectangle.FromLTRB(
					rect.Left / worldDownscaleFactor,
					rect.Top / worldDownscaleFactor,
					(rect.Right + worldDownscaleFactor - 1) / worldDownscaleFactor,
					(rect.Bottom + worldDownscaleFactor - 1) / worldDownscaleFactor);
				worldBuffer.EnableScissor(r);
			}
			else
				Context.EnableScissor(rect.X, rect.Y, rect.Width, rect.Height);

			scissorState.Push(rect);
		}

		public void DisableScissor()
		{
			scissorState.Pop();
			Flush();

			if (renderType == RenderType.World)
			{
				// Restore previous scissor rect
				if (scissorState.Count > 0)
				{
					var rect = scissorState.Peek();
					var r = Rectangle.FromLTRB(
						rect.Left / worldDownscaleFactor,
						rect.Top / worldDownscaleFactor,
						(rect.Right + worldDownscaleFactor - 1) / worldDownscaleFactor,
						(rect.Bottom + worldDownscaleFactor - 1) / worldDownscaleFactor);
					worldBuffer.EnableScissor(r);
				}
				else
					worldBuffer.DisableScissor();
			}
			else
			{
				// Restore previous scissor rect
				if (scissorState.Count > 0)
				{
					var rect = scissorState.Peek();
					Context.EnableScissor(rect.X, rect.Y, rect.Width, rect.Height);
				}
				else
					Context.DisableScissor();
			}
		}

		public void SetBlendMode(BlendMode blendMode)
		{
			Context.SetBlendMode(blendMode);
		}

		public void EnableDepthBuffer()
		{
			Flush();
			Context.EnableDepthBuffer(DepthFunc.LessEqual);
		}

		public void EnableDepthWrite(bool enable)
		{
			Context.EnableDepthWrite(enable);
		}

		public void DisableDepthTest()
		{
			Flush();
			Context.DisableDepthBuffer();
		}

		public void ClearDepthBuffer()
		{
			Flush();
			Context.ClearDepthBuffer();
		}

		public void SetFaceCull(FaceCullFunc faceCullFunc)
		{
			if (faceCullFunc == FaceCullFunc.None)
				Context.DisableCullFace();
			else
				Context.EnableCullFace(faceCullFunc);
		}

		public void EnableAntialiasingFilter()
		{
			if (renderType != RenderType.UI)
				throw new InvalidOperationException($"EndFrame called with renderType = {renderType}, expected RenderType.UI.");

			Flush();
			SpriteRenderer.SetAntialiasingPixelsPerTexel(Window.EffectiveWindowScale);
		}

		public void DisableAntialiasingFilter()
		{
			if (renderType != RenderType.UI)
				throw new InvalidOperationException($"EndFrame called with renderType = {renderType}, expected RenderType.UI.");

			Flush();
			SpriteRenderer.SetAntialiasingPixelsPerTexel(0);
		}

		public void GrabWindowMouseFocus()
		{
			Window.GrabWindowMouseFocus();
		}

		public void ReleaseWindowMouseFocus()
		{
			Window.ReleaseWindowMouseFocus();
		}

		public void SaveScreenshot(string path)
		{
			var worldsrc = worldBuffer.Texture.GetData();
			var worldsrcWidth = worldTexture.Size.Width;
			var worldsrcHeight = worldTexture.Size.Height;
			var worlddestHeight = -worldTexture.Size.Height;

			// Pull the data from the Texture directly to prevent the sheet from buffering it
			// var src = screenBuffer.Texture.GetData();
			// var srcWidth = screenSprite.Sheet.Size.Width;
			// var destWidth = screenSprite.Bounds.Width;
			// var destHeight = -screenSprite.Bounds.Height;

			ThreadPool.QueueUserWorkItem(_ =>
			{
				// Extract the screen rect from the (larger) backing surface
				//var dest = new byte[4 * destWidth * destHeight];
				//for (var y = 0; y < destHeight; y++)
				//	Array.Copy(src, 4 * y * srcWidth, dest, 4 * y * destWidth, 4 * destWidth);

				//new Png(dest, SpriteFrameType.Bgra32, destWidth, destHeight).Save(path);

				var destworld = new byte[4 * worldsrcWidth * worldsrcHeight];
				for (var y = 0; y < worldsrcHeight; y++)
					Array.Copy(worldsrc, 4 * y * worldsrcWidth, destworld, 4 * (worldsrcHeight - y - 1) * worldsrcWidth, 4 * worldsrcWidth);
				new Png(destworld, SpriteFrameType.Bgra32, worldsrcWidth, worldsrcHeight).Save(path);

			});
		}

		public void Dispose()
		{
			WorldVxlRenderer.Dispose();
			tempBuffer.Dispose();
			tempMapBuffer.Dispose();
			fontSheetBuilder?.Dispose();
			if (Fonts != null)
				foreach (var font in Fonts.Values)
					font.Dispose();
			Window.Dispose();
		}

		public void SetVSyncEnabled(bool enabled)
		{
			Window.Context.SetVSyncEnabled(enabled);
		}

		public string GetClipboardText()
		{
			return Window.GetClipboardText();
		}

		public bool SetClipboardText(string text)
		{
			return Window.SetClipboardText(text);
		}

		public string GLVersion => Context.GLVersion;

		public IFont CreateFont(byte[] data)
		{
			return platform.CreateFont(data);
		}

		public int DisplayCount => Window.DisplayCount;

		public int CurrentDisplay => Window.CurrentDisplay;
	}
}
