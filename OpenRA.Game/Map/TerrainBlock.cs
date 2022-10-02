using GlmSharp;
using OpenRA.FileFormats;
using OpenRA.Graphics;
using OpenRA.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using TrueSync;

namespace OpenRA
{
	public class MaskBrush
	{
		public string Name;

		/// <summary>
		/// brush texture type
		/// </summary>
		public int Type;

		public readonly int DefaultSize;

		/// <summary>
		/// in WDist
		/// </summary>
		public readonly int2 MapSize;

		public MaskBrush(string name, int type, int size, Map map)
		{
			Name = name;
			Type = type;
			DefaultSize = size;
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
					Type, layer, intensity);
			}

			return brushVertices;
		}
	}

	public class TerrainRenderBlock
	{
		/// <summary>
		/// Mask texture contains size*szie MiniCells
		/// </summary>
		public const int SizeLimit = 24;
		public const int TextureSize = 1024;

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

		public static IShader TerrainMaskShader { get; private set; }
		public static IShader TextureBlendShader { get; private set; }
		public static IShader TerrainShader { get; private set; }

		public IFrameBuffer MaskFramebuffer { get; private set; }
		static TerrainMaskVertex[] terrainMaskVertices;
		static IVertexBuffer<TerrainMaskVertex> maskVertexBuffer;

		BlendMode currentBrushMode = BlendMode.Additive;
		int numMaskVertices;
		TerrainMaskVertex[] tempMaskVertices;
		IVertexBuffer<TerrainMaskVertex> tempMaskBuffer;
		readonly List<PaintSpot> paintSpots = new List<PaintSpot>();

		public readonly IFrameBuffer BlendFramebuffer;
		readonly TerrainBlendingVertex[] blendVertices;
		readonly IVertexBuffer<TerrainBlendingVertex> blendVertexBuffer;

		readonly TerrainFinalVertex[] finalVertices;
		readonly IVertexBuffer<TerrainFinalVertex> finalVertexBuffer;

		bool needUpdateTexture = true;
		readonly float[] tileScales;
		readonly WorldRenderer worldRenderer;

		public TerrainRenderBlock(WorldRenderer worldRenderer, Map map, int2 tl, int2 br)
		{
			Range = new float2((float)SizeLimit / (map.VertexArrayWidth - 1), (float)SizeLimit / (map.VertexArrayHeight - 1));

			this.worldRenderer = worldRenderer;
			BlendFramebuffer = Game.Renderer.Context.CreateFrameBuffer(new Size(TextureSize, TextureSize), 2);

			if (TextureBlendShader == null)
				TextureBlendShader = Game.Renderer.Context.CreateUnsharedShader<TerrainBlendingShaderBindings>();
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

			// skip the first , last row and col
			// to avoid the stripe error
			for (int y = Math.Max(TopLeft.Y, 1); y <= Math.Min(BottomRight.Y, map.VertexArrayHeight - 3); y++)
			{
				for (int x = Math.Max(TopLeft.X, 1); x <= Math.Min(BottomRight.X, map.VertexArrayWidth - 3); x++)
				{
					MiniCell cell = Map.MiniCells[y, x];
					var vertTL = Map.TerrainVertices[cell.TL];
					var vertTR = Map.TerrainVertices[cell.TR];
					var vertBL = Map.TerrainVertices[cell.BL];
					var vertBR = Map.TerrainVertices[cell.BR];

					var maskuvTL = CalMaskUV(new int2(x, y), TopLeft);
					var maskuvTR = CalMaskUV(new int2(x + 1, y), TopLeft);
					var maskuvBL = CalMaskUV(new int2(x, y + 1), TopLeft);
					var maskuvBR = CalMaskUV(new int2(x + 1, y + 1), TopLeft);

					if (cell.Type == MiniCellType.TRBL)
					{
						blendVertices[index + 0] = new TerrainBlendingVertex(vertTR.UV, maskuvTR, vertTR.TBN, vertTR.Color, 1.0f, 1);
						blendVertices[index + 1] = new TerrainBlendingVertex(vertBL.UV, maskuvBL, vertBL.TBN, vertBL.Color, 1.0f, 1);
						blendVertices[index + 2] = new TerrainBlendingVertex(vertBR.UV, maskuvBR, vertBR.TBN, vertBR.Color, 1.0f, 1);

						blendVertices[index + 3] = new TerrainBlendingVertex(vertTR.UV, maskuvTR, vertTR.TBN, vertTR.Color, 1.0f, 1);
						blendVertices[index + 4] = new TerrainBlendingVertex(vertTL.UV, maskuvTL, vertTL.TBN, vertTL.Color, 1.0f, 1);
						blendVertices[index + 5] = new TerrainBlendingVertex(vertBL.UV, maskuvBL, vertBL.TBN, vertBL.Color, 1.0f, 1);
					}
					else
					{
						blendVertices[index + 0] = new TerrainBlendingVertex(vertTL.UV, maskuvTL, vertTL.TBN, vertTL.Color, 1.0f, 1);
						blendVertices[index + 1] = new TerrainBlendingVertex(vertBL.UV, maskuvBL, vertBL.TBN, vertBL.Color, 1.0f, 1);
						blendVertices[index + 2] = new TerrainBlendingVertex(vertBR.UV, maskuvBR, vertBR.TBN, vertBR.Color, 1.0f, 1);

						blendVertices[index + 3] = new TerrainBlendingVertex(vertTR.UV, maskuvTR, vertTR.TBN, vertTR.Color, 1.0f, 1);
						blendVertices[index + 4] = new TerrainBlendingVertex(vertTL.UV, maskuvTL, vertTL.TBN, vertTL.Color, 1.0f, 1);
						blendVertices[index + 5] = new TerrainBlendingVertex(vertBR.UV, maskuvBR, vertBR.TBN, vertBR.Color, 1.0f, 1);
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

		float2 CalMaskUV(int2 idx, int2 topLeft)
		{
			float x = (float)(idx.X - topLeft.X) / SizeLimit;
			float y = (float)(idx.Y - topLeft.Y) / SizeLimit;

			return new float2(x, y);
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
			}
		}

		public void Dispose()
		{
			blendVertexBuffer?.Dispose();
			BlendFramebuffer?.Dispose();
			MaskFramebuffer?.Dispose();
			finalVertexBuffer?.Dispose();
			tempMaskBuffer?.Dispose();
		}

		public void UpdateMask(int left, int right, int top, int bottom, bool init)
		{
			if (init)
			{

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

				Console.WriteLine(TopLeft + " paint update " + paintSpots.Count);
				needUpdateTexture = true;
				paintSpots.Clear();
			}
		}

		public void UpdateTexture(int left, int right, int top, int bottom, bool init)
		{
			if (TextureBlendShader == null)
				return;

			if (init)
			{

			}
			else if (LeftBound > right || RightBound < left)
				return;
			else if (BottomBound < top || TopBound > bottom)
				return;

			if (!needUpdateTexture)
				return;

			Console.WriteLine("Updating block texture: " + TopLeft);

			needUpdateTexture = false;

			BlendFramebuffer.Bind();

			TextureBlendShader.SetTexture("Mask123", MaskFramebuffer.GetTexture(0));

			TextureBlendShader.SetTexture("Mask456", MaskFramebuffer.GetTexture(1));

			TextureBlendShader.SetTexture("Mask789", MaskFramebuffer.GetTexture(2));

			var cloud = Map.TextureCache.Textures["MaskCloud"];
			TextureBlendShader.SetTexture(cloud.Item1, cloud.Item2.GetTexture());

			TextureBlendShader.SetTexture("Tiles", Map.TextureCache.TileTextureArray);
			TextureBlendShader.SetTexture("TilesNorm", Map.TextureCache.TileNormalTextureArray);

			TextureBlendShader.SetVec("Offset",
				topLeftOffset.X,
				topLeftOffset.Y);
			TextureBlendShader.SetVec("Range",
				bottomRightOffset.X - topLeftOffset.X,
				bottomRightOffset.Y - topLeftOffset.Y);

			TextureBlendShader.SetVecArray("TileScales", tileScales, 1, tileScales.Length);

			Game.Renderer.Context.SetBlendMode(BlendMode.None);
			TextureBlendShader.PrepareRender();

			Game.Renderer.DrawBatch(TextureBlendShader, blendVertexBuffer, 0, blendVertices.Length, PrimitiveType.TriangleList);

			Game.Renderer.Context.SetBlendMode(BlendMode.Alpha);
			BlendFramebuffer.Unbind();
		}

		public void RenderBlock(int left, int right, int top, int bottom)
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

			TerrainShader.SetTexture("Mask123", MaskFramebuffer.GetTexture(0));

			TerrainShader.SetTexture("WaterNormal",
							Map.TextureCache.Textures["WaterNormal"].Item2.GetTexture());

			TerrainShader.SetTexture("Caustics",
				Map.TextureCache.CausticsTextures[Math.Min((Game.LocalTick % 93) / 3,
				Map.TextureCache.CausticsTextures.Length - 1)].GetTexture());

			var cloud = Map.TextureCache.Textures["MaskCloud"];
			TerrainShader.SetTexture(cloud.Item1, cloud.Item2.GetTexture());

			TerrainShader.SetTexture("BakedTerrainTexture", BlendFramebuffer.Texture);
			TerrainShader.SetTexture("BakedTerrainNormalTexture", BlendFramebuffer.GetTexture(1));

			Game.Renderer.Context.SetBlendMode(BlendMode.None);
			TerrainShader.PrepareRender();

			Game.Renderer.DrawBatch(TerrainShader, finalVertexBuffer, 0, finalVertices.Length, PrimitiveType.TriangleList);

			Game.Renderer.Context.SetBlendMode(BlendMode.None);
		}

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
			in WPos pos, int size, int layer, int intensity)
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
					map.TerrainBlocks[y, x].paintSpots.Add(new PaintSpot(brush, pos, size, layer, intensity));
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

		public static void SaveAsPng(ITexture texture, string path, string name)
		{
			var colorData = texture.GetData();

			new Png(colorData, SpriteFrameType.Bgra32, texture.Size.Width, texture.Size.Height).Save(path + name + ".png");
		}
	}
}
