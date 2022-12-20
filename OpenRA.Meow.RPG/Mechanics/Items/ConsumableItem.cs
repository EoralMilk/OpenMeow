using System.Linq;
using OpenRA.GameRules;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Warheads;
using OpenRA.Traits;

namespace OpenRA.Meow.RPG.Mechanics
{
	public interface INotifyConsumeItem
	{
		bool CanConsume(Item item);
		void Consume(Item item);
	}

	public interface IConsumeAction
	{
		void OnConsumeBy(Item selfItem, Actor user);
	}

	public class ConsumableItemInfo : ConditionItemInfo
	{
		public readonly string UseAnim = null;

		public readonly int UseFrame = 0;

		public readonly string PutAwayAnim = null;

		public override object Create(ActorInitializer init)
		{
			return new ConsumableItem(this, init.Self);
		}
	}

	/// <summary>
	/// WIP
	/// </summary>
	public class ConsumableItem : ConditionItem
	{
		readonly ConsumableItemInfo info;
		IConsumeAction[] consumeActions;
		public string UseAnim => info.UseAnim;
		public int UseFrame => info.UseFrame;

		bool consuming = false;

		public bool IsBeingConsumed => consuming;

		public ConsumableItem(ConsumableItemInfo info, Actor self)
			: base(info, self)
		{
			this.info = info;
		}

		public override void Created(Actor self)
		{
			base.Created(self);
			consumeActions = self.TraitsImplementing<IConsumeAction>().ToArray();
		}

		public override void EquipingEffect(Actor slotActor, EquipmentSlot slot)
		{
			StartConsume(slotActor);

			base.EquipingEffect(slotActor, slot);
		}

		public virtual void StartConsume(Actor user)
		{
			consuming = true;
			ItemActor.CancelActivity();

			var notifies = user.TraitsImplementing<INotifyConsumeItem>();
			foreach (var notify in notifies)
			{
				notify.Consume(this);
			}

			if (!ItemActor.IsInWorld)
			{
				user.World.Add(ItemActor);
			}
		}

		public void ConsumeAction(Actor user)
		{
			consuming = false;
			ItemActor.TraitOrDefault<ItemSkeletonHandler>()?.ReleaseFrom(user, EquipmentSlot);

			if (ItemActor == null || ItemActor.IsDead || user.IsDead)
				return;

			foreach (var consume in consumeActions)
			{
				consume.OnConsumeBy(this, user);
			}
		}

		public void StopConsume(Actor user)
		{
			consuming = false;
			ItemActor.TraitOrDefault<ItemSkeletonHandler>()?.ReleaseFrom(user, EquipmentSlot);
		}

	}
}
