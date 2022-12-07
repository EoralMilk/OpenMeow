using GlmSharp;
using OpenRA.FileFormats;
using OpenRA.Graphics;
using OpenRA.Primitives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using TrueSync;

namespace OpenRA
{
	public class MaskBrush
	{
		public string Name;

		public string[] Categories;

		public readonly int Id;

		public readonly Map Map;

		/// <summary>
		/// brush texture type
		/// </summary>
		public readonly int TextureIndex;

		public readonly int DefaultSize;

		public readonly int2 TextureSize;

		/// <summary>
		/// in WDist
		/// </summary>
		public readonly int2 MapSize;

		public MaskBrush(string name, string[] categories, int id, int type, int2 textureSize, int size, Map map)
		{
			Name = name;
			Categories = categories;
			Id = id;
			TextureIndex = type;
			DefaultSize = size;
			TextureSize = textureSize;
			Map = map;
			MapSize = new int2((map.VertexArrayWidth - 1) * 724, (map.VertexArrayHeight - 1) * 724);
		}

		public TerrainMaskVertex[] GetVertices(float2 blockTL, float2 blockBR,in WPos pos, int size, int layer, int intensity)
		{
			// a quad
			TerrainMaskVertex[] brushVertices = new TerrainMaskVertex[6];

			float2 centerPos = new float2(
				((float)pos.X / MapSize.X - blockTL.X) / TerrainRenderBlock.Range.X * 2f - 1f,
				((float)pos.Y / MapSize.Y - blockTL.Y) / TerrainRenderBlock.Range.Y * 2f - 1f);
			float renderSizeX = (float)size / MapSize.X / TerrainRenderBlock.Range.X;
			float renderSizeY = (float)size / MapSize.Y / TerrainRenderBlock.Range.Y;

			float[] quadVertices = {
									// positions																// texCoords
					centerPos.X - renderSizeX,  centerPos.Y + renderSizeY,           0.0f, 1.0f,
					centerPos.X - renderSizeX,  centerPos.Y - renderSizeY,            0.0f, 0.0f,
					centerPos.X + renderSizeX, centerPos.Y - renderSizeY,            1.0f, 0.0f,

					centerPos.X - renderSizeX,  centerPos.Y + renderSizeY,           0.0f, 1.0f,
					centerPos.X + renderSizeX, centerPos.Y - renderSizeY,            1.0f, 0.0f,
					centerPos.X + renderSizeX, centerPos.Y + renderSizeY,           1.0f, 1.0f
				};

			for (int i = 0; i < 6; i++)
			{
				brushVertices[i] = new TerrainMaskVertex(
					quadVertices[i * 4], quadVertices[i * 4 + 1],
					quadVertices[i * 4 + 2], quadVertices[i * 4 + 3],
					TextureIndex, layer, intensity);
			}

			return brushVertices;
		}
	}

	public class TerrainRenderBlock
	{
		/// <summary>
		/// Mask texture contains size*szie MiniCells
		/// </summary>
		public static int SizeLimit => Game.Settings.Graphics.BlockSizeAsMiniCell;
		public static int TextureSize => Game.Settings.Graphics.TerrainBlockTextureSize;

		public static int MiniCellPix => TextureSize / SizeLimit;
		public static float2 Range { get; private set; }

		public static vec3 ViewOffset;

		public readonly Map Map;
		public readonly int2 TopLeft;
		public readonly int2 BottomRight;

		/// <summary>
		/// 0 - 1, offset of block relative to map
		/// </summary>
		readonly float2 topLeftOffset;

		/// <summary>
		/// 0 - 1, offset of block relative to map
		/// </summary>
		readonly float2 bottomRightOffset;

		/// <summary>
		/// using for render check, as wdist
		/// </summary>
		public readonly int LeftBound;

		/// <summary>
		/// using for render check, as wdist
		/// </summary>
		public readonly int RightBound;

		/// <summary>
		/// using for render check, as wdist
		/// </summary>
		public readonly int TopBound;

		/// <summary>
		/// using for render check, as wdist
		/// </summary>
		public readonly int BottomBound;

		#region Mask Pass
		public static IShader TerrainMaskShader { get; private set; }
		public IFrameBuffer MaskFramebuffer { get; private set; }

		/// <summary>
		/// screen plane mesh
		/// </summary>
		static TerrainMaskVertex[] terrainMaskVertices;

		/// <summary>
		/// screen plane vert-buffer
		/// </summary>
		static IVertexBuffer<TerrainMaskVertex> maskVertexBuffer;

		BlendMode currentBrushMode = BlendMode.Additive;

		/// <summary>
		/// temp mask brushes vertices count
		/// </summary>
		int numMaskVertices;

		/// <summary>
		/// temp mask brushes meshes
		/// </summary>
		TerrainMaskVertex[] tempMaskVertices;

		/// <summary>
		/// temp mask brushes vert-buffer
		/// </summary>
		IVertexBuffer<TerrainMaskVertex> tempMaskBuffer;

		/// <summary>
		/// all brushes paint spot on this block
		/// </summary>
		readonly List<PaintSpot> paintSpots = new List<PaintSpot>();

		/// <summary>
		/// using for map editor redo/undo logic
		/// </summary>
		public IFrameBuffer EditorCachedMaskFramebuffer { get; private set; }
		readonly SortedDictionary<int, List<PaintSpot>> editorPaintSpots = new SortedDictionary<int, List<PaintSpot>>();
		public static int MaxEditorDrawCache => Game.Settings.Graphics.EditorBrushMaxHistoryCache;
		bool editorMaskBufferNeedUpdate = false;

		int solifiedIndex = -1;

		/// <summary>
		/// a spot to paint
		/// </summary>
		public struct PaintSpot
		{
			public readonly MaskBrush Brush;
			public readonly WPos Pos;
			public readonly int Size;
			public readonly int Layer;
			public readonly int Intensity;

			public PaintSpot(MaskBrush brush, WPos pos, int size, int layer, int intensity)
			{
				Brush = brush;
				Pos = pos;
				Size = size;
				Layer = layer;
				Intensity = intensity;
			}
		}

		/// <summary>
		/// use a type of 'Brush' to paint or erase mask of a layer
		/// </summary>
		/// <param name="map"> map </param>
		/// <param name="brush"> brush type </param>
		/// <param name="pos"> brush pos </param>
		/// <param name="size"> brush size as WDist </param>
		/// <param name="layer"> brushing layer </param>
		/// <param name="intensity"> brush intensity , negative is erase </param>
		public static void PaintAt(Map map, MaskBrush brush,
			in WPos pos, int size, int layer, int intensity, int editorDrawOrder = -1)
		{
			// Console.WriteLine("paint at " + pos.X + "," + pos.Y + " with size: " + size + " layer: " + layer);
			var blockx = (pos.X / 724) / SizeLimit;
			var blocky = (pos.Y / 724) / SizeLimit;
			for (int y = blocky - 1; y <= blocky + 1; y++)
				for (int x = blockx - 1; x <= blockx + 1; x++)
				{
					if (y < 0 || y >= map.BlocksArrayHeight || x < 0 || x >= map.BlocksArrayWidth)
						continue;
					if (pos.X - size > map.TerrainBlocks[y, x].RightBound ||
						pos.X + size < map.TerrainBlocks[y, x].LeftBound ||
						pos.Y - size > map.TerrainBlocks[y, x].BottomBound ||
						pos.Y + size < map.TerrainBlocks[y, x].TopBound)
						continue;
					map.TerrainBlocks[y, x].needUpdateTexture = true;
					if (editorDrawOrder > map.TerrainBlocks[y, x].solifiedIndex)
					{
						map.TerrainBlocks[y, x].editorMaskBufferNeedUpdate = true;
						if (map.TerrainBlocks[y, x].editorPaintSpots.ContainsKey(editorDrawOrder))
						{
							map.TerrainBlocks[y, x].editorPaintSpots[editorDrawOrder].Add(new PaintSpot(brush, pos, size, layer, intensity));
						}
						else
						{
							// draw count is bigger then cache max count, solidify editor brush to common mask buffer
							if (map.TerrainBlocks[y, x].editorPaintSpots.Count > MaxEditorDrawCache)
							{
								map.TerrainBlocks[y, x].SolidifyEditorBrush();
							}

							map.TerrainBlocks[y, x].editorPaintSpots.Add(editorDrawOrder, new List<PaintSpot>());
							map.TerrainBlocks[y, x].editorPaintSpots[editorDrawOrder].Add(new PaintSpot(brush, pos, size, layer, intensity));
						}
					}
					else
					{
						map.TerrainBlocks[y, x].paintSpots.Add(new PaintSpot(brush, pos, size, layer, intensity));
					}
				}
		}

		void PaintMask(MaskBrush brush, in WPos pos, int size, int layer, int intensity)
		{
			if (tempMaskVertices == null)
			{
				numMaskVertices = 0;
				tempMaskVertices = new TerrainMaskVertex[Game.Renderer.TempBufferSize];
			}

			// erase
			if (intensity < 0)
			{
				// if brush mode not erase, flush
				if (currentBrushMode != BlendMode.Subtractive)
				{
					FlushBrush(currentBrushMode);
				}

				currentBrushMode = BlendMode.Subtractive;
			}
			else
			{
				// if brush mode not add, flush
				if (currentBrushMode != BlendMode.Additive)
				{
					FlushBrush(currentBrushMode);
				}

				currentBrushMode = BlendMode.Additive;
			}

			var brushVertices = brush.GetVertices(topLeftOffset, bottomRightOffset, pos, size, layer, Math.Abs(intensity));
			if (numMaskVertices + brushVertices.Length >= tempMaskVertices.Length)
				FlushBrush(currentBrushMode);
			Array.Copy(brushVertices, 0, tempMaskVertices, numMaskVertices, brushVertices.Length);
			numMaskVertices += brushVertices.Length;
		}

		void FlushBrush(BlendMode blendMode)
		{
			if (numMaskVertices == 0)
				return;

			Game.Renderer.Context.SetBlendMode(blendMode);

			tempMaskBuffer.SetData(tempMaskVertices, numMaskVertices);

			TerrainMaskShader.PrepareRender();

			Game.Renderer.DrawBatch(TerrainMaskShader, tempMaskBuffer, 0, numMaskVertices, PrimitiveType.TriangleList);

			numMaskVertices = 0;
		}

		public void InitMask()
		{
			if (MaskFramebuffer == null)
			{
				MaskFramebuffer = Game.Renderer.Context.CreateFrameBuffer(new Size(TextureSize, TextureSize), 3);

				if (TerrainMaskShader == null)
				{
					TerrainMaskShader = Game.Renderer.Context.CreateUnsharedShader<TerrainMaskShaderBindings>();
				}

				if (terrainMaskVertices == null || maskVertexBuffer == null)
				{
					terrainMaskVertices = new TerrainMaskVertex[6];

					float[] quadVertices = {
							// positions			// texCoords
								-1, 1,                  0.0f, 1.0f,
								-1, -1,                 0.0f, 0.0f,
								1, -1,                  1.0f, 0.0f,

								-1, 1,                  0.0f, 1.0f,
								1, -1,                  1.0f, 0.0f,
								1, 1,                   1.0f, 1.0f
				};

					for (int i = 0; i < 6; i++)
					{
						terrainMaskVertices[i] = new TerrainMaskVertex(quadVertices[i * 4], quadVertices[i * 4 + 1], quadVertices[i * 4 + 2], quadVertices[i * 4 + 3]);
					}

					maskVertexBuffer = Game.Renderer.CreateVertexBuffer<TerrainMaskVertex>(terrainMaskVertices.Length);
					maskVertexBuffer.SetData(terrainMaskVertices, terrainMaskVertices.Length);
				}

				numMaskVertices = 0;
				tempMaskVertices = new TerrainMaskVertex[Game.Renderer.TempBufferSize];
				tempMaskBuffer = Game.Renderer.Context.CreateVertexBuffer<TerrainMaskVertex>(Game.Renderer.TempBufferSize);

				{
					Sheet sheet123;
					Sheet sheet456;
					Sheet sheet789;

					Map.TextureCache.ReadMapTexture(TopLeft.ToString() + "_Mask123.png", TextureWrap.ClampToEdge, out sheet123);
					Map.TextureCache.ReadMapTexture(TopLeft.ToString() + "_Mask456.png", TextureWrap.ClampToEdge, out sheet456);
					Map.TextureCache.ReadMapTexture(TopLeft.ToString() + "_Mask789.png", TextureWrap.ClampToEdge, out sheet789);
					if (sheet123 != null && sheet456 != null && sheet789 != null)
						Map.MaskInitByTexFile = true;

					{
						MaskFramebuffer.Bind();

						Game.Renderer.SetFaceCull(FaceCullFunc.None);
						Game.Renderer.EnableDepthWrite(false);
						Game.Renderer.DisableDepthTest();

						if (sheet123 != null)
							TerrainMaskShader.SetTexture("InitMask123", sheet123.GetTexture());
						else
							TerrainMaskShader.SetTexture("InitMask123", Map.TextureCache.AdditionTextures["Black"].Item2.GetTexture());
						if (sheet456 != null)
							TerrainMaskShader.SetTexture("InitMask456", sheet456.GetTexture());
						else
							TerrainMaskShader.SetTexture("InitMask456", Map.TextureCache.AdditionTextures["Black"].Item2.GetTexture());
						if (sheet789 != null)
							TerrainMaskShader.SetTexture("InitMask789", sheet789.GetTexture());
						else
							TerrainMaskShader.SetTexture("InitMask789", Map.TextureCache.AdditionTextures["Black"].Item2.GetTexture());

						TerrainMaskShader.SetBool("InitWithTextures", Map.MaskInitByTexFile);

						TerrainMaskShader.SetTexture("Brushes", Map.TextureCache.BrushTextureArray);

						Game.Renderer.Context.SetBlendMode(BlendMode.None);
						TerrainMaskShader.PrepareRender();

						Game.Renderer.DrawBatch(TerrainMaskShader, maskVertexBuffer, 0, terrainMaskVertices.Length, PrimitiveType.TriangleList);

						Game.Renderer.EnableDepthBuffer();
						Game.Renderer.EnableDepthWrite(true);

						Game.Renderer.Context.SetBlendMode(BlendMode.Alpha);

						MaskFramebuffer.Unbind();
					}

					sheet123?.Dispose();
					sheet456?.Dispose();
					sheet789?.Dispose();
				}

			}
		}

		public void InitEditorMask()
		{
			if (EditorCachedMaskFramebuffer == null)
			{
				EditorCachedMaskFramebuffer = Game.Renderer.Context.CreateFrameBuffer(new Size(TextureSize, TextureSize), 3);

				if (TerrainMaskShader == null)
				{
					TerrainMaskShader = Game.Renderer.Context.CreateUnsharedShader<TerrainMaskShaderBindings>();
				}
			}
		}

		public void UpdateMask(int left, int right, int top, int bottom, bool force)
		{
			if (force)
			{
				// skip bound cal
			}
			else if (LeftBound > right || RightBound < left)
				return;
			else if (BottomBound < top || TopBound > bottom)
				return;

			if (paintSpots.Count > 0)
			{
				// we don't want to clear the renderer result last bake, just paint on it.
				MaskFramebuffer.BindNoClear();

				Game.Renderer.SetFaceCull(FaceCullFunc.None);
				Game.Renderer.EnableDepthWrite(false);
				Game.Renderer.DisableDepthTest();

				TerrainMaskShader.SetTexture("Brushes", Map.TextureCache.BrushTextureArray);
				TerrainMaskShader.SetBool("InitWithTextures", false);

				Game.Renderer.Context.SetBlendMode(BlendMode.None);
				TerrainMaskShader.PrepareRender();

				foreach (var p in paintSpots)
				{
					PaintMask(p.Brush, p.Pos, p.Size, p.Layer, p.Intensity);
				}

				FlushBrush(currentBrushMode);

				Game.Renderer.EnableDepthBuffer();
				Game.Renderer.EnableDepthWrite(true);

				Game.Renderer.Context.SetBlendMode(BlendMode.Alpha);

				MaskFramebuffer.Unbind();

				// Console.WriteLine(TopLeft + " paint update " + paintSpots.Count);
				needUpdateTexture = true;
				paintSpots.Clear();
			}

			UpdateEditorMask();
		}

		public void UpdateEditorMask()
		{
			if (editorPaintSpots.Count == 0 && EditorCachedMaskFramebuffer != null)
			{
				EditorCachedMaskFramebuffer?.Dispose();
				EditorCachedMaskFramebuffer = null;
				return;
			}

			if (!editorMaskBufferNeedUpdate)
				return;

			editorMaskBufferNeedUpdate = false;

			InitEditorMask();

			// update with maskbuffer out put
			{
				EditorCachedMaskFramebuffer.Bind();

				Game.Renderer.SetFaceCull(FaceCullFunc.None);
				Game.Renderer.EnableDepthWrite(false);
				Game.Renderer.DisableDepthTest();

				TerrainMaskShader.SetTexture("InitMask123", MaskFramebuffer.GetTexture(0));
				TerrainMaskShader.SetTexture("InitMask456", MaskFramebuffer.GetTexture(1));
				TerrainMaskShader.SetTexture("InitMask789", MaskFramebuffer.GetTexture(2));

				TerrainMaskShader.SetBool("InitWithTextures", true);

				Game.Renderer.Context.SetBlendMode(BlendMode.None);
				TerrainMaskShader.PrepareRender();

				Game.Renderer.DrawBatch(TerrainMaskShader, maskVertexBuffer, 0, terrainMaskVertices.Length, PrimitiveType.TriangleList);

				Game.Renderer.EnableDepthBuffer();
				Game.Renderer.EnableDepthWrite(true);

				Game.Renderer.Context.SetBlendMode(BlendMode.Alpha);

				EditorCachedMaskFramebuffer.Unbind();
			}

			// update with brush cache
			if (editorPaintSpots.Count > 0)
			{
				// we don't want to clear the renderer result last bake, just paint on it.
				EditorCachedMaskFramebuffer.BindNoClear();

				Game.Renderer.SetFaceCull(FaceCullFunc.None);
				Game.Renderer.EnableDepthWrite(false);
				Game.Renderer.DisableDepthTest();

				TerrainMaskShader.SetTexture("Brushes", Map.TextureCache.BrushTextureArray);
				TerrainMaskShader.SetBool("InitWithTextures", false);

				Game.Renderer.Context.SetBlendMode(BlendMode.None);
				TerrainMaskShader.PrepareRender();

				foreach (var kv in editorPaintSpots)
				{
					foreach (var p in kv.Value)
					{
						PaintMask(p.Brush, p.Pos, p.Size, p.Layer, p.Intensity);
					}
				}

				FlushBrush(currentBrushMode);

				Game.Renderer.EnableDepthBuffer();
				Game.Renderer.EnableDepthWrite(true);

				Game.Renderer.Context.SetBlendMode(BlendMode.Alpha);

				EditorCachedMaskFramebuffer.Unbind();

				// Console.WriteLine(TopLeft + " paint update " + paintSpots.Count);
				needUpdateTexture = true;
			}
		}

		public void SolidifyEditorBrush()
		{
			solifiedIndex = editorPaintSpots.First().Key;
			paintSpots.AddRange(editorPaintSpots.First().Value);
			editorPaintSpots.Remove(editorPaintSpots.First().Key);
			editorMaskBufferNeedUpdate = true;
			needUpdateTexture = true;
		}

		public void UndoEditorDraw(int key)
		{
			editorPaintSpots.Remove(key);
			editorMaskBufferNeedUpdate = true;
			needUpdateTexture = true;
		}

		public void SolidifyAllEditorBrush()
		{
			while (editorPaintSpots.Count > 0)
			{
				SolidifyEditorBrush();
			}
		}

		#endregion

		#region Blend Pass
		public static IShader TerrainBlendShader { get; private set; }

		public readonly IFrameBuffer BlendFramebuffer;

		/// <summary>
		/// terrain blend mesh
		/// </summary>
		readonly TerrainBlendingVertex[] blendVertices;

		/// <summary>
		/// terrain blend vert-buffer
		/// </summary>
		readonly IVertexBuffer<TerrainBlendingVertex> blendVertexBuffer;

		bool needUpdateTexture = true;

		readonly float[] tileScales;

		void ShaderSetTileInfos()
		{
			TerrainBlendShader.SetVecArray("TileScales", tileScales, 1, tileScales.Length);
		}

		public void UpdateTexture(int left, int right, int top, int bottom, bool force)
		{
			if (TerrainBlendShader == null)
				return;

			if (hasUpdateVertex)
			{
				blendVertexBuffer.SetData(blendVertices, blendVertices.Length);
				finalVertexBuffer.SetData(finalVertices, finalVertices.Length);
			}

			if (force)
			{
				// skip bound cal
			}
			else if (LeftBound > right || RightBound < left)
				return;
			else if (BottomBound < top || TopBound > bottom)
				return;

			if (!needUpdateTexture)
				return;

			// Console.WriteLine("Updating block texture: " + TopLeft);
			needUpdateTexture = false;

			BlendFramebuffer.Bind();

			if (editorPaintSpots.Count == 0)
			{
				TerrainBlendShader.SetTexture("Mask123", MaskFramebuffer.GetTexture(0));

				TerrainBlendShader.SetTexture("Mask456", MaskFramebuffer.GetTexture(1));

				TerrainBlendShader.SetTexture("Mask789", MaskFramebuffer.GetTexture(2));
			}
			else
			{
				TerrainBlendShader.SetTexture("Mask123", EditorCachedMaskFramebuffer.GetTexture(0));

				TerrainBlendShader.SetTexture("Mask456", EditorCachedMaskFramebuffer.GetTexture(1));

				TerrainBlendShader.SetTexture("Mask789", EditorCachedMaskFramebuffer.GetTexture(2));
			}

			var cloud = Map.TextureCache.AdditionTextures["MaskCloud"];
			TerrainBlendShader.SetTexture(cloud.Item1, cloud.Item2.GetTexture());

			TerrainBlendShader.SetTexture("Tiles", Map.TextureCache.TileTextureArray);
			TerrainBlendShader.SetTexture("TilesNorm", Map.TextureCache.TileNormalTextureArray);

			TerrainBlendShader.SetVec("Offset",
				topLeftOffset.X,
				topLeftOffset.Y);
			TerrainBlendShader.SetVec("Range",
				bottomRightOffset.X - topLeftOffset.X,
				bottomRightOffset.Y - topLeftOffset.Y);

			ShaderSetTileInfos();

			Game.Renderer.Context.SetBlendMode(BlendMode.None);
			TerrainBlendShader.PrepareRender();

			Game.Renderer.DrawBatch(TerrainBlendShader, blendVertexBuffer, 0, blendVertices.Length, PrimitiveType.TriangleList);

			Game.Renderer.Context.SetBlendMode(BlendMode.Alpha);
			BlendFramebuffer.Unbind();
		}

		#endregion

		#region Final Pass
		public static IShader TerrainShader { get; private set; }

		/// <summary>
		/// terrain mesh
		/// </summary>
		readonly TerrainFinalVertex[] finalVertices;

		/// <summary>
		/// terrain mesh buffer
		/// </summary>
		readonly IVertexBuffer<TerrainFinalVertex> finalVertexBuffer;

		public void Render(int left, int right, int top, int bottom)
		{
			if (TerrainShader == null)
				return;

			if (LeftBound > right || RightBound < left)
				return;
			if (BottomBound < top || TopBound > bottom)
				return;

			TerrainShader.SetVec("Offset",
				topLeftOffset.X,
				topLeftOffset.Y);
			TerrainShader.SetVec("Range",
				bottomRightOffset.X - topLeftOffset.X,
				bottomRightOffset.Y - topLeftOffset.Y);

			TerrainShader.SetFloat("WaterUVOffset", (float)(Game.LocalTick % 256) / 256);
			TerrainShader.SetFloat("WaterUVOffset2", (float)(Game.LocalTick % 1784) / 1784);

			if (editorPaintSpots.Count == 0)
			{
				TerrainShader.SetTexture("Mask123", MaskFramebuffer.GetTexture(0));
			}
			else
			{
				TerrainShader.SetTexture("Mask123", EditorCachedMaskFramebuffer.GetTexture(0));
			}

			TerrainShader.SetTexture("Water",
							Map.TextureCache.AdditionTextures["Water"].Item2.GetTexture());
			TerrainShader.SetTexture("WaterNormal",
							Map.TextureCache.AdditionTextures["WaterNormal"].Item2.GetTexture());

			TerrainShader.SetTexture("Caustics",
				Map.TextureCache.CausticsTextures[(Game.LocalTick % (Map.TextureCache.CausticsTextures.Length * 3)) / 3].GetTexture());

			var cloud = Map.TextureCache.AdditionTextures["MaskCloud"];
			TerrainShader.SetTexture(cloud.Item1, cloud.Item2.GetTexture());

			TerrainShader.SetTexture("BakedTerrainTexture", BlendFramebuffer.Texture);
			TerrainShader.SetTexture("BakedTerrainNormalTexture", BlendFramebuffer.GetTexture(1));

			Game.Renderer.Context.SetBlendMode(BlendMode.None);
			TerrainShader.PrepareRender();

			Game.Renderer.DrawBatch(TerrainShader, finalVertexBuffer, 0, finalVertices.Length, PrimitiveType.TriangleList);

			Game.Renderer.Context.SetBlendMode(BlendMode.None);
		}

		public static void SetCameraParams(in World3DRenderer w3dr, bool sunCamera)
		{
			if (TerrainShader == null)
				return;
			TerrainShader.SetCommonParaments(w3dr, sunCamera);
			ViewOffset = Game.Renderer.World3DRenderer.InverseCameraFrontMeterPerWDist * (-17);

			TerrainShader.SetVec("ViewOffset",
				ViewOffset.x,
				ViewOffset.y,
				ViewOffset.z);
			TerrainShader.SetVec("CameraInvFront",
				Game.Renderer.World3DRenderer.InverseCameraFront.x,
				Game.Renderer.World3DRenderer.InverseCameraFront.y,
				Game.Renderer.World3DRenderer.InverseCameraFront.z);
		}

		public static void SetShadowParams(in World3DRenderer w3dr)
		{
			if (TerrainShader == null)
				return;

			Game.Renderer.SetShadowParams(TerrainShader, w3dr, Game.Settings.Graphics.TerrainShadowType);
			Game.Renderer.SetLightParams(TerrainShader, w3dr);
		}

		#endregion

		public static IShader TerrainCoveringShader { get; private set; }

		public IFrameBuffer CoveringFrameBuffer { get; private set; }

		bool hasUpdateVertex = false;

		public readonly Dictionary<int2, int> VertexIndexOfMiniCellUV = new Dictionary<int2, int>();

		public static void UpdateVerticesByMiniCellUV(int2 uv, Map map)
		{
			var blockx = uv.X / SizeLimit;
			var blocky = uv.Y / SizeLimit;
			if (map.BlocksArrayWidth <= blockx || map.BlocksArrayHeight <= blocky)
				return;

			var block = map.TerrainBlocks[blocky, blockx];

			if (!block.VertexIndexOfMiniCellUV.TryGetValue(uv, out int index))
				return;

			block.needUpdateTexture = true;
			block.hasUpdateVertex = true;

			MiniCell cell = map.MiniCells[uv.Y, uv.X];
			var vertTL = map.TerrainVertices[cell.TL];
			var vertTR = map.TerrainVertices[cell.TR];
			var vertBL = map.TerrainVertices[cell.BL];
			var vertBR = map.TerrainVertices[cell.BR];

			if (cell.Type == MiniCellType.TRBL)
			{
				block.blendVertices[index + 0] = new TerrainBlendingVertex(vertTR.UV,
					new float2(block.blendVertices[index + 0].MaskU, block.blendVertices[index + 0].MaskV),
					vertTR.TBN, vertTR.Color, 1.0f,
					block.blendVertices[index + 0]);
				block.blendVertices[index + 1] = new TerrainBlendingVertex(vertBL.UV,
					new float2(block.blendVertices[index + 1].MaskU, block.blendVertices[index + 1].MaskV),
					vertBL.TBN, vertBL.Color, 1.0f,
					block.blendVertices[index + 1]);
				block.blendVertices[index + 2] = new TerrainBlendingVertex(vertBR.UV,
					new float2(block.blendVertices[index + 2].MaskU, block.blendVertices[index + 2].MaskV),
					vertBR.TBN, vertBR.Color, 1.0f,
					block.blendVertices[index + 2]);

				block.blendVertices[index + 3] = new TerrainBlendingVertex(vertTR.UV,
					new float2(block.blendVertices[index + 3].MaskU, block.blendVertices[index + 3].MaskV),
					vertTR.TBN, vertTR.Color, 1.0f,
					block.blendVertices[index + 3]);
				block.blendVertices[index + 4] = new TerrainBlendingVertex(vertTL.UV,
					new float2(block.blendVertices[index + 4].MaskU, block.blendVertices[index + 4].MaskV),
					vertTL.TBN, vertTL.Color, 1.0f,
					block.blendVertices[index + 4]);
				block.blendVertices[index + 5] = new TerrainBlendingVertex(vertBL.UV,
					new float2(block.blendVertices[index + 5].MaskU, block.blendVertices[index + 5].MaskV),
					vertBL.TBN, vertBL.Color, 1.0f,
					block.blendVertices[index + 5]);
			}
			else
			{
				block.blendVertices[index + 0] = new TerrainBlendingVertex(vertTL.UV,
					new float2(block.blendVertices[index + 0].MaskU, block.blendVertices[index + 0].MaskV),
					vertTL.TBN, vertTL.Color, 1.0f,
					block.blendVertices[index + 0]);
				block.blendVertices[index + 1] = new TerrainBlendingVertex(vertBL.UV,
					new float2(block.blendVertices[index + 1].MaskU, block.blendVertices[index + 1].MaskV),
					vertBL.TBN, vertBL.Color, 1.0f,
					block.blendVertices[index + 1]);
				block.blendVertices[index + 2] = new TerrainBlendingVertex(vertBR.UV,
					new float2(block.blendVertices[index + 2].MaskU, block.blendVertices[index + 2].MaskV),
					vertBR.TBN, vertBR.Color, 1.0f,
					block.blendVertices[index + 2]);

				block.blendVertices[index + 3] = new TerrainBlendingVertex(vertTR.UV,
					new float2(block.blendVertices[index + 3].MaskU, block.blendVertices[index + 3].MaskV),
					vertTR.TBN, vertTR.Color, 1.0f,
					block.blendVertices[index + 3]);
				block.blendVertices[index + 4] = new TerrainBlendingVertex(vertTL.UV,
					new float2(block.blendVertices[index + 4].MaskU, block.blendVertices[index + 4].MaskV),
					vertTL.TBN, vertTL.Color, 1.0f,
					block.blendVertices[index + 4]);
				block.blendVertices[index + 5] = new TerrainBlendingVertex(vertBR.UV,
					new float2(block.blendVertices[index + 5].MaskU, block.blendVertices[index + 5].MaskV),
					vertBR.TBN, vertBR.Color, 1.0f,
					block.blendVertices[index + 5]);
			}

			if (cell.Type == MiniCellType.TRBL)
			{
				block.finalVertices[index + 0] = new TerrainFinalVertex(vertTR.Pos, new float2(block.finalVertices[index + 0].U, block.finalVertices[index + 0].V));
				block.finalVertices[index + 1] = new TerrainFinalVertex(vertBL.Pos, new float2(block.finalVertices[index + 1].U, block.finalVertices[index + 1].V));
				block.finalVertices[index + 2] = new TerrainFinalVertex(vertBR.Pos, new float2(block.finalVertices[index + 2].U, block.finalVertices[index + 2].V));

				block.finalVertices[index + 3] = new TerrainFinalVertex(vertTR.Pos, new float2(block.finalVertices[index + 3].U, block.finalVertices[index + 3].V));
				block.finalVertices[index + 4] = new TerrainFinalVertex(vertTL.Pos, new float2(block.finalVertices[index + 4].U, block.finalVertices[index + 4].V));
				block.finalVertices[index + 5] = new TerrainFinalVertex(vertBL.Pos, new float2(block.finalVertices[index + 5].U, block.finalVertices[index + 5].V));
			}
			else
			{
				block.finalVertices[index + 0] = new TerrainFinalVertex(vertTL.Pos, new float2(block.finalVertices[index + 0].U, block.finalVertices[index + 0].V));
				block.finalVertices[index + 1] = new TerrainFinalVertex(vertBL.Pos, new float2(block.finalVertices[index + 1].U, block.finalVertices[index + 1].V));
				block.finalVertices[index + 2] = new TerrainFinalVertex(vertBR.Pos, new float2(block.finalVertices[index + 2].U, block.finalVertices[index + 2].V));

				block.finalVertices[index + 3] = new TerrainFinalVertex(vertTR.Pos, new float2(block.finalVertices[index + 3].U, block.finalVertices[index + 3].V));
				block.finalVertices[index + 4] = new TerrainFinalVertex(vertTL.Pos, new float2(block.finalVertices[index + 4].U, block.finalVertices[index + 4].V));
				block.finalVertices[index + 5] = new TerrainFinalVertex(vertBR.Pos, new float2(block.finalVertices[index + 5].U, block.finalVertices[index + 5].V));
			}
		}

		public TerrainRenderBlock(WorldRenderer worldRenderer, Map map, int2 tl, int2 br)
		{
			Range = new float2((float)SizeLimit / (map.VertexArrayWidth - 1), (float)SizeLimit / (map.VertexArrayHeight - 1));

			BlendFramebuffer = Game.Renderer.Context.CreateFrameBuffer(new Size(TextureSize, TextureSize), 2);

			if (TerrainBlendShader == null)
				TerrainBlendShader = Game.Renderer.Context.CreateUnsharedShader<TerrainBlendingShaderBindings>();
			if (TerrainShader == null)
				TerrainShader = Game.Renderer.Context.CreateUnsharedShader<TerrainFinalShaderBindings>();

			Map = map;
			TopLeft = tl;
			BottomRight = br;
			LeftBound = (tl.X - 1) * 724;
			RightBound = (br.X + 1) * 724;
			TopBound = (tl.Y + 1) * 724;
			BottomBound = (br.Y + 1) * 724;
			var topLeftOffsetX = (double)TopLeft.X / (Map.VertexArrayWidth - 1);
			var topLeftOffsetY = (double)TopLeft.Y / (Map.VertexArrayHeight - 1);
			topLeftOffset = new float2((float)topLeftOffsetX, (float)topLeftOffsetY);
			var bottomRightOffsetX = (double)(BottomRight.X + 1) / (Map.VertexArrayWidth - 1);
			var bottomRightOffsetY = (double)(BottomRight.Y + 1) / (Map.VertexArrayHeight - 1);
			bottomRightOffset = new float2((float)bottomRightOffsetX, (float)bottomRightOffsetY);
			var sizeX = br.X - tl.X + 1;
			var sizeY = br.Y - tl.Y + 1;
			if (sizeX > SizeLimit || sizeY > SizeLimit)
			{
				throw new Exception("TerrainBlock size limit as " + SizeLimit + " but giving " + tl + " " + br);
			}

			var index = 0;
			blendVertices = new TerrainBlendingVertex[sizeX * sizeY * 6];
			finalVertices = new TerrainFinalVertex[sizeX * sizeY * 6];

			var rand = new Support.MersenneTwister();

			// skip the first , last row and col
			// to avoid the stripe error
			for (int y = Math.Max(TopLeft.Y, 1); y <= Math.Min(BottomRight.Y, map.VertexArrayHeight - 3); y++)
			{
				for (int x = Math.Max(TopLeft.X, 1); x <= Math.Min(BottomRight.X, map.VertexArrayWidth - 3); x++)
				{
					MiniCell cell = Map.MiniCells[y, x];
					VertexIndexOfMiniCellUV.Add(new int2(x, y), index);

					var vertTL = Map.TerrainVertices[cell.TL];
					var vertTR = Map.TerrainVertices[cell.TR];
					var vertBL = Map.TerrainVertices[cell.BL];
					var vertBR = Map.TerrainVertices[cell.BR];

					var maskuvTL = CalMaskUV(new int2(x, y), TopLeft);
					var maskuvTR = CalMaskUV(new int2(x + 1, y), TopLeft);
					var maskuvBL = CalMaskUV(new int2(x, y + 1), TopLeft);
					var maskuvBR = CalMaskUV(new int2(x + 1, y + 1), TopLeft);

					// random set texture
					int[] indexs = new int[9];

					// skip 0
					for (int i = 1; i < 9; i++)
					{
						indexs[i] = Map.TextureCache.TileTypeTexIndices[Map.TextureCache.LayerTileTypes[i].RandomOrDefault(rand)].RandomOrDefault(rand);
					}

					if (cell.Type == MiniCellType.TRBL)
					{
						blendVertices[index + 0] = new TerrainBlendingVertex(vertTR.UV, maskuvTR, vertTR.TBN, vertTR.Color, 1.0f,
							indexs[1], indexs[2], indexs[3], indexs[4], indexs[5], indexs[6], indexs[7], indexs[8]);
						blendVertices[index + 1] = new TerrainBlendingVertex(vertBL.UV, maskuvBL, vertBL.TBN, vertBL.Color, 1.0f,
							indexs[1], indexs[2], indexs[3], indexs[4], indexs[5], indexs[6], indexs[7], indexs[8]);
						blendVertices[index + 2] = new TerrainBlendingVertex(vertBR.UV, maskuvBR, vertBR.TBN, vertBR.Color, 1.0f,
							indexs[1], indexs[2], indexs[3], indexs[4], indexs[5], indexs[6], indexs[7], indexs[8]);

						blendVertices[index + 3] = new TerrainBlendingVertex(vertTR.UV, maskuvTR, vertTR.TBN, vertTR.Color, 1.0f,
							indexs[1], indexs[2], indexs[3], indexs[4], indexs[5], indexs[6], indexs[7], indexs[8]);
						blendVertices[index + 4] = new TerrainBlendingVertex(vertTL.UV, maskuvTL, vertTL.TBN, vertTL.Color, 1.0f,
							indexs[1], indexs[2], indexs[3], indexs[4], indexs[5], indexs[6], indexs[7], indexs[8]);
						blendVertices[index + 5] = new TerrainBlendingVertex(vertBL.UV, maskuvBL, vertBL.TBN, vertBL.Color, 1.0f,
							indexs[1], indexs[2], indexs[3], indexs[4], indexs[5], indexs[6], indexs[7], indexs[8]);
					}
					else
					{
						blendVertices[index + 0] = new TerrainBlendingVertex(vertTL.UV, maskuvTL, vertTL.TBN, vertTL.Color, 1.0f,
							indexs[1], indexs[2], indexs[3], indexs[4], indexs[5], indexs[6], indexs[7], indexs[8]);
						blendVertices[index + 1] = new TerrainBlendingVertex(vertBL.UV, maskuvBL, vertBL.TBN, vertBL.Color, 1.0f,
							indexs[1], indexs[2], indexs[3], indexs[4], indexs[5], indexs[6], indexs[7], indexs[8]);
						blendVertices[index + 2] = new TerrainBlendingVertex(vertBR.UV, maskuvBR, vertBR.TBN, vertBR.Color, 1.0f,
							indexs[1], indexs[2], indexs[3], indexs[4], indexs[5], indexs[6], indexs[7], indexs[8]);

						blendVertices[index + 3] = new TerrainBlendingVertex(vertTR.UV, maskuvTR, vertTR.TBN, vertTR.Color, 1.0f,
							indexs[1], indexs[2], indexs[3], indexs[4], indexs[5], indexs[6], indexs[7], indexs[8]);
						blendVertices[index + 4] = new TerrainBlendingVertex(vertTL.UV, maskuvTL, vertTL.TBN, vertTL.Color, 1.0f,
							indexs[1], indexs[2], indexs[3], indexs[4], indexs[5], indexs[6], indexs[7], indexs[8]);
						blendVertices[index + 5] = new TerrainBlendingVertex(vertBR.UV, maskuvBR, vertBR.TBN, vertBR.Color, 1.0f,
							indexs[1], indexs[2], indexs[3], indexs[4], indexs[5], indexs[6], indexs[7], indexs[8]);
					}

					if (cell.Type == MiniCellType.TRBL)
					{
						finalVertices[index + 0] = new TerrainFinalVertex(vertTR.Pos, maskuvTR);
						finalVertices[index + 1] = new TerrainFinalVertex(vertBL.Pos, maskuvBL);
						finalVertices[index + 2] = new TerrainFinalVertex(vertBR.Pos, maskuvBR);

						finalVertices[index + 3] = new TerrainFinalVertex(vertTR.Pos, maskuvTR);
						finalVertices[index + 4] = new TerrainFinalVertex(vertTL.Pos, maskuvTL);
						finalVertices[index + 5] = new TerrainFinalVertex(vertBL.Pos, maskuvBL);
					}
					else
					{
						finalVertices[index + 0] = new TerrainFinalVertex(vertTL.Pos, maskuvTL);
						finalVertices[index + 1] = new TerrainFinalVertex(vertBL.Pos, maskuvBL);
						finalVertices[index + 2] = new TerrainFinalVertex(vertBR.Pos, maskuvBR);

						finalVertices[index + 3] = new TerrainFinalVertex(vertTR.Pos, maskuvTR);
						finalVertices[index + 4] = new TerrainFinalVertex(vertTL.Pos, maskuvTL);
						finalVertices[index + 5] = new TerrainFinalVertex(vertBR.Pos, maskuvBR);
					}

					index += 6;
				}
			}

			blendVertexBuffer = Game.Renderer.CreateVertexBuffer<TerrainBlendingVertex>(blendVertices.Length);
			blendVertexBuffer.SetData(blendVertices, blendVertices.Length);

			finalVertexBuffer = Game.Renderer.CreateVertexBuffer<TerrainFinalVertex>(finalVertices.Length);
			finalVertexBuffer.SetData(finalVertices, finalVertices.Length);

			tileScales = new float[map.TextureCache.TileArrayTextures.Count];
			foreach (var ts in map.TextureCache.TileArrayTextures)
			{
				tileScales[ts.Value.Item1] = ts.Value.Item2;
			}
		}

		static float2 CalMaskUV(int2 idx, int2 topLeft)
		{
			float x = (float)(idx.X - topLeft.X) / SizeLimit;
			float y = (float)(idx.Y - topLeft.Y) / SizeLimit;

			return new float2(x, y);
		}

		public void Dispose()
		{
			blendVertexBuffer?.Dispose();
			BlendFramebuffer?.Dispose();
			MaskFramebuffer?.Dispose();
			finalVertexBuffer?.Dispose();
			tempMaskBuffer?.Dispose();
			EditorCachedMaskFramebuffer?.Dispose();
		}

		public static void SaveAsPng(ITexture texture, string path, string name)
		{
			var colorData = texture.GetData();

			new Png(colorData, SpriteFrameType.Bgra32, texture.Size.Width, texture.Size.Height).Save(path + name + ".png");
		}

		public byte[] MaskTextureData1()
		{
			ITexture texture;
			if (editorPaintSpots == null || EditorCachedMaskFramebuffer == null || editorPaintSpots.Count == 0)
				texture = MaskFramebuffer.GetTexture(0);
			else
				texture = EditorCachedMaskFramebuffer.GetTexture(0);
			return new Png(texture.GetData(), SpriteFrameType.Bgra32, texture.Size.Width, texture.Size.Height).Save();
		}

		public byte[] MaskTextureData2()
		{
			ITexture texture;
			if (editorPaintSpots == null || EditorCachedMaskFramebuffer == null || editorPaintSpots.Count == 0)
				texture = MaskFramebuffer.GetTexture(1);
			else
				texture = EditorCachedMaskFramebuffer.GetTexture(1);
			return new Png(texture.GetData(), SpriteFrameType.Bgra32, texture.Size.Width, texture.Size.Height).Save();
		}

		public byte[] MaskTextureData3()
		{
			ITexture texture;
			if (editorPaintSpots == null || EditorCachedMaskFramebuffer == null || editorPaintSpots.Count == 0)
				texture = MaskFramebuffer.GetTexture(2);
			else
				texture = EditorCachedMaskFramebuffer.GetTexture(2);
			return new Png(texture.GetData(), SpriteFrameType.Bgra32, texture.Size.Width, texture.Size.Height).Save();
		}
	}
}
