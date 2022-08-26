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
using System.IO;
using System.Linq;
using System.Reflection;
using GlmSharp;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Effects;
using OpenRA.Primitives;
using OpenRA.Traits;


namespace OpenRA.Mods.Common.Traits
{
	public struct MapSmudge
	{
		public string Type;
		public int Depth;
	}

	[TraitLocation(SystemActors.World)]
	[Desc("Attach this to the world actor.", "Order of the layers defines the Z sorting.")]
	public class SmudgeLayerInfo : TraitInfo
	{
		public readonly string Type = "Scorch";

		[Desc("Sprite sequence name")]
		public readonly string Sequence = "scorch";

		[Desc("Chance of smoke rising from the ground")]
		public readonly int SmokeChance = 0;

		[Desc("Smoke sprite image name")]
		public readonly string SmokeImage = null;

		[SequenceReference(nameof(SmokeImage), allowNullImage: true)]
		[Desc("Smoke sprite sequences randomly chosen from")]
		public readonly string[] SmokeSequences = Array.Empty<string>();

		[PaletteReference]
		public readonly string SmokePalette = "effect";

		[PaletteReference]
		public readonly string Palette = TileSet.TerrainPaletteInternalName;

		[FieldLoader.LoadUsing(nameof(LoadInitialSmudges))]
		public readonly Dictionary<CPos, MapSmudge> InitialSmudges;

		public readonly int[] Size = { 1024, 1448 };

		public readonly int MaxCountPerCell = 4;

		public readonly int[] FadeTick = { 600, 800 };

		public readonly int ZOffsetMin = 256;
		public readonly int ZOffsetAdd = 6;
		public readonly int ZOffsetMax = 3030;

		public static object LoadInitialSmudges(MiniYaml yaml)
		{
			var nd = yaml.ToDictionary();
			var smudges = new Dictionary<CPos, MapSmudge>();
			if (nd.TryGetValue("InitialSmudges", out var smudgeYaml))
			{
				foreach (var node in smudgeYaml.Nodes)
				{
					try
					{
						var cell = FieldLoader.GetValue<CPos>("key", node.Key);
						var parts = node.Value.Value.Split(',');
						var type = parts[0];
						var depth = FieldLoader.GetValue<int>("depth", parts[1]);
						smudges.Add(cell, new MapSmudge { Type = type, Depth = depth });
					}
					catch { }
				}
			}

			return smudges;
		}

		public override object Create(ActorInitializer init) { return new SmudgeLayer(init.Self, this); }
	}

	public class SmudgeLayer : IRenderOverlay, IWorldLoaded, ITick, ITickRender, INotifyActorDisposing
	{
		class CellSmudge
		{
			public Smudge[] Smudges;
			HashSet<int> indicesUnused = new HashSet<int>();

			public int Count { get; private set; }

			public CellSmudge(int max)
			{
				Smudges = new Smudge[max];
				for (int i = 0; i < max; i++)
				{
					indicesUnused.Add(i);
				}

				Count = 0;
			}

			public void Add(Smudge smudge)
			{
				if (indicesUnused.Count == 0)
					return;
				var index = indicesUnused.First();
				Smudges[index] = smudge;
				indicesUnused.Remove(index);
				Count++;
			}

			public void Clear()
			{
				for (int i = 0; i < Smudges.Length; i++)
				{
					indicesUnused.Add(i);
					Smudges[i] = Smudge.NoSmudge;
				}

				Count = 0;
			}

			public void Remove(int i)
			{
				if (indicesUnused.Contains(i))
					return;

				Smudges[i] = Smudge.NoSmudge;
				indicesUnused.Add(i);
				Count--;
			}

			public void TickAt(int i)
			{
				if (Smudges[i].LifeTime <= 0)
					return;

				Smudges[i].AlphaTint = MathF.Min(MathF.Max((float)Smudges[i].Tick / (Smudges[i].LifeTime / 2), 0f), 1f);
				Smudges[i].Tick--;

				if (Smudges[i].AlphaTint <= 0)
					Remove(i);
			}
		}

		struct Smudge
		{
			//public string Type;
			//public int Depth;
			//public ISpriteSequence Sequence;

			public static readonly Smudge NoSmudge = new Smudge(0);

			/// <summary>
			/// LifeTime == 0 is NoSmudge, LifeTime == -1 is static
			/// </summary>
			public readonly int LifeTime;
			public readonly WPos Pos;

			public readonly int Size;

			public OverlayVertex[] OverlayVertices;
			public float AlphaTint;
			public int Tick;

			public Smudge(int lifeTime)
			{
				LifeTime = lifeTime;
				OverlayVertices = Array.Empty<OverlayVertex>();
				AlphaTint = 1;
				Tick = lifeTime;
				Pos = WPos.Zero;
				Size = 0;
			}

			public Smudge(World world, SmudgeLayer smudgeLayer, WPos pos)
			{
				if (smudgeLayer.Info.FadeTick.Length > 1)
					LifeTime = world.SharedRandom.Next(smudgeLayer.Info.FadeTick[0], smudgeLayer.Info.FadeTick[1]);
				else
					LifeTime = smudgeLayer.Info.FadeTick[0];

				if (smudgeLayer.Info.Size.Length > 1)
					Size = world.SharedRandom.Next(smudgeLayer.Info.Size[0], smudgeLayer.Info.Size[1]);
				else
					Size = smudgeLayer.Info.Size[0];

				OverlayVertices = smudgeLayer.CreateTileOverlayVertex(pos, smudgeLayer.map, Size, smudgeLayer.nowZOffset * Game.Renderer.World3DRenderer.InverseCameraFrontMeterPerWPos);
				AlphaTint = 1;
				Tick = LifeTime;
				Pos = pos;
			}

			public Smudge(World world, SmudgeLayer smudgeLayer, WPos pos, int lifeTime)
			{
				if (smudgeLayer.Info.Size.Length > 1)
					Size = world.SharedRandom.Next(smudgeLayer.Info.Size[0], smudgeLayer.Info.Size[1]);
				else
					Size = smudgeLayer.Info.Size[0];

				LifeTime = lifeTime;
				OverlayVertices = smudgeLayer.CreateTileOverlayVertex(pos, smudgeLayer.map, Size, smudgeLayer.nowZOffset * Game.Renderer.World3DRenderer.InverseCameraFrontMeterPerWPos);
				AlphaTint = 1;
				Tick = LifeTime;
				Pos = pos;
			}

			public bool InBound(in WPos tr, in WPos br)
			{
				var projectY = Pos.Y - Pos.Z * Game.Renderer.World3DRenderer.InverseCameraFront.y / Game.Renderer.World3DRenderer.InverseCameraFront.z;
				if ((Pos.X + Size > tr.X && Pos.X - Size < br.X) &&
					(projectY + Size > tr.Y &&
					projectY - Size < br.Y))
					return true;
				else
					return false;
			}

		}

		OverlayVertex[] CreateTileOverlayVertex(in WPos pos, Map map, int size, in vec3 zOffset)
		{
			var width = size;
			var height = size;

			var viewOffset = new float3(zOffset.x, zOffset.y, zOffset.z);
			var w = width / 2;
			var h = height / 2;
			var TL = new int2((pos.X - w - MapGrid.MapMiniCellWidth) / MapGrid.MapMiniCellWidth + 1,
				(pos.Y - h - MapGrid.MapMiniCellWidth) / MapGrid.MapMiniCellWidth + 1);
			var BR = new int2((pos.X + w + MapGrid.MapMiniCellWidth) / MapGrid.MapMiniCellWidth,
				(pos.Y + h + MapGrid.MapMiniCellWidth) / MapGrid.MapMiniCellWidth);

			// 6 vertex one minicell
			int count = (BR.X - TL.X) * (BR.Y - TL.Y) * 6;
			var TLX = pos.X - w;
			var TLY = pos.Y - h;
			OverlayVertex[] overlayVertices = new OverlayVertex[count];

			{
				int i = 0;
				for (int y = TL.Y; y < BR.Y; y++)
					for (int x = TL.X; x < BR.X; x++)
					{
						var iLT = x + y * map.VertexArrayWidth;
						var iRT = x + 1 + y * map.VertexArrayWidth;
						var iLB = x + (y + 1) * map.VertexArrayWidth;
						var iRB = x + 1 + (y + 1) * map.VertexArrayWidth;
						var index = iRT;
						if (TL.X % 2 == TL.Y % 2)
						{
							// ------------
							// |           / |
							// |      /      |
							// |  /          |
							// ------------
							index = iRT;
							overlayVertices[i] = new OverlayVertex(map.VertexPos[index] + viewOffset, map.VertexTBN[index], CalUV(index, TLX, TLY, width, height));
							index = iLT;
							overlayVertices[i + 1] = new OverlayVertex(map.VertexPos[index] + viewOffset, map.VertexTBN[index], CalUV(index, TLX, TLY, width, height));
							index = iLB;
							overlayVertices[i + 2] = new OverlayVertex(map.VertexPos[index] + viewOffset, map.VertexTBN[index], CalUV(index, TLX, TLY, width, height));

							index = iRT;
							overlayVertices[i + 3] = new OverlayVertex(map.VertexPos[index] + viewOffset, map.VertexTBN[index], CalUV(index, TLX, TLY, width, height));
							index = iLB;
							overlayVertices[i + 4] = new OverlayVertex(map.VertexPos[index] + viewOffset, map.VertexTBN[index], CalUV(index, TLX, TLY, width, height));
							index = iRB;
							overlayVertices[i + 5] = new OverlayVertex(map.VertexPos[index] + viewOffset, map.VertexTBN[index], CalUV(index, TLX, TLY, width, height));
						}
						else
						{
							// ------------
							// |  \          |
							// |      \      |
							// |          \  |
							// ------------

							index = iRT;
							overlayVertices[i] = new OverlayVertex(map.VertexPos[index] + viewOffset, map.VertexTBN[index], CalUV(index, TLX, TLY, width, height));
							index = iLT;
							overlayVertices[i + 1] = new OverlayVertex(map.VertexPos[index] + viewOffset, map.VertexTBN[index], CalUV(index, TLX, TLY, width, height));
							index = iRB;
							overlayVertices[i + 2] = new OverlayVertex(map.VertexPos[index] + viewOffset, map.VertexTBN[index], CalUV(index, TLX, TLY, width, height));

							index = iLT;
							overlayVertices[i + 3] = new OverlayVertex(map.VertexPos[index] + viewOffset, map.VertexTBN[index], CalUV(index, TLX, TLY, width, height));
							index = iLB;
							overlayVertices[i + 4] = new OverlayVertex(map.VertexPos[index] + viewOffset, map.VertexTBN[index], CalUV(index, TLX, TLY, width, height));
							index = iRB;
							overlayVertices[i + 5] = new OverlayVertex(map.VertexPos[index] + viewOffset, map.VertexTBN[index], CalUV(index, TLX, TLY, width, height));
						}

						i += 6;
					}
			}

			return overlayVertices;
		}

		float2 CalUV(int index, int TLX, int TLY, int width, int height)
		{
			var vpos = map.VertexWPos[index];
			return new float2((float)(vpos.X - TLX) / width, (float)(vpos.Y - TLY) / height);
		}

		public readonly SmudgeLayerInfo Info;
		readonly Dictionary<CPos, CellSmudge> tiles = new Dictionary<CPos, CellSmudge>();
		readonly Dictionary<CPos, CellSmudge> dirty = new Dictionary<CPos, CellSmudge>();
		readonly Dictionary<string, ISpriteSequence> smudges = new Dictionary<string, ISpriteSequence>();
		readonly World world;
		readonly Map map;
		readonly bool hasSmoke;

		TerrainSpriteLayer render;
		PaletteReference paletteReference;
		bool disposed;

		int nowZOffset = 10;
		public SmudgeLayer(Actor self, SmudgeLayerInfo info)
		{
			Info = info;
			world = self.World;
			map = world.Map;
			hasSmoke = !string.IsNullOrEmpty(info.SmokeImage) && info.SmokeSequences.Length > 0;
			nowZOffset = info.ZOffsetMin;

			var sequenceProvider = world.Map.Rules.Sequences;
			var types = sequenceProvider.Sequences(Info.Sequence);
			foreach (var t in types)
				smudges.Add(t, sequenceProvider.GetSequence(Info.Sequence, t));
		}

		public void WorldLoaded(World w, WorldRenderer wr)
		{
			var sprites = smudges.Values.SelectMany(v => Exts.MakeArray(v.Length, x => v.GetSprite(x))).ToList();
			var sheet = sprites[0].Sheet;
			var blendMode = sprites[0].BlendMode;
			var emptySprite = new Sprite(sheet, Rectangle.Empty, TextureChannel.Alpha, spriteMeshType: SpriteMeshType.Plane);

			if (sprites.Any(s => s.BlendMode != blendMode))
				throw new InvalidDataException("Smudges specify different blend modes. "
					+ "Try using different smudge types for smudges that use different blend modes.");

			paletteReference = wr.Palette(Info.Palette);
			render = new TerrainSpriteLayer(w, wr, emptySprite, blendMode, w.Type != WorldType.Editor);

			// Add map smudges
			foreach (var kv in Info.InitialSmudges)
			{
				var s = kv.Value;
				if (!smudges.ContainsKey(s.Type))
					continue;

				var seq = smudges[s.Type];
				var cell = new CellSmudge(Info.MaxCountPerCell);
				nowZOffset = nowZOffset > Info.ZOffsetMax ? Info.ZOffsetMin : nowZOffset + Info.ZOffsetAdd;
				//var smudge = new Smudge
				//{
				//	Type = s.Type,
				//	Depth = s.Depth,
				//	Sequence = seq,
				//	OverlayVertices = CreateTileOverlayVertex(map.CenterOfCell(kv.Key), map, count * Game.Renderer.World3DRenderer.InverseCameraFrontMeterPerWPos)
				//};
				var smudge = new Smudge(world, this, map.CenterOfCell(kv.Key), -1);
				cell.Add(smudge);

				tiles.Add(kv.Key, cell);
				render.Update(kv.Key, seq, paletteReference, s.Depth, true);
			}
		}

		public void AddSmudge(CPos loc, WPos pos)
		{
			if (!world.Map.Contains(loc))
				return;

			if (hasSmoke && Game.CosmeticRandom.Next(0, 100) <= Info.SmokeChance)
				world.AddFrameEndTask(w => w.Add(new SpriteEffect(
					pos, w, Info.SmokeImage, Info.SmokeSequences.Random(w.SharedRandom), Info.SmokePalette)));

			nowZOffset = nowZOffset > Info.ZOffsetMax ? Info.ZOffsetMin : nowZOffset + Info.ZOffsetAdd;
			//var st = smudges.Keys.Random(Game.CosmeticRandom);

			if (!dirty.ContainsKey(loc))
			{
				dirty[loc] = new CellSmudge(Info.MaxCountPerCell);

				dirty[loc].Add(new Smudge(world, this, pos));
			}
			else if (dirty[loc].Count < Info.MaxCountPerCell)
			{
				dirty[loc].Add(new Smudge(world, this, pos));
			}
		}

		public void RemoveSmudge(CPos loc)
		{
			if (!world.Map.Contains(loc))
				return;

			//var tile = dirty.ContainsKey(loc) ? dirty[loc] : default(Smudge);

			//// Setting Sequence to null to indicate a deleted smudge.
			//tile.Sequence = null;
			//dirty[loc] = tile;

			if (dirty.ContainsKey(loc))
			{
				dirty[loc].Clear();
			}
		}

		void ITickRender.TickRender(WorldRenderer wr, Actor self)
		{
			var remove = new List<CPos>();
			foreach (var kv in dirty)
			{
				if (!world.FogObscures(kv.Key))
				{
					// A null Sequence
					if (kv.Value.Count == 0)
					{
						tiles.Remove(kv.Key);
						//render.Clear(kv.Key);
						remove.Add(kv.Key);
					}
					else
					{
						tiles[kv.Key] = kv.Value;
					}

					//remove.Add(kv.Key);
				}
			}

			foreach (var r in remove)
				dirty.Remove(r);
		}

		public void Tick(Actor self)
		{
			foreach (var kv in dirty)
			{
				if (kv.Value.Count == 0)
					continue;

				for (int i = 0; i < kv.Value.Smudges.Length; i++)
				{
					kv.Value.TickAt(i);
				}
			}
		}

		readonly float3 dark = new float3(-0.5f, -0.5f, -0.5f);

		void IRenderOverlay.ModifyTerrainRender(WorldRenderer wr) {
			//foreach (var kv in tiles)
			//{
			//	if (kv.Value.Smudges.Count == 0)
			//		wr.TerrainRenderer.ModifyCellTint(kv.Key, float3.Zero);
			//	else
			//		wr.TerrainRenderer.ModifyCellTint(kv.Key, dark);
			//}
		}

		void IRenderOverlay.Render(WorldRenderer wr)
		{
			//render.Draw(wr.Viewport, false);

			//Game.Renderer.MapRenderer.Flush();
			//Console.WriteLine("tiles.Count: " + tiles.Count);

			Game.Renderer.MapRenderer.SetTextures(wr.World, UsageType.Smudge);

			var tp = wr.Viewport.TopLeftPosition;
			var br = wr.Viewport.BottomRightPosition;

			if (tiles.Count == 0)
				return;

			foreach (var kv in tiles)
			{
				if (kv.Value.Count != 0)
				{
					for (int i = 0; i < kv.Value.Smudges.Length; i++)
					{
						if (kv.Value.Smudges[i].LifeTime == 0 || !kv.Value.Smudges[i].InBound(tp, br))
							continue;

						Game.Renderer.MapRenderer.DrawOverlay(kv.Value.Smudges[i].OverlayVertices, kv.Value.Smudges[i].AlphaTint);
					}
				}
			}
		}

		void INotifyActorDisposing.Disposing(Actor self)
		{
			if (disposed)
				return;

			render.Dispose();
			disposed = true;
		}
	}
}
