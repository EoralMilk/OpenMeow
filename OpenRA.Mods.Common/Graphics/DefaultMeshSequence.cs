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
using OpenRA.FileSystem;
using OpenRA.Graphics;

namespace OpenRA.Mods.Common.Graphics
{
	public class DefaultMeshSequenceLoader : IMeshSequenceLoader
	{
		public DefaultMeshSequenceLoader(ModData modData) { }

		public MeshCache CacheMeshes(IMeshLoader[] loaders, SkeletonCache skeletonCache, IReadOnlyFileSystem fileSystem, ModData modData, IReadOnlyDictionary<string, MiniYamlNode> meshSequences)
		{
			var cache = new MeshCache(loaders, fileSystem);

			foreach (var (unit, unitYaml) in meshSequences)
			{
				modData.LoadScreen.Display();

				var defines = unitYaml.Value.ToDictionary();
				SkeletonAsset skeletonType = null;
				OrderedSkeleton skeleton = null;
				if (defines.ContainsKey("Skeleton"))
				{
					string skeletonFIle = defines["Skeleton"].Value;
					skeletonType = skeletonCache.UpdateSkeletonAsset(fileSystem, skeletonFIle, defines["Skeleton"], unit);

					skeleton = skeletonCache.UpdateOrderedSkeleton(skeletonType, unit, defines["Skeleton"]);
				}

				if (defines.ContainsKey("Meshes"))
				{
					var sequences = defines["Meshes"].ToDictionary();
					foreach (var (sequence, sequenceYaml) in sequences)
						cache.CacheMesh(unit, sequence, sequenceYaml, skeletonType, skeleton);
				}
			}

			return cache;
		}
	}
}
