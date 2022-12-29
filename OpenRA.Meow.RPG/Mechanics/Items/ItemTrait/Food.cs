using System;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Meow.RPG.Mechanics
{
	public class FoodInfo : TraitInfo, Requires<ConsumableItemInfo>
	{
		public readonly int Size = 100;

		public readonly int Value = 1000;

		public readonly int DigestResistance = 10;

		public readonly int HealthResistance = 10;

		public readonly string[] EatSounds = Array.Empty<string>();

		public readonly NutritionType NutritionType = NutritionType.Common;
		public override object Create(ActorInitializer init)
		{
			return new Food(init.Self, this);
		}
	}

	public class Food : IFood, INotifyCreated, IConsumeAction
	{
		readonly FoodInfo info;
		int value;
		int size;
		Health health;
		public readonly Actor Self;
		Stomach stomach;
		public Actor FoodActor => Self;

		public Food(Actor self, FoodInfo info)
		{
			this.Self = self;
			this.info = info;
			value = info.Value;
			size = info.Size;
		}

		int IFood.CurrentSize => size;

		public void Created(Actor self)
		{
			health = self.TraitOrDefault<Health>();

			// hack: disable health remove when death
			if (health != null)
				health.RemoveOnDeath = false;
		}

		bool IFood.Digesting(Actor digester, int acid, BitSet<DamageType> damageTypes, out Nutrition nutrition)
		{
			var digestValue = value;

			if (health != null && !health.IsDead)
			{
				health.InflictDamage(Self , digester, new Damage(acid / info.HealthResistance, damageTypes), false);
			}
			else
			{
				value -= acid / info.DigestResistance;
				value = value < 0 ? 0 : value;

				if (value == 0)
				{
					digester.World.AddFrameEndTask(w => Self.Dispose());
				}
			}

			digestValue = digestValue - value;

			nutrition = new Nutrition(digestValue, info.NutritionType);

			if (info.Value != 0)
				size = info.Size * value / info.Value;
			else
				size = info.Size;

			return value == 0;
		}

		public void EatBy(Actor eater)
		{
			if (stomach == null)
			{
				var sound = info.EatSounds.RandomOrDefault(eater.World.LocalRandom);
				if (sound != null)
					Game.Sound.Play(SoundType.World, sound, eater.CenterPosition);
				stomach = eater.TraitOrDefault<Stomach>();
				stomach?.AcceptFood(this);
			}

			if (Self.IsInWorld)
			{
				Self.World.AddFrameEndTask(w => w.Remove(Self));
			}
		}

		public void RemoveFromStomach()
		{
			stomach = null;
		}

		void IConsumeAction.OnConsumeBy(Item selfItem, Actor user)
		{
			EatBy(user);
		}
	}
}
