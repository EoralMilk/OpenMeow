using System;
using System.ComponentModel.Design;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits.Trait3D;
using OpenRA.Traits;
using static System.Net.Mime.MediaTypeNames;

namespace OpenRA.Meow.RPG.Mechanics
{

	public enum GearType
	{
		/// <summary>
		/// only change body part covering material
		/// </summary>
		CloseFitting,

		/// <summary>
		/// only add mesh
		/// </summary>
		MeshAddon,

		/// <summary>
		/// replace body part and add cloth mesh part
		/// </summary>
		Replacement,

		/// <summary>
		/// replace body part and add cloth mesh part, also change  body part covering material
		/// </summary>
		Misc,

	}

	public class GearItemInfo : ConditionItemInfo
	{
		[FieldLoader.Require]
		public readonly GearType GearType;

		public readonly string Image = null;

		[Desc("The addon mesh part will directly add to render.")]
		public readonly string[] AddonMeshPart = null;

		[Desc("Define which body parts are covered by clothing. \n" +
			"Will affect material coverage and body mesh displaying")]
		public readonly BodyMask[] Masks = { BodyMask.None };

		[Desc("Define the coving material on body mesh.")]
		public readonly string CoveringMaterail = null;

		// The body parts provided by the clothes will replace the body parts themselves
		public readonly string HeadMesh = null;
		public readonly string TorsoMesh = null;
		public readonly string HipMesh = null;
		public readonly string ThighMesh = null;
		public readonly string LegMesh = null;
		public readonly string FootMesh = null;
		public readonly string UpperArmMesh = null;
		public readonly string LowerArmMesh = null;
		public readonly string HandMesh = null;

		public override object Create(ActorInitializer init)
		{
			return new GearItem(this, init.Self);
		}
	}

	public class GearItem : ConditionItem
	{
		readonly GearItemInfo info;
		public GearType ClothType => info.GearType;
		public Func<bool> IsActive;
		public bool Active { get; set; }
		readonly bool[] drawFlags;

		protected string[] addonMeshSequences;
		public readonly MeshInstance[] AddonMeshInstances;
		protected string[] bodyMeshSequences;
		public readonly MeshInstance[] BodyMeshInstances;
		public readonly IMaterial CoveringMaterail;

		public bool GetMaskAt(BodyMask mask)
		{
			return drawFlags[(int)mask];
		}

		public GearItem(GearItemInfo info, Actor self)
			: base(info, self)
		{
			this.info = info;
			var image = info.Image;
			Active = true;
			IsActive = () => Active;

			drawFlags = new bool[(int)BodyMask.None];
			for (int i = 0; i < drawFlags.Length; i++)
			{
				drawFlags[i] = info.Masks.Contains((BodyMask)i);
			}

			if (info.CoveringMaterail != null)
			{
				CoveringMaterail = self.World.MeshCache.GetMaterial(info.CoveringMaterail);
				if (CoveringMaterail == null)
					throw new Exception("Can't find CoveringMaterail: " + info.CoveringMaterail +
						" in MeshCache, might not exist or not declare in any sequences");
			}

			if (ClothType == GearType.CloseFitting)
				return;

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

			addonMeshSequences = info.AddonMeshPart;

			BodyMeshInstances = new MeshInstance[(int)BodyMask.None];

			for (int i = 0; i < bodyMeshSequences.Length; i++)
			{
				int mask = i;
				if (bodyMeshSequences[i] == null)
					continue;
				var mesh = self.World.MeshCache.GetMeshSequence(image, bodyMeshSequences[i]);
				BodyMeshInstances[i] = new MeshInstance(mesh,
					() => self.CenterPosition,
					() => WRot.None,
					() => IsActive(),
					null);

				if (CoveringMaterail != null)
					BodyMeshInstances[i].Material = CoveringMaterail;
			}

			AddonMeshInstances = new MeshInstance[addonMeshSequences.Length];

			for (int i = 0; i < addonMeshSequences.Length; i++)
			{
				int mask = i;
				if (addonMeshSequences[i] == null)
					continue;
				var mesh = self.World.MeshCache.GetMeshSequence(image, addonMeshSequences[i]);
				AddonMeshInstances[i] = new MeshInstance(mesh,
					() => self.CenterPosition,
					() => WRot.None,
					() => IsActive(),
					null);
			}
		}

		public override void EquipingEffect(Actor actor)
		{
			base.EquipingEffect(actor);
			Active = true;
			actor.TraitOrDefault<WithMeshBody>()?.AddCloth(ItemActor, this);
		}

		public override void UnequipingEffect(Actor actor)
		{
			base.UnequipingEffect(actor);
			Active = false;
			actor.TraitOrDefault<WithMeshBody>()?.RemoveCloth(ItemActor);
		}

	}
}
