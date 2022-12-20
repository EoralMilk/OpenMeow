using OpenRA.GameRules;
using OpenRA.Traits;

namespace OpenRA.Meow.RPG.Mechanics
{
	public class ConsumeFireWeaponInfo : TraitInfo, IRulesetLoaded, Requires<ConsumableItemInfo>
	{
		[WeaponReference]
		[Desc("Has to be defined in weapons.yaml as well.")]
		public readonly string Weapon = null;

		public override object Create(ActorInitializer init)
		{
			return new ConsumeFireWeapon(init.Self, this);
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

	public class ConsumeFireWeapon : IConsumeAction
	{
		public readonly Actor Self;
		public readonly ConsumeFireWeaponInfo info;
		public ConsumeFireWeapon(Actor self, ConsumeFireWeaponInfo info)
		{
			this.Self = self;
			this.info = info;
		}

		void IConsumeAction.OnConsumeBy(Item selfItem, Actor user)
		{
			if (info.WeaponInfo != null)
			{
				info.WeaponInfo.Impact(Target.FromActor(user), selfItem.ItemActor);
			}
		}
	}
}
