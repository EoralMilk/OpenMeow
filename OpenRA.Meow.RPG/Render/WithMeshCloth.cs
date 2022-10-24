//using System;
//using OpenRA.Graphics;
//using OpenRA.Traits;

//namespace OpenRA.Mods.Common.Traits.Trait3D
//{
//	public interface IWithCloth
//	{
//		BodyMask[] GetBodyMasks();
//	}

//	public class WithMeshClothInfo : WithMeshInfo, Requires<WithMeshBodyInfo>
//	{
//		public readonly BodyMask[] Masks = { BodyMask.None };

//		public readonly string CoveringMaterail = null;

//		public readonly string HeadMesh = null;
//		public readonly string TorsoMesh = null;
//		public readonly string HipMesh = null;
//		public readonly string ThighMesh = null;
//		public readonly string LegMesh = null;
//		public readonly string FootMesh = null;
//		public readonly string UpperArmMesh = null;
//		public readonly string LowerArmMesh = null;
//		public readonly string HandMesh = null;
//		public override object Create(ActorInitializer init) { return new WithMeshCloth(init.Self, this); }
//	}

//	public class WithMeshCloth : WithMesh, IWithCloth
//	{
//		WithMeshBody withMeshBody;
//		readonly WithMeshClothInfo info;

//		protected string[] meshSequences;
//		protected MeshInstance[] meshInstances;

//		protected IMaterial coveringMaterail;

//		readonly bool[] drawFlags = new bool[9];

//		public void SetDrawPart(BodyMask mask, bool draw)
//		{
//			drawFlags[(int)mask] = draw;
//		}

//		public WithMeshCloth(Actor self, WithMeshClothInfo info)
//			: base(self, info)
//		{
//			this.info = info;

//			var body = self.TraitOrDefault<BodyOrientation>();

//			var image = RenderMeshes.Image;
//			if (Info.Image != null)
//			{
//				image = Info.Image;
//			}

//			IFacing facing = self.TraitOrDefault<IFacing>();

//			for (int i = 0; i < drawFlags.Length; i++)
//				drawFlags[i] = true;

//			meshInstances = new MeshInstance[9];
//			meshSequences = new string[9];

//			meshSequences[(int)BodyMask.Head] = info.HeadMesh;
//			meshSequences[(int)BodyMask.Torso] = info.TorsoMesh;
//			meshSequences[(int)BodyMask.Hip] = info.HipMesh;
//			meshSequences[(int)BodyMask.Thigh] = info.ThighMesh;
//			meshSequences[(int)BodyMask.Leg] = info.LegMesh;
//			meshSequences[(int)BodyMask.Foot] = info.FootMesh;
//			meshSequences[(int)BodyMask.UpperArm] = info.UpperArmMesh;
//			meshSequences[(int)BodyMask.LowerArm] = info.LowerArmMesh;
//			meshSequences[(int)BodyMask.Hand] = info.HandMesh;

//			for (int i = 0; i < meshInstances.Length; i++)
//			{
//				int mask = i;
//				if (meshSequences[i] == null)
//					continue;
//				var mesh = self.World.MeshCache.GetMeshSequence(image, meshSequences[i]);
//				meshInstances[i] = new MeshInstance(mesh,
//					() => self.CenterPosition,
//					() => facing == null ? body?.QuantizeOrientation(self.Orientation) ?? self.Orientation : facing.Orientation,
//					() => !IsTraitDisabled && drawFlags[mask],
//					info.SkeletonBinded);
//				RenderMeshes.Add(meshInstances[i]);
//			}

//			if (info.CoveringMaterail != null)
//			{
//				coveringMaterail = self.World.MeshCache.GetMaterial(info.CoveringMaterail);
//				if (coveringMaterail == null)
//					throw new Exception("Can't find CoveringMaterail: " + info.CoveringMaterail +
//						" in MeshCache, might not exist or not declare in any sequences");
//			}
//		}

//		public BodyMask[] GetBodyMasks()
//		{
//			return info.Masks;
//		}

//		protected override void Created(Actor self)
//		{
//			withMeshBody = self.Trait<WithMeshBody>();

//			base.Created(self);
//		}

//		protected override void TraitEnabled(Actor self)
//		{
//			base.TraitEnabled(self);
//			foreach (var t in info.Masks)
//			{
//				withMeshBody.SetDrawPart(t, false);
//				if (coveringMaterail != null)
//					ChangeBodyPartMaterail(coveringMaterail, t);
//			}
//		}

//		protected override void TraitDisabled(Actor self)
//		{
//			base.TraitDisabled(self);

//			foreach (var t in info.Masks)
//			{
//				withMeshBody.SetDrawPart(t, true);
//				if (coveringMaterail != null)
//					ResetBodyPartMaterail(t);
//			}
//		}

//		void ChangeBodyPartMaterail(IMaterial material, BodyMask bodyMask)
//		{
//			withMeshBody.ChangeBodyPartMaterail(material, bodyMask);
//			if (meshInstances[(int)bodyMask] != null)
//				meshInstances[(int)bodyMask].Material = material;
//		}

//		void ResetBodyPartMaterail(BodyMask bodyMask)
//		{
//			withMeshBody.ResetBodyPartMaterail(bodyMask);
//			if (meshInstances[(int)bodyMask] != null)
//				meshInstances[(int)bodyMask].Material = meshInstances[(int)bodyMask].OrderedMesh.DefaultMaterial;
//		}
//	}
//}
