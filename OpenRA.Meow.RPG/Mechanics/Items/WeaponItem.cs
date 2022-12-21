using OpenRA.GameRules;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;
using System;
using OpenRA.Mods.Common.Traits.Trait3D;

namespace OpenRA.Meow.RPG.Mechanics
{
	public interface INotifyWeaponItemAttack
	{
		void OnWeaponItemAttack();
	}

	public class WeaponItemInfo : ConditionItemInfo, IRulesetLoaded
	{
		[Desc("Muzzle position relative to turret or body, (forward, right, up) triples.",
			"If weapon Burst = 1, it cycles through all listed offsets, otherwise the offset corresponding to current burst is used.")]
		public readonly WVec[] LocalOffset = new WVec[1] {WVec.Zero};

		[WeaponReference]
		[FieldLoader.Require]
		[Desc("Has to be defined in weapons.yaml as well.")]
		public readonly string Weapon = null;

		public override object Create(ActorInitializer init)
		{
			return new WeaponItem(this, init.Self);
		}

		public WeaponInfo WeaponInfo { get; private set; }

		public void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			var weaponToLower = Weapon.ToLowerInvariant();
			if (!rules.Weapons.TryGetValue(weaponToLower, out var weaponInfo))
				throw new YamlException($"Weapons Ruleset does not contain an entry '{weaponToLower}'");

			WeaponInfo = weaponInfo;
		}

	}

	public class WeaponItem : ConditionItem
	{
		readonly WeaponItemInfo info;
		public WeaponItemMuzzle UsingMuzzle;

		int fire = 0;

		INotifyWeaponItemAttack[] notifyWeaponItemAttacks;

		public WeaponInfo WeaponInfo => info.WeaponInfo;
		public WeaponItem(WeaponItemInfo info, Actor self)
			: base(info, self)
		{
			this.info = info;
		}

		public virtual WVec GetMuzzleOffset()
		{
			// Weapon offset in turret coordinates
			var localOffset = info.LocalOffset[fire];

			fire = (fire + 1) % info.LocalOffset.Length;
			return localOffset;
		}

		public override void Created(Actor self)
		{
			base.Created(self);
			notifyWeaponItemAttacks = self.TraitsImplementing<INotifyWeaponItemAttack>().ToArray();
		}

		public virtual void OnAttack()
		{
			foreach (var notify in notifyWeaponItemAttacks)
			{
				notify.OnWeaponItemAttack();
			}
		}
	}
}
