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
	class StomachInfo : ConditionalTraitInfo, Requires<WithSkeletonInfo>
	{
		// visual
		public readonly string BellyScaleModifier = null;

		public readonly string SkeletonToUse = null;

		public readonly float ScaleCapacity = 100;

		// auto

		public readonly int ScanFoodInterval = 50;

		// logic
		public readonly int MaxCapacity = 100;

		public readonly int MinAcidStrength = 5;

		public readonly int AcidStrengthChangeRate = 1;

		public readonly int MaxAcidStrength = 300;

		public readonly int DigestInterval = 5;

		public readonly NutritionType[] NutritionToAbsorb = new NutritionType[1] { NutritionType.Common };

		[Desc("Stomach acis damage type.")]
		public readonly BitSet<DamageType> DamageTypes = default;

		public override object Create(ActorInitializer init) { return new Stomach(init.Self, this); }
	}

	class Stomach : ConditionalTrait<StomachInfo>, ITick, INotifyCreated, IRenderMeshesUpdate,
		INotifyConsumeItem, INotifyActorDisposing, INotifyInventory
	{
		readonly List<IFood> foods = new List<IFood>();
		readonly List<IFood> foodsToRemove = new List<IFood>();
		IReceiveNutrition[] receiveNutritions;
		readonly Dictionary<NutritionType, int> currentDigestNutritionAbrosb = new Dictionary<NutritionType, int>();

		readonly WithSkeleton withSkeleton;
		readonly SkeletonRestPoseModifier bellyModifier;
		Inventory inventory;
		PickUpItem pickUpItem;

		[Sync]
		int acidPower;

		FP stomachSize = 0;
		int currentFoodSizeSum = 0;

		int tickInterval = 0;

		public Stomach(Actor self, StomachInfo info)
			: base(info)
		{
			acidPower = info.MinAcidStrength;
			foreach (var nt in info.NutritionToAbsorb)
				currentDigestNutritionAbrosb.Add(nt, 0);

			if (!string.IsNullOrEmpty(info.BellyScaleModifier))
			{
				if (info.SkeletonToUse == null)
					throw new YamlException("BlendTreeHandler must define a SkeletonToUse for get animations");
				withSkeleton = self.TraitsImplementing<WithSkeleton>().Single(w => w.Info.Name == info.SkeletonToUse);

				if (withSkeleton.OrderedSkeleton.SkeletonRestPoseModifiers.TryGetValue(info.BellyScaleModifier, out var modifier))
				{
					bellyModifier = modifier;
				}
				else
					throw new YamlException("Can't find BellyScaleModifier names " + info.BellyScaleModifier);
			}
		}

		protected override void Created(Actor self)
		{
			base.Created(self);

			receiveNutritions = self.TraitsImplementing<IReceiveNutrition>().ToArray();
			inventory = self.TraitOrDefault<Inventory>();
			pickUpItem = self.TraitOrDefault<PickUpItem>();
		}

		public bool CanAcceptFood(IFood food)
		{
			if (food.CurrentSize + currentFoodSizeSum <= Info.MaxCapacity)
			{
				return true;
			}

			return false;
		}

		public bool AcceptFood(IFood food)
		{
			if (CanAcceptFood(food))
			{
				currentFoodSizeSum += food.CurrentSize;
				foods.Add(food);
				foodToEat = null;
				return true;
			}

			return false;
		}

		Item foodToEat;
		bool inventoryHasUpdate;
		int scanFoodTick = 0;
		void FindFoodToEat()
		{
			if (inventory != null && inventoryHasUpdate)
			{
				inventoryHasUpdate = false;

				foreach (var item in inventory.Items)
				{
					if (item.ItemActor.TraitOrDefault<IFood>() != null)
					{
						foodToEat = item;
						break;
					}
				}
			}
		}

		void ITick.Tick(Actor self)
		{
			if (self.IsDead || IsTraitDisabled)
			{
				acidPower = Math.Clamp(acidPower - Info.AcidStrengthChangeRate, Info.MinAcidStrength, Info.MaxAcidStrength);
			}
			else
			{
				if (foodToEat != null)
				{
					foreach (var equipmentSlot in self.TraitsImplementing<EquipmentSlot>())
					{
						if (equipmentSlot.TryEquip(self, foodToEat, false))
						{
							foodToEat = null;
							break;
						}
					}
				}
				else if (currentFoodSizeSum < Info.MaxCapacity)
				{
					FindFoodToEat();

					if (foodToEat == null && pickUpItem != null && scanFoodTick-- < 0)
					{
						scanFoodTick = Info.ScanFoodInterval;
						if (pickUpItem.FindItemToPick(self, "Food", new WDist(9048)))
						{
							scanFoodTick += Info.ScanFoodInterval;
						}
					}
				}
			}

			if (foods.Any())
			{
				currentFoodSizeSum = 0;
				acidPower = Math.Clamp(acidPower + Info.AcidStrengthChangeRate, Info.MinAcidStrength, Info.MaxAcidStrength);

				if (tickInterval-- <= 0)
				{
					tickInterval = Info.DigestInterval;
					foreach (var food in foods)
					{
						if (food.Digesting(self, acidPower, Info.DamageTypes, out var nutrition))
						{
							foodsToRemove.Add(food);
						}

						if (currentDigestNutritionAbrosb.ContainsKey(nutrition.Type))
							currentDigestNutritionAbrosb[nutrition.Type] += nutrition.Value;
					}

					foreach (var re in receiveNutritions)
					{
						re.GainNutrition(currentDigestNutritionAbrosb);
					}

					// remove the food which already been digested
					foreach (var foodToRemove in foodsToRemove)
					{
						foods.Remove(foodToRemove);
					}

					foodsToRemove.Clear();
				}

				foreach (var food in foods)
				{
					currentFoodSizeSum += food.CurrentSize;
				}

				// visual
				if (bellyModifier != null)
				{
					stomachSize = TSMath.Lerp(stomachSize, (FP)currentFoodSizeSum / Info.ScaleCapacity, FP.Half);
				}

			}
			else
			{
				acidPower = Math.Clamp(acidPower - Info.AcidStrengthChangeRate, Info.MinAcidStrength, Info.MaxAcidStrength);
				currentFoodSizeSum = 0;
				// visual
				if (bellyModifier != null && stomachSize != 0)
				{
					stomachSize = TSMath.Lerp(stomachSize, FP.Zero, FP.Half);
					if (stomachSize < 0.002)
						stomachSize = 0;
				}
			}
		}

		FP lastStomachRenderSize;
		public void RenderUpdateMeshes(Actor self)
		{
			if (bellyModifier != null && lastStomachRenderSize != stomachSize)
			{
				lastStomachRenderSize = stomachSize;
				withSkeleton.Skeleton.ApplySkeletonModifier(bellyModifier, stomachSize);
			}

		}

		public bool CanConsume(Item item)
		{
			var food = item.ItemActor.TraitOrDefault<Food>();
			if (food != null)
				return CanAcceptFood(food);
			else
				return true;
		}

		public void Consume(Item item)
		{
			// add food to stomach by food itself, avoid multiple eat;
			return;
		}

		void INotifyActorDisposing.Disposing(Actor self)
		{
			foreach (var food in foods)
			{
				food.RemoveFromStomach();
				food.FoodActor?.Dispose();
			}

			currentFoodSizeSum = 0;
			foods.Clear();
		}

		bool INotifyInventory.TryAdd(Actor self, Item item)
		{
			return true;
		}

		void INotifyInventory.Added(Actor self, Item item)
		{
			inventoryHasUpdate = true;
		}

		bool INotifyInventory.TryRemove(Actor self, Item item)
		{
			return true;
		}

		void INotifyInventory.Removed(Actor self, Item item)
		{
			inventoryHasUpdate = true;
		}
	}
}
