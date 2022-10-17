using System;
using System.Collections.Generic;
using System.IO;
using OpenRA.FileSystem;
using OpenRA.Graphics;

namespace OpenRA.Mods.Common.Graphics
{
	class MaterialReader
	{
		readonly string name;

		readonly float3 diffuseTint;
		readonly float specTint;

		readonly float shininess;
		readonly string diffMapName;
		readonly string combinedMapName;
		readonly Sheet diffuseTex;
		readonly Sheet combinedTex;

		readonly IReadOnlyFileSystem fileSystem;
		readonly MeshCache cache;
		readonly string filename;
		public MaterialReader(IReadOnlyFileSystem fileSystem, MeshCache cache, string filename)
		{
			this.fileSystem = fileSystem;
			this.cache = cache;
			this.filename = filename;
			if (!fileSystem.Exists(filename))
				filename += ".mat";

			if (!fileSystem.Exists(filename))
				throw new Exception("Can not find Material: " + filename);

			List<MiniYamlNode> nodes = MiniYaml.FromStream(fileSystem.Open(filename));

			if (nodes.Count > 1)
			{
				throw new InvalidDataException("Invalid Material Node: Too Many Nodes!");
			}

			var node = nodes[0];
			var info = node.Value.ToDictionary();

			name = node.Key;
			diffuseTint = ReadYamlInfo.LoadField(info, "DiffuseTint", float3.Ones);
			specTint = ReadYamlInfo.LoadField(info, "Specular", 0.5f);

			diffMapName = ReadYamlInfo.LoadField(info, "DiffuseMap", "NO_TEXTURE");
			combinedMapName = ReadYamlInfo.LoadField(info, "CombinedMap", "NO_TEXTURE");
			shininess = ReadYamlInfo.LoadField(info, "Shininess", 0.0f);
			if (diffMapName == "NO_TEXTURE")
			{
				diffuseTex = null;
			}
			else
			{
				// texture
				diffMapName = diffMapName.Trim();
				PrepareTexture(diffMapName, out diffuseTex);
			}

			if (combinedMapName == "NO_TEXTURE")
			{
				combinedTex = null;
			}
			else
			{
				// texture
				combinedMapName = combinedMapName.Trim();
				PrepareTexture(combinedMapName, out combinedTex);
			}

			if (diffuseTex != null && combinedTex != null && diffuseTex.Size != combinedTex.Size)
				throw new Exception("The textures used by a material must have same size");
		}

		void PrepareTexture(string name, out Sheet texture)
		{
			if (cache.HasTexture(name))
			{
				texture = cache.GetSheet(name);
			}
			else
			{
				if (!fileSystem.Exists(name))
				{
					throw new Exception(filename + " Can not find texture " + name);
				}

				var sheet = new Sheet(fileSystem.Open(name), TextureWrap.Repeat);

				texture = cache.AddOrGetSheet(name, sheet);
			}
		}

		public IMaterial CreateMaterial()
		{
			return new CombinedMaterial(name, diffuseTex, diffuseTint, combinedTex, specTint, shininess);
		}
	}

}
