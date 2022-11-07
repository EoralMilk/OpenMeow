using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits.Trait3D;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;
using TagLib.Ape;
using GlmSharp;
using System;

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

	public class ItemMesh
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
				SkeletonBinded);
		}
	}
}
