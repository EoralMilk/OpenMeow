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

using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;

namespace OpenRA.Mods.Cnc.Graphics
{
	public class ClassicTilesetSpecificSpriteSequenceLoader : ClassicSpriteSequenceLoader
	{
		public readonly string DefaultSpriteExtension = ".shp";
		public readonly Dictionary<string, string> TilesetExtensions = new Dictionary<string, string>();
		public readonly Dictionary<string, string> TilesetCodes = new Dictionary<string, string>();

		public ClassicTilesetSpecificSpriteSequenceLoader(ModData modData)
			: base(modData)
		{
			var metadata = modData.Manifest.Get<SpriteSequenceFormat>().Metadata;
			if (metadata.TryGetValue("DefaultSpriteExtension", out var yaml))
				DefaultSpriteExtension = yaml.Value;

			if (metadata.TryGetValue("TilesetExtensions", out yaml))
				TilesetExtensions = yaml.ToDictionary(kv => kv.Value);

			if (metadata.TryGetValue("TilesetCodes", out yaml))
				TilesetCodes = yaml.ToDictionary(kv => kv.Value);
		}

		public override ISpriteSequence CreateSequence(ModData modData, string tileSet, SpriteCache cache, string sequence, string animation, MiniYaml info)
		{
			return new ClassicTilesetSpecificSpriteSequence(modData, tileSet, cache, this, sequence, animation, info);
		}
	}

	public class ClassicTilesetSpecificSpriteSequence : ClassicSpriteSequence
	{
		public ClassicTilesetSpecificSpriteSequence(ModData modData, string tileSet, SpriteCache cache, ISpriteSequenceLoader loader, string sequence, string animation, MiniYaml info)
			: base(modData, tileSet, cache, loader, sequence, animation, info) { }

		string ResolveTilesetId(string tileSet, Dictionary<string, MiniYaml> d)
		{
			if (d.TryGetValue("TilesetOverrides", out var yaml))
			{
				var tsNode = yaml.Nodes.FirstOrDefault(n => n.Key == tileSet);
				if (tsNode != null)
					tileSet = tsNode.Value.Value;
			}

			return tileSet;
		}

		protected override string GetSpriteSrc(ModData modData, string tileSet, string sequence, string animation, string sprite, Dictionary<string, MiniYaml> d)
		{
			var loader = (ClassicTilesetSpecificSpriteSequenceLoader)Loader;

			var spriteName = sprite ?? sequence;

			if (LoadField(d, "UseTilesetCode", false))
			{
				if (loader.TilesetCodes.TryGetValue(ResolveTilesetId(tileSet, d), out var code))
					spriteName = spriteName.Substring(0, 1) + code + spriteName.Substring(2, spriteName.Length - 2);
			}

			if (LoadField(d, "AddExtension", true))
			{
				var useTilesetExtension = LoadField(d, "UseTilesetExtension", false);

				if (useTilesetExtension && loader.TilesetExtensions.TryGetValue(ResolveTilesetId(tileSet, d), out var tilesetExtension))
					return spriteName + tilesetExtension;

				return spriteName + loader.DefaultSpriteExtension;
			}

			return spriteName;
		}
	}
}
