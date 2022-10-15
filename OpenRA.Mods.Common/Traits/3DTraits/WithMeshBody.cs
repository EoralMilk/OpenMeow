using System;
using OpenRA.Graphics;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits.Trait3D
{
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

	public class WithMeshBodyInfo : ConditionalTraitInfo, Requires<RenderMeshesInfo>
	{
		public readonly string SkeletonBinded = null;
		public readonly string Image = null;

		public readonly string HeadMesh = "body-head";
		public readonly string TorsoMesh = "body-torso";
		public readonly string HipMesh = "body-hip";
		public readonly string ThighMesh = "body-thigh";
		public readonly string LegMesh = "body-leg";
		public readonly string FootMesh = "body-foot";
		public readonly string UpperArmMesh = "body-upperarm";
		public readonly string LowerArmMesh = "body-lowerarm";
		public readonly string HandMesh = "body-hand";

		public override object Create(ActorInitializer init) { return new WithMeshBody(init.Self, this); }
	}

	public class WithMeshBody : ConditionalTrait<WithMeshBodyInfo>, IWithMesh
	{
		/// <summary>
		/// The first 9 bit are 1
		/// </summary>
		int partMask = 0x1FF;

		public readonly string SkeletonBinded;
		protected readonly RenderMeshes RenderMeshes;

		protected MeshInstance head;
		protected MeshInstance torso;
		protected MeshInstance hip;
		protected MeshInstance thigh;
		protected MeshInstance leg;
		protected MeshInstance foot;
		protected MeshInstance upperArm;
		protected MeshInstance lowerArm;
		protected MeshInstance hand;

		readonly bool[] drawFlags = new bool[9];

		public void SetDrawPart(BodyMask mask, bool draw)
		{
			drawFlags[(int)mask] = draw;
		}

		public WithMeshBody(Actor self, WithMeshBodyInfo info)
			: base(info)
		{
			SkeletonBinded = info.SkeletonBinded;

			var body = self.TraitOrDefault<BodyOrientation>();
			RenderMeshes = self.Trait<RenderMeshes>();

			var image = RenderMeshes.Image;
			if (Info.Image != null)
			{
				image = Info.Image;
			}

			IFacing facing = self.TraitOrDefault<IFacing>();

			for (int i = 0; i < drawFlags.Length; i++)
				drawFlags[i] = true;

			#region body part init
			{
				var headMesh = self.World.MeshCache.GetMeshSequence(image, info.HeadMesh);
				head = new MeshInstance(headMesh, () => self.CenterPosition,
					() => facing == null ? body?.QuantizeOrientation(self.Orientation) ?? self.Orientation : facing.Orientation,
					() => !IsTraitDisabled && drawFlags[(int)BodyMask.Head],
					info.SkeletonBinded);
				RenderMeshes.Add(head);
			}

			{
				var torsoMesh = self.World.MeshCache.GetMeshSequence(image, info.TorsoMesh);
				torso = new MeshInstance(torsoMesh, () => self.CenterPosition,
					() => facing == null ? body?.QuantizeOrientation(self.Orientation) ?? self.Orientation : facing.Orientation,
					() => !IsTraitDisabled && drawFlags[(int)BodyMask.Torso],
					info.SkeletonBinded);
				RenderMeshes.Add(torso);
			}

			{
				var hipMesh = self.World.MeshCache.GetMeshSequence(image, info.HipMesh);
				hip = new MeshInstance(hipMesh, () => self.CenterPosition,
					() => facing == null ? body?.QuantizeOrientation(self.Orientation) ?? self.Orientation : facing.Orientation,
					() => !IsTraitDisabled && drawFlags[(int)BodyMask.Hip],
					info.SkeletonBinded);
				RenderMeshes.Add(hip);
			}

			{
				var thighMesh = self.World.MeshCache.GetMeshSequence(image, info.ThighMesh);
				thigh = new MeshInstance(thighMesh, () => self.CenterPosition,
					() => facing == null ? body?.QuantizeOrientation(self.Orientation) ?? self.Orientation : facing.Orientation,
					() => !IsTraitDisabled && drawFlags[(int)BodyMask.Thigh],
					info.SkeletonBinded);
				RenderMeshes.Add(thigh);
			}

			{
				var legMesh = self.World.MeshCache.GetMeshSequence(image, info.LegMesh);
				leg = new MeshInstance(legMesh, () => self.CenterPosition,
					() => facing == null ? body?.QuantizeOrientation(self.Orientation) ?? self.Orientation : facing.Orientation,
					() => !IsTraitDisabled && drawFlags[(int)BodyMask.Leg],
					info.SkeletonBinded);
				RenderMeshes.Add(leg);
			}

			{
				var footMesh = self.World.MeshCache.GetMeshSequence(image, info.FootMesh);
				foot = new MeshInstance(footMesh, () => self.CenterPosition,
					() => facing == null ? body?.QuantizeOrientation(self.Orientation) ?? self.Orientation : facing.Orientation,
					() => !IsTraitDisabled && drawFlags[(int)BodyMask.Foot],
					info.SkeletonBinded);
				RenderMeshes.Add(foot);
			}

			{
				var upperArmMesh = self.World.MeshCache.GetMeshSequence(image, info.UpperArmMesh);
				upperArm = new MeshInstance(upperArmMesh, () => self.CenterPosition,
					() => facing == null ? body?.QuantizeOrientation(self.Orientation) ?? self.Orientation : facing.Orientation,
					() => !IsTraitDisabled && drawFlags[(int)BodyMask.UpperArm],
					info.SkeletonBinded);
				RenderMeshes.Add(upperArm);
			}

			{
				var lowerArmMesh = self.World.MeshCache.GetMeshSequence(image, info.LowerArmMesh);
				lowerArm = new MeshInstance(lowerArmMesh, () => self.CenterPosition,
					() => facing == null ? body?.QuantizeOrientation(self.Orientation) ?? self.Orientation : facing.Orientation,
					() => !IsTraitDisabled && drawFlags[(int)BodyMask.LowerArm],
					info.SkeletonBinded);
				RenderMeshes.Add(lowerArm);
			}

			{
				var handMesh = self.World.MeshCache.GetMeshSequence(image, info.HandMesh);
				hand = new MeshInstance(handMesh, () => self.CenterPosition,
					() => facing == null ? body?.QuantizeOrientation(self.Orientation) ?? self.Orientation : facing.Orientation,
					() => !IsTraitDisabled && drawFlags[(int)BodyMask.Hand],
					info.SkeletonBinded);
				RenderMeshes.Add(hand);
			}
			#endregion
		}
	}
}
