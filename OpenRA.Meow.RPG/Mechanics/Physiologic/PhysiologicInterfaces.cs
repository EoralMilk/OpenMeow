using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Meow.RPG.Mechanics
{
	public enum NutritionType
	{
		Common,
	}

	public struct Nutrition
	{
		public readonly int Value;
		public readonly NutritionType Type;

		public Nutrition(int value, NutritionType type)
		{
			Value = value;
			Type = type;
		}
	}

	public interface IFood
	{
		/// <summary>
		/// Digest food according to stomach acid strength
		/// </summary>
		/// <returns>true means the food has been digested</returns>
		bool Digesting(Actor digester, int acid, BitSet<DamageType> damageTypes, out Nutrition nutrition);
		void EatBy(Actor eater);
		void RemoveFromStomach();

		int CurrentSize { get; }

		Actor FoodActor { get; }
	}

	public interface IReceiveNutrition
	{
		void GainNutrition(Dictionary<NutritionType, int> gainNutrition);
	}
}
