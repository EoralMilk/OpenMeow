using OpenRA.Graphics;
using OpenRA.Traits;

namespace OpenRA.Meow.RPG.Mechanics
{
	public class ConsumableItemInfo : ConditionItemInfo
	{
		public readonly int CanUse = 1;

		public override object Create(ActorInitializer init)
		{
			return new ConsumableItem(this, init.Self);
		}
	}

	public class ConsumableItem : ConditionItem
	{

		public ConsumableItem(ConsumableItemInfo info, Actor self)
			: base(info, self)
		{
		}

		public override void EquipingEffect(Actor actor)
		{
			base.EquipingEffect(actor);
		}

		public override void UnequipingEffect(Actor actor)
		{
			base.UnequipingEffect(actor);
		}
	}
}
