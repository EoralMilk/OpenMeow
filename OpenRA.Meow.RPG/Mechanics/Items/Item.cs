using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Traits;
using TagLib.Ape;

namespace OpenRA.Meow.RPG.Mechanics
{
	public interface INotifyBeingEquiped
	{
		void EquipedBy(Actor user, EquipmentSlot slot);
		void UnequipedBy(Actor user, EquipmentSlot slot);
	}

	public interface ITickByItem
	{
		void TickByItem(Item item);
	}


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

		public readonly bool CanExistInWorld = true;

		public override object Create(ActorInitializer init)
		{
			return new Item(this, init.Self);
		}
	}

	public class Item : INotifyKilled, INotifyActorDisposing, INotifyAddedToWorld, INotifyCreated
	{
		public readonly ItemInfo Info;

		public ItemCache ItemCache;

		public string ThumbnailImage
		{
			get
			{
				if (string.IsNullOrEmpty(Info.ThumbnailImage) && ItemActor != null && !ItemActor.IsDead)
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

		protected INotifyBeingEquiped[] beingEquipeds;
		protected ITickByItem[] tickByItems;

		public string Name
		{
			get
			{
				if (string.IsNullOrEmpty(Info.Name) && ItemActor != null && !ItemActor.IsDead)
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

		public virtual void EquipingEffect(Actor slotActor, EquipmentSlot slot)
		{
			foreach (var notify in beingEquipeds)
			{
				notify.EquipedBy(slotActor, slot);
			}
		}

		public virtual void UnequipingEffect(Actor slotActor, EquipmentSlot slot)
		{
			foreach (var notify in beingEquipeds)
			{
				notify.UnequipedBy(slotActor, slot);
			}
		}

		public virtual void Killed(Actor self, AttackInfo e)
		{
			if (EquipmentSlot != null)
			{
				EquipmentSlot.TryUnequip(EquipmentSlot.SlotOwnerActor);
			}

			if (Inventory != null)
			{
				Inventory.TryRemove(Inventory.InventoryActor, this);
			}
		}

		void INotifyActorDisposing.Disposing(Actor self)
		{
			ItemCache?.RemvoeItem(ItemActor.ActorID);
		}

		void INotifyAddedToWorld.AddedToWorld(Actor self)
		{
			if (!Info.CanExistInWorld)
			{
				self.Kill(self);
				self.Dispose();
			}
		}

		public virtual void Created(Actor self)
		{
			beingEquipeds = self.TraitsImplementing<INotifyBeingEquiped>().ToArray();
			tickByItems = self.TraitsImplementing<ITickByItem>().ToArray();
		}

		public virtual void TickItem()
		{
			foreach (var tick in tickByItems)
			{
				tick.TickByItem(this);
			}
		}
	}
}
