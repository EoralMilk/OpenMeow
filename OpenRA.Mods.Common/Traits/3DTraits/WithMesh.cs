using System;
using OpenRA.Graphics;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits.Trait3D
{

	public interface IWithMesh
	{

	}

	public class WithMeshInfo : ConditionalTraitInfo, Requires<RenderMeshesInfo>
	{
		public readonly string Mesh = "idle";
		public readonly string SkeletonBinded = null;
		public readonly string Image = null;
		public override object Create(ActorInitializer init) { return new WithMesh(init.Self, this); }
	}

	public class WithMesh : ConditionalTrait<WithMeshInfo>, IWithMesh
	{
		public readonly string SkeletonBinded;
		protected MeshInstance meshInstance;
		protected readonly RenderMeshes RenderMeshes;

		public WithMesh(Actor self, WithMeshInfo info, bool replaceMeshInit = false)
			: base(info)
		{
			SkeletonBinded = info.SkeletonBinded;

			var body = self.TraitOrDefault<BodyOrientation>();
			RenderMeshes = self.Trait<RenderMeshes>();

			var image = RenderMeshes.Image;
			if (!string.IsNullOrEmpty(info.Image))
			{
				image = Info.Image;
			}

			IFacing facing = self.TraitOrDefault<IFacing>();
			if (!replaceMeshInit)
			{
				var mesh = self.World.MeshCache.GetMeshSequence(image, info.Mesh);
				meshInstance = new MeshInstance(mesh, () => self.CenterPosition,
					() => facing == null ? body?.QuantizeOrientation(self.Orientation) ?? self.Orientation : facing.Orientation,
					() => !IsTraitDisabled,
					SkeletonBinded);

				RenderMeshes.Add(meshInstance);
			}
		}
	}
}
