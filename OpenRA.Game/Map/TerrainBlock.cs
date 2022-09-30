using GlmSharp;
using OpenRA.FileFormats;
using OpenRA.Graphics;
using OpenRA.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;

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

			var range = blockBR - blockTL;

			float2 centerPos = new float2(
				((float)pos.X / MapSize.X - blockTL.X) / range.X * 2f - 1f,
				((float)pos.Y / MapSize.Y - blockTL.Y) / range.Y * 2f - 1f);
			float renderSizeX = (float)size / MapSize.X / range.X;
			float renderSizeY = (float)size / MapSize.Y / range.Y;

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
		public const int SizeLimit = 32;
		public const int TextureSize = 1024;

		public static int MiniCellPix => TextureSize / SizeLimit;

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
		public readonly int UpBound;

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
		bool needInit = true;
		readonly float[] tileScales;

		public TerrainRenderBlock(Map map, int2 tl, int2 br)
		{
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
			UpBound = (tl.Y + 1) * 724;
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

			// skip the first row and col
			// to avoid the stripe error
			for (int y = Math.Max(TopLeft.Y, 1); y <= BottomRight.Y; y++)
			{
				for (int x = Math.Max(TopLeft.X, 1); x <= BottomRight.X; x++)
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

		void InitMask()
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

				// if (map.Mask123 != null && map.Mask456 != null && map.Mask789 != null)
				// {
				// Console.WriteLine("Completed");
				// }
				// else
				{
					Console.WriteLine("Painting Spot on mask at each cell");
					MaskFramebuffer.Bind();

					Game.Renderer.SetFaceCull(FaceCullFunc.None);
					Game.Renderer.EnableDepthWrite(false);
					Game.Renderer.DisableDepthTest();

					TerrainMaskShader.SetTexture("Brushes", Map.TextureCache.BrushTextureArray);
					TerrainMaskShader.SetBool("InitWithTextures", false);

					Game.Renderer.Context.SetBlendMode(BlendMode.None);
					TerrainMaskShader.PrepareRender();

					Game.Renderer.DrawBatch(TerrainMaskShader, maskVertexBuffer, 0, terrainMaskVertices.Length, PrimitiveType.TriangleList);

					foreach (var uv in Map.MapCells)
					{
						var type = Map.Rules.TerrainInfo.GetTerrainInfo(Map.Tiles[uv]);
						var typename = Map.Rules.TerrainInfo.TerrainTypes[type.TerrainType].Type;
						var id = Map.Tiles[uv].Type;
						int layer = 8;

						switch (typename)
						{
							case "Rough":
								layer = 6;
								break;
							case "DirtRoad":
								layer = 5;
								break;
							case "Cliff":
								layer = 4;
								break;
							case "Impassable":
								layer = 4;
								break;
							case "Rock":
								layer = 4;
								break;
							case "Rail":
								layer = 2;
								break;
							case "Road":
								layer = 2;
								break;
							case "Bridge":
								layer = 2;
								break;
							case "Water":
								layer = 0;
								break;
							default:
								break;
						}

						if (id >= 108 && id <= 149)
						{
							// shore
							if (typename == "Water")
								layer = 0;
							else
								layer = 1;
						}
						else if (id >= 626 && id <= 642)
						{
							// grass
							layer = 3;
						}
						else if (id >= 535 && id <= 551)
						{
							// sand
							layer = 1;
						}
						else if (id >= 150 && id <= 166)
						{
							// rough
							layer = 6;
						}
						else if (id >= 552 && id <= 561)
						{
							// crack ground
							layer = 7;
						}

						var brush = Map.TextureCache.AllBrushes.First().Value;

						PaintMask(brush, Map.CenterOfCell(uv), brush.DefaultSize, layer, 255);
					}

					FlushBrush(currentBrushMode);

					Game.Renderer.EnableDepthBuffer();
					Game.Renderer.EnableDepthWrite(true);

					Game.Renderer.Context.SetBlendMode(BlendMode.Alpha);

					MaskFramebuffer.Unbind();

					Console.WriteLine("Completed");
					//Console.WriteLine("Saving Mask Texture as PNG at ./MapToPng/");

					//SaveAsPng(MaskFramebuffer.GetTexture(0), "./MapToPng/", "Mask123");
					//SaveAsPng(MaskFramebuffer.GetTexture(1), "./MapToPng/", "Mask456");
					//SaveAsPng(MaskFramebuffer.GetTexture(2), "./MapToPng/", "Mask789");

					//Console.WriteLine("Saved");
				}
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

		public void UpdateMask(int left, int right)
		{
			InitMask();

			if (LeftBound > right || RightBound < left)
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

				paintSpots.Clear();
				Console.WriteLine("paint update");
			}
		}

		public void UpdateTexture(int left, int right)
		{
			if (TextureBlendShader == null)
				return;

			if (needInit)
			{

			}
			else if (LeftBound > right || RightBound < left)
				return;

			if (!needUpdateTexture)
				return;

			Console.WriteLine("Updating block texture: " + TopLeft);

			needInit = false;
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

		public void RenderBlock(int left, int right)
		{
			if (TerrainShader == null)
				return;

			if (LeftBound > right || RightBound < left)
				return;

			TerrainShader.SetVec("Offset",
				topLeftOffset.X,
				topLeftOffset.Y);
			TerrainShader.SetVec("Range",
				bottomRightOffset.X - topLeftOffset.X,
				bottomRightOffset.Y - topLeftOffset.Y);
			TerrainShader.SetFloat("WaterUVOffset", (float)(Game.LocalTick % 256) / 256);

			TerrainShader.SetTexture("Mask123", MaskFramebuffer.GetTexture(0));

			TerrainShader.SetTexture("WaterNormal",
							Map.TextureCache.Textures["WaterNormal"].Item2.GetTexture());

			TerrainShader.SetTexture("Caustics",
				Map.TextureCache.CausticsTextures[Math.Min((Game.LocalTick % 93) / 3,
				Map.TextureCache.CausticsTextures.Length - 1)].GetTexture());

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
			Console.WriteLine("paint at " + pos.X + "," + pos.Y + " with size: " + size + " layer: " + layer);

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
						pos.Y + size < map.TerrainBlocks[y, x].UpBound)
						continue;
					map.TerrainBlocks[y, x].needUpdateTexture = true;
					map.TerrainBlocks[y, x].paintSpots.Add(new PaintSpot(brush, pos, size, layer, intensity));
					Console.WriteLine("block " + y + "," + x + " need update");
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

			Game.Renderer.SetShadowParams(TerrainShader, w3dr);
			Game.Renderer.SetLightParams(TerrainShader, w3dr);
		}

		public static void SaveAsPng(ITexture texture, string path, string name)
		{
			var colorData = texture.GetData();

			new Png(colorData, SpriteFrameType.Bgra32, texture.Size.Width, texture.Size.Height).Save(path + name + ".png");
		}
	}
}
