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
	}

	public class MapTextureCache
	{
		readonly IReadOnlyFileSystem fileSystem;
		public readonly Sheet[] CausticsTextures;
		public readonly Dictionary<string, (string, Sheet)> Textures = new Dictionary<string, (string, Sheet)>();
		public readonly HashSet<string> TerrainTexturesSet = new HashSet<string>();
		public readonly HashSet<string> SmudgeTexturesSet = new HashSet<string>();

		public const string TN_GrassNormal	= "Texture4";
		public const string TN_CliffNormal		= "Texture5";
		public const string TN_Cliff				= "Texture6";
		public const string TN_SlopeNormal	= "Texture7";
		public const string TN_Slope				= "Texture8";
		public const string TN_WaterNormal	= "Texture9";
		public const string TN_Caustics			= "Texture10";
		public const string TN_Scroch			= "Texture4";

		public MapTextureCache(IReadOnlyFileSystem fileSystem)
		{
			this.fileSystem = fileSystem;
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
				if (!fileSystem.Exists(filename))
				{
					throw new Exception(" Can not find texture " + filename);
				}

				CausticsTextures[i] = new Sheet(SheetType.BGRA, fileSystem.Open(filename), TextureWrap.Repeat, false);
			}
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

			if (!fileSystem.Exists(filename))
			{
				throw new Exception(filename + " Can not find texture " + name);
			}

			var sheet = new Sheet(SheetType.BGRA, fileSystem.Open(filename), TextureWrap.Repeat, false);

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
	}
}
