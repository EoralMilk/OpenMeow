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
		public readonly Dictionary<string, (string, Sheet)> Textures = new Dictionary<string, (string, Sheet)>();
		public readonly HashSet<string> TerrainTexturesSet = new HashSet<string>();
		public readonly HashSet<string> SmudgeTexturesSet = new HashSet<string>();

		public readonly Dictionary<string, int> TileArrayTextures = new Dictionary<string, int>();
		public readonly Dictionary<string, MaskBrush> AllBrushes = new Dictionary<string, MaskBrush>();
		public readonly ITexture TileTextureArray;
		public readonly ITexture BrushTextureArray;

		public const string TN_GrassNormal	= "Texture4";
		public const string TN_CliffNormal		= "Texture5";
		public const string TN_Cliff				= "Texture6";
		public const string TN_SlopeNormal	= "Texture7";
		public const string TN_Slope				= "Texture8";
		public const string TN_WaterNormal	= "Texture9";
		public const string TN_Caustics			= "Texture10";
		public const string TN_Scroch			= "Texture4";

		public MapTextureCache(Map map)
		{
			Map = map;
			AddTexture("WaterNormal", "WaterNormal.png", TN_WaterNormal);
			AddTexture("GrassNormal", "GrassNormal.png", TN_GrassNormal);
			AddTexture("Cliff", "Cliff.png", TN_Cliff);
			AddTexture("CliffNormal", "CliffNormal.png", TN_CliffNormal);
			AddTexture("Slope", "Slope.png", TN_Slope);
			AddTexture("SlopeNormal", "SlopeNormal.png", TN_SlopeNormal);

			AddTexture("Scroch", "Scroch.png", TN_Scroch, UsageType.Smudge);

			CausticsTextures = new Sheet[32];

			for (int i = 0; i < CausticsTextures.Length; i++)
			{
				var filename = "caustics_" + (i + 1).ToString().PadLeft(3, '0') + ".png";
				if (!map.Exists(filename))
				{
					throw new Exception(" Can not find texture " + filename);
				}

				CausticsTextures[i] = new Sheet( map.Open(filename), TextureWrap.Repeat);
			}

			// tiles
			var tileSet = "tile-set.yaml";

			if (!map.Exists(tileSet))
				throw new Exception("Can't Find " + tileSet + " to define tiles texture");
			List<MiniYamlNode> tileNodes = MiniYaml.FromStream(map.Open(tileSet));

			TileTextureArray = Game.Renderer.Context.CreateTextureArray(tileNodes.Count);

			foreach (var node in tileNodes)
			{
				if (!AddTileTexture(node.Key, node.Value.Value))
					throw new Exception("duplicate " + node.Key + " in " + tileSet);
			}

			// brushes
			var brushesSet = "brush-set.yaml";
			if (!map.Exists(brushesSet))
				throw new Exception("Can't Find " + brushesSet + " to define tiles texture");

			List<MiniYamlNode> brushnodes = MiniYaml.FromStream(map.Open(brushesSet));

			BrushTextureArray = Game.Renderer.Context.CreateTextureArray(brushnodes.Count);
			Console.WriteLine("brushyaml.Count" + brushnodes.Count);
			foreach (var node in brushnodes)
			{
				var info = node.Value.ToDictionary();
				var size = ReadYamlInfo.LoadField(info, "Size", new WDist(1024));
				if (!AddBrushTexture(node.Key, node.Value.Value, map, size.Length))
					throw new Exception("duplicate " + node.Key + " in " + brushesSet);
				Console.WriteLine(node.Key + ": Size " + size.Length);
			}

			Map.TextureCache = this;
		}

		public void RefreshAllTextures()
		{
			foreach (var sheet in CausticsTextures)
				sheet?.RefreshTexture();
			foreach (var t in Textures.Values)
			{
				t.Item2?.RefreshTexture();
			}
		}

		public void DisposeAllTextures()
		{
			foreach (var sheet in CausticsTextures)
				sheet?.Dispose();
			foreach (var t in Textures.Values)
			{
				t.Item2?.Dispose();
			}

			TileTextureArray?.Dispose();
			BrushTextureArray?.Dispose();
		}

		public bool AddTexture(string name, string filename, string uniform, UsageType type = UsageType.Terrain)
		{
			if (Textures.ContainsKey(name))
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

			Textures.Add(name, (uniform, sheet));
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

		public bool AddTileTexture(string name, string filename)
		{
			if (TileArrayTextures.ContainsKey(name))
				return false;

			if (!Map.Exists(filename))
			{
				throw new Exception(filename + " Can not find texture " + name);
			}

			var sheet = new Sheet(Map.Open(filename), TextureWrap.Repeat);

			TileTextureArray.SetData(sheet.GetData(), sheet.Size.Width, sheet.Size.Height);

			TileArrayTextures.Add(name, TileArrayTextures.Count);

			return true;
		}

		public bool AddBrushTexture(string name, string filename, Map map, int defaultSize)
		{
			if (AllBrushes.ContainsKey(name))
				return false;

			if (!Map.Exists(filename))
			{
				throw new Exception(filename + " Can not find texture " + name);
			}

			var sheet = new Sheet(Map.Open(filename), TextureWrap.Repeat);

			BrushTextureArray.SetData(sheet.GetData(), sheet.Size.Width, sheet.Size.Height);

			AllBrushes.Add(name, new MaskBrush(name, AllBrushes.Count, defaultSize, map));

			return true;
		}
	}
}
