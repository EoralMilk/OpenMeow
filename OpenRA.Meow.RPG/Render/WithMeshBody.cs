using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using OpenRA.Graphics;
using OpenRA.Meow.RPG.Mechanics;
using OpenRA.Primitives;
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

	public class GearLayers
	{
		public SortedList<GearItem, Actor>[] CloseFitGears;

		public SortedList<GearItem, Actor>[] ReplaceBodyPartGears;

		public List<GearItem>[] BodyPartMaskGears;

		class GearSort : IComparer<GearItem>
		{
			public int Compare(GearItem x, GearItem y)
			{
				int a = x.GearOrder;
				int b = y.GearOrder;

				return a.CompareTo(b);
			}
		}

		public GearLayers()
		{
			CloseFitGears = new SortedList<GearItem, Actor>[(int)BodyMask.None];
			ReplaceBodyPartGears = new SortedList<GearItem, Actor>[(int)BodyMask.None];
			BodyPartMaskGears = new List<GearItem>[(int)BodyMask.None];
		}

		public IMaterial GetCoveringMaterail(BodyMask mask)
		{
			if (CloseFitGears[(int)mask] == null || CloseFitGears[(int)mask].Count == 0)
			{
				return null;
			}
			else
			{
				return CloseFitGears[(int)mask].LastOrDefault().Key.CoveringMaterail;
			}
		}

		public bool HasMask(BodyMask mask)
		{
			return BodyPartMaskGears[(int)mask] != null && BodyPartMaskGears[(int)mask].Count > 0;
		}

		public MeshInstance GetBodyMeshPart(BodyMask mask)
		{
			if (ReplaceBodyPartGears[(int)mask] == null || ReplaceBodyPartGears[(int)mask].Count == 0)
			{
				return null;
			}
			else
			{
				return ReplaceBodyPartGears[(int)mask].FirstOrDefault().Key.BodyMeshInstances[(int)mask];
			}
		}

		public void AddGear(GearItem gearItem)
		{
			if ((gearItem.ClothType == GearType.CloseFitting || gearItem.ClothType == GearType.Misc) && gearItem.CoveringMaterail != null)
			{
				foreach (var mask in gearItem.Masks)
				{
					if (CloseFitGears[(int)mask] == null)
					{
						CloseFitGears[(int)mask] = new SortedList<GearItem, Actor>(new GearSort());
					}

					CloseFitGears[(int)mask].Add(gearItem, gearItem.ItemActor);
				}
			}

			if (gearItem.ClothType == GearType.Replacement || gearItem.ClothType == GearType.Misc)
			{
				foreach (var mask in gearItem.Masks)
				{
					if (gearItem.BodyMeshInstances[(int)mask] != null)
					{
						if (ReplaceBodyPartGears[(int)mask] == null)
						{
							ReplaceBodyPartGears[(int)mask] = new SortedList<GearItem, Actor>(new GearSort());
						}

						ReplaceBodyPartGears[(int)mask].Add(gearItem, gearItem.ItemActor);
					}

					if (BodyPartMaskGears[(int)mask] == null)
					{
						BodyPartMaskGears[(int)mask] = new List<GearItem>();
					}

					BodyPartMaskGears[(int)mask].Add(gearItem);
				}
			}
		}

		public void RemoveGear(GearItem gearItem)
		{
			if ((gearItem.ClothType == GearType.CloseFitting || gearItem.ClothType == GearType.Misc) && gearItem.CoveringMaterail != null)
			{
				foreach (var mask in gearItem.Masks)
				{
					if (CloseFitGears[(int)mask] != null)
					{
						CloseFitGears[(int)mask].Remove(gearItem);
					}
				}
			}

			if (gearItem.ClothType == GearType.Replacement || gearItem.ClothType == GearType.Misc)
			{
				foreach (var mask in gearItem.Masks)
				{
					if (gearItem.BodyMeshInstances[(int)mask] != null)
					{
						if (ReplaceBodyPartGears[(int)mask] != null)
						{
							ReplaceBodyPartGears[(int)mask].Remove(gearItem);
						}
					}

					if (BodyPartMaskGears[(int)mask] != null)
					{
						BodyPartMaskGears[(int)mask].Remove(gearItem);
					}
				}
			}
		}

	}

	public class WithMeshBodyInfo : ConditionalTraitInfo, Requires<RenderMeshesInfo>
	{
		public readonly string SkeletonBinded = null;
		public readonly string Image = null;

		public readonly Color[] HairColors = null;
		public readonly string[] HairMeshs = null;
		public readonly string FaceAddonMesh = null;

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
		protected MeshInstance hair;
		readonly Color haircolor;

		protected string[] bodyMeshSequences;
		protected MeshInstance[] bodyMeshInstances;

		readonly bool[] drawFlags = new bool[(int)BodyMask.None];

		readonly Dictionary<Actor, GearItem> clothes = new Dictionary<Actor, GearItem>();
		readonly MeshInstance[] currentBodyPart = new MeshInstance[(int)BodyMask.None];

		readonly GearLayers gearLayers = new GearLayers();

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
			gearLayers.AddGear(clothItem);
			clothItem.IsActive = () => clothItem.Active && !IsTraitDisabled;

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

				if (clothItem.ClothType > GearType.MeshAddon)
				{
					foreach (var mask in clothItem.Masks)
					{
						SetDrawPart(mask, !gearLayers.HasMask(mask));

						var meshReplace = gearLayers.GetBodyMeshPart(mask);

						if (meshReplace == null)
							continue;

						meshReplace.PoistionFunc = () => self.CenterPosition;
						meshReplace.RotationFunc = () => facing == null ? body?.QuantizeOrientation(self.Orientation) ?? self.Orientation : facing.Orientation;
						meshReplace.SkeletonBinded = info.SkeletonBinded;
						SetPartMesh(mask, meshReplace);
					}
				}
			}

			foreach (var mask in clothItem.Masks)
			{
				ChangeBodyPartMaterail(gearLayers.GetCoveringMaterail(mask), mask);
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
			gearLayers.RemoveGear(clothItem);
			clothItem.IsActive = () => clothItem.Active;

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

				if (clothItem.ClothType > GearType.MeshAddon)
				{
					foreach (var mask in clothItem.Masks)
					{
						SetDrawPart(mask, !gearLayers.HasMask(mask));

						var meshReplace = gearLayers.GetBodyMeshPart(mask);

						if (meshReplace == null)
							continue;

						// meshReplace.PoistionFunc = () => clothactor.CenterPosition;
						// meshReplace.RotationFunc = () => WRot.None;
						// meshReplace.SkeletonBinded = null;
						// meshReplace.Material = meshReplace.OrderedMesh.DefaultMaterial;
						// ResetPartMesh(mask);

						meshReplace.PoistionFunc = () => self.CenterPosition;
						meshReplace.RotationFunc = () => facing == null ? body?.QuantizeOrientation(self.Orientation) ?? self.Orientation : facing.Orientation;
						meshReplace.SkeletonBinded = info.SkeletonBinded;
						SetPartMesh(mask, meshReplace);
					}
				}
			}

			foreach (var mask in clothItem.Masks)
			{
				ChangeBodyPartMaterail(gearLayers.GetCoveringMaterail(mask), mask);
			}
		}

		void SetPartMesh(BodyMask mask, MeshInstance meshInstance)
		{
			SetDrawPart(mask, false);
			if (currentBodyPart[(int)mask] != null)
				RenderMeshes.Remove(currentBodyPart[(int)mask]);
			currentBodyPart[(int)mask] = meshInstance;
			RenderMeshes.Add(currentBodyPart[(int)mask]);
		}

		public void SetDrawPart(BodyMask mask, bool draw)
		{
			drawFlags[(int)mask] = draw;
		}

		void ChangeBodyPartMaterail(IMaterial material, BodyMask bodyMask)
		{
			if (material != null)
			{
				if (currentBodyPart[(int)bodyMask] != null)
					currentBodyPart[(int)bodyMask].Material = material;
				bodyMeshInstances[(int)bodyMask].Material = material;
			}
			else
				ResetBodyPartMaterail(bodyMask);
		}

		void ResetBodyPartMaterail(BodyMask bodyMask)
		{
			if (currentBodyPart[(int)bodyMask] != null)
			{
				currentBodyPart[(int)bodyMask].Material = bodyMeshInstances[(int)bodyMask].OrderedMesh.DefaultMaterial;
			}

			bodyMeshInstances[(int)bodyMask].Material = bodyMeshInstances[(int)bodyMask].OrderedMesh.DefaultMaterial;
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
				RenderMeshes.Add(bodyMeshInstances[i]);
			}

			if (!string.IsNullOrEmpty(info.FaceAddonMesh))
			{
				var faceAddonMesh = self.World.MeshCache.GetMeshSequence(image, info.FaceAddonMesh);
				faceAddon = new MeshInstance(faceAddonMesh,
					() => self.CenterPosition,
					() => facing == null ? body?.QuantizeOrientation(self.Orientation) ?? self.Orientation : facing.Orientation,
					() => !IsTraitDisabled && drawFlags[(int)BodyMask.Head],
					info.SkeletonBinded);
				RenderMeshes.Add(faceAddon);
			}

			if (info.HairMeshs != null && info.HairMeshs.Length > 0)
			{
				var hairMesh = self.World.MeshCache.GetMeshSequence(image, info.HairMeshs[self.World.SharedRandom.Next(0, info.HairMeshs.Length)]);
				hair = new MeshInstance(hairMesh,
					() => self.CenterPosition,
					() => facing == null ? body?.QuantizeOrientation(self.Orientation) ?? self.Orientation : facing.Orientation,
					() => !IsTraitDisabled,
					info.SkeletonBinded);
				if (info.HairColors != null && info.HairColors.Length > 0)
				{
					haircolor = info.HairColors[self.World.SharedRandom.Next(0, info.HairColors.Length)];
					hair.GetRemap = () => haircolor;
				}

				RenderMeshes.Add(hair);
			}
		}
	}
}
