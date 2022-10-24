using System;
using System.Collections.Generic;
using System.Numerics;
using OpenRA.Graphics;
using OpenRA.Meow.RPG.Mechanics;
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
		readonly WithMeshBodyInfo info;

		readonly Actor self;
		readonly BodyOrientation body;
		readonly IFacing facing;

		public readonly string SkeletonBinded;
		protected readonly RenderMeshes RenderMeshes;

		protected MeshInstance faceAddon;
		protected string[] bodyMeshSequences;
		protected MeshInstance[] bodyMeshInstances;

		readonly bool[] drawFlags = new bool[(int)BodyMask.None];

		readonly Dictionary<Actor, GearItem> clothes = new Dictionary<Actor, GearItem>();
		readonly MeshInstance[] currentBodyPart = new MeshInstance[(int)BodyMask.None];
		readonly IMaterial[] currentCoveringMaterial = new IMaterial[(int)BodyMask.None];

		#region add and remove cloth
		public void AddClothFromClothActor(Actor actor)
		{
			var cloth = actor.Trait<GearItem>();
			AddClothInner(actor, cloth);
		}

		public void AddCloth(uint clothActorId)
		{
			var actor = self.World.WorldActor.Trait<ItemCache>().GetActor(clothActorId);
			AddClothInner(actor, actor.Trait<GearItem>());
		}

		public void AddCloth(uint clothActorId, GearItem clothItem)
		{
			var actor = self.World.WorldActor.Trait<ItemCache>().GetActor(clothActorId);
			AddClothInner(actor, clothItem);
		}

		public void AddCloth(Actor actor, GearItem clothItem)
		{
			AddClothInner(actor, clothItem);
		}

		void AddClothInner(Actor actor, GearItem clothItem)
		{
			clothes.Add(actor, clothItem);
			clothItem.IsActive = () => clothItem.Active && !IsTraitDisabled;

			if (clothItem.CoveringMaterail != null)
			{
				for (int i = 0; i < (int)BodyMask.None; i++)
				{
					var mask = (BodyMask)i;
					if (clothItem.GetMaskAt(mask))
					{
						ChangeBodyPartMaterail(clothItem.CoveringMaterail, mask);
					}
				}
			}

			if (clothItem.ClothType > GearType.CloseFitting)
			{
				if (clothItem.AddonMeshInstances != null)
				{
					foreach (var mesh in clothItem.AddonMeshInstances)
					{
						if (mesh == null)
							continue;

						mesh.PoistionFunc = () => self.CenterPosition;
						mesh.RotationFunc = () => facing == null ? body?.QuantizeOrientation(self.Orientation) ?? self.Orientation : facing.Orientation;
						mesh.SkeletonBinded = info.SkeletonBinded;
						RenderMeshes.Add(mesh);
					}
				}

				if (clothItem.ClothType > GearType.MeshAddon && clothItem.BodyMeshInstances != null)
				{
					for (int i = 0; i < clothItem.BodyMeshInstances.Length; i++)
					{
						var mask = (BodyMask)i;
						if (!clothItem.GetMaskAt(mask))
							continue;

						if (clothItem.BodyMeshInstances[i] == null)
						{
							SetDrawPart(mask, false);
							continue;
						}

						clothItem.BodyMeshInstances[i].PoistionFunc = () => self.CenterPosition;
						clothItem.BodyMeshInstances[i].RotationFunc = () => facing == null ? body?.QuantizeOrientation(self.Orientation) ?? self.Orientation : facing.Orientation;
						clothItem.BodyMeshInstances[i].SkeletonBinded = info.SkeletonBinded;
						SetPartMesh(mask, clothItem.BodyMeshInstances[i]);
					}
				}
			}
		}

		public void RemoveCloth(Actor clothActor)
		{
			RemoveClothInner(clothActor);
		}

		void RemoveClothInner(Actor clothactor)
		{
			var clothItem = clothes[clothactor];
			clothes.Remove(clothactor);
			clothItem.IsActive = () => clothItem.Active;

			if (clothItem.CoveringMaterail != null)
			{
				for (int i = 0; i < (int)BodyMask.None; i++)
				{
					var mask = (BodyMask)i;
					if (clothItem.GetMaskAt(mask))
					{
						ResetBodyPartMaterail(mask);
					}
				}
			}

			if (clothItem.ClothType > GearType.CloseFitting)
			{
				if (clothItem.AddonMeshInstances != null)
				{
					foreach (var mesh in clothItem.AddonMeshInstances)
					{
						if (mesh == null)
							continue;

						mesh.PoistionFunc = () => clothactor.CenterPosition;
						mesh.RotationFunc = () => WRot.None;
						mesh.SkeletonBinded = null;
						RenderMeshes.Remove(mesh);
					}
				}

				if (clothItem.ClothType > GearType.MeshAddon && clothItem.BodyMeshInstances != null)
				{
					for (int i = 0; i < clothItem.BodyMeshInstances.Length; i++)
					{
						var mask = (BodyMask)i;
						if (!clothItem.GetMaskAt(mask))
							continue;

						if (clothItem.BodyMeshInstances[i] == null)
						{
							SetDrawPart(mask, true);
							continue;
						}

						clothItem.BodyMeshInstances[i].PoistionFunc = () => clothactor.CenterPosition;
						clothItem.BodyMeshInstances[i].RotationFunc = () => WRot.None;
						clothItem.BodyMeshInstances[i].SkeletonBinded = null;
						clothItem.BodyMeshInstances[i].Material = clothItem.BodyMeshInstances[i].OrderedMesh.DefaultMaterial;
						ResetPartMesh(mask);
					}
				}
			}
		}

		void SetPartMesh(BodyMask mask, MeshInstance meshInstance)
		{
			SetDrawPart(mask, false);
			if (currentBodyPart[(int)mask] != null)
				RenderMeshes.Remove(currentBodyPart[(int)mask]);
			currentBodyPart[(int)mask] = meshInstance;
			UpdateBodyPartMaterail(mask);
			RenderMeshes.Add(currentBodyPart[(int)mask]);
		}

		void ResetPartMesh(BodyMask mask)
		{
			SetDrawPart(mask, true);
			if (currentBodyPart[(int)mask] != null)
				RenderMeshes.Remove(currentBodyPart[(int)mask]);
			currentBodyPart[(int)mask] = null;
			UpdateBodyPartMaterail(mask);
		}

		public void SetDrawPart(BodyMask mask, bool draw)
		{
			drawFlags[(int)mask] = draw;
		}

		void ChangeBodyPartMaterail(IMaterial material, BodyMask bodyMask)
		{
			if (material != null)
			{
				currentCoveringMaterial[(int)bodyMask] = material;
				UpdateBodyPartMaterail(bodyMask);
			}
		}

		void UpdateBodyPartMaterail(BodyMask bodyMask)
		{
			if (currentBodyPart[(int)bodyMask] != null)
				currentBodyPart[(int)bodyMask].Material = currentCoveringMaterial[(int)bodyMask];
			bodyMeshInstances[(int)bodyMask].Material = currentCoveringMaterial[(int)bodyMask];
		}

		void ResetBodyPartMaterail(BodyMask bodyMask)
		{
			currentCoveringMaterial[(int)bodyMask] = bodyMeshInstances[(int)bodyMask].OrderedMesh.DefaultMaterial;
			if (currentBodyPart[(int)bodyMask] != null)
			{
				currentBodyPart[(int)bodyMask].Material = currentCoveringMaterial[(int)bodyMask];
			}

			bodyMeshInstances[(int)bodyMask].Material = currentCoveringMaterial[(int)bodyMask];
		}

		#endregion
		public WithMeshBody(Actor self, WithMeshBodyInfo info)
			: base(info)
		{
			this.self = self;
			this.info = info;
			SkeletonBinded = info.SkeletonBinded;

			body = self.TraitOrDefault<BodyOrientation>();
			RenderMeshes = self.Trait<RenderMeshes>();
			facing = self.TraitOrDefault<IFacing>();

			var image = RenderMeshes.Image;
			if (Info.Image != null)
			{
				image = Info.Image;
			}

			for (int i = 0; i < drawFlags.Length; i++)
				drawFlags[i] = true;

			bodyMeshInstances = new MeshInstance[(int)BodyMask.None];
			bodyMeshSequences = new string[(int)BodyMask.None];

			bodyMeshSequences[(int)BodyMask.Head] = info.HeadMesh;
			bodyMeshSequences[(int)BodyMask.Torso] = info.TorsoMesh;
			bodyMeshSequences[(int)BodyMask.Hip] = info.HipMesh;
			bodyMeshSequences[(int)BodyMask.Thigh] = info.ThighMesh;
			bodyMeshSequences[(int)BodyMask.Leg] = info.LegMesh;
			bodyMeshSequences[(int)BodyMask.Foot] = info.FootMesh;
			bodyMeshSequences[(int)BodyMask.UpperArm] = info.UpperArmMesh;
			bodyMeshSequences[(int)BodyMask.LowerArm] = info.LowerArmMesh;
			bodyMeshSequences[(int)BodyMask.Hand] = info.HandMesh;

			for (int i = 0; i < bodyMeshInstances.Length; i++)
			{
				int mask = i;
				if (bodyMeshSequences[i] == null)
					continue;
				var mesh = self.World.MeshCache.GetMeshSequence(image, bodyMeshSequences[i]);
				bodyMeshInstances[i] = new MeshInstance(mesh,
					() => self.CenterPosition,
					() => facing == null ? body?.QuantizeOrientation(self.Orientation) ?? self.Orientation : facing.Orientation,
					() => !IsTraitDisabled && drawFlags[mask],
					info.SkeletonBinded);
				currentCoveringMaterial[i] = bodyMeshInstances[i].Material;
				RenderMeshes.Add(bodyMeshInstances[i]);
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
	}
}
