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

	public class ConsumableItemInfo : ConditionItemInfo, IRulesetLoaded
	{
		public readonly int CanUse = 1;

		[WeaponReference]
		[Desc("Has to be defined in weapons.yaml as well.")]
		public readonly string Weapon = null;

		public override object Create(ActorInitializer init)
		{
			return new ConsumableItem(this, init.Self);
		}

		public WeaponInfo WeaponInfo { get; private set; }

		public void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			if (string.IsNullOrEmpty(Weapon))
				return;

			var weaponToLower = Weapon.ToLowerInvariant();
			if (!rules.Weapons.TryGetValue(weaponToLower, out var weaponInfo))
				throw new YamlException($"Weapons Ruleset does not contain an entry '{weaponToLower}'");

			WeaponInfo = weaponInfo;
		}
	}

	/// <summary>
	/// WIP
	/// </summary>
	public class ConsumableItem : ConditionItem
	{
		int useTime = 0;
		readonly ConsumableItemInfo info;
		public ConsumableItem(ConsumableItemInfo info, Actor self)
			: base(info, self)
		{
			useTime = info.CanUse;
			this.info = info;
		}

		public override void EquipingEffect(Actor actor)
		{
			var notifies = actor.TraitsImplementing<INotifyConsumeItem>();
			foreach (var notify in notifies)
			{
				if (!notify.CanConsume(this))
				{
					base.EquipingEffect(actor);
					EquipmentSlot.TryUnequip(actor);
					return;
				}
			}

			if (useTime-- > 0)
			{
				Consume(actor);
			}

			base.EquipingEffect(actor);
			EquipmentSlot.TryUnequip(actor);

			if (useTime == 0)
			{
				var inventory = Inventory;

				inventory?.TryRemove(actor, this);
				actor.World.AddFrameEndTask(w =>
					{
						inventory?.ItemCache.RemvoeItem(ItemActor.ActorID);
						ItemActor.Kill(actor);
					}
				);
			}
		}

		public override void UnequipingEffect(Actor actor)
		{
			base.UnequipingEffect(actor);
		}

		public virtual void Consume(Actor actor)
		{
			if (info.WeaponInfo != null)
			{
				info.WeaponInfo.Impact(Target.FromActor(actor), ItemActor);
			}

			var notifies = actor.TraitsImplementing<INotifyConsumeItem>();
			foreach (var notify in notifies)
			{
				notify.Consume(this);
			}
		}
	}
}
