using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Trait3D;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Meow.RPG.Render
{
	public class WithMeshBodyPartInfo : ConditionalTraitInfo, Requires<WithMeshBodyInfo>
	{
		[FieldLoader.Require]
		public readonly string Mesh = null;

		public readonly string Image = null;

		public readonly bool UseHairColorAsRemap = true;

		public readonly string SkeletonBinded = null;
		public override object Create(ActorInitializer init) { return new WithMeshBodyPart(init.Self, this); }
	}

	public class WithMeshBodyPart : ConditionalTrait<WithMeshBodyPartInfo>, IWithMesh
	{
		public readonly WithMeshBody WithMeshBody;
		protected MeshInstance meshInstance;
		protected readonly RenderMeshes RenderMeshes;
		public readonly Color HairColor;

		public WithMeshBodyPart(Actor self, WithMeshBodyPartInfo info)
			: base(info)
		{

			WithMeshBody = self.Trait<WithMeshBody>();
			var body = self.TraitOrDefault<BodyOrientation>();
			RenderMeshes = self.Trait<RenderMeshes>();
			var facing = self.TraitOrDefault<IFacing>();

			if (string.IsNullOrEmpty(info.Mesh))
			{
				throw new Exception("WithMeshBodyPart need a Mesh");
			}

			var image = RenderMeshes.Image;
			if (!string.IsNullOrEmpty(info.Image))
			{
				image = Info.Image;
			}

			{
				var mesh = self.World.MeshCache.GetMeshSequence(image,info.Mesh);
				meshInstance = new MeshInstance(mesh,
					() => self.CenterPosition,
					() => facing == null ? body?.QuantizeOrientation(self.Orientation) ?? self.Orientation : facing.Orientation,
					() => !IsTraitDisabled,
					info.SkeletonBinded);
				if (info.UseHairColorAsRemap)
				{
					HairColor = WithMeshBody.HairColor;
					meshInstance.GetRemap = () => HairColor;
				}

				RenderMeshes.Add(meshInstance);
			}

		}
	}
}
