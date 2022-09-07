using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using OpenRA.FileSystem;
using OpenRA.Primitives;
using StbImageSharp;

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
		public readonly ITexture[] CausticsTextures;
		public readonly Dictionary<string, (string, ITexture)> Textures = new Dictionary<string, (string, ITexture)>();
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

			CausticsTextures = new ITexture[32];

			for (int i = 0; i < CausticsTextures.Length; i++)
			{
				var filename = "caustics_" + (i + 1).ToString().PadLeft(3, '0') + ".bmp";
				if (!fileSystem.Exists(filename))
				{
					throw new Exception(" Can not find texture " + filename);
				}

				ImageResult image;
				using (var ss = fileSystem.Open(filename))
				{
					image = ImageResult.FromStream(ss, ColorComponents.RedGreenBlueAlpha);
				}

				var texture = Game.Renderer.CreateTexture();
				texture.WrapType = TextureWrap.Repeat;
				//texture.ScaleFilter = TextureScaleFilter.Linear;
				texture.SetData(image.Data, image.Width, image.Height, TextureType.RGBA);
				CausticsTextures[i] = texture;
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

			ImageResult image;
			using (var ss = fileSystem.Open(filename))
			{
				image = ImageResult.FromStream(ss, ColorComponents.RedGreenBlueAlpha);
			}

			var texture = Game.Renderer.CreateTexture();
			texture.WrapType = TextureWrap.Repeat;
			//texture.ScaleFilter = TextureScaleFilter.Linear;
			texture.SetData(image.Data, image.Width, image.Height, TextureType.RGBA);
			Textures.Add(name, (uniform, texture));
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
