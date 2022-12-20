using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Trait3D;
using OpenRA.Primitives;
using OpenRA.Traits;
using TrueSync;

namespace OpenRA.Meow.RPG.Mechanics
{
	class BodyFatInfo : ConditionalTraitInfo, Requires<WithSkeletonInfo>
	{
		// visual
		public readonly string[] BodyFatModifiers = Array.Empty<string>();

		public readonly string SkeletonToUse = null;

		// logic
		public readonly int MaxNutritionCapacity = 1000;

		public readonly int PercentageOfNutritionForHeal = 100;

		public readonly int HealPercentage = 10;

		public readonly int[] InitFatValue = new int[1] {0};

		public readonly int FatLoseSpeed = 1;

		public readonly NutritionType[] NutritionToAbsorb = new NutritionType[1] { NutritionType.Common };

		public override object Create(ActorInitializer init) { return new BodyFat(init.Self, this); }
	}

	class BodyFat : ConditionalTrait<BodyFatInfo>, ITick, INotifyCreated, IRenderMeshesUpdate, IReceiveNutrition
	{
		IHealth health;
		readonly Actor self;

		readonly SkeletonRestPoseModifier[] fatModifiers;

		readonly WithSkeleton withSkeleton;

		readonly BodyFatInfo info;

		FP fatMul = 0;
		int fatValue = 0;

		public BodyFat(Actor self, BodyFatInfo info)
			: base(info)
		{
			this.self = self;
			this.info = info;
			if (info.InitFatValue.Length >= 2)
				fatValue = self.World.SharedRandom.Next(info.InitFatValue[0], info.InitFatValue[1] + 1);
			else
				fatValue = info.InitFatValue[0];

			if (info.BodyFatModifiers.Length != 0)
			{
				List<SkeletonRestPoseModifier> tempmodifiers = new List<SkeletonRestPoseModifier>();
				if (info.SkeletonToUse == null)
					throw new YamlException("BlendTreeHandler must define a SkeletonToUse for get animations");
				withSkeleton = self.TraitsImplementing<WithSkeleton>().Single(w => w.Info.Name == info.SkeletonToUse);

				foreach (var nm in info.BodyFatModifiers)
				{
					if (withSkeleton.OrderedSkeleton.SkeletonRestPoseModifiers.TryGetValue(nm, out var modifier))
					{
						tempmodifiers.Add(modifier);
					}
				}

				fatModifiers = tempmodifiers.ToArray();
			}
		}

		protected override void Created(Actor self)
		{
			base.Created(self);

			health = self.TraitOrDefault<IHealth>();
		}

		void ITick.Tick(Actor self)
		{
			if (self.IsDead || IsTraitDisabled)
				return;

			fatValue -= info.FatLoseSpeed;
			fatValue = Math.Clamp(fatValue, 0, info.MaxNutritionCapacity);
			// visual
			if (fatModifiers != null)
			{
				fatMul = (FP)fatValue / info.MaxNutritionCapacity;
			}
		}

		FP lastFatMul;
		public void RenderUpdateMeshes(Actor self)
		{
			if (fatModifiers != null && lastFatMul != fatMul)
			{
				lastFatMul = fatMul;
				foreach (var modifier in fatModifiers)
				{
					withSkeleton.Skeleton.ApplySkeletonModifier(modifier, fatMul);
				}
			}

		}

		void IReceiveNutrition.GainNutrition(Dictionary<NutritionType, int> gainNutrition)
		{
			foreach (var nt in Info.NutritionToAbsorb)
			{
				if (gainNutrition.TryGetValue(nt, out var nvalue))
				{
					if (health != null && health.HP < health.MaxHP)
					{
						var healNValue = nvalue * info.PercentageOfNutritionForHeal / 100;
						nvalue -= healNValue;
						health.InflictDamage(self, self, new Damage(-healNValue * info.HealPercentage / 100), true);
					}

					fatValue += nvalue;
				}
			}

			fatValue = Math.Clamp(fatValue, 0, info.MaxNutritionCapacity);
		}
	}
}
