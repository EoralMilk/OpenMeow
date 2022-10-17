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

		public readonly string FaceAddonMesh = "face_addon";

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

		public readonly string SkeletonBinded;
		protected readonly RenderMeshes RenderMeshes;

		protected MeshInstance faceAddon;
		protected string[] meshSequences;
		protected MeshInstance[] meshInstances;

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

			meshInstances = new MeshInstance[9];
			meshSequences = new string[9];

			meshSequences[(int)BodyMask.Head] = info.HeadMesh;
			meshSequences[(int)BodyMask.Torso] = info.TorsoMesh;
			meshSequences[(int)BodyMask.Hip] = info.HipMesh;
			meshSequences[(int)BodyMask.Thigh] = info.ThighMesh;
			meshSequences[(int)BodyMask.Leg] = info.LegMesh;
			meshSequences[(int)BodyMask.Foot] = info.FootMesh;
			meshSequences[(int)BodyMask.UpperArm] = info.UpperArmMesh;
			meshSequences[(int)BodyMask.LowerArm] = info.LowerArmMesh;
			meshSequences[(int)BodyMask.Hand] = info.HandMesh;

			for (int i = 0; i < meshInstances.Length; i++)
			{
				int mask = i;
				if (meshSequences[i] == null)
					continue;
				var mesh = self.World.MeshCache.GetMeshSequence(image, meshSequences[i]);
				meshInstances[i] = new MeshInstance(mesh,
					() => self.CenterPosition,
					() => facing == null ? body?.QuantizeOrientation(self.Orientation) ?? self.Orientation : facing.Orientation,
					() => !IsTraitDisabled && drawFlags[mask],
					info.SkeletonBinded);
				RenderMeshes.Add(meshInstances[i]);
			}

			{
				var faceAddonMesh = self.World.MeshCache.GetMeshSequence(image, info.FaceAddonMesh);
				faceAddon = new MeshInstance(faceAddonMesh,
					() => self.CenterPosition,
					() => facing == null ? body?.QuantizeOrientation(self.Orientation) ?? self.Orientation : facing.Orientation,
					() => !IsTraitDisabled && drawFlags[(int)BodyMask.Head],
					info.SkeletonBinded);
				RenderMeshes.Add(faceAddon);
			}
		}

		public void ChangeBodyPartMaterail(IMaterial material, BodyMask bodyMask)
		{
			if (meshInstances[(int)bodyMask] != null)
				meshInstances[(int)bodyMask].Material = material;
		}

		public void ResetBodyPartMaterail(BodyMask bodyMask)
		{
			if (meshInstances[(int)bodyMask] != null)
				meshInstances[(int)bodyMask].Material = meshInstances[(int)bodyMask].OrderedMesh.DefaultMaterial;
		}
	}
}
