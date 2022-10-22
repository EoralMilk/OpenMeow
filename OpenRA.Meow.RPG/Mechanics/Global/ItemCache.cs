using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenRA.Primitives;
using System.Xml.Linq;

namespace OpenRA.Meow.RPG.Mechanics
{
	public static class ItemCache
	{
		public static Dictionary<uint, Actor> GameItemActors = new Dictionary<uint, Actor>();

		public static void AddItem(uint actorId, Actor itemActor)
		{
			if (GameItemActors.ContainsKey(actorId))
				throw new Exception("the itemactor id " + actorId + " has already exist");

			GameItemActors.Add(actorId, itemActor);
		}

		public static Item AddItem(World world,string itemActorInfoName)
		{
			var a = world.CreateActor(false, itemActorInfoName, new TypeDictionary());
			GameItemActors.Add(a.ActorID, a);
			var item = a.TraitOrDefault<Item>();
			if (item == null)
				throw new Exception("The actor: " + itemActorInfoName + " does not have Item trait");

			return item;
		}
		public static Item GetItem(uint itemActorId)
		{
			if (!GameItemActors.TryGetValue(itemActorId, out Actor a))
				throw new Exception("The actorid: " + itemActorId + " does not in dictionary");

			return a.TraitOrDefault<Item>();
		}

	}
}
