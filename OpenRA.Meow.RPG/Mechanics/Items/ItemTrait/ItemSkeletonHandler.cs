using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits.Trait3D;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;
using TagLib.Ape;
using GlmSharp;
using System;
using System.Linq;

namespace OpenRA.Meow.RPG.Mechanics
{
	public class ItemSkeletonHandlerInfo : TraitInfo, Requires<WithSkeletonInfo>, Requires<ItemInfo>
	{
		public readonly string Skeleton = null;
		public override object Create(ActorInitializer init)
		{
			return new ItemSkeletonHandler(init.Self, this);
		}
	}

	public class ItemSkeletonHandler: INotifyBeingEquiped
	{
		public readonly WithSkeleton Skeleton;
		readonly Item item;

		public ItemSkeletonHandler(Actor self, ItemSkeletonHandlerInfo info)
		{
			Skeleton = self.TraitsImplementing<WithSkeleton>().Single(s => s.Name == info.Skeleton);

			item = self.Trait<Item>();
		}

		void INotifyBeingEquiped.EquipedBy(Actor user, EquipmentSlot slot)
		{
			if (slot.SkeletonBind != null && slot.BoneId != -1)
				Skeleton.SetParent(slot.SkeletonBind, slot.BoneId, Skeleton.Scale);
		}

		void INotifyBeingEquiped.UnequipedBy(Actor user, EquipmentSlot slot)
		{
			if (slot.SkeletonBind != null && slot.BoneId != -1)
				Skeleton.ReleaseFromParent();
		}
	}
}
