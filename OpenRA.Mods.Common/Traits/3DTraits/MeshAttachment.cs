using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Primitives;
using OpenRA.Traits;
using TrueSync;

namespace OpenRA.Mods.Common.Traits.Trait3D
{
	public class MeshAttachmentInfo : WithMeshInfo, Requires<WithSkeletonInfo>
	{
		public readonly string AttachmentSkeleton = null;
		public readonly string AttachingBone = "a-Position";
		public readonly float Scale = 0.0f;
		public override object Create(ActorInitializer init) { return new MeshAttachment(init.Self, this); }
	}

	public class MeshAttachment : WithMesh
	{
		protected readonly int AttachBoneId = -1;
		protected readonly WithSkeleton MainSkeleton;
		protected readonly WithSkeleton AttachmentSkeleton;
		protected readonly string image;
		public readonly float Scale = 1.0f;
		public MeshAttachment(Actor self, MeshAttachmentInfo info, bool replaceMeshInit = false)
			: base(self, info, true)
		{
			if (info.Scale == 0.0f)
				Scale = RenderMeshes.Info.Scale;
			else
				Scale = info.Scale;
			if (info.SkeletonBinded == null)
				throw new Exception(self.Info.Name + " Mesh Attachment must have a main skeleton: need to give SkeletonBinded a valid value");
			MainSkeleton = self.TraitsImplementing<WithSkeleton>().Single(w => w.Info.Name == info.SkeletonBinded);
			if (MainSkeleton == null)
				throw new Exception(self.Info.Name + " Mesh Attachment Can not find main skeleton " + info.SkeletonBinded);
			if (info.AttachmentSkeleton != null)
				AttachmentSkeleton = self.TraitsImplementing<WithSkeleton>().Single(w => w.Info.Name == info.AttachmentSkeleton);
			if (info.AttachmentSkeleton != null && AttachmentSkeleton == null)
				throw new Exception(self.Info.Name + " Mesh Attachment Can not find attachment skeleton " + info.AttachmentSkeleton);

			AttachBoneId = MainSkeleton.GetBoneId(info.AttachingBone);
			if (AttachBoneId == -1)
				throw new Exception("can't find bone " + info.AttachingBone + " in skeleton.");

			if (AttachmentSkeleton != null)
				AttachmentSkeleton.SetParent(MainSkeleton, AttachBoneId, Scale);

			if (Info.Image != null)
			{
				image = Info.Image;
			}
			else if (AttachmentSkeleton != null)
			{
				image = AttachmentSkeleton.Image;
			}
			else if (info.SkeletonBinded != null)
			{
				image = MainSkeleton.Image;
			}
			else
			{
				image = RenderMeshes.Image;
			}

			if (!replaceMeshInit)
			{
				var mesh = self.World.MeshCache.GetMeshSequence(image, info.Mesh);
				meshInstance = new MeshInstance(mesh, () => (AttachmentSkeleton == null ? Transformation.MatWithNewScale(MainSkeleton.Skeleton.BoneOffsetMat(AttachBoneId), Scale).ToMat4() : AttachmentSkeleton.Skeleton.Offset.ToMat4()),
					() => !IsTraitDisabled, AttachmentSkeleton == null ? null : info.AttachmentSkeleton);

				RenderMeshes.Add(meshInstance);
			}
		}
	}
}
