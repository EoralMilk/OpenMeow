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
	public class WithMeshInfo : ConditionalTraitInfo, Requires<RenderMeshesInfo>
	{
		public readonly string Mesh = "idle";
		public readonly string SkeletonBinded = null;
		public override object Create(ActorInitializer init) { return new WithMesh(init.Self, this); }
	}

	public class WithMesh : ConditionalTrait<WithMeshInfo>
	{
		public readonly string SkeletonBinded;
		protected MeshInstance MeshInstance;
		protected readonly RenderMeshes RenderMeshes;

		public WithMesh(Actor self, WithMeshInfo info, bool replaceMeshInit = false)
			: base(info)
		{
			SkeletonBinded = info.SkeletonBinded;
			var body = self.Trait<BodyOrientation>();
			RenderMeshes = self.Trait<RenderMeshes>();
			IFacing facing = self.TraitOrDefault<IFacing>();
			if (!replaceMeshInit)
			{
				var mesh = self.World.MeshCache.GetMeshSequence(RenderMeshes.Image, info.Mesh);
				MeshInstance = new MeshInstance(mesh, () => self.CenterPosition,
					() => facing == null ? body.QuantizeOrientation(self.Orientation) : facing.Orientation,
					() => !IsTraitDisabled,
					SkeletonBinded);

				RenderMeshes.Add(MeshInstance);
			}
		}
	}

	public enum BodyMask
	{
		Head,
		Torso,
		Hip,
		Thigh,
		Leg,
		Foot,
		UpperArm,
		LowerArm,
		Hand,

		None,
	}

	public class WithMeshBodyInfo : WithMeshInfo
	{
		public override object Create(ActorInitializer init) { return new WithMeshBody(init.Self, this); }
	}

	public class WithMeshBody : WithMesh
	{
		/// <summary>
		/// The first 9 bit are 1
		/// </summary>
		int partMask = 0x1FF;

		public WithMeshBody(Actor self, WithMeshBodyInfo info)
			: base(self, info)
		{
		}

		public void SetPartDisable(int mask)
		{
			partMask = partMask & (~mask);
			MeshInstance.DrawMask = partMask;
		}

		public void SetPartEnable(int mask)
		{
			partMask = partMask | mask;
			MeshInstance.DrawMask = partMask;
		}
	}

	public class WithMeshClothInfo : WithMeshInfo, Requires<WithMeshBodyInfo>
	{
		public readonly BodyMask[] Masks = { BodyMask.None };
		public override object Create(ActorInitializer init) { return new WithMeshCloth(init.Self, this); }
	}

	public class WithMeshCloth : WithMesh
	{
		WithMeshBody withMeshBody;
		readonly int mask = 0;

		public WithMeshCloth(Actor self, WithMeshClothInfo info)
			: base(self, info)
		{
			foreach (var t in info.Masks)
			{
				switch (t)
				{
					case BodyMask.Head:
						mask = mask | (1 << 0);
						break;
					case BodyMask.Torso:
						mask = mask | (1 << 1);
						break;
					case BodyMask.Hip:
						mask = mask | (1 << 2);
						break;
					case BodyMask.Thigh:
						mask = mask | (1 << 3);
						break;
					case BodyMask.Leg:
						mask = mask | (1 << 4);
						break;
					case BodyMask.Foot:
						mask = mask | (1 << 5);
						break;
					case BodyMask.UpperArm:
						mask = mask | (1 << 6);
						break;
					case BodyMask.LowerArm:
						mask = mask | (1 << 7);
						break;
					case BodyMask.Hand:
						mask = mask | (1 << 8);
						break;
					default:
						break;
				}
			}
		}

		protected override void Created(Actor self)
		{
			withMeshBody = self.Trait<WithMeshBody>();

			base.Created(self);
		}

		protected override void TraitEnabled(Actor self)
		{
			base.TraitEnabled(self);

			withMeshBody.SetPartDisable(mask);
		}

		protected override void TraitDisabled(Actor self)
		{
			base.TraitDisabled(self);

			withMeshBody.SetPartEnable(mask);
		}
	}
}
