using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Traits;

namespace OpenRA.Meow.RPG.Mechanics.Display
{

	public class UnitDisplayInfoInfo : ConditionalTraitInfo
	{

		[SequenceReference]
		public readonly string IconSequence = "icon";

		[PaletteReference(nameof(IconPaletteIsPlayerPalette))]
		[Desc("Palette used for the production icon.")]
		public readonly string IconPalette = "chrome";

		[Desc("Custom palette is a player palette BaseName")]
		public readonly bool IconPaletteIsPlayerPalette = false;

		public readonly string Description = null;

		public override object Create(ActorInitializer init) { return new ActorDisplayInfomation(init, this); }

	}

	public class ActorDisplayInfomation : ConditionalTrait<UnitDisplayInfoInfo>
	{
		UnitDisplayInfoInfo info;

		public Actor Self;
		public ActorDisplayInfomation(ActorInitializer init, UnitDisplayInfoInfo info)
			: base(info)
		{
			this.info = info;
			Self = init.Self;
		}
	}

}
