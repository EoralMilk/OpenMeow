using OpenRA.Graphics;
using OpenRA.Traits;

namespace OpenRA.Meow.RPG.Mechanics
{
	public class ConditionItemInfo : ItemInfo
	{
		[FieldLoader.Require]
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

		public override void EquipingEffect()
		{
			if (conditionToken == Actor.InvalidConditionToken)
				conditionToken = EquipmentSlot.SlotOwnerActor.GrantCondition(Condition);
		}

		public override void UnequipingEffect() {
			if (conditionToken != Actor.InvalidConditionToken)
				conditionToken = EquipmentSlot.SlotOwnerActor.RevokeCondition(conditionToken);
		}
	}
}
