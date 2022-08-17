using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using OpenRA.FileSystem;
using OpenRA.Primitives;
using StbImageSharp;

namespace OpenRA
{
	public class MapTextureCache
	{
		readonly IReadOnlyFileSystem fileSystem;
		public readonly ITexture[] Caustics;
		public readonly Dictionary<string, ITexture> Textures = new Dictionary<string, ITexture>();

		public MapTextureCache(IReadOnlyFileSystem fileSystem)
		{
			this.fileSystem = fileSystem;
			AddTexture("Water", "water.png");

			Caustics = new ITexture[32];

			for (int i = 0; i < Caustics.Length; i++)
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
				Caustics[i] = texture;
			}
		}

		public bool AddTexture(string name, string filename)
		{
			if (Textures.ContainsKey(name))
				return false;

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
			Textures.Add(name, texture);
			return true;
		}
	}
}
