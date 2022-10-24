using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenRA.Meow.RPG.Mechanics.Display;
using OpenRA.Traits;

namespace OpenRA.Meow.RPG
{
	public class CharacterClothManagerInfo : TraitInfo
	{

		public override object Create(ActorInitializer init) { return new CharacterClothManager(init.Self, this); }

	}

	public class CharacterClothManager
	{
		public CharacterClothManager(Actor self , CharacterClothManagerInfo info)
		{

		}
	}
}
