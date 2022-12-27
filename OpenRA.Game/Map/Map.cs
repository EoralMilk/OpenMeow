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
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Text.RegularExpressions;
using GlmSharp;
using OpenRA.Effects;
using OpenRA.FileFormats;
using OpenRA.FileSystem;
using OpenRA.Graphics;
using OpenRA.Primitives;
using OpenRA.Support;
using OpenRA.Traits;
using TrueSync;

namespace OpenRA
{
	readonly struct BinaryDataHeader
	{
		public readonly byte Format;
		public readonly uint TilesOffset;
		public readonly uint HeightsOffset;
		public readonly uint ResourcesOffset;

		public BinaryDataHeader(Stream s, int2 expectedSize)
		{
			Format = s.ReadUInt8();
			var width = s.ReadUInt16();
			var height = s.ReadUInt16();
			if (width != expectedSize.X || height != expectedSize.Y)
				throw new InvalidDataException("Invalid tile data");

			if (Format == 1)
			{
				TilesOffset = 5;
				HeightsOffset = 0;
				ResourcesOffset = (uint)(3 * width * height + 5);
			}
			else if (Format == 2)
			{
				TilesOffset = s.ReadUInt32();
				HeightsOffset = s.ReadUInt32();
				ResourcesOffset = s.ReadUInt32();
			}
			else
				throw new InvalidDataException($"Unknown binary map format '{Format}'");
		}
	}

	[Flags]
	public enum MapVisibility
	{
		Lobby = 1,
		Shellmap = 2,
		MissionSelector = 4
	}

	class MapField
	{
		enum Type { Normal, NodeList, MiniYaml }
		readonly FieldInfo field;
		readonly PropertyInfo property;
		readonly Type type;

		readonly string key;
		readonly string fieldName;
		readonly bool required;
		readonly string ignoreIfValue;

		public MapField(string key, string fieldName = null, bool required = true, string ignoreIfValue = null)
		{
			this.key = key;
			this.fieldName = fieldName ?? key;
			this.required = required;
			this.ignoreIfValue = ignoreIfValue;

			field = typeof(Map).GetField(this.fieldName);
			property = typeof(Map).GetProperty(this.fieldName);
			if (field == null && property == null)
				throw new InvalidOperationException("Map does not have a field/property " + fieldName);

			var t = field != null ? field.FieldType : property.PropertyType;
			type = t == typeof(List<MiniYamlNode>) ? Type.NodeList :
				t == typeof(MiniYaml) ? Type.MiniYaml : Type.Normal;
		}

		public void Deserialize(Map map, List<MiniYamlNode> nodes)
		{
			var node = nodes.FirstOrDefault(n => n.Key == key);
			if (node == null)
			{
				if (required)
					throw new YamlException($"Required field `{key}` not found in map.yaml");
				return;
			}

			if (field != null)
			{
				if (type == Type.NodeList)
					field.SetValue(map, node.Value.Nodes);
				else if (type == Type.MiniYaml)
					field.SetValue(map, node.Value);
				else
					FieldLoader.LoadField(map, fieldName, node.Value.Value);
			}

			if (property != null)
			{
				if (type == Type.NodeList)
					property.SetValue(map, node.Value.Nodes, null);
				else if (type == Type.MiniYaml)
					property.SetValue(map, node.Value, null);
				else
					FieldLoader.LoadField(map, fieldName, node.Value.Value);
			}
		}

		public void Serialize(Map map, List<MiniYamlNode> nodes)
		{
			var value = field != null ? field.GetValue(map) : property.GetValue(map, null);
			if (type == Type.NodeList)
			{
				var listValue = (List<MiniYamlNode>)value;
				if (required || listValue.Count > 0)
					nodes.Add(new MiniYamlNode(key, null, listValue));
			}
			else if (type == Type.MiniYaml)
			{
				var yamlValue = (MiniYaml)value;
				if (required || (yamlValue != null && (yamlValue.Value != null || yamlValue.Nodes.Count > 0)))
					nodes.Add(new MiniYamlNode(key, yamlValue));
			}
			else
			{
				var formattedValue = FieldSaver.FormatValue(value);
				if (required || formattedValue != ignoreIfValue)
					nodes.Add(new MiniYamlNode(key, formattedValue));
			}
		}
	}

	public class Map : IReadOnlyFileSystem
	{
		public const int SupportedMapFormat = 11;
		public const int CurrentMapFormat = 12;
		const short InvalidCachedTerrainIndex = -1;

		/// <summary>Defines the order of the fields in map.yaml</summary>
		static readonly MapField[] YamlFields =
		{
			new MapField("MapFormat"),
			new MapField("RequiresMod"),
			new MapField("Title"),
			new MapField("Author"),
			new MapField("Tileset"),
			new MapField("TileTexSet", required: false, ignoreIfValue: "DEFAULT"),
			new MapField("MapSize"),
			new MapField("Bounds"),
			new MapField("Visibility"),
			new MapField("Categories"),
			new MapField("Translations", required: false, ignoreIfValue: ""),
			new MapField("LockPreview", required: false, ignoreIfValue: "False"),
			new MapField("Players", "PlayerDefinitions"),
			new MapField("Actors", "ActorDefinitions"),
			new MapField("Rules", "RuleDefinitions", required: false),
			new MapField("Sequences", "SequenceDefinitions", required: false),
			new MapField("ModelSequences", "ModelSequenceDefinitions", required: false),
			new MapField("MeshSequences", "MeshSequenceDefinitions", required: false),
			new MapField("Weapons", "WeaponDefinitions", required: false),
			new MapField("Voices", "VoiceDefinitions", required: false),
			new MapField("Music", "MusicDefinitions", required: false),
			new MapField("Notifications", "NotificationDefinitions", required: false),
		};

		// Format versions
		public int MapFormat { get; private set; }
		public readonly byte TileFormat = 2;

		// Standard yaml metadata
		public string RequiresMod;
		public string Title;
		public string Author;
		public string Tileset;
		public string TileTexSet;
		public bool LockPreview;
		public Rectangle Bounds;
		public MapVisibility Visibility = MapVisibility.Lobby;
		public string[] Categories = { "Conquest" };
		public string[] Translations;

		public int2 MapSize { get; private set; }

		// Player and actor yaml. Public for access by the map importers and lint checks.
		public List<MiniYamlNode> PlayerDefinitions = new List<MiniYamlNode>();
		public List<MiniYamlNode> ActorDefinitions = new List<MiniYamlNode>();

		// Custom map yaml. Public for access by the map importers and lint checks
		public readonly MiniYaml RuleDefinitions;
		public readonly MiniYaml SequenceDefinitions;
		public readonly MiniYaml ModelSequenceDefinitions;
		public readonly MiniYaml MeshSequenceDefinitions;
		public readonly MiniYaml WeaponDefinitions;
		public readonly MiniYaml VoiceDefinitions;
		public readonly MiniYaml MusicDefinitions;
		public readonly MiniYaml NotificationDefinitions;

		public readonly Dictionary<CPos, TerrainTile> ReplacedInvalidTerrainTiles = new Dictionary<CPos, TerrainTile>();

		// Generated data
		public readonly MapGrid Grid;
		public IReadOnlyPackage Package { get; private set; }
		public string Uid { get; private set; }

		public Ruleset Rules { get; private set; }
		public bool InvalidCustomRules { get; private set; }
		public Exception InvalidCustomRulesException { get; private set; }

		/// <summary>
		/// The top-left of the playable area in projected world coordinates
		/// This is a hacky workaround for legacy functionality.  Do not use for new code.
		/// </summary>
		public WPos ProjectedTopLeft { get; private set; }

		/// <summary>
		/// The bottom-right of the playable area in projected world coordinates
		/// This is a hacky workaround for legacy functionality.  Do not use for new code.
		/// </summary>
		public WPos ProjectedBottomRight { get; private set; }

		public CellLayer<TerrainTile> Tiles { get; private set; }
		public CellLayer<ResourceTile> Resources { get; private set; }
		public CellLayer<byte> HeightStep { get; private set; }
		protected CellLayer<byte> Ramp { get; private set; }
		public CellLayer<byte> CustomTerrain { get; private set; }

		public CellLayer<CellInfo> CellInfos { get; private set; }

		public TerrainVertex[] TerrainVertices { get; private set; }
		public MiniCell[,] MiniCells { get; private set; }
		public TerrainRenderBlock[,] TerrainBlocks { get; private set; }
		public int BlocksArrayWidth { get; private set; }
		public int BlocksArrayHeight { get; private set; }

		public int VertexArrayWidth { get; private set; }
		public int VertexArrayHeight { get; private set; }

		public MapTextureCache TextureCache;

		//// all TerrainPatch on map
		//public readonly TerrainPatch[] MapTerrainPatches;

		//// one cell may coved more than one TerrainPatch
		//public readonly Dictionary<MPos, TerrainPatchInstance[]> PatchOnCell;

		public PPos[] ProjectedCells { get; private set; }
		public MPos[] MapCells { get; private set; }
		public CellRegion AllCells { get; private set; }
		public CPos[] AllCellsArray { get; private set; }
		public List<CPos> AllEdgeCells { get; private set; }

		public event Action<CPos> CellProjectionChanged;

		// Internal data
		readonly ModData modData;
		CellLayer<short> cachedTerrainIndexes;
		bool initializedCellProjection;
		CellLayer<PPos[]> cellProjection;
		CellLayer<List<MPos>> inverseCellProjection;
		CellLayer<byte> projectedHeight;
		Rectangle projectionSafeBounds;

		const string TerrainMeshFileName = "Terrain.tm";

		internal Translation Translation;

		public static string ComputeUID(IReadOnlyPackage package)
		{
			return ComputeUID(package, GetMapFormat(package));
		}

		static string ComputeUID(IReadOnlyPackage package, int format)
		{
			// UID is calculated by taking an SHA1 of the yaml and binary data
			var requiredFiles = new[] { "map.yaml", "map.bin" };
			var contents = package.Contents.ToList();
			foreach (var required in requiredFiles)
				if (!contents.Contains(required))
					throw new FileNotFoundException($"Required file {required} not present in this map");

			var streams = new List<Stream>();
			try
			{
				foreach (var filename in contents)
					if (filename.EndsWith(".yaml") || filename.EndsWith(".bin") || filename.EndsWith(".lua") || (format >= 12 && filename == "map.png"))
						streams.Add(package.GetStream(filename));

				// Take the SHA1
				if (streams.Count == 0)
					return CryptoUtil.SHA1Hash(Array.Empty<byte>());

				var merged = streams[0];
				for (var i = 1; i < streams.Count; i++)
					merged = new MergedStream(merged, streams[i]);

				return CryptoUtil.SHA1Hash(merged);
			}
			finally
			{
				foreach (var stream in streams)
					stream.Dispose();
			}
		}

		static int GetMapFormat(IReadOnlyPackage p)
		{
			using (var yamlStream = p.GetStream("map.yaml"))
			{
				if (yamlStream == null)
					throw new FileNotFoundException("Required file map.yaml not present in this map");

				foreach (var line in yamlStream.ReadAllLines())
				{
					// PERF This is a way to get MapFormat without expensive yaml parsing
					var search = Regex.Match(line, "^MapFormat:\\s*(\\d*)\\s*$");
					if (search.Success && search.Groups.Count > 0)
						return FieldLoader.GetValue<int>("MapFormat", search.Groups[1].Value);
				}
			}

			throw new InvalidDataException("MapFormat is not defined");
		}

		/// <summary>
		/// Initializes a new map created by the editor or importer.
		/// The map will not receive a valid UID until after it has been saved and reloaded.
		/// </summary>
		public Map(ModData modData, ITerrainInfo terrainInfo, int width, int height)
		{
			this.modData = modData;
			var size = new Size(width, height);
			Grid = modData.Manifest.Get<MapGrid>();

			Title = "Name your map here";
			Author = "Your name here";

			MapSize = new int2(size);
			Tileset = terrainInfo.Id;
			TileTexSet = "DEFAULT";

			// Empty rules that can be added to by the importers.
			// Will be dropped on save if nothing is added to it
			RuleDefinitions = new MiniYaml("");

			CellInfos = new CellLayer<CellInfo>(Grid.Type, size);
			Tiles = new CellLayer<TerrainTile>(Grid.Type, size);
			Resources = new CellLayer<ResourceTile>(Grid.Type, size);
			HeightStep = new CellLayer<byte>(Grid.Type, size);
			Ramp = new CellLayer<byte>(Grid.Type, size);
			Tiles.Clear(terrainInfo.DefaultTerrainTile);

			if (Grid.MaximumTerrainHeight > 0)
			{
				Tiles.CellEntryChanged += UpdateRamp;
			}

			PostInit();
			CalculateTileVertexInfo();

			if (Grid.MaximumTerrainHeight > 0)
			{
				CellInfos.CellEntryChanged += UpdateProjection;
				Tiles.CellEntryChanged += UpdateProjection;
				HeightStep.CellEntryChanged += UpdateProjection;
			}

			InitializeCellProjection();
		}

		public Map(ModData modData, IReadOnlyPackage package)
		{
			this.modData = modData;
			Package = package;

			if (!Package.Contains("map.yaml") || !Package.Contains("map.bin"))
				throw new InvalidDataException($"Not a valid map\n File: {package.Name}");

			var yaml = new MiniYaml(null, MiniYaml.FromStream(Package.GetStream("map.yaml"), package.Name));
			foreach (var field in YamlFields)
				field.Deserialize(this, yaml.Nodes);

			if (MapFormat < SupportedMapFormat)
				throw new InvalidDataException($"Map format {MapFormat} is not supported.\n File: {package.Name}");

			PlayerDefinitions = MiniYaml.NodesOrEmpty(yaml, "Players");
			ActorDefinitions = MiniYaml.NodesOrEmpty(yaml, "Actors");

			Grid = modData.Manifest.Get<MapGrid>();

			var size = new Size(MapSize.X, MapSize.Y);
			Tiles = new CellLayer<TerrainTile>(Grid.Type, size);
			Resources = new CellLayer<ResourceTile>(Grid.Type, size);
			HeightStep = new CellLayer<byte>(Grid.Type, size);
			Ramp = new CellLayer<byte>(Grid.Type, size);
			CellInfos = new CellLayer<CellInfo>(Grid.Type, size);

			using (var s = Package.GetStream("map.bin"))
			{
				var header = new BinaryDataHeader(s, MapSize);
				if (header.TilesOffset > 0)
				{
					s.Position = header.TilesOffset;
					for (var i = 0; i < MapSize.X; i++)
					{
						for (var j = 0; j < MapSize.Y; j++)
						{
							var tile = s.ReadUInt16();
							var index = s.ReadUInt8();

							// TODO: Remember to remove this when rewriting tile variants / PickAny
							if (index == byte.MaxValue)
								index = (byte)(i % 4 + (j % 4) * 4);

							Tiles[new MPos(i, j)] = new TerrainTile(tile, index);
						}
					}
				}

				if (header.ResourcesOffset > 0)
				{
					s.Position = header.ResourcesOffset;
					for (var i = 0; i < MapSize.X; i++)
					{
						for (var j = 0; j < MapSize.Y; j++)
						{
							var type = s.ReadUInt8();
							var density = s.ReadUInt8();
							Resources[new MPos(i, j)] = new ResourceTile(type, density);
						}
					}
				}

				if (header.HeightsOffset > 0)
				{
					s.Position = header.HeightsOffset;
					for (var i = 0; i < MapSize.X; i++)
						for (var j = 0; j < MapSize.Y; j++)
							HeightStep[new MPos(i, j)] = s.ReadUInt8().Clamp((byte)0, Grid.MaximumTerrainHeight);
				}
			}

			if (Grid.MaximumTerrainHeight > 0)
			{
				Tiles.CellEntryChanged += UpdateRamp;
			}

			PostInit();
			if (Package.Contains(TerrainMeshFileName))
				ImportVertexInfo();
			else
				CalculateTileVertexInfo();

			if (Grid.MaximumTerrainHeight > 0)
			{
				CellInfos.CellEntryChanged += UpdateProjection;
				Tiles.CellEntryChanged += UpdateProjection;
				HeightStep.CellEntryChanged += UpdateProjection;
			}

			InitializeCellProjection();

			Uid = ComputeUID(Package, MapFormat);
		}

		void PostInit()
		{
			try
			{
				Rules = Ruleset.Load(modData, this, Tileset, RuleDefinitions, WeaponDefinitions,
					VoiceDefinitions, NotificationDefinitions, MusicDefinitions, SequenceDefinitions, ModelSequenceDefinitions, MeshSequenceDefinitions);
			}
			catch (Exception e)
			{
				Log.Write("debug", "Failed to load rules for {0} with error {1}", Title, e);
				InvalidCustomRules = true;
				InvalidCustomRulesException = e;
				Rules = Ruleset.LoadDefaultsForTileSet(modData, Tileset);
			}

			Rules.Sequences.Preload();

			Translation = new Translation(Game.Settings.Player.Language, Translations, this);

			var tl = new MPos(0, 0).ToCPos(this);
			var br = new MPos(MapSize.X - 1, MapSize.Y - 1).ToCPos(this);
			AllCells = new CellRegion(Grid.Type, tl, br);

			var btl = new PPos(Bounds.Left, Bounds.Top);
			var bbr = new PPos(Bounds.Right - 1, Bounds.Bottom - 1);
			SetBounds(btl, bbr);

			CustomTerrain = new CellLayer<byte>(this);
			foreach (var uv in AllCells.MapCoords)
				CustomTerrain[uv] = byte.MaxValue;

			// Replace invalid tiles and cache ramp state
			var terrainInfo = Rules.TerrainInfo;
			foreach (var uv in AllCells.MapCoords)
			{
				if (!terrainInfo.TryGetTerrainInfo(Tiles[uv], out var info))
				{
					ReplacedInvalidTerrainTiles[uv.ToCPos(this)] = Tiles[uv];
					Tiles[uv] = terrainInfo.DefaultTerrainTile;
					info = terrainInfo.GetTerrainInfo(terrainInfo.DefaultTerrainTile);
				}

				Ramp[uv] = info.RampType;
			}

			AllEdgeCells = UpdateEdgeCells();

			// Invalidate the entry for a cell if anything could cause the terrain index to change.
			Action<CPos> invalidateTerrainIndex = c =>
			{
				if (cachedTerrainIndexes != null)
					cachedTerrainIndexes[c] = InvalidCachedTerrainIndex;
			};

			// Even though the cache is lazily initialized, we must attach these event handlers on init.
			// This ensures our handler to invalidate the cache runs first,
			// so other listeners to these same events will get correct data when calling GetTerrainIndex.
			CustomTerrain.CellEntryChanged += invalidateTerrainIndex;
			Tiles.CellEntryChanged += invalidateTerrainIndex;

			var listMPos = new List<MPos>();
			for (var y = 0; y < MapSize.Y; y++)
			{
				for (var x = 0; x < MapSize.X; x++)
				{
					listMPos.Add(new MPos(x, y));
				}
			}

			MapCells = listMPos.ToArray();

			var listCPos = new List<CPos>();
			foreach (var c in AllCells)
			{
				listCPos.Add(c);
			}

			AllCellsArray = listCPos.ToArray();
		}

		void UpdateRamp(CPos cell)
		{
			Ramp[cell] = Rules.TerrainInfo.GetTerrainInfo(Tiles[cell]).RampType;
		}

		void InitializeCellProjection()
		{
			if (initializedCellProjection)
				return;

			initializedCellProjection = true;

			cellProjection = new CellLayer<PPos[]>(this);
			inverseCellProjection = new CellLayer<List<MPos>>(this);
			projectedHeight = new CellLayer<byte>(this);

			// Initialize collections
			foreach (var cell in AllCells)
			{
				var uv = cell.ToMPos(Grid.Type);
				cellProjection[uv] = Array.Empty<PPos>();
				inverseCellProjection[uv] = new List<MPos>(1);
			}

			// Initialize projections
			foreach (var cell in AllCells)
				UpdateProjection(cell);
		}

		void UpdateProjection(CPos cell)
		{
			MPos uv;

			if (Grid.MaximumTerrainHeight == 0)
			{
				uv = cell.ToMPos(Grid.Type);
				cellProjection[cell] = new[] { (PPos)uv };
				var inverse = inverseCellProjection[uv];
				inverse.Clear();
				inverse.Add(uv);
				CellProjectionChanged?.Invoke(cell);
				return;
			}

			if (!initializedCellProjection)
				InitializeCellProjection();

			uv = cell.ToMPos(Grid.Type);

			// Remove old reverse projection
			foreach (var puv in cellProjection[uv])
			{
				var temp = (MPos)puv;
				inverseCellProjection[temp].Remove(uv);
				projectedHeight[temp] = ProjectedCellHeightInner(puv);
			}

			var projected = ProjectCellInner(uv);
			cellProjection[uv] = projected;

			foreach (var puv in projected)
			{
				var temp = (MPos)puv;
				inverseCellProjection[temp].Add(uv);

				var height = ProjectedCellHeightInner(puv);
				projectedHeight[temp] = height;

				// Propagate height up cliff faces
				while (true)
				{
					temp = new MPos(temp.U, temp.V - 1);
					if (!inverseCellProjection.Contains(temp) || inverseCellProjection[temp].Count > 0)
						break;

					projectedHeight[temp] = height;
				}
			}

			CellProjectionChanged?.Invoke(cell);
		}

		byte ProjectedCellHeightInner(PPos puv)
		{
			while (inverseCellProjection.Contains((MPos)puv))
			{
				var inverse = inverseCellProjection[(MPos)puv];
				if (inverse.Count > 0)
				{
					// The original games treat the top of cliffs the same way as the bottom
					// This information isn't stored in the map data, so query the offset from the tileset
					var temp = inverse.MaxBy(uv => uv.V);
					return (byte)(HeightStep[temp] - Rules.TerrainInfo.GetTerrainInfo(Tiles[temp]).Height);
				}

				// Try the next cell down if this is a cliff face
				puv = new PPos(puv.U, puv.V + 1);
			}

			return 0;
		}

		PPos[] ProjectCellInner(MPos uv)
		{
			if (!HeightStep.Contains(uv))
				return NoProjectedCells;

			var mapHeight = HeightStep;

			// Any changes to this function should be reflected when setting projectionSafeBounds.
			var height = mapHeight[uv];
			if (height == 0)
				return new[] { (PPos)uv };

			// Odd-height ramps get bumped up a level to the next even height layer
			if ((height & 1) == 1 && Ramp[uv] != 0)
				height += 1;

			var candidates = new List<PPos>();

			// Odd-height level tiles are equally covered by four projected tiles
			if ((height & 1) == 1)
			{
				if ((uv.V & 1) == 1)
					candidates.Add(new PPos(uv.U + 1, uv.V - height));
				else
					candidates.Add(new PPos(uv.U - 1, uv.V - height));

				candidates.Add(new PPos(uv.U, uv.V - height));
				candidates.Add(new PPos(uv.U, uv.V - height + 1));
				candidates.Add(new PPos(uv.U, uv.V - height - 1));
			}
			else
				candidates.Add(new PPos(uv.U, uv.V - height));

			return candidates.Where(c => mapHeight.Contains((MPos)c)).ToArray();
		}

		public void Save(IReadWritePackage toPackage)
		{
			MapFormat = CurrentMapFormat;

			var root = new List<MiniYamlNode>();
			foreach (var field in YamlFields)
				field.Serialize(this, root);

			// HACK: map.yaml is expected to have empty lines between top-level blocks
			for (var i = root.Count - 1; i > 0; i--)
				root.Insert(i, new MiniYamlNode("", ""));

			// Saving to a new package: copy over all the content from the map
			if (Package != null && toPackage != Package)
				foreach (var file in Package.Contents)
					toPackage.Update(file, Package.GetStream(file).ReadAllBytes());

			if (!LockPreview)
				toPackage.Update("map.png", SavePreview());

			// Update the package with the new map data
			var s = root.WriteToString();
			toPackage.Update("map.yaml", Encoding.UTF8.GetBytes(s));
			toPackage.Update("map.bin", SaveBinaryData());

			if (TerrainBlocks != null)
			{
				foreach (var block in TerrainBlocks)
				{
					toPackage.Update(block.TopLeft.ToString() + "_Mask123.png", block.MaskTextureData1());
					toPackage.Update(block.TopLeft.ToString() + "_Mask456.png", block.MaskTextureData2());
					toPackage.Update(block.TopLeft.ToString() + "_Mask789.png", block.MaskTextureData3());
				}
			}
			else
			{
				Console.WriteLine("This map is saving without starting the world render that belongs to it. \n" +
					"No TerrainRenderBlock information will be saved.");
			}

			Package = toPackage;

			// Update UID to match the newly saved data
			Uid = ComputeUID(toPackage, MapFormat);
		}

		#region Terrain Vertex

		public void ImportVertexInfo()
		{
			VertexArrayWidth = MapSize.X * 2 + 2;
			VertexArrayHeight = MapSize.Y + 2;

			TerrainVertices = new TerrainVertex[VertexArrayHeight * VertexArrayWidth];
			MiniCells = new MiniCell[(VertexArrayHeight - 1), (VertexArrayWidth - 1)];
			var vertexNmlCaled = new bool[VertexArrayWidth * VertexArrayHeight];
			// var vertexNml = new vec3[VertexArrayWidth * VertexArrayHeight];
			var vertexTerrType = new int[VertexArrayWidth * VertexArrayHeight];

			Dictionary<string, byte> terrainTypeNameIndex = new Dictionary<string, byte>();
			for (int i = 0; i < Rules.TerrainInfo.TerrainTypes.Length; i++) 
			{
				terrainTypeNameIndex.Add(Rules.TerrainInfo.TerrainTypes[i].Type, (byte)i);
			}

			using (var s = Package.GetStream(TerrainMeshFileName))
			{
				var width = s.ReadInt32();
				var height = s.ReadInt32();
				if (width != MapSize.X || height != MapSize.Y)
					throw new InvalidDataException("Invalid terrain mesh data: map size is " + MapSize + " and data size is " + width + ", " + height);

				for (var i = 0; i < TerrainVertices.Length; i++)
				{
					TerrainVertices[i].LogicPos = new WPos(s.ReadInt32(), s.ReadInt32(), Math.Max(s.ReadInt32(), 0));
					TerrainVertices[i].Color = new float3(s.ReadFloat(), s.ReadFloat(), s.ReadFloat());
					vertexTerrType[i] = s.ReadInt32();
					//vertexNml[i] = new vec3(s.ReadFloat(), s.ReadFloat(), s.ReadFloat());
					TerrainVertices[i].UpdatePos();
				}
			}

			for (var y = 0; y < MapSize.Y; y++)
			{
				for (var x = 0; x < MapSize.X; x++)
				{
					var uv = new MPos(x, y);

					int2 mid;
					if (y % 2 == 0)
					{
						mid = new int2(2 * x + 1, y + 1);
					}
					else
					{
						mid = new int2(2 * x + 2, y + 1);
					}

					var ramp = Ramp[uv];

					int im, it, ib, il, ir;
					im = mid.Y * VertexArrayWidth + mid.X;
					it = (mid.Y - 1) * VertexArrayWidth + mid.X;
					ib = (mid.Y + 1) * VertexArrayWidth + mid.X;
					il = mid.Y * VertexArrayWidth + mid.X - 1;
					ir = mid.Y * VertexArrayWidth + mid.X + 1;

					float3 pm, pt, pb, pl, pr;
					float2 uvm, uvt, uvb, uvl, uvr;

					pt = TerrainVertices[it].Pos;
					pb = TerrainVertices[ib].Pos;
					pl = TerrainVertices[il].Pos;
					pr = TerrainVertices[ir].Pos;
					pm = TerrainVertices[im].Pos;

					uvm = new float2(0.5f, 0.5f);
					uvt = new float2(CellInfo.TU, CellInfo.TV);
					uvb = new float2(CellInfo.BU, CellInfo.BV);
					uvl = new float2(CellInfo.LU, CellInfo.LV);
					uvr = new float2(CellInfo.RU, CellInfo.RV);

					var tlm = CellInfo.CalTBN(pt, pl, pm, uvt, uvl, uvm);
					var tmr = CellInfo.CalTBN(pt, pm, pr, uvt, uvm, uvr);
					var mlb = CellInfo.CalTBN(pm, pl, pb, uvm, uvl, uvb);
					var mbr = CellInfo.CalTBN(pm, pb, pr, uvm, uvb, uvr);

					TerrainVertices[im].TBN = (tlm * 0.25f + tmr * 0.25f + mlb * 0.25f + mbr * 0.25f);

					if (vertexNmlCaled[it])
						TerrainVertices[it].TBN = 0.25f * (tlm * 0.5f + tmr * 0.5f) + TerrainVertices[it].TBN;
					else
						TerrainVertices[it].TBN = 0.25f * (tlm * 0.5f + tmr * 0.5f);

					if (vertexNmlCaled[ib])
						TerrainVertices[ib].TBN = 0.25f * (mlb * 0.5f + mbr * 0.5f) + TerrainVertices[ib].TBN;
					else
						TerrainVertices[ib].TBN = 0.25f * (mlb * 0.5f + mbr * 0.5f);

					if (vertexNmlCaled[il])
						TerrainVertices[il].TBN = 0.25f * (mlb * 0.5f + tlm * 0.5f) + TerrainVertices[il].TBN;
					else
						TerrainVertices[il].TBN = 0.25f * (mlb * 0.5f + tlm * 0.5f);

					if (vertexNmlCaled[ir])
						TerrainVertices[ir].TBN = 0.25f * (tmr * 0.5f + mbr * 0.5f) + TerrainVertices[ir].TBN;
					else
						TerrainVertices[ir].TBN = 0.25f * (tmr * 0.5f + mbr * 0.5f);

					vertexNmlCaled[im] = true;
					vertexNmlCaled[it] = true;
					vertexNmlCaled[ib] = true;
					vertexNmlCaled[il] = true;
					vertexNmlCaled[ir] = true;

					WPos wposm = TerrainVertices[im].LogicPos;
					WPos wpost = TerrainVertices[it].LogicPos;
					WPos wposb = TerrainVertices[ib].LogicPos;
					WPos wposl = TerrainVertices[il].LogicPos;
					WPos wposr = TerrainVertices[ir].LogicPos;

					var tlNml = CellInfo.CalLogicNml(wposm, wpost, wposl);
					var trNml = CellInfo.CalLogicNml(wposm, wposr, wpost);
					var blNml = CellInfo.CalLogicNml(wposm, wposl, wposb);
					var brNml = CellInfo.CalLogicNml(wposm, wposb, wposl);

					var itl = (mid.Y - 1) * VertexArrayWidth + mid.X - 1;
					var itr = (mid.Y - 1) * VertexArrayWidth + mid.X + 1;
					var ibl = (mid.Y + 1) * VertexArrayWidth + mid.X - 1;
					var ibr = (mid.Y + 1) * VertexArrayWidth + mid.X + 1;
					MiniCells[mid.Y - 1, mid.X - 1] = new MiniCell(itl, it, il, im, MiniCellType.TRBL); // tl
					MiniCells[mid.Y - 1, mid.X] = new MiniCell(it, itr, im, ir, MiniCellType.TLBR); // tr
					MiniCells[mid.Y, mid.X - 1] = new MiniCell(il, im, ibl, ib, MiniCellType.TLBR); // bl
					MiniCells[mid.Y, mid.X] = new MiniCell(im, ir, ib, ibr, MiniCellType.TRBL); // br

					bool flatCell = false;
					if (TerrainVertices[im].LogicPos.Z == TerrainVertices[it].LogicPos.Z &&
						TerrainVertices[im].LogicPos.Z == TerrainVertices[ib].LogicPos.Z &&
						TerrainVertices[im].LogicPos.Z == TerrainVertices[ir].LogicPos.Z &&
						TerrainVertices[im].LogicPos.Z == TerrainVertices[il].LogicPos.Z)
						flatCell = true;

					CellInfo cellInfo = new CellInfo(TerrainVertices[im].LogicPos, Math.Min(
						Math.Min(
							Math.Min(
								Math.Min(TerrainVertices[im].LogicPos.Z,
									TerrainVertices[it].LogicPos.Z),
								TerrainVertices[ib].LogicPos.Z),
							TerrainVertices[ir].LogicPos.Z),
						TerrainVertices[il].LogicPos.Z),
						im, it, ib, il, ir,
						new int2(mid.X - 1, mid.Y - 1), new int2(mid.X, mid.Y - 1),
						new int2(mid.X - 1, mid.Y), new int2(mid.X, mid.Y), flatCell, ramp == 0,
						tlNml, trNml, blNml, brNml, this);
					var cpos = uv.ToCPos(this);
					cellInfo.TileType = Tiles[cpos].Type;
					string terrtype = "Clear";
					terrtype = GetTerrainType(vertexTerrType[im]);

					cellInfo.TerrainType = terrainTypeNameIndex[terrtype];
					CellInfos[uv.ToCPos(this)] = cellInfo;
				}
			}

			for (int i = 0; i < TerrainVertices.Length; i++)
			{
				TerrainVertices[i].TBN = CellInfo.NormalizeTBN(TerrainVertices[i].TBN);
			}

			// uv retarget
			float texScale = 1f;
			for (int i = 0; i < TerrainVertices.Length; i++)
			{
				TerrainVertices[i].UV = new float2(TerrainVertices[i].Pos.X / texScale, TerrainVertices[i].Pos.Y / texScale);
				TerrainVertices[i].MapUV = new float2((float)(i % VertexArrayWidth) / VertexArrayWidth, (float)(i / VertexArrayHeight) / VertexArrayHeight);
			}

			// clamp vert color
			for (int i = 0; i < TerrainVertices.Length; i++)
			{
				TerrainVertices[i].Color = float3.Clamp(TerrainVertices[i].Color, 0, 1);
			}

			// update heightStep
			foreach (var c in AllCellsArray)
			{
				HeightStep[c] = CellInfos[c].CellCenterPos.Z > 0 ? (byte)((CellInfos[c].CellCenterPos.Z + 1) / MapGrid.MapHeightStep) : (byte)(0);
			}
		}

		string GetTerrainType(int t)
		{
			string terrTypeName;
			switch (t)
			{
				case 0:
					terrTypeName = "Water";
					break;
				case 1:
					terrTypeName = "Cliff";
					break;
				case 2:
					terrTypeName = "Road";
					break;
				case 3:
					terrTypeName = "Rough";
					break;
				case 4:
					terrTypeName = "Rough";
					break;
				case 5:
					terrTypeName = "DirtRoad";
					break;
				case 7:
					terrTypeName = "Rough";
					break;
				case 8:
					terrTypeName = "Rough";
					break;
				case 11:
					terrTypeName = "Rail";
					break;
				case 12:
					terrTypeName = "Impassable";
					break;
				case 13:
					terrTypeName = "Rock";
					break;
				case 14:
					terrTypeName = "Bridge";
					break;
				default:
					terrTypeName = "Clear";
					break;
			}
			return terrTypeName;
		}

		public void CalculateTileVertexInfo()
		{
			VertexArrayWidth = MapSize.X * 2 + 2;
			VertexArrayHeight = MapSize.Y + 2;

			TerrainVertices = new TerrainVertex[VertexArrayHeight * VertexArrayWidth];
			MiniCells = new MiniCell[(VertexArrayHeight - 1) , (VertexArrayWidth - 1)];

			var vertexCaled = new bool[VertexArrayWidth * VertexArrayHeight];
			var vertexNmlCaled = new bool[VertexArrayWidth * VertexArrayHeight];
			var vertexCliffCaled = new bool[VertexArrayWidth * VertexArrayHeight];

			// pos and color
			for (var y = 0; y < MapSize.Y; y++)
			{
				for (var x = 0; x < MapSize.X; x++)
				{
					var uv = new MPos(x, y);
					int heightBase = HeightStep[uv] * MapGrid.MapHeightStep;
					int heightHalf = (HeightStep[uv] * 2 + 1) * MapGrid.MapHeightStep / 2;
					int heightUpper = (HeightStep[uv] + 1) * MapGrid.MapHeightStep;
					int heightUppest = (HeightStep[uv] + 2) * MapGrid.MapHeightStep;
					var ramp = Ramp[uv];

					int2 mid;
					if (y % 2 == 0)
					{
						mid = new int2(2 * x + 1, y + 1);
					}
					else
					{
						mid = new int2(2 * x + 2, y + 1);
					}

					int im, it, ib, il, ir;
					im = mid.Y * VertexArrayWidth + mid.X;
					it = (mid.Y - 1) * VertexArrayWidth + mid.X;
					ib = (mid.Y + 1) * VertexArrayWidth + mid.X;
					il = mid.Y * VertexArrayWidth + mid.X - 1;
					ir = mid.Y * VertexArrayWidth + mid.X + 1;

					var type = Rules.TerrainInfo.GetTerrainInfo(Tiles[uv]);
					var typename = Rules.TerrainInfo.TerrainTypes[type.TerrainType].Type;

					TerrainVertices[im].Color = Color2Float3(type.GetColor(Game.CosmeticRandom));

					if (vertexCaled[it])
						TerrainVertices[it].Color = float3.Lerp(Color2Float3(type.GetColor(Game.CosmeticRandom)), TerrainVertices[it].Color, 0.5f);
					else
						TerrainVertices[it].Color = Color2Float3(type.GetColor(Game.CosmeticRandom));

					if (vertexCaled[ib])
						TerrainVertices[ib].Color = float3.Lerp(Color2Float3(type.GetColor(Game.CosmeticRandom)), TerrainVertices[ib].Color, 0.5f);
					else
						TerrainVertices[ib].Color = Color2Float3(type.GetColor(Game.CosmeticRandom));

					if (vertexCaled[il])
						TerrainVertices[il].Color = float3.Lerp(Color2Float3(type.GetColor(Game.CosmeticRandom)), TerrainVertices[il].Color, 0.5f);
					else
						TerrainVertices[il].Color = Color2Float3(type.GetColor(Game.CosmeticRandom));

					if (vertexCaled[ir])
						TerrainVertices[ir].Color = float3.Lerp(Color2Float3(type.GetColor(Game.CosmeticRandom)), TerrainVertices[ir].Color, 0.5f);
					else
						TerrainVertices[ir].Color = Color2Float3(type.GetColor(Game.CosmeticRandom));

					int hm, ht, hb, hl, hr;
					float3 pm, pt, pb, pl, pr;
					switch (ramp)
					{
						case 0:
							hm = heightBase;
							ht = heightBase;
							hb = heightBase;
							hl = heightBase;
							hr = heightBase;
							break;
						case 1:
							hm = heightHalf;
							ht = heightBase;
							hb = heightUpper;
							hl = heightBase;
							hr = heightUpper;
							break;
						case 2:
							hm = heightHalf;
							ht = heightBase;
							hb = heightUpper;
							hl = heightUpper;
							hr = heightBase;
							break;
						case 3:
							hm = heightHalf;
							ht = heightUpper;
							hb = heightBase;
							hl = heightUpper;
							hr = heightBase;
							break;
						case 4:
							hm = heightHalf;
							ht = heightUpper;
							hb = heightBase;
							hl = heightBase;
							hr = heightUpper;
							break;
						case 5:
							hm = heightUpper;
							ht = heightUpper;
							hb = heightUpper;
							hl = heightBase;
							hr = heightUpper;
							break;
						case 6:
							hm = heightBase;
							ht = heightBase;
							hb = heightBase;
							hl = heightUpper;
							hr = heightBase;
							break;
						case 7:
							hm = heightBase;
							ht = heightUpper;
							hb = heightBase;
							hl = heightBase;
							hr = heightBase;
							break;
						case 8:
							hm = heightBase;
							ht = heightBase;
							hb = heightBase;
							hl = heightBase;
							hr = heightUpper;
							break;
						case 9:
							hm = heightUpper;
							ht = heightBase;
							hb = heightUpper;
							hl = heightUpper;
							hr = heightUpper;
							break;
						case 10:
							hm = heightUpper;
							ht = heightUpper;
							hb = heightUpper;
							hl = heightUpper;
							hr = heightBase;
							break;
						case 11:
							hm = heightUpper;
							ht = heightUpper;
							hb = heightBase;
							hl = heightUpper;
							hr = heightUpper;
							break;
						case 12:
							hm = heightUpper;
							ht = heightUpper;
							hb = heightUpper;
							hl = heightBase;
							hr = heightUpper;
							break;
						case 13:
							hm = heightUpper;
							ht = heightBase;
							hb = heightUppest;
							hl = heightUpper;
							hr = heightUpper;
							break;
						case 14:
							hm = heightUpper;
							ht = heightUpper;
							hb = heightUpper;
							hl = heightUppest;
							hr = heightBase;
							break;
						case 15:
							hm = heightUpper;
							ht = heightUppest;
							hb = heightBase;
							hl = heightUpper;
							hr = heightUpper;
							break;
						case 16:
							hm = heightUpper;
							ht = heightUpper;
							hb = heightUpper;
							hl = heightBase;
							hr = heightUppest;
							break;
						case 17:
							hm = heightBase;
							ht = heightBase;
							hb = heightBase;
							hl = heightUpper;
							hr = heightUpper;
							break;
						case 18:
							hm = heightUpper;
							ht = heightUpper;
							hb = heightUpper;
							hl = heightBase;
							hr = heightBase;
							break;
						case 19:
							hm = heightUpper;
							ht = heightBase;
							hb = heightBase;
							hl = heightUpper;
							hr = heightUpper;
							break;
						case 20:
							hm = heightBase;
							ht = heightUpper;
							hb = heightUpper;
							hl = heightBase;
							hr = heightBase;
							break;
						default:
							hm = heightBase;
							ht = heightBase;
							hb = heightBase;
							hl = heightBase;
							hr = heightBase;
							Console.WriteLine("Invalid Ramp Type: " + ramp);
							break;
					}

					var cpos = uv.ToCPos(this);
					TerrainVertices[im].LogicPos = TileWPos(hm, cpos, WPos.Zero);
					TerrainVertices[im].Pos = TilePos(TerrainVertices[im].LogicPos);
					pm = TerrainVertices[im].Pos;

					if (vertexCaled[it])
					{
						if (typename == "Cliff" || vertexCliffCaled[it])
						{
							if (vertexCliffCaled[it] || Math.Abs(ht - TerrainVertices[it].LogicPos.Z) > 1024)
							{
								TerrainVertices[it].LogicPos = TileWPos(Math.Max(ht, TerrainVertices[it].LogicPos.Z), cpos, new WPos(0, -724, 0));
								vertexCliffCaled[it] = true;
							}
							else
								TerrainVertices[it].LogicPos = TileWPos(HMix(ht, TerrainVertices[it].LogicPos.Z), cpos, new WPos(0, -724, 0));
							//TerrainVertices[it].LogicPos = TileWPos(Math.Min(ht, TerrainVertices[it].LogicPos.Z), cpos, new WPos(0, -724, 0));
						}
						else
						{
							TerrainVertices[it].LogicPos = TileWPos(HMix(ht, TerrainVertices[it].LogicPos.Z), cpos, new WPos(0, -724, 0));
						}
					}
					else
						TerrainVertices[it].LogicPos = TileWPos(ht / 4, cpos, new WPos(0, -724, 0));
					TerrainVertices[it].Pos = TilePos(TerrainVertices[it].LogicPos);
					pt = TerrainVertices[it].Pos;

					if (vertexCaled[ib])
					{
						if (typename == "Cliff" || vertexCliffCaled[ib])
						{
							if (vertexCliffCaled[ib] || Math.Abs(hb - TerrainVertices[ib].LogicPos.Z) > 1024)
							{
								TerrainVertices[ib].LogicPos = TileWPos(Math.Max(hb, TerrainVertices[ib].LogicPos.Z), cpos, new WPos(0, -724, 0));
								vertexCliffCaled[ib] = true;
							}
							else
								TerrainVertices[ib].LogicPos = TileWPos(HMix(hb, TerrainVertices[ib].LogicPos.Z), cpos, new WPos(0, 724, 0));
						}
						else
						{
							TerrainVertices[ib].LogicPos = TileWPos(HMix(hb, TerrainVertices[ib].LogicPos.Z), cpos, new WPos(0, 724, 0));
						}
					}
					else
						TerrainVertices[ib].LogicPos = TileWPos(hb / 4, cpos, new WPos(0, 724, 0));
					TerrainVertices[ib].Pos = TilePos(TerrainVertices[ib].LogicPos);
					pb = TerrainVertices[ib].Pos;

					if (vertexCaled[il])
					{
						if (typename == "Cliff" || vertexCliffCaled[il])
						{
							if (vertexCliffCaled[il] || Math.Abs(hl - TerrainVertices[il].LogicPos.Z) > 1024)
							{
								TerrainVertices[il].LogicPos = TileWPos(Math.Max(hl, TerrainVertices[il].LogicPos.Z), cpos, new WPos(0, -724, 0));
								vertexCliffCaled[il] = true;
							}
							else
								TerrainVertices[il].LogicPos = TileWPos(HMix(hl, TerrainVertices[il].LogicPos.Z), cpos, new WPos(-724, 0, 0));
						}
						else
						{
							TerrainVertices[il].LogicPos = TileWPos(HMix(hl, TerrainVertices[il].LogicPos.Z), cpos, new WPos(-724, 0, 0));
						}
					}
					else
						TerrainVertices[il].LogicPos = TileWPos(hl / 4, cpos, new WPos(-724, 0, 0));
					TerrainVertices[il].Pos = TilePos(TerrainVertices[il].LogicPos);
					pl = TerrainVertices[il].Pos;

					if (vertexCaled[ir])
					{
						if (typename == "Cliff" || vertexCliffCaled[ir])
						{
							if (vertexCliffCaled[ir] || Math.Abs(hr - TerrainVertices[ir].LogicPos.Z) > 1024)
							{
								TerrainVertices[ir].LogicPos = TileWPos(Math.Max(hr, TerrainVertices[ir].LogicPos.Z), cpos, new WPos(0, -724, 0));
								vertexCliffCaled[ir] = true;
							}
							else
								TerrainVertices[ir].LogicPos = TileWPos(HMix(hr, TerrainVertices[ir].LogicPos.Z), cpos, new WPos(724, 0, 0));
						}
						else
						{
							TerrainVertices[ir].LogicPos = TileWPos(HMix(hr, TerrainVertices[ir].LogicPos.Z), cpos, new WPos(724, 0, 0));
						}
					}
					else
						TerrainVertices[ir].LogicPos = TileWPos(hr / 4, cpos, new WPos(724, 0, 0));
					TerrainVertices[ir].Pos = TilePos(TerrainVertices[ir].LogicPos);
					pr = TerrainVertices[ir].Pos;

					vertexCaled[im] = true;
					vertexCaled[it] = true;
					vertexCaled[ib] = true;
					vertexCaled[il] = true;
					vertexCaled[ir] = true;
				}
			}

			// nml
			for (var y = 0; y < MapSize.Y; y++)
			{
				for (var x = 0; x < MapSize.X; x++)
				{
					var uv = new MPos(x, y);

					int2 mid;
					if (y % 2 == 0)
					{
						mid = new int2(2 * x + 1, y + 1);
					}
					else
					{
						mid = new int2(2 * x + 2, y + 1);
					}

					var ramp = Ramp[uv];

					int im, it, ib, il, ir;
					im = mid.Y * VertexArrayWidth + mid.X;
					it = (mid.Y - 1) * VertexArrayWidth + mid.X;
					ib = (mid.Y + 1) * VertexArrayWidth + mid.X;
					il = mid.Y * VertexArrayWidth + mid.X - 1;
					ir = mid.Y * VertexArrayWidth + mid.X + 1;

					float3 pm, pt, pb, pl, pr;
					float2 uvm, uvt, uvb, uvl, uvr;

					if (IsBoundVert(it))
					{
						TerrainVertices[it].LogicPos = TerrainVertices[im].LogicPos;
						TerrainVertices[it].Pos = TilePos(TerrainVertices[im].LogicPos);
					}

					if (IsBoundVert(ib))
					{
						TerrainVertices[ib].LogicPos = TerrainVertices[im].LogicPos;
						TerrainVertices[ib].Pos = TilePos(TerrainVertices[im].LogicPos);
					}

					if (IsBoundVert(il))
					{
						TerrainVertices[il].LogicPos = TerrainVertices[im].LogicPos;
						TerrainVertices[il].Pos = TilePos(TerrainVertices[im].LogicPos);
					}

					if (IsBoundVert(ir))
					{
						TerrainVertices[ir].LogicPos = TerrainVertices[im].LogicPos;
						TerrainVertices[ir].Pos = TilePos(TerrainVertices[im].LogicPos);
					}

					pt = TerrainVertices[it].Pos;
					pb = TerrainVertices[ib].Pos;
					pl = TerrainVertices[il].Pos;
					pr = TerrainVertices[ir].Pos;

					if (ramp == 0)
					{
						TerrainVertices[im].LogicPos = new WPos(
							TerrainVertices[im].LogicPos.X,
							TerrainVertices[im].LogicPos.Y,
							(int)(TerrainVertices[it].LogicPos.Z / 4) +
							(int)(TerrainVertices[ib].LogicPos.Z / 4) +
							(int)(TerrainVertices[il].LogicPos.Z / 4) +
							(int)(TerrainVertices[ir].LogicPos.Z / 4));
						TerrainVertices[im].Pos = TilePos(TerrainVertices[im].LogicPos);
					}

					pm = TerrainVertices[im].Pos;

					uvm = new float2(0.5f, 0.5f);
					uvt = new float2(CellInfo.TU, CellInfo.TV);
					uvb = new float2(CellInfo.BU, CellInfo.BV);
					uvl = new float2(CellInfo.LU, CellInfo.LV);
					uvr = new float2(CellInfo.RU, CellInfo.RV);

					var tlm = CellInfo.CalTBN(pt, pl, pm, uvt, uvl, uvm);
					var tmr = CellInfo.CalTBN(pt, pm, pr, uvt, uvm, uvr);
					var mlb = CellInfo.CalTBN(pm, pl, pb, uvm, uvl, uvb);
					var mbr = CellInfo.CalTBN(pm, pb, pr, uvm, uvb, uvr);

					TerrainVertices[im].TBN = (tlm * 0.25f + tmr * 0.25f + mlb * 0.25f + mbr * 0.25f);

					if (vertexNmlCaled[it])
						TerrainVertices[it].TBN = 0.25f * (tlm * 0.5f + tmr * 0.5f) + TerrainVertices[it].TBN;
					else
						TerrainVertices[it].TBN = 0.25f * (tlm * 0.5f + tmr * 0.5f);

					if (vertexNmlCaled[ib])
						TerrainVertices[ib].TBN = 0.25f * (mlb * 0.5f + mbr * 0.5f) + TerrainVertices[ib].TBN;
					else
						TerrainVertices[ib].TBN = 0.25f * (mlb * 0.5f + mbr * 0.5f);

					if (vertexNmlCaled[il])
						TerrainVertices[il].TBN = 0.25f * (mlb * 0.5f + tlm * 0.5f) + TerrainVertices[il].TBN;
					else
						TerrainVertices[il].TBN = 0.25f * (mlb * 0.5f + tlm * 0.5f);

					if (vertexNmlCaled[ir])
						TerrainVertices[ir].TBN = 0.25f * (tmr * 0.5f + mbr * 0.5f) + TerrainVertices[ir].TBN;
					else
						TerrainVertices[ir].TBN = 0.25f * (tmr * 0.5f + mbr * 0.5f);

					vertexNmlCaled[im] = true;
					vertexNmlCaled[it] = true;
					vertexNmlCaled[ib] = true;
					vertexNmlCaled[il] = true;
					vertexNmlCaled[ir] = true;

					var type = Rules.TerrainInfo.GetTerrainInfo(Tiles[uv]);

					WPos wposm = TerrainVertices[im].LogicPos;
					WPos wpost = TerrainVertices[it].LogicPos;
					WPos wposb = TerrainVertices[ib].LogicPos;
					WPos wposl = TerrainVertices[il].LogicPos;
					WPos wposr = TerrainVertices[ir].LogicPos;

					var tlNml = CellInfo.CalLogicNml(wposm, wpost, wposl);
					var trNml = CellInfo.CalLogicNml(wposm, wposr, wpost);
					var blNml = CellInfo.CalLogicNml(wposm, wposl, wposb);
					var brNml = CellInfo.CalLogicNml(wposm, wposb, wposl);

					var itl = (mid.Y - 1) * VertexArrayWidth + mid.X - 1;
					var itr = (mid.Y - 1) * VertexArrayWidth + mid.X + 1;
					var ibl = (mid.Y + 1) * VertexArrayWidth + mid.X - 1;
					var ibr = (mid.Y + 1) * VertexArrayWidth + mid.X + 1;
					MiniCells[mid.Y - 1, mid.X - 1] = new MiniCell(itl, it, il, im, MiniCellType.TRBL); // tl
					MiniCells[mid.Y - 1, mid.X] = new MiniCell(it, itr, im, ir, MiniCellType.TLBR); // tr
					MiniCells[mid.Y, mid.X - 1] = new MiniCell(il, im, ibl, ib, MiniCellType.TLBR); // bl
					MiniCells[mid.Y, mid.X] = new MiniCell(im, ir, ib, ibr, MiniCellType.TRBL); // br

					bool flatCell = false;
					if (TerrainVertices[im].LogicPos.Z == TerrainVertices[it].LogicPos.Z &&
						TerrainVertices[im].LogicPos.Z == TerrainVertices[ib].LogicPos.Z &&
						TerrainVertices[im].LogicPos.Z == TerrainVertices[ir].LogicPos.Z &&
						TerrainVertices[im].LogicPos.Z == TerrainVertices[il].LogicPos.Z)
						flatCell = true;

					CellInfo cellInfo = new CellInfo(TerrainVertices[im].LogicPos, Math.Min(
						Math.Min(
							Math.Min(
								Math.Min(TerrainVertices[im].LogicPos.Z,
									TerrainVertices[it].LogicPos.Z),
								TerrainVertices[ib].LogicPos.Z),
							TerrainVertices[ir].LogicPos.Z),
						TerrainVertices[il].LogicPos.Z),
						im, it, ib, il, ir,
						new int2(mid.X - 1, mid.Y - 1), new int2(mid.X, mid.Y - 1),
						new int2(mid.X - 1, mid.Y), new int2(mid.X, mid.Y), flatCell, ramp == 0,
						tlNml, trNml, blNml, brNml, this);
					var cpos = uv.ToCPos(this);
					cellInfo.TileType = Tiles[cpos].Type;
					cellInfo.TerrainType = Rules.TerrainInfo.GetTerrainInfo(Tiles[cpos]).TerrainType;
					CellInfos[cpos] = cellInfo;
				}
			}

			// normal fix for mixed height tile
			for (var y = 0; y < MapSize.Y; y++)
			{
				for (var x = 0; x < MapSize.X; x++)
				{
					if (Ramp[new MPos(x, y)] != 0)
						continue;

					int2 mid;
					if (y % 2 == 0)
					{
						mid = new int2(2 * x + 1, y + 1);
					}
					else
					{
						mid = new int2(2 * x + 2, y + 1);
					}

					int im, it, ib, il, ir;
					im = mid.Y * VertexArrayWidth + mid.X;
					it = (mid.Y - 1) * VertexArrayWidth + mid.X;
					ib = (mid.Y + 1) * VertexArrayWidth + mid.X;
					il = mid.Y * VertexArrayWidth + mid.X - 1;
					ir = mid.Y * VertexArrayWidth + mid.X + 1;

					TerrainVertices[im].TBN = (0.25f * TerrainVertices[it].TBN + 0.25f * TerrainVertices[ib].TBN + 0.25f * TerrainVertices[il].TBN + 0.25f * TerrainVertices[ir].TBN);
				}
			}

			for (int i = 0; i < TerrainVertices.Length; i++)
			{
				TerrainVertices[i].TBN = CellInfo.NormalizeTBN(TerrainVertices[i].TBN);
			}

			// uv retarget
			float texScale = 1f;
			for (int i = 0; i < TerrainVertices.Length; i++)
			{
				TerrainVertices[i].UV = new float2(TerrainVertices[i].Pos.X / texScale, TerrainVertices[i].Pos.Y / texScale);
				TerrainVertices[i].MapUV = new float2((float)(i % VertexArrayWidth) / VertexArrayWidth, (float)(i / VertexArrayHeight) / VertexArrayHeight);
			}

			// clamp vert color
			for (int i = 0; i < TerrainVertices.Length; i++)
			{
				TerrainVertices[i].Color = float3.Clamp(4.0f * TerrainVertices[i].Color, 0, 1);
			}

			// update heightStep
			foreach (var c in AllCellsArray)
			{
				HeightStep[c] = CellInfos[c].CellCenterPos.Z > 0 ? (byte)((CellInfos[c].CellCenterPos.Z + 1) / MapGrid.MapHeightStep) : (byte)(0);
			}
		}

		public void FlatCellWithHeight(World world, CPos cell, int height)
		{
			if (!CellInfos.Contains(cell))
				return;

			CellInfos[cell].FlatCell(height, this);

			UpdateCellVertex(world, cell);
		}

		public void UpdateCellVertex(World world, CPos cell)
		{
			CPos tL = new CPos(cell.X - 1, cell.Y - 1, cell.Layer);
			CPos tT = new CPos(cell.X, cell.Y - 1, cell.Layer);
			CPos tR = new CPos(cell.X + 1, cell.Y - 1, cell.Layer);

			CPos mL = new CPos(cell.X - 1, cell.Y, cell.Layer);

			CPos mR = new CPos(cell.X + 1, cell.Y, cell.Layer);

			CPos bL = new CPos(cell.X - 1, cell.Y + 1, cell.Layer);
			CPos bB = new CPos(cell.X, cell.Y + 1, cell.Layer);
			CPos bR = new CPos(cell.X + 1, cell.Y + 1, cell.Layer);

			var cposs = new CPos[]
			{
				tL,tT,tR,
				mL,cell,mR,
				bL,bB,bR
			};

			foreach (var c in cposs)
			{
				if (!CellInfos.Contains(c))
					continue;

				CellInfos[c].UpdateCell(this);

				HeightStep[c] = CellInfos[c].CellCenterPos.Z > 0 ? (byte)((CellInfos[c].CellCenterPos.Z + 1) / MapGrid.MapHeightStep) : (byte)(0);

				if (CellInfos[c].AlmostFlat)
					Ramp[c] = 0;

				world.ActorMap.UpdateActorsPositionAt(c);

				var minicells = new int2[]
				{
					CellInfos[c].MiniCellTL,
					CellInfos[c].MiniCellTR,
					CellInfos[c].MiniCellBL,
					CellInfos[c].MiniCellBR,
				};

				foreach (var minicell in minicells)
				{
					TerrainRenderBlock.UpdateVerticesByMiniCellUV(minicell, this);
				}
			}
		}

		#endregion

		#region util for CalculateTileVertexInfo

		int HMix(int a, int b)
		{
			//return a + (b - a) / 2;
			return a / 4 + b;
		}

		bool IsBoundVert(int vi)
		{
			if (vi <= VertexArrayWidth || vi >= (VertexArrayHeight - 1) * VertexArrayWidth)
				return true;
			if (vi % VertexArrayWidth == 0 || (vi + 1) % VertexArrayWidth == 0)
				return true;
			return false;
		}

		float3 Color2Float3(in Color color)
		{
			return new float3(((float)color.R) / 255, ((float)color.G) / 255, ((float)color.B) / 255);
		}

		WPos TileWPos(int h, CPos cell, WPos offset)
		{
			return new WPos(724 * (cell.X - cell.Y + 1) + offset.X, 724 * (cell.X + cell.Y + 1) + offset.Y, h + offset.Z);
		}

		float3 TilePos(WPos pos)
		{
			return new float3(-(float)pos.X / 256,
										(float)pos.Y / 256,
										(float)pos.Z / 256);
		}

		float3 TilePos(int h, CPos cell, WPos offset)
		{
			var pos = new WPos(724 * (cell.X - cell.Y + 1) + offset.X, 724 * (cell.X + cell.Y + 1) + offset.Y, 0);

			return new float3(-(float)pos.X / 256,
										(float)pos.Y / 256,
										(float)h / 256);
		}

		#endregion

		#region terrain block
		public void DisposeRenderBlocks()
		{
			foreach (var rb in TerrainBlocks)
				rb.Dispose();
			TerrainBlocks = null;
		}

		public void CreateRenderBlocks(WorldRenderer worldRenderer)
		{
			// Create Terrain Render Blocks
			var blockX = (VertexArrayWidth - 1) / TerrainRenderBlock.SizeLimit;
			if ((VertexArrayWidth - 1) % TerrainRenderBlock.SizeLimit != 0)
				blockX += 1;
			var blockY = (VertexArrayHeight - 1) / TerrainRenderBlock.SizeLimit;
			if ((VertexArrayHeight - 1) % TerrainRenderBlock.SizeLimit != 0)
				blockY += 1;
			BlocksArrayWidth = blockX;
			BlocksArrayHeight = blockY;

			Console.WriteLine("BlocksArray " + BlocksArrayWidth + " " + BlocksArrayHeight);

			Console.WriteLine("VertexArray " + VertexArrayWidth + " " + VertexArrayHeight);

			Console.WriteLine("MapSize " + MapSize);

			TerrainBlocks = new TerrainRenderBlock[blockY, blockX];

			for (int y = 0; y < blockY; y++)
				for (int x = 0; x < blockX; x++)
				{
					int2 tl = new int2(x * TerrainRenderBlock.SizeLimit, y * TerrainRenderBlock.SizeLimit);
					int2 br = new int2(
						Math.Min((x + 1) * TerrainRenderBlock.SizeLimit, VertexArrayWidth - 2) - 1,
						Math.Min((y + 1) * TerrainRenderBlock.SizeLimit, VertexArrayHeight - 2) - 1);
					TerrainBlocks[y, x] = new TerrainRenderBlock(worldRenderer, this, tl, br);
				}
		}

		public bool MaskInitByTexFile = false;
		public void DrawMaskWithTSMapInfo(WorldRenderer wr)
		{
			if (MaskInitByTexFile)
				return;

			Console.WriteLine("Painting Spot on mask at each cell");

#pragma warning disable IDE1006 // 命名样式
			int WATER = 0;
			int CLIFF = 1;
			int CONC = 2;
			int GRASS = 3;
			int SAND = 4;
			int DIRT = 5;
			int GRAVEL = 6;
			int CRACK = 7;
#pragma warning restore IDE1006 // 命名样式

			var brush = TextureCache.AllBrushes.First().Value;
			var waterBrush = TextureCache.AllBrushes["DefualtWaterBrush"];
			var random = wr.World.SharedRandom;

			foreach (var uv in MapCells)
			{
				var typename = Rules.TerrainInfo.TerrainTypes[CellInfos[uv].TerrainType].Type;
				var id = CellInfos[uv].TileType;
				int layer = 8;

				switch (typename)
				{
					case "Rough":
						layer = GRAVEL;
						break;
					case "DirtRoad":
						layer = DIRT;
						break;
					case "Cliff":
						layer = CLIFF;
						break;
					case "Impassable":
						layer = CLIFF;
						break;
					case "Rock":
						layer = CLIFF;
						break;
					case "Rail":
						layer = CONC;
						break;
					case "Road":
						layer = CONC;
						break;
					case "Bridge":
						layer = CONC;
						break;
					case "Water":
						layer = WATER;
						break;
					default:
						break;
				}

				bool isShore = false;
				if (id >= 108 && id <= 149)
				{
					isShore = true;

					// shore
					if (typename != "Water")
						layer = SAND;
				}
				else if (id >= 626 && id <= 642)
				{
					// grass
					layer = GRASS;
				}
				else if (id >= 535 && id <= 551)
				{
					// sand
					layer = SAND;
				}
				else if (id >= 150 && id <= 166)
				{
					// rough
					layer = GRAVEL;
				}
				else if (id >= 552 && id <= 561)
				{
					// crack ground
					layer = CRACK;
				}

				if (isShore)
					TerrainRenderBlock.PaintAt(this, brush, CenterOfCell(uv), brush.DefaultSize * random.Next(20, 35) / random.Next(10, 15), SAND, random.Next(75, 255));

				if (layer == 0)
				{
					var underwater = random.Next(0, 10);
					if (underwater < 7)
						TerrainRenderBlock.PaintAt(this, brush, CenterOfCell(uv), brush.DefaultSize * random.Next(40, 50) / random.Next(25, 30), SAND, random.Next(1, 255));
					else
						TerrainRenderBlock.PaintAt(this, brush, CenterOfCell(uv), brush.DefaultSize * random.Next(40, 50) / random.Next(25, 30), DIRT, random.Next(1, 255));
					if (isShore)
						TerrainRenderBlock.PaintAt(this, waterBrush, CenterOfCell(uv), waterBrush.DefaultSize * random.Next(25, 35) / random.Next(10, 20), WATER, random.Next(1, 75));
					else
						TerrainRenderBlock.PaintAt(this, waterBrush, CenterOfCell(uv), waterBrush.DefaultSize, WATER, 255);
				}
				else
					TerrainRenderBlock.PaintAt(this, brush, CenterOfCell(uv), brush.DefaultSize * random.Next(22, 28) / random.Next(22, 28), layer, 255);
			}

			foreach (var uv in MapCells)
			{
				if (!CellInfos[uv].Flat)
				{
					// TerrainRenderBlock.PaintAt(this, waterBrush, CenterOfCell(uv), waterBrush.DefaultSize, WATER, -255);
					TerrainRenderBlock.PaintAt(this, brush, CenterOfCell(uv), brush.DefaultSize, WATER, -255);
				}
			}

			Console.WriteLine("Completed");
		}

		public void InitTerrainBlockMask()
		{
			foreach (var block in TerrainBlocks)
			{
				block.InitMask();
			}
		}

		public void UpdateTerrainBlockMask(Viewport vp, bool init = false)
		{
			var tl = vp.TopLeftPosition;
			var br = vp.BottomRightPosition;
			var left = tl.X;
			var right = br.X;
			var top = tl.Y - Grid.MaximumTerrainHeight * MapGrid.MapHeightStep * 2;
			var bottom = br.Y + Grid.MaximumTerrainHeight * MapGrid.MapHeightStep * 2;

			foreach (var block in TerrainBlocks)
			{
				block.UpdateMask(left, right, top, bottom, init);
			}
		}

		public void UpdateTerrainBlockTexture(Viewport vp, bool init = false)
		{
			var tl = vp.TopLeftPosition;
			var br = vp.BottomRightPosition;
			var left = tl.X;
			var right = br.X;
			var top = tl.Y - Grid.MaximumTerrainHeight * MapGrid.MapHeightStep * 2;
			var bottom = br.Y + Grid.MaximumTerrainHeight * MapGrid.MapHeightStep * 2;

			foreach (var block in TerrainBlocks)
			{
				block.UpdateTexture(left, right, top, bottom, init);
			}
		}

		public void SetTerrainShaderParams(in World3DRenderer w3dr, bool sunCamera)
		{
			TerrainRenderBlock.SetCameraParams(w3dr, sunCamera);
			TerrainRenderBlock.SetShadowParams(w3dr);
		}

		public void DrawTerrainBlock(Viewport vp)
		{
			var tl = vp.TopLeftPosition;
			var br = vp.BottomRightPosition;
			var left = tl.X;
			var right = br.X;
			var top = tl.Y - Grid.MaximumTerrainHeight * MapGrid.MapHeightStep * 2;
			var bottom = br.Y + Grid.MaximumTerrainHeight * MapGrid.MapHeightStep * 2;

			foreach (var block in TerrainBlocks)
			{
				block.Render(left, right, top, bottom);
			}
		}

		#endregion

		/// <summary>
		///  WIP
		/// </summary>
		public void SaveMapAsPng(string path)
		{
			if (Grid.Type != MapGridType.RectangularIsometric)
				return;
			Console.WriteLine("Saving Map " + Title + " As Png");
			float mapMaxHeight = ((float)Grid.MaximumTerrainHeight * MapGrid.MapHeightStep) / 256;

			var heightData = new byte[TerrainVertices.Length * 4];
			var colorData = new byte[TerrainVertices.Length * 4];

			for (int i = 0; i < TerrainVertices.Length; i++)
			{
				heightData[i * 4] = (byte)(TerrainVertices[i].Pos.Z * byte.MaxValue / mapMaxHeight);
				heightData[i * 4 + 1] = 0;
				heightData[i * 4 + 2] = 0;
				heightData[i * 4 + 3] = byte.MaxValue;
			}

			for (int i = 0; i < TerrainVertices.Length; i++)
			{
				colorData[i * 4] = (byte)(TerrainVertices[i].Color.X * byte.MaxValue);
				colorData[i * 4 + 1] = (byte)(TerrainVertices[i].Color.Y * byte.MaxValue);
				colorData[i * 4 + 2] = (byte)(TerrainVertices[i].Color.Z * byte.MaxValue);
				colorData[i * 4 + 3] = byte.MaxValue;
			}

			new Png(heightData, SpriteFrameType.Rgba32, VertexArrayWidth, VertexArrayHeight).Save(path + Title + "-height.png");
			new Png(colorData, SpriteFrameType.Rgba32, VertexArrayWidth, VertexArrayHeight).Save(path + Title + "-color.png");
		}

		public byte[] SaveBinaryData()
		{
			var dataStream = new MemoryStream();
			using (var writer = new BinaryWriter(dataStream))
			{
				// Binary data version
				writer.Write(TileFormat);

				// Size
				writer.Write((ushort)MapSize.X);
				writer.Write((ushort)MapSize.Y);

				// Data offsets
				var tilesOffset = 17;
				var heightsOffset = Grid.MaximumTerrainHeight > 0 ? 3 * MapSize.X * MapSize.Y + 17 : 0;
				var resourcesOffset = (Grid.MaximumTerrainHeight > 0 ? 4 : 3) * MapSize.X * MapSize.Y + 17;

				writer.Write((uint)tilesOffset);
				writer.Write((uint)heightsOffset);
				writer.Write((uint)resourcesOffset);

				// Tile data
				if (tilesOffset != 0)
				{
					for (var i = 0; i < MapSize.X; i++)
					{
						for (var j = 0; j < MapSize.Y; j++)
						{
							var tile = Tiles[new MPos(i, j)];
							writer.Write(tile.Type);
							writer.Write(tile.Index);
						}
					}
				}

				// Height data
				if (heightsOffset != 0)
					for (var i = 0; i < MapSize.X; i++)
						for (var j = 0; j < MapSize.Y; j++)
							writer.Write(HeightStep[new MPos(i, j)]);

				// Resource data
				if (resourcesOffset != 0)
				{
					for (var i = 0; i < MapSize.X; i++)
					{
						for (var j = 0; j < MapSize.Y; j++)
						{
							var tile = Resources[new MPos(i, j)];
							writer.Write(tile.Type);
							writer.Write(tile.Index);
						}
					}
				}
			}

			return dataStream.ToArray();
		}

		public (Color Left, Color Right) GetTerrainColorPair(MPos uv, bool fromTile = false)
		{
			if (fromTile)
			{
				var terrainInfo = Rules.TerrainInfo;
				var type = terrainInfo.GetTerrainInfo(Tiles[uv]);
				var left = type.GetColor(Game.CosmeticRandom);
				var right = type.GetColor(Game.CosmeticRandom);

				if (terrainInfo.MinHeightColorBrightness != 1.0f || terrainInfo.MaxHeightColorBrightness != 1.0f)
				{
					var scale = float2.Lerp(terrainInfo.MinHeightColorBrightness, terrainInfo.MaxHeightColorBrightness, HeightStep[uv] * 1f / Grid.MaximumTerrainHeight);
					left = Color.FromArgb((int)(scale * left.R).Clamp(0, 255), (int)(scale * left.G).Clamp(0, 255), (int)(scale * left.B).Clamp(0, 255));
					right = Color.FromArgb((int)(scale * right.R).Clamp(0, 255), (int)(scale * right.G).Clamp(0, 255), (int)(scale * right.B).Clamp(0, 255));
				}

				return (left, right);
			}
			else
			{
				return (CellInfos[uv].ColorA, CellInfos[uv].ColorB);
			}

		}

		public byte[] SavePreview()
		{
			var actorTypes = Rules.Actors.Values.Where(a => a.HasTraitInfo<IMapPreviewSignatureInfo>());
			var actors = ActorDefinitions.Where(a => actorTypes.Where(ai => ai.Name == a.Value.Value).Any());
			var positions = new List<(MPos Position, Color Color)>();
			foreach (var actor in actors)
			{
				var s = new ActorReference(actor.Value.Value, actor.Value.ToDictionary());

				var ai = Rules.Actors[actor.Value.Value];
				var impsis = ai.TraitInfos<IMapPreviewSignatureInfo>();
				foreach (var impsi in impsis)
					impsi.PopulateMapPreviewSignatureCells(this, ai, s, positions);
			}

			// ResourceLayer is on world actor, which isn't caught above, so an extra check for it.
			var worldActorInfo = Rules.Actors[SystemActors.World];
			var worldimpsis = worldActorInfo.TraitInfos<IMapPreviewSignatureInfo>();
			foreach (var worldimpsi in worldimpsis)
				worldimpsi.PopulateMapPreviewSignatureCells(this, worldActorInfo, null, positions);

			var isRectangularIsometric = Grid.Type == MapGridType.RectangularIsometric;

			var top = int.MaxValue;
			var bottom = int.MinValue;

			if (Grid.MaximumTerrainHeight > 0)
			{
				// The minimap is drawn in cell space, so we need to
				// unproject the PPos bounds to find the MPos boundaries.
				// This matches the calculation in RadarWidget that is used ingame
				for (var x = Bounds.Left; x < Bounds.Right; x++)
				{
					var allTop = Unproject(new PPos(x, Bounds.Top));
					var allBottom = Unproject(new PPos(x, Bounds.Bottom));
					if (allTop.Count > 0)
						top = Math.Min(top, allTop.MinBy(uv => uv.V).V);

					if (allBottom.Count > 0)
						bottom = Math.Max(bottom, allBottom.MaxBy(uv => uv.V).V);
				}
			}
			else
			{
				// If the mod uses flat maps, MPos == PPos and we can take the bounds rect directly
				top = Bounds.Top;
				bottom = Bounds.Bottom;
			}

			var width = Bounds.Width;
			var height = bottom - top;

			var bitmapWidth = width;
			if (isRectangularIsometric)
				bitmapWidth = 2 * bitmapWidth - 1;

			var stride = bitmapWidth * 4;
			var pxStride = 4;
			var minimapData = new byte[stride * height];
			(Color Left, Color Right) terrainColor = default;

			for (var y = 0; y < height; y++)
			{
				for (var x = 0; x < width; x++)
				{
					var uv = new MPos(x + Bounds.Left, y + top);

					// FirstOrDefault will return a (MPos.Zero, Color.Transparent) if positions is empty
					var actorColor = positions.FirstOrDefault(ap => ap.Position == uv).Color;
					if (actorColor.A == 0)
						terrainColor = GetTerrainColorPair(uv);

					if (isRectangularIsometric)
					{
						// Odd rows are shifted right by 1px
						var dx = uv.V & 1;
						var xOffset = pxStride * (2 * x + dx);
						if (x + dx > 0)
						{
							var z = y * stride + xOffset - pxStride;
							var c = actorColor.A == 0 ? terrainColor.Left : actorColor;
							minimapData[z++] = c.R;
							minimapData[z++] = c.G;
							minimapData[z++] = c.B;
							minimapData[z] = c.A;
						}

						if (xOffset < stride)
						{
							var z = y * stride + xOffset;
							var c = actorColor.A == 0 ? terrainColor.Right : actorColor;
							minimapData[z++] = c.R;
							minimapData[z++] = c.G;
							minimapData[z++] = c.B;
							minimapData[z] = c.A;
						}
					}
					else
					{
						var z = y * stride + pxStride * x;
						var c = actorColor.A == 0 ? terrainColor.Left : actorColor;
						minimapData[z++] = c.R;
						minimapData[z++] = c.G;
						minimapData[z++] = c.B;
						minimapData[z] = c.A;
					}
				}
			}

			var png = new Png(minimapData, SpriteFrameType.Rgba32, bitmapWidth, height);
			return png.Save();
		}

		public bool Contains(CPos cell)
		{
			if (Grid.Type == MapGridType.RectangularIsometric)
			{
				// .ToMPos() returns the same result if the X and Y coordinates
				// are switched. X < Y is invalid in the RectangularIsometric coordinate system,
				// so we pre-filter these to avoid returning the wrong result
				if (cell.X < cell.Y)
					return false;
			}
			else
			{
				// If the mod uses flat & rectangular maps, ToMPos and Contains(MPos) create unnecessary cost.
				// Just check if CPos is within map bounds.
				if (Grid.MaximumTerrainHeight == 0)
					return Bounds.Contains(cell.X, cell.Y);
			}

			return Contains(cell.ToMPos(this));
		}

		public bool Contains(MPos uv)
		{
			// The first check ensures that the cell is within the valid map region, avoiding
			// potential crashes in deeper code.  All CellLayers have the same geometry, and
			// CustomTerrain is convenient.
			return CustomTerrain.Contains(uv) && ContainsAllProjectedCellsCovering(uv);
		}

		bool ContainsAllProjectedCellsCovering(MPos uv)
		{
			// PERF: Checking the bounds directly here is the same as calling Contains((PPos)uv) but saves an allocation
			if (Grid.MaximumTerrainHeight == 0)
				return Bounds.Contains(uv.U, uv.V);

			// PERF: Most cells lie within a region where no matter their height,
			// all possible projected cells would remain in the map area.
			// For these, we can do a fast-path check.
			if (projectionSafeBounds.Contains(uv.U, uv.V))
				return true;

			// Now we need to do a slow-check. Determine the actual projected tiles
			// as they may or may not be in bounds depending on height.
			// If the cell has no valid projection, then we're off the map.
			var projectedCells = ProjectedCellsCovering(uv);
			if (projectedCells.Length == 0)
				return false;

			foreach (var puv in projectedCells)
				if (!Contains(puv))
					return false;

			return true;
		}

		public bool Contains(PPos puv)
		{
			return Bounds.Contains(puv.U, puv.V);
		}

		//public WPos CenterOfCell(CPos cell)
		//{
		//	if (Grid.Type == MapGridType.Rectangular)
		//		return new WPos(1024 * cell.X + 512, 1024 * cell.Y + 512, 0);

		//	// Convert from isometric cell position (x, y) to world position (u, v):
		//	// (a) Consider the relationships:
		//	//  - Center of origin cell is (512, 512)
		//	//  - +x adds (512, 512) to world pos
		//	//  - +y adds (-512, 512) to world pos
		//	// (b) Therefore:
		//	//  - ax + by adds (a - b) * 512 + 512 to u
		//	//  - ax + by adds (a + b) * 512 + 512 to v
		//	// (c) u, v coordinates run diagonally to the cell axes, and we define
		//	//     1024 as the length projected onto the primary cell axis
		//	//  - 512 * sqrt(2) = 724
		//	var z = Height.TryGetValue(cell, out var height) ? MapGrid.MapHeightStep * height + Grid.Ramps[Ramp[cell]].CenterHeightOffset : 0;
		//	return new WPos(724 * (cell.X - cell.Y + 1), 724 * (cell.X + cell.Y + 1), z);
		//}

		public WPos CenterOfCell(CPos cell)
		{
			if (CellInfos != null && CellInfos.Contains(cell))
				return CellInfos[cell].CellCenterPos;

			return new WPos(724 * (cell.X - cell.Y + 1), 724 * (cell.X + cell.Y + 1), 0);
		}

		public WPos CenterOfCell(MPos uv)
		{
			if (CellInfos != null && CellInfos.Contains(uv))
				return CellInfos[uv].CellCenterPos;

			var cell = uv.ToCPos(this);
			return new WPos(724 * (cell.X - cell.Y + 1), 724 * (cell.X + cell.Y + 1), 0);
		}

		public int MiniHeightOfCell(MPos uv)
		{
			if (CellInfos != null && CellInfos.Contains(uv))
				return CellInfos[uv].CellMiniHeight;

			return 0;
		}

		public int MiniHeightOfCell(CPos cell)
		{
			if (CellInfos != null && CellInfos.Contains(cell))
				return CellInfos[cell].CellMiniHeight;

			return 0;
		}

		public WPos CenterOfSubCell(CPos cell, SubCell subCell)
		{
			var index = (int)subCell;
			if (index >= 0 && index < Grid.SubCellOffsets.Length)
			{
				var center = CenterOfCell(cell);
				var offset = Grid.SubCellOffsets[index];
				var result = center + offset;
				result = new WPos(result.X, result.Y, HeightOfTerrain(result));
				return result;
			}

			return CenterOfCell(cell);
		}

		int Lerp(int a, int b, int mul, int div)
		{
			return a + (b - a) * mul / div;
		}

		public int HeightOfTerrain(in WPos pos)
		{
			if (pos.X < 0 || pos.Y < 0)
				return 0;

			var u = pos.X / MapGrid.MapMiniCellWidth;
			var v = pos.Y / MapGrid.MapMiniCellWidth;
			if (u < 0 || v < 0 || u >= VertexArrayWidth - 1 || v >= VertexArrayHeight - 1)
				return 0;

			var tx = pos.X % MapGrid.MapMiniCellWidth;
			var ty = pos.Y % MapGrid.MapMiniCellWidth;

			var tl = TerrainVertices[MiniCells[v, u].TL].LogicPos.Z;
			var tr = TerrainVertices[MiniCells[v, u].TR].LogicPos.Z;
			var bl = TerrainVertices[MiniCells[v, u].BL].LogicPos.Z;
			var br = TerrainVertices[MiniCells[v, u].BR].LogicPos.Z;

			// if (u % 2 == v % 2)
			if (MiniCells[v, u].Type == MiniCellType.TRBL)
			{
				// tl           tr
				// ------------
				// |           / |
				// |      /      |
				// |  /          |
				// ------------
				// bl          br
				if (ty == 724 - tx)
				{
					return Lerp(bl, tr, tx, 724);
				}
				else if (ty < 724 - tx)
				{
					var a = Lerp(tl, bl, ty, 724);
					var b = Lerp(tr, bl, ty, 724);
					return Lerp(a, b, tx, 724 - ty);
				}
				else
				{
					var a = Lerp(bl, br, tx, 724);
					var b = Lerp(bl, tr, tx, 724);
					return Lerp(a, b, 724 - ty, tx);
				}
			}
			else
			{
				// tl           tr
				// ------------
				// |  \          |
				// |      \      |
				// |          \  |
				// ------------
				// bl          br
				if (tx == ty)
				{
					return Lerp(tl, br, tx, 724);
				}
				else if (tx > ty)
				{
					var a = Lerp(tl, tr, tx, 724);
					var b = Lerp(tl, br, tx, 724);
					return Lerp(a, b, ty, tx);
				}
				else
				{
					var a = Lerp(tl, bl, ty, 724);
					var b = Lerp(tl, br, ty, 724);
					return Lerp(a, b, tx, ty);
				}
			}
		}

		public int HeightOfCell(CPos cell)
		{
			if (CellInfos != null && CellInfos.Contains(cell))
				return CellInfos[cell].CellCenterPos.Z;
			else
				return 0;
		}

		public int HeightOfCell(MPos uv)
		{
			if (CellInfos != null && CellInfos.Contains(uv))
				return CellInfos[uv].CellCenterPos.Z;
			else
				return 0;
		}

		public WDist DistanceAboveTerrain(WPos pos)
		{
			if (Grid.Type == MapGridType.Rectangular)
				return new WDist(pos.Z);

			return new WDist(pos.Z - HeightOfTerrain(pos));
		}

		public WRot TerrainOrientation(CPos cell)
		{
			// if (Ramp.TryGetValue(cell, out var ramp))
			// 	return Grid.Ramps[ramp].Orientation;
			// else
			// 	return WRot.None;
			if (CellInfos.Contains(cell))
			{
				//if (Game.GetWorldRenderer() != null)
				//{
				//	var pos = CenterOfCell(cell);
				//	var start = World3DCoordinate.WPosToFloat3(pos);
				//	var end = start + 10 * World3DCoordinate.TSVec3ToFloat3(CellInfos[cell].LogicNml.normalized);
				//	var world = Game.GetWorldRenderer().World;
				//	world.AddFrameEndTask(w => w.Add(new DrawWorldLineEffect(world, pos, start, end, new WDist(64), Color.AliceBlue, 5)));
				//}

				return CellInfos[cell].TerrainOrientationM;
			}
			else
				return WRot.None;
		}

		public WRot TerrainOrientation(WPos pos)
		{
			var cell = CellContaining(pos);
			if (CellInfos.Contains(cell))
			{
				if (pos.X == CellInfos[cell].CellCenterPos.X && pos.Y == CellInfos[cell].CellCenterPos.Y)
					return CellInfos[cell].TerrainOrientationM;

				if (pos.X < CellInfos[cell].CellCenterPos.X)
				{
					if (pos.Y < CellInfos[cell].CellCenterPos.Y)
						return CellInfos[cell].TerrainOrientationTL;
					else
						return CellInfos[cell].TerrainOrientationBL;
				}
				else
				{
					if (pos.Y < CellInfos[cell].CellCenterPos.Y)
						return CellInfos[cell].TerrainOrientationTR;
					else
						return CellInfos[cell].TerrainOrientationBR;
				}
			}
			else
				return WRot.None;
		}

		/// <summary>
		/// calculate WRot of TerrainOrientation from fix point normal
		/// </summary>
		/// <param name="normal"> should be normalized</param>
		/// <returns>TerrainOrientation</returns>
		public static WRot NormalToTerrainOrientation(in TSVector normal)
		{
			if (normal.x == 0 && normal.y == 0)
				return WRot.None;

			var horizon = new TSVector2(normal.x, normal.y);
			var hl = horizon.magnitude;
			var angle = new WAngle((int)(FP.Asin(hl) * 512 / FP.Pi));

			var hnml = horizon.normalized;
			WVec dir = new WVec(
				(int)(-hnml.x * 1024),
				(int)(hnml.y * 1024),
				0);

			// debug draw
			// if (Game.GetWorldRenderer() != null)
			// {
			// 	var pos = CenterOfCell(cell);
			// 	var start = World3DCoordinate.WPosToFloat3(pos);
			// 	var end = start + 10 * World3DCoordinate.TSVec3ToFloat3(normal.normalized);
			// 	var world = Game.GetWorldRenderer().World;
			// 	world.AddFrameEndTask(w => w.Add(new DrawWorldLineEffect(world, pos, start, end, new WDist(64), Color.AliceBlue, 5)));
			// }

			return new WRot(dir, angle);
		}

		public TSVector NormalOfTerrain(in WPos pos)
		{
			var cell = CellContaining(pos);

			if (!CellInfos.Contains(cell))
				return new TSVector(0, 0, 1);

			var cellinfos = CellInfos[cell];
			var center = cellinfos.CellCenterPos;
			var tl = cellinfos.LogicNmlTL;
			var tr = cellinfos.LogicNmlTR;
			var bl = cellinfos.LogicNmlBL;
			var br = cellinfos.LogicNmlTR;

			if (pos.X < center.X && pos.Y < center.Y)
			{
				return tl;
			}
			else if (pos.X > center.X && pos.Y < center.Y)
			{
				return tr;
			}
			else if (pos.X < center.X && pos.Y > center.Y)
			{
				return bl;
			}
			else if (pos.X > center.X && pos.Y > center.Y)
			{
				return br;
			}
			else
			{
				return cellinfos.LogicNml;
			}

		}

		public WVec Offset(CVec delta, int dz)
		{
			if (Grid.Type == MapGridType.Rectangular)
				return new WVec(1024 * delta.X, 1024 * delta.Y, 0);

			return new WVec(724 * (delta.X - delta.Y), 724 * (delta.X + delta.Y), MapGrid.MapHeightStep * dz);
		}

		/// <summary>
		/// The size of the map Height step in world units
		/// </summary>
		/// RectangularIsometric defines 1024 units along the diagonal axis,
		/// giving a half-tile height step of sqrt(2) * 512
		public WDist CellHeightStep => new WDist(Grid.Type == MapGridType.RectangularIsometric ? MapGrid.MapHeightStep : 512);

		public CPos CellContaining(WPos pos)
		{
			if (Grid.Type == MapGridType.Rectangular)
				return new CPos(pos.X / 1024, pos.Y / 1024);

			// Convert from world position to isometric cell position:
			// (a) Subtract ([1/2 cell], [1/2 cell]) to move the rotation center to the middle of the corner cell
			// (b) Rotate axes by -pi/4 to align the world axes with the cell axes
			// (c) Apply an offset so that the integer division by [1 cell] rounds in the right direction:
			//      (i) u is always positive, so add [1/2 cell] (which then partially cancels the -[1 cell] term from the rotation)
			//     (ii) v can be negative, so we need to be careful about rounding directions.  We add [1/2 cell] *away from 0* (negative if y > x).
			// (e) Divide by [1 cell] to bring into cell coords.
			// The world axes are rotated relative to the cell axes, so the standard cell size (1024) is increased by a factor of sqrt(2)
			var u = (pos.Y + pos.X - 724) / 1448;
			var v = (pos.Y - pos.X + (pos.Y > pos.X ? 724 : -724)) / 1448;
			return new CPos(u, v);
		}

		public PPos ProjectedCellCovering(WPos pos)
		{
			var projectedPos = pos - new WVec(0, pos.Z, pos.Z);
			return (PPos)CellContaining(projectedPos).ToMPos(Grid.Type);
		}

		static readonly PPos[] NoProjectedCells = Array.Empty<PPos>();
		public PPos[] ProjectedCellsCovering(MPos uv)
		{
			if (!initializedCellProjection)
				InitializeCellProjection();

			if (!cellProjection.Contains(uv))
				return NoProjectedCells;

			return cellProjection[uv];
		}

		public List<MPos> Unproject(PPos puv)
		{
			var uv = (MPos)puv;

			if (!initializedCellProjection)
				InitializeCellProjection();

			if (!inverseCellProjection.Contains(uv))
				return new List<MPos>();

			return inverseCellProjection[uv];
		}

		public byte ProjectedHeight(PPos puv)
		{
			return projectedHeight[(MPos)puv];
		}

		public WAngle FacingBetween(CPos cell, CPos towards, WAngle fallbackfacing)
		{
			var delta = CenterOfCell(towards) - CenterOfCell(cell);
			if (delta.HorizontalLengthSquared == 0)
				return fallbackfacing;

			return delta.Yaw;
		}

		public void Resize(int width, int height)
		{
			Console.WriteLine("The Resize Method still in WIP, don't use this");
			var oldMapTiles = Tiles;
			var oldMapResources = Resources;
			var oldMapHeight = HeightStep;
			var oldMapRamp = Ramp;
			var oldCellInfos = CellInfos;
			var newSize = new Size(width, height);

			Tiles = CellLayer.Resize(oldMapTiles, newSize, oldMapTiles[MPos.Zero]);
			Resources = CellLayer.Resize(oldMapResources, newSize, oldMapResources[MPos.Zero]);
			HeightStep = CellLayer.Resize(oldMapHeight, newSize, oldMapHeight[MPos.Zero]);
			Ramp = CellLayer.Resize(oldMapRamp, newSize, oldMapRamp[MPos.Zero]);
			CellInfos = CellLayer.Resize(oldCellInfos, newSize, oldCellInfos[MPos.Zero]);
			MapSize = new int2(newSize);

			var tl = new MPos(0, 0);
			var br = new MPos(MapSize.X - 1, MapSize.Y - 1);
			AllCells = new CellRegion(Grid.Type, tl.ToCPos(this), br.ToCPos(this));
			SetBounds(new PPos(tl.U + 1, tl.V + 1), new PPos(br.U - 1, br.V - 1));
		}

		public void SetBounds(PPos tl, PPos br)
		{
			// The tl and br coordinates are inclusive, but the Rectangle
			// is exclusive.  Pad the right and bottom edges to match.
			Bounds = Rectangle.FromLTRB(tl.U, tl.V, br.U + 1, br.V + 1);

			// See ProjectCellInner to see how any given position may be projected.
			// U: May gain or lose 1, so bring in the left and right edge by 1.
			// V: For an even height tile, this ranges from 0 to height
			//    For an odd tile, the height may get rounded up to next even.
			//    Then also it projects to four tiles which adds one more to the possible height change.
			//    So we get a range of 0 to height + 1 + 1.
			//    As the height only goes upwards, we only need to make room at the top of the map and not the bottom.
			var maxHeight = Grid.MaximumTerrainHeight;
			if ((maxHeight & 1) == 1)
				maxHeight += 2;
			projectionSafeBounds = Rectangle.FromLTRB(
				Bounds.Left + 1,
				Bounds.Top + maxHeight,
				Bounds.Right - 1,
				Bounds.Bottom);

			// Directly calculate the projected map corners in world units avoiding unnecessary
			// conversions.  This abuses the definition that the width of the cell along the x world axis
			// is always 1024 or 1448 units, and that the height of two rows is 2048 for classic cells and 724
			// for isometric cells.
			if (Grid.Type == MapGridType.RectangularIsometric)
			{
				ProjectedTopLeft = new WPos(tl.U * 1448, tl.V * 724, 0);
				ProjectedBottomRight = new WPos(br.U * 1448 - 1, (br.V + 1) * 724 - 1, 0);
			}
			else
			{
				ProjectedTopLeft = new WPos(tl.U * 1024, tl.V * 1024, 0);
				ProjectedBottomRight = new WPos(br.U * 1024 - 1, (br.V + 1) * 1024 - 1, 0);
			}

			// PERF: This enumeration isn't going to change during the game
			ProjectedCells = new ProjectedCellRegion(this, tl, br).ToArray();
		}

		public byte GetTerrainIndex(CPos cell)
		{
			// Lazily initialize a cache for terrain indexes.
			if (cachedTerrainIndexes == null)
			{
				cachedTerrainIndexes = new CellLayer<short>(this);
				cachedTerrainIndexes.Clear(InvalidCachedTerrainIndex);
			}

			var uv = cell.ToMPos(this);
			var terrainIndex = cachedTerrainIndexes[uv];

			// PERF: Cache terrain indexes per cell on demand.
			if (terrainIndex == InvalidCachedTerrainIndex)
			{
				var custom = CustomTerrain[uv];
				terrainIndex = cachedTerrainIndexes[uv] = custom != byte.MaxValue ? custom : CellInfos[uv].TerrainType;
			}

			return (byte)terrainIndex;
		}

		public TerrainTypeInfo GetTerrainInfo(CPos cell)
		{
			return Rules.TerrainInfo.TerrainTypes[GetTerrainIndex(cell)];
		}

		public CPos Clamp(CPos cell)
		{
			return Clamp(cell.ToMPos(this)).ToCPos(this);
		}

		public MPos Clamp(MPos uv)
		{
			if (Grid.MaximumTerrainHeight == 0)
				return (MPos)Clamp((PPos)uv);

			// Already in bounds, so don't need to do anything.
			if (ContainsAllProjectedCellsCovering(uv))
				return uv;

			// Clamping map coordinates is trickier than it might first look!
			// This needs to handle three nasty cases:
			//  * The requested cell is well outside the map region
			//  * The requested cell is near the top edge inside the map but outside the projected layer
			//  * The clamped projected cell lands on a cliff face with no associated map cell
			//
			// Handling these cases properly requires abuse of our knowledge of the projection transform.
			//
			// The U coordinate doesn't change significantly in the projection, so clamp this
			// straight away and ensure the point is somewhere inside the map
			uv = cellProjection.Clamp(new MPos(uv.U.Clamp(Bounds.Left, Bounds.Right), uv.V));

			// Project this guessed cell and take the first available cell
			// If it is projected outside the layer, then make another guess.
			var allProjected = ProjectedCellsCovering(uv);
			var projected = allProjected.Length > 0 ? allProjected.First()
				: new PPos(uv.U, uv.V.Clamp(Bounds.Top, Bounds.Bottom));

			// Clamp the projected cell to the map area
			projected = Clamp(projected);

			// Project the cell back into map coordinates.
			// This may fail if the projected cell covered a cliff or another feature
			// where there is a large change in terrain height.
			var unProjected = Unproject(projected);
			if (unProjected.Count == 0)
			{
				// Adjust V until we find a cell that works
				for (var x = 2; x <= 2 * Grid.MaximumTerrainHeight; x++)
				{
					var dv = ((x & 1) == 1 ? 1 : -1) * x / 2;
					var test = new PPos(projected.U, projected.V + dv);
					if (!Contains(test))
						continue;

					unProjected = Unproject(test);
					if (unProjected.Count > 0)
						break;
				}

				// This shouldn't happen.  But if it does, return the original value and hope the caller doesn't explode.
				if (unProjected.Count == 0)
				{
					Log.Write("debug", "Failed to clamp map cell {0} to map bounds", uv);
					return uv;
				}
			}

			return projected.V == Bounds.Bottom ? unProjected.MaxBy(x => x.V) : unProjected.MinBy(x => x.V);
		}

		public PPos Clamp(PPos puv)
		{
			var bounds = new Rectangle(Bounds.X, Bounds.Y, Bounds.Width - 1, Bounds.Height - 1);
			return puv.Clamp(bounds);
		}

		public CPos ChooseRandomCell(MersenneTwister rand)
		{
			List<MPos> cells;
			do
			{
				var u = rand.Next(Bounds.Left, Bounds.Right);
				var v = rand.Next(Bounds.Top, Bounds.Bottom);

				cells = Unproject(new PPos(u, v));
			}
			while (cells.Count == 0);

			return cells.Random(rand).ToCPos(Grid.Type);
		}

		public CPos ChooseClosestEdgeCell(CPos cell)
		{
			return ChooseClosestEdgeCell(cell.ToMPos(Grid.Type)).ToCPos(Grid.Type);
		}

		public MPos ChooseClosestEdgeCell(MPos uv)
		{
			var allProjected = ProjectedCellsCovering(uv);

			PPos edge;
			if (allProjected.Length > 0)
			{
				var puv = allProjected.First();
				var horizontalBound = ((puv.U - Bounds.Left) < Bounds.Width / 2) ? Bounds.Left : Bounds.Right;
				var verticalBound = ((puv.V - Bounds.Top) < Bounds.Height / 2) ? Bounds.Top : Bounds.Bottom;

				var du = Math.Abs(horizontalBound - puv.U);
				var dv = Math.Abs(verticalBound - puv.V);

				edge = du < dv ? new PPos(horizontalBound, puv.V) : new PPos(puv.U, verticalBound);
			}
			else
				edge = new PPos(Bounds.Left, Bounds.Top);

			var unProjected = Unproject(edge);
			if (unProjected.Count == 0)
			{
				// Adjust V until we find a cell that works
				for (var x = 2; x <= 2 * Grid.MaximumTerrainHeight; x++)
				{
					var dv = ((x & 1) == 1 ? 1 : -1) * x / 2;
					var test = new PPos(edge.U, edge.V + dv);
					if (!Contains(test))
						continue;

					unProjected = Unproject(test);
					if (unProjected.Count > 0)
						break;
				}

				// This shouldn't happen.  But if it does, return the original value and hope the caller doesn't explode.
				if (unProjected.Count == 0)
				{
					Log.Write("debug", "Failed to find closest edge for map cell {0}", uv);
					return uv;
				}
			}

			return edge.V == Bounds.Bottom ? unProjected.MaxBy(x => x.V) : unProjected.MinBy(x => x.V);
		}

		public CPos ChooseClosestMatchingEdgeCell(CPos cell, Func<CPos, bool> match)
		{
			return AllEdgeCells.OrderBy(c => (cell - c).Length).FirstOrDefault(c => match(c));
		}

		List<CPos> UpdateEdgeCells()
		{
			var edgeCells = new List<CPos>();
			var unProjected = new List<MPos>();
			var bottom = Bounds.Bottom - 1;
			for (var u = Bounds.Left; u < Bounds.Right; u++)
			{
				unProjected = Unproject(new PPos(u, Bounds.Top));
				if (unProjected.Count > 0)
					edgeCells.Add(unProjected.MinBy(x => x.V).ToCPos(Grid.Type));

				unProjected = Unproject(new PPos(u, bottom));
				if (unProjected.Count > 0)
					edgeCells.Add(unProjected.MaxBy(x => x.V).ToCPos(Grid.Type));
			}

			for (var v = Bounds.Top; v < Bounds.Bottom; v++)
			{
				unProjected = Unproject(new PPos(Bounds.Left, v));
				if (unProjected.Count > 0)
					edgeCells.Add((v == bottom ? unProjected.MaxBy(x => x.V) : unProjected.MinBy(x => x.V)).ToCPos(Grid.Type));

				unProjected = Unproject(new PPos(Bounds.Right - 1, v));
				if (unProjected.Count > 0)
					edgeCells.Add((v == bottom ? unProjected.MaxBy(x => x.V) : unProjected.MinBy(x => x.V)).ToCPos(Grid.Type));
			}

			return edgeCells;
		}

		public CPos ChooseRandomEdgeCell(MersenneTwister rand)
		{
			return AllEdgeCells.Random(rand);
		}

		public WDist DistanceToEdge(WPos pos, in WVec dir)
		{
			var projectedPos = pos - new WVec(0, pos.Z, pos.Z);
			var x = dir.X == 0 ? int.MaxValue : ((dir.X < 0 ? ProjectedTopLeft.X : ProjectedBottomRight.X) - projectedPos.X) / dir.X;
			var y = dir.Y == 0 ? int.MaxValue : ((dir.Y < 0 ? ProjectedTopLeft.Y : ProjectedBottomRight.Y) - projectedPos.Y) / dir.Y;
			return new WDist(Math.Min(x, y) * dir.Length);
		}

		// Both ranges are inclusive because everything that calls it is designed for maxRange being inclusive:
		// it rounds the actual distance up to the next integer so that this call
		// will return any cells that intersect with the requested range circle.
		// The returned positions are sorted by distance from the center.
		public IEnumerable<CPos> FindTilesInAnnulus(CPos center, int minRange, int maxRange, bool allowOutsideBounds = false)
		{
			if (maxRange < minRange)
				throw new ArgumentOutOfRangeException(nameof(maxRange), "Maximum range is less than the minimum range.");

			if (maxRange >= Grid.TilesByDistance.Length)
				throw new ArgumentOutOfRangeException(nameof(maxRange),
					$"The requested range ({maxRange}) cannot exceed the value of MaximumTileSearchRange ({Grid.MaximumTileSearchRange})");

			for (var i = minRange; i <= maxRange; i++)
			{
				foreach (var offset in Grid.TilesByDistance[i])
				{
					var t = offset + center;
					if (allowOutsideBounds ? CellInfos.Contains(t) : Contains(t))
						yield return t;
				}
			}
		}

		public IEnumerable<CPos> FindTilesInCircle(CPos center, int maxRange, bool allowOutsideBounds = false)
		{
			return FindTilesInAnnulus(center, 0, maxRange, allowOutsideBounds);
		}

		public Stream Open(string filename)
		{
			// Explicit package paths never refer to a map
			if (!filename.Contains('|') && Package.Contains(filename))
				return Package.GetStream(filename);

			return modData.DefaultFileSystem.Open(filename);
		}

		public bool TryGetPackageContaining(string path, out IReadOnlyPackage package, out string filename)
		{
			// Packages aren't supported inside maps
			return modData.DefaultFileSystem.TryGetPackageContaining(path, out package, out filename);
		}

		public bool TryOpen(string filename, out Stream s)
		{
			// Explicit package paths never refer to a map
			if (!filename.Contains('|'))
			{
				s = Package.GetStream(filename);
				if (s != null)
					return true;
			}

			return modData.DefaultFileSystem.TryOpen(filename, out s);
		}

		public bool Exists(string filename)
		{
			// Explicit package paths never refer to a map
			if (!filename.Contains('|') && Package.Contains(filename))
				return true;

			return modData.DefaultFileSystem.Exists(filename);
		}

		public bool IsExternalModFile(string filename)
		{
			// Explicit package paths never refer to a map
			if (filename.Contains('|'))
				return modData.DefaultFileSystem.IsExternalModFile(filename);

			return false;
		}

		public string Translate(string key, IDictionary<string, object> args = null)
		{
			if (Translation.TryGetString(key, out var message, args))
				return message;

			return modData.Translation.GetString(key, args);
		}
	}
}
