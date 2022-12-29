using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits.Trait3D;
using OpenRA.Traits;

namespace OpenRA.Meow.RPG.Mechanics
{
	public class ItemMeshInfo : TraitInfo, Requires<RenderMeshesInfo>, Requires<ItemInfo>
	{
		public readonly string Mesh = "idle";
		public readonly string SkeletonBinded = null;
		public readonly string Image = null;

		public override object Create(ActorInitializer init)
		{
			return new ItemMesh(init.Self, this);
		}
	}

	public class ItemMesh: INotifyBeingEquiped
	{
		public readonly string SkeletonBinded;
		public readonly MeshInstance MeshInstance;
		public readonly RenderMeshes RenderMeshes;
		readonly Item item;

		public ItemMesh(Actor self, ItemMeshInfo info)
		{
			SkeletonBinded = info.SkeletonBinded;

			RenderMeshes = self.Trait<RenderMeshes>();
			item = self.Trait<Item>();

			var image = RenderMeshes.Image;
			if (info.Image != null)
			{
				image = info.Image;
			}

			var mesh = self.World.MeshCache.GetMeshSequence(image, info.Mesh);
			MeshInstance = new MeshInstance(mesh,
				null,
				() => MeshInstance.Matrix != null && item.EquipmentSlot != null,
				SkeletonBinded)
			{
				UseMatrix = true
			};
		}

		void INotifyBeingEquiped.EquipedBy(Actor user, EquipmentSlot slot)
		{
			if (slot.RenderMeshes == null)
				return;

			if (SkeletonBinded == null)
			{
				MeshInstance.Matrix = slot.SlotGetRenderMatrix;
				MeshInstance.DrawId = () => -1;
			}
			else
			{
				// we should handle the skeleton binding by ourself
				// the rendermeshes will not able to use because the item always not in world when equiped
				var skeleton = item.ItemActor.TraitsImplementing<WithSkeleton>().Single(s => s.Name == SkeletonBinded);

				MeshInstance.DrawId = () => skeleton.GetDrawId();
				MeshInstance.Matrix = () => skeleton.Skeleton.Offset.ToMat4();
			}

			slot.RenderMeshes.Add(MeshInstance);
		}

		void INotifyBeingEquiped.UnequipedBy(Actor user, EquipmentSlot slot)
		{
			MeshInstance.Matrix = null;
			MeshInstance.DrawId = () => -1;

			if (slot.RenderMeshes == null)
				return;

			slot.RenderMeshes.Remove(MeshInstance);
		}
	}
}
