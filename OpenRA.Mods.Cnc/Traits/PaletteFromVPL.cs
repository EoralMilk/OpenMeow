using OpenRA.Graphics;
using OpenRA.Mods.RA2.Graphics;
using OpenRA.Traits;
namespace OpenRA.Mods.RA2.Traits
{
	[Desc("Creates a vpl palette")]
	class PaletteFromVPLInfo : TraitInfo
	{
		[PaletteDefinition]
		[FieldLoader.Require]
		[Desc("internal palette name")]
		public readonly string Name = null;

		public readonly string FilePath = null;

		public readonly bool AllowModifiers = false;
		public override object Create(ActorInitializer init)
		{
			return new PaletteFromVPL(init.World, this);
		}
	}

	class PaletteFromVPL : ILoadsPalettes
	{
		readonly World world;
		readonly PaletteFromVPLInfo info;

		public PaletteFromVPL(World world, PaletteFromVPLInfo info)
		{
			this.world = world;
			this.info = info;
		}

		public void LoadPalettes(WorldRenderer wr)
		{
			VPLFile vplFile = new VPLFile();
			vplFile.LoadFromFile(info.FilePath);
			for (int i = 0; i < vplFile.GetSectionCount(); i++)
			{
				var section = vplFile[i];
				uint[] colors = new uint[VPLSectionTable.SectionIndexCount];
				for (int j = 0; j < VPLSectionTable.SectionIndexCount; j++)
				{
					var index = section.Table[j];
					uint saveColor = (uint)((255 << 24) | (index << 16) | (index << 8) | index);
					colors[j] = saveColor;
				}

				var palette = new ImmutablePalette(colors);
				wr.AddPalette(info.Name + i, palette);
			}
		}
	}
}
