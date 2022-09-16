#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.FileSystem;

namespace OpenRA.Graphics
{
	using System.IO;

	public class MeshCache
	{
		readonly IMeshLoader[] loaders;
		readonly IReadOnlyFileSystem fileSystem;
		readonly Dictionary<string, IMaterial> materials = new Dictionary<string, IMaterial>();
		readonly Dictionary<string, Sheet> textures = new Dictionary<string, Sheet>();

		readonly Dictionary<string, MeshVertexData> meshDatas = new Dictionary<string, MeshVertexData>();
		readonly Dictionary<string, IOrderedMesh> meshes = new Dictionary<string, IOrderedMesh>();
		readonly Dictionary<string, Dictionary<string, IOrderedMesh>> meshesRef = new Dictionary<string, Dictionary<string, IOrderedMesh>>();

		public MeshCache(IMeshLoader[] loaders, IReadOnlyFileSystem fileSystem)
		{
			this.loaders = loaders;
			this.fileSystem = fileSystem;
		}

		public void RefreshAllTextures()
		{
			foreach (var kv in textures)
			{
				kv.Value?.RefreshTexture();
			}
		}

		public void DisposeAllTextures()
		{
			foreach (var kv in textures)
			{
				kv.Value?.Dispose();
			}
		}

		public void CacheMesh(string unit, string sequence, MiniYaml definition, SkeletonAsset skeletonType, OrderedSkeleton skeleton)
		{
			// this is not only meshName, Also can add others info such as texture
			var name = definition?.Value;

			// orderdmesh should be unit unique
			var dictKey = unit + name;

			if (!meshes.ContainsKey(dictKey))
				meshes.Add(dictKey, LoadMesh(unit, sequence, name, definition, skeletonType, skeleton));

			if (!meshesRef.ContainsKey(unit))
				meshesRef.Add(unit, new Dictionary<string, IOrderedMesh>());

			meshesRef[unit].Add(sequence, meshes[dictKey]);
		}

		IOrderedMesh LoadMesh(string unit, string sequence, string fileName, MiniYaml definition, SkeletonAsset skeletonType, OrderedSkeleton skeleton)
		{
			foreach (var loader in loaders)
				if (loader.TryLoadMesh(fileSystem, fileName, definition, this, skeleton, skeletonType, out var mesh))
					return mesh;

			throw new InvalidDataException(unit + "." + sequence + " file: " + fileName + " is not a valid mesh file!");
		}

		public bool HasMeshData(string name)
		{
			if (meshDatas.ContainsKey(name))
				return true;
			else
				return false;
		}

		public MeshVertexData GetMeshData(string name)
		{
			if (!meshDatas.ContainsKey(name))
			{
				return null;
			}

			return meshDatas[name];
		}

		public MeshVertexData AddOrGetMeshData(string name, MeshVertexData meshVertexData)
		{
			if (!meshDatas.ContainsKey(name))
			{
				meshDatas.Add(name, meshVertexData);
			}

			return meshDatas[name];
		}

		public IOrderedMesh GetMeshSequence(string unit, string sequence)
		{
			if (!HasMeshSequence(unit, sequence))
				throw new InvalidOperationException(
					$"Unit `{unit}` does not have a sequence `{sequence}`");

			return meshesRef[unit][sequence];
		}

		public bool HasMeshSequence(string unit, string sequence)
		{
			if (!meshesRef.ContainsKey(unit))
				throw new InvalidOperationException(
					$"Unit `{unit}` does not have any sequences defined.");

			return meshesRef[unit].ContainsKey(sequence);
		}

		public bool HasMaterial(string name)
		{
			if (materials.ContainsKey(name))
				return true;
			else
				return false;
		}

		public IMaterial GetMaterial(string name)
		{
			if (!materials.ContainsKey(name))
			{
				return null;
			}

			return materials[name];
		}

		public IMaterial AddOrGetMaterial(string name, IMaterial material)
		{
			if (!materials.ContainsKey(name))
			{
				materials.Add(name, material);
			}

			return materials[name];
		}

		public bool HasTexture(string name)
		{
			if (textures.ContainsKey(name))
				return true;
			else
				return false;
		}

		public Sheet GetSheet(string name)
		{
			if (!textures.ContainsKey(name))
			{
				return null;
			}

			return textures[name];
		}

		public Sheet AddOrGetSheet(string name, Sheet texture)
		{
			if (!textures.ContainsKey(name))
			{
				textures.Add(name, texture);
			}

			return textures[name];
		}

		public void FlushInstances()
		{
			foreach (var orderedMesh in meshes)
			{
				orderedMesh.Value.Flush();
			}
		}

		public void DrawInstances(bool shadowBuffer = false)
		{
			foreach (var orderedMesh in meshes)
			{
				orderedMesh.Value.DrawInstances(shadowBuffer);
			}
		}

		public void Dispose()
		{
			//foreach (var m in meshes)
			//	m.Value.Dispose();
		}
	}
}
