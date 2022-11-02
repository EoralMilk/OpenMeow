using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Traits;

namespace OpenRA.Meow.RPG.Mechanics
{
	public class ItemInfo : TraitInfo
	{
		public readonly string Name = null;

		[Desc("The type name of this item, for matching the equipment slot.")]
		public readonly string Type = "Item";

		public readonly string ThumbnailImage = null;

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
		string image;
		public string ThumbnailImage
		{
			get
			{
				if (string.IsNullOrEmpty(Info.ThumbnailImage))
				{
					var rs = ItemActor.TraitOrDefault<RenderSprites>();
					if (rs != null)
						return rs.GetImage(ItemActor);
					else
						throw new System.Exception("Can't find image of item");
				}
				else
					return Info.ThumbnailImage;
			}
		}

		public string ThumbnailPal => Info.ThumbnailPaletteIsPlayerPalette && ItemActor.Owner != null ? Info.ThumbnailPalette + ItemActor.Owner.InternalName : Info.ThumbnailPalette;
		public readonly Actor ItemActor;

		// This is managed by the Inventory class.
		public Inventory Inventory;

		// This is managed by the EquipmentSlot class.
		public EquipmentSlot EquipmentSlot;

		public string Name
		{
			get
			{
				if (string.IsNullOrEmpty(Info.Name))
				{
					var tooltip = ItemActor.TraitsImplementing<Tooltip>().Where(t => !t.IsTraitDisabled).FirstOrDefault();
					if (tooltip != null)
						return tooltip.Info.Name;
					else
						throw new System.Exception("Can't find name of item");
				}
				else
					return Info.Name;
			}
		}

		public string Type => this.Info.Type;

		public Item(ItemInfo info, Actor self)
		{
			this.Info = info;
			this.ItemActor = self;
		}

		public virtual void EquipingEffect(Actor actor) { }
		public virtual void UnequipingEffect(Actor actor) { }

	}
}
