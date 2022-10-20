using OpenRA.Graphics;
using OpenRA.Traits;

namespace OpenRA.Meow.RPG.Mechanics
{
	public class ItemInfo : TraitInfo
	{
		public readonly string Name = "none";

		[Desc("The type name of this item, for matching the equipment slot.")]
		public readonly string Type = "Item";

		public readonly string ThumbnailImage = "items";

		[SequenceReference]
		[Desc("The sequence name that defines the item thumbnail sprites.")]
		public readonly string ThumbnailSequence = "none";

		[PaletteReference(nameof(ThumbnailPaletteIsPlayerPalette))]
		[Desc("Palette used for the production icon.")]
		public readonly string ThumbnailPalette = "chrome";

		[Desc("Custom palette is a player palette BaseName")]
		public readonly bool ThumbnailPaletteIsPlayerPalette = false;

		public override object Create(ActorInitializer init)
		{
			return new Item(this, init.Self);
		}
	}

	public class Item
	{
		public readonly ItemInfo Info;
		public string ThumbnailPal => Info.ThumbnailPaletteIsPlayerPalette ? Info.ThumbnailPalette + ItemActor.Owner.InternalName : Info.ThumbnailPalette;
		public readonly Actor ItemActor;

		// This is managed by the Inventory class.
		public Inventory Inventory;

		// This is managed by the EquipmentSlot class.
		public EquipmentSlot EquipmentSlot;

		public string Name => this.Info.Name;
		public string Type => this.Info.Type;

		public Item(ItemInfo info, Actor self)
		{
			this.Info = info;
			this.ItemActor = self;
		}
	}
}
