using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using OpenRA.FileSystem;
using OpenRA.Primitives;

namespace OpenRA.Graphics
{
	public enum UsageType
	{
		Terrain,
		Overlay,
		Smudge,
		Shroud,
		Mask,
	}

	public class MapTextureCache
	{
		public readonly Map Map;
		public readonly Sheet[] CausticsTextures;
		public readonly Dictionary<string, (string, Sheet)> AdditionTextures = new Dictionary<string, (string, Sheet)>();
		public readonly HashSet<string> TerrainTexturesSet = new HashSet<string>();
		public readonly HashSet<string> SmudgeTexturesSet = new HashSet<string>();

		public readonly List<string>[] LayerTileTypes = new List<string>[9];

		public readonly Dictionary<string, List<int>> TileTypeTexIndices = new Dictionary<string, List<int>>();
		public readonly Dictionary<string, (int, float)> TileArrayTextures = new Dictionary<string, (int, float)>();
		public readonly Dictionary<string, MaskBrush> AllBrushes = new Dictionary<string, MaskBrush>();
		public readonly ITexture TileTextureArray;
		public readonly ITexture TileNormalTextureArray;

		public readonly ITexture BrushTextureArray;

		public MapTextureCache(Map map)
		{
			Map = map;
			//AddTexture("GrassNormal", "GrassNormal.png", "GrassNormal");

			CausticsTextures = new Sheet[32];

			for (int i = 0; i < CausticsTextures.Length; i++)
			{
				var filename = "caustics_" + (i + 1).ToString().PadLeft(4, '0') + ".png";
				if (!map.Exists(filename))
				{
					throw new Exception(" Can not find texture " + filename);
				}

				CausticsTextures[i] = new Sheet( map.Open(filename), TextureWrap.Repeat);
			}

			AddTexture("MaskCloud", "maskcloud01.png", "MaskCloud", UsageType.Mask);

			// tiles
			string tileSet;
			if (string.IsNullOrEmpty(Map.TileTexSet) || Map.TileTexSet == "DEFAULT")
				tileSet = Map.Tileset.ToLowerInvariant() + "-tileset.yaml";
			else
				tileSet = Map.TileTexSet.ToLowerInvariant() + "-tileset.yaml";

			if (!map.Exists(tileSet))
				throw new Exception("Can't Find " + tileSet + " to define tiles texture");

			// tile textures
			{
				var nodes = MiniYaml.FromStream(map.Open(tileSet));
				Dictionary<string ,Dictionary<string, MiniYaml>> typeDefine = new Dictionary<string, Dictionary<string, MiniYaml>>();
				int texCount = 0;

				for (int i = 0; i < LayerTileTypes.Length; i++)
				{
					LayerTileTypes[i] = new List<string>();
				}

				foreach (var node in nodes)
				{
					if (node.Key == "TypeDefine")
					{
						var types = node.Value.ToDictionary();
						foreach (var (typename, typeYaml) in types)
						{
							int layer = Convert.ToInt32(typeYaml.Value);
							if (layer < 0 || layer > 8)
								throw new Exception("Layer Index Should be 0 - 8");

							LayerTileTypes[layer].Add(typename);

							var texs = typeYaml.ToDictionary();
							texCount += texs.Count;
							typeDefine.Add(typename, texs);
						}
					}
					else if (node.Key == "WaterDefine")
					{
						var info = node.Value.ToDictionary();
						var water = ReadYamlInfo.LoadField(info, "Color", "Water") + ".png";
						var normal = ReadYamlInfo.LoadField(info, "Normal", "WaterNormal") + ".png";
						AddTexture("Water", water, "WaterNormal");
						AddTexture("WaterNormal", normal, "WaterNormal");
					}
				}

				TileTextureArray = Game.Renderer.Context.CreateTextureArray(texCount);
				TileNormalTextureArray = Game.Renderer.Context.CreateTextureArray(texCount);
				foreach (var (typeName, typeTexs) in typeDefine)
				{
					foreach (var (texName, texYaml) in typeTexs)
					{
						var info = texYaml.ToDictionary();
						var scale = ReadYamlInfo.LoadField(info, "Scale", 1f);
						if (!AddTileTexture(typeName + "-" + texName, texYaml.Value, scale, typeName))
							throw new Exception("duplicate " + typeName + "-" + texName + " in " + tileSet);
					}
				}

				Console.WriteLine("TileArrayTextures.Count" + TileArrayTextures.Count);

			}

			// brushes
			var brushesSet = "brush-set.yaml";
			if (!map.Exists(brushesSet))
				throw new Exception("Can't Find " + brushesSet + " to define tiles texture");

			List<MiniYamlNode> brushnodes = MiniYaml.FromStream(map.Open(brushesSet));

			BrushTextureArray = Game.Renderer.Context.CreateTextureArray(brushnodes.Count);

			foreach (var node in brushnodes)
			{
				var info = node.Value.ToDictionary();
				if (!AddBrushTexture(node.Key, node.Value.Value, map, info))
					throw new Exception("duplicate " + node.Key + " in " + brushesSet);
			}

			Console.WriteLine("AllBrush.Count" + AllBrushes.Count);
			Map.TextureCache = this;
		}

		public void RefreshAllTextures()
		{
			foreach (var sheet in CausticsTextures)
				sheet?.RefreshTexture();
			foreach (var t in AdditionTextures.Values)
			{
				t.Item2?.RefreshTexture();
			}
		}

		public void DisposeAllTextures()
		{
			foreach (var sheet in CausticsTextures)
				sheet?.Dispose();
			foreach (var t in AdditionTextures.Values)
			{
				t.Item2?.Dispose();
			}

			TileTextureArray?.Dispose();
			TileNormalTextureArray?.Dispose();
			BrushTextureArray?.Dispose();
		}

		public bool AddTexture(string name, string filename, string uniform, UsageType type = UsageType.Terrain)
		{
			if (AdditionTextures.ContainsKey(name))
			{
				switch (type)
				{
					case UsageType.Terrain:
						TerrainTexturesSet.Add(name);
						break;
					case UsageType.Smudge:
						SmudgeTexturesSet.Add(name);
						break;
				}

				return true;
			}

			if (!Map.Exists(filename))
			{
				throw new Exception(filename + " Can not find texture " + name);
			}

			var sheet = new Sheet(Map.Open(filename), TextureWrap.Repeat);

			AdditionTextures.Add(name, (uniform, sheet));
			switch (type)
			{
				case UsageType.Terrain:
					TerrainTexturesSet.Add(name);
					break;
				case UsageType.Smudge:
					SmudgeTexturesSet.Add(name);
					break;
			}

			return true;
		}

		public bool AddTileTexture(string name, string filename, float scale, string type)
		{
			if (TileArrayTextures.ContainsKey(name))
				return false;

			if (!Map.Exists(filename + ".png"))
			{
				throw new Exception(filename + " Can not find texture " + name);
			}

			var sheet = new Sheet(Map.Open(filename + ".png"), TextureWrap.Repeat);

			TileTextureArray.SetData(sheet.GetData(), sheet.Size.Width, sheet.Size.Height);

			if (Map.Exists(filename + "_NORM.png"))
			{
				sheet = new Sheet(Map.Open(filename + "_NORM.png"), TextureWrap.Repeat);
				TileNormalTextureArray.SetData(sheet.GetData(), sheet.Size.Width, sheet.Size.Height);
			}
			else
			{
				var data = new byte[4 * sheet.Size.Width * sheet.Size.Height];
				for (int i = 0; i < sheet.Size.Width * sheet.Size.Height; i++)
				{
					data[i] = 0;
					data[i + 1] = 0;
					data[i + 2] = 0;
					data[i + 3] = 0;
				}

				TileNormalTextureArray.SetData(data, sheet.Size.Width, sheet.Size.Height);
			}

			if (TileTypeTexIndices.ContainsKey(type))
			{
				TileTypeTexIndices[type].Add(TileArrayTextures.Count);
			}
			else
			{
				TileTypeTexIndices.Add(type, new List<int>());
				TileTypeTexIndices[type].Add(TileArrayTextures.Count);
			}

			TileArrayTextures.Add(name, (TileArrayTextures.Count, scale));

			return true;
		}

		public bool AddBrushTexture(string name, string filename, Map map, Dictionary<string, MiniYaml> info)
		{
			if (AllBrushes.ContainsKey(name))
				return false;

			if (!Map.Exists(filename))
			{
				throw new Exception(filename + " Can not find texture " + name);
			}

			var size = ReadYamlInfo.LoadField(info, "Size", new WDist(1024));
			var categories = ReadYamlInfo.LoadField(info, "Categories", new string[] { "Common" });

			var sheet = new Sheet(Map.Open(filename), TextureWrap.Repeat);

			BrushTextureArray.SetData(sheet.GetData(), sheet.Size.Width, sheet.Size.Height);

			AllBrushes.Add(name, new MaskBrush(name, categories, AllBrushes.Count, AllBrushes.Count, new int2(sheet.Size.Width, sheet.Size.Height), size.Length, map));

			return true;
		}

		public bool ReadMapTexture(string filename, TextureWrap textureWrap, out Sheet sheet)
		{
			if (!Map.Exists(filename))
			{
				sheet = null;
				return false;
			}

			sheet = new Sheet(Map.Open(filename), textureWrap);

			return true;
		}
	}
}
