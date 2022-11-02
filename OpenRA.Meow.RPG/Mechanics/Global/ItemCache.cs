using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenRA.Primitives;
using System.Xml.Linq;
using OpenRA.Traits;

namespace OpenRA.Meow.RPG.Mechanics
{
	public class ItemCacheInfo : TraitInfo
	{
		public override object Create(ActorInitializer init) { return new ItemCache(init.World, this); }
	}

	public class ItemCache : IDisposable
	{
		public ItemCache(World world, ItemCacheInfo itemCacheInfo)
		{

		}

		public Dictionary<uint, Actor> GameItemActors = new Dictionary<uint, Actor>();

		public Actor GetActor(uint actorId)
		{
			return GameItemActors[actorId];
		}

		public void TryAddItem(Actor itemActor)
		{
			if (GameItemActors.ContainsKey(itemActor.ActorID))
				return;

			GameItemActors.Add(itemActor.ActorID, itemActor);
		}

		public void AddItem(uint actorId, Actor itemActor)
		{
			if (GameItemActors.ContainsKey(actorId))
				throw new Exception("the itemactor id " + actorId + " has already exist");

			GameItemActors.Add(actorId, itemActor);
		}

		public Item AddItem(World world,string itemActorInfoName)
		{
			var a = world.CreateActor(false, itemActorInfoName, new TypeDictionary());
			GameItemActors.Add(a.ActorID, a);
			var item = a.TraitOrDefault<Item>();
			if (item == null)
				throw new Exception("The actor: " + itemActorInfoName + " does not have Item trait");

			return item;
		}

		public Item GetItem(uint itemActorId)
		{
			if (!GameItemActors.TryGetValue(itemActorId, out Actor a))
				throw new Exception("The actorid: " + itemActorId + " does not in dictionary");

			return a.TraitOrDefault<Item>();
		}

		public bool HasItem(uint itemActorId)
		{
			return GameItemActors.ContainsKey(itemActorId);
		}

		public void RemvoeItem(uint itemActorId)
		{
			if (GameItemActors.ContainsKey(itemActorId))
			{
				GameItemActors.Remove(itemActorId);
			}
		}

		public void Dispose()
		{
			GameItemActors.Clear();
		}

	}
}
