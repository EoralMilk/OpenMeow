using OpenRA.Graphics;
using OpenRA.Traits;

namespace OpenRA.Meow.RPG.Mechanics
{
	public class ConditionItemInfo : ItemInfo
	{
		[GrantedConditionReference]
		[Desc("Condition to grant when equiped.")]
		public readonly string Condition = null;

		public override object Create(ActorInitializer init)
		{
			return new ConditionItem(this, init.Self);
		}
	}

	public class ConditionItem : Item
	{
		public readonly string Condition;
		int conditionToken = Actor.InvalidConditionToken;

		public ConditionItem(ConditionItemInfo info, Actor self)
			: base(info, self)
		{
			Condition = info.Condition;
		}

		public override void EquipingEffect(Actor actor, EquipmentSlot slot)
		{
			if (Condition != null && actor != null && conditionToken == Actor.InvalidConditionToken)
				conditionToken = actor.GrantCondition(Condition);

			base.EquipingEffect(actor, slot);
		}

		public override void UnequipingEffect(Actor actor, EquipmentSlot slot) {
			if (Condition != null && actor != null && conditionToken != Actor.InvalidConditionToken)
				conditionToken = actor.RevokeCondition(conditionToken);

			base.UnequipingEffect(actor, slot);
		}
	}
}
