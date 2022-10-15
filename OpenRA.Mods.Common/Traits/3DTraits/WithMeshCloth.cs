using System;
using OpenRA.Graphics;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits.Trait3D
{
	public interface IWithCloth
	{
		BodyMask[] GetBodyMasks();
	}

	public class WithMeshClothInfo : WithMeshInfo, Requires<WithMeshBodyInfo>
	{
		public readonly BodyMask[] Masks = { BodyMask.None };

		public readonly string HeadMesh = null;
		public readonly string TorsoMesh = null;
		public readonly string HipMesh = null;
		public readonly string ThighMesh = null;
		public readonly string LegMesh = null;
		public readonly string FootMesh = null;
		public readonly string UpperArmMesh = null;
		public readonly string LowerArmMesh = null;
		public readonly string HandMesh = null;
		public override object Create(ActorInitializer init) { return new WithMeshCloth(init.Self, this); }
	}

	public class WithMeshCloth : WithMesh, IWithCloth
	{
		WithMeshBody withMeshBody;
		readonly WithMeshClothInfo info;

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

		public WithMeshCloth(Actor self, WithMeshClothInfo info)
			: base(self, info)
		{
			this.info = info;

			var body = self.TraitOrDefault<BodyOrientation>();

			var image = RenderMeshes.Image;
			if (Info.Image != null)
			{
				image = Info.Image;
			}

			IFacing facing = self.TraitOrDefault<IFacing>();

			for (int i = 0; i < drawFlags.Length; i++)
				drawFlags[i] = true;

			#region body part init
			if (info.HandMesh != null)
			{
				var headMesh = self.World.MeshCache.GetMeshSequence(image, info.HeadMesh);
				head = new MeshInstance(headMesh, () => self.CenterPosition,
					() => facing == null ? body?.QuantizeOrientation(self.Orientation) ?? self.Orientation : facing.Orientation,
					() => !IsTraitDisabled && drawFlags[(int)BodyMask.Head],
					info.SkeletonBinded);
				RenderMeshes.Add(head);
			}

			if (info.TorsoMesh != null)
			{
				var torsoMesh = self.World.MeshCache.GetMeshSequence(image, info.TorsoMesh);
				torso = new MeshInstance(torsoMesh, () => self.CenterPosition,
					() => facing == null ? body?.QuantizeOrientation(self.Orientation) ?? self.Orientation : facing.Orientation,
					() => !IsTraitDisabled && drawFlags[(int)BodyMask.Torso],
					info.SkeletonBinded);
				RenderMeshes.Add(torso);
			}

			if (info.HipMesh != null)
			{
				var hipMesh = self.World.MeshCache.GetMeshSequence(image, info.HipMesh);
				hip = new MeshInstance(hipMesh, () => self.CenterPosition,
					() => facing == null ? body?.QuantizeOrientation(self.Orientation) ?? self.Orientation : facing.Orientation,
					() => !IsTraitDisabled && drawFlags[(int)BodyMask.Hip],
					info.SkeletonBinded);
				RenderMeshes.Add(hip);
			}

			if (info.ThighMesh != null)
			{
				var thighMesh = self.World.MeshCache.GetMeshSequence(image, info.ThighMesh);
				thigh = new MeshInstance(thighMesh, () => self.CenterPosition,
					() => facing == null ? body?.QuantizeOrientation(self.Orientation) ?? self.Orientation : facing.Orientation,
					() => !IsTraitDisabled && drawFlags[(int)BodyMask.Thigh],
					info.SkeletonBinded);
				RenderMeshes.Add(thigh);
			}

			if (info.LegMesh != null)
			{
				var legMesh = self.World.MeshCache.GetMeshSequence(image, info.LegMesh);
				leg = new MeshInstance(legMesh, () => self.CenterPosition,
					() => facing == null ? body?.QuantizeOrientation(self.Orientation) ?? self.Orientation : facing.Orientation,
					() => !IsTraitDisabled && drawFlags[(int)BodyMask.Leg],
					info.SkeletonBinded);
				RenderMeshes.Add(leg);
			}

			if (info.FootMesh != null)
			{
				var footMesh = self.World.MeshCache.GetMeshSequence(image, info.FootMesh);
				foot = new MeshInstance(footMesh, () => self.CenterPosition,
					() => facing == null ? body?.QuantizeOrientation(self.Orientation) ?? self.Orientation : facing.Orientation,
					() => !IsTraitDisabled && drawFlags[(int)BodyMask.Foot],
					info.SkeletonBinded);
				RenderMeshes.Add(foot);
			}

			if (info.UpperArmMesh != null)
			{
				var upperArmMesh = self.World.MeshCache.GetMeshSequence(image, info.UpperArmMesh);
				upperArm = new MeshInstance(upperArmMesh, () => self.CenterPosition,
					() => facing == null ? body?.QuantizeOrientation(self.Orientation) ?? self.Orientation : facing.Orientation,
					() => !IsTraitDisabled && drawFlags[(int)BodyMask.UpperArm],
					info.SkeletonBinded);
				RenderMeshes.Add(upperArm);
			}

			if (info.LowerArmMesh != null)
			{
				var lowerArmMesh = self.World.MeshCache.GetMeshSequence(image, info.LowerArmMesh);
				lowerArm = new MeshInstance(lowerArmMesh, () => self.CenterPosition,
					() => facing == null ? body?.QuantizeOrientation(self.Orientation) ?? self.Orientation : facing.Orientation,
					() => !IsTraitDisabled && drawFlags[(int)BodyMask.LowerArm],
					info.SkeletonBinded);
				RenderMeshes.Add(lowerArm);
			}

			if (info.HandMesh != null)
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

		public BodyMask[] GetBodyMasks()
		{
			return info.Masks;
		}

		protected override void Created(Actor self)
		{
			withMeshBody = self.Trait<WithMeshBody>();

			base.Created(self);
		}

		protected override void TraitEnabled(Actor self)
		{
			base.TraitEnabled(self);
			foreach (var t in info.Masks)
				withMeshBody.SetDrawPart(t, false);
		}

		protected override void TraitDisabled(Actor self)
		{
			base.TraitDisabled(self);

			foreach (var t in info.Masks)
				withMeshBody.SetDrawPart(t, true);
		}
	}
}
