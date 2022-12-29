using System.Linq;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;
using OpenRA.GameRules;

namespace OpenRA.Meow.RPG.Mechanics
{
	public class SlotArmamentInfo : ArmamentInfo, Requires<EquipmentSlotInfo>
	{
		[FieldLoader.Require]
		[Desc("The EquipmentSlot to bind, which the weapon item should be equiped at")]
		public readonly string SlotBind = null;

		public readonly bool UseSlotDefaultWeapon = false;

		public override object Create(ActorInitializer init) { return new SlotArmament(init.Self, this); }

	}

	public class SlotArmament : Armament, INotifyEquip
	{
		readonly SlotArmamentInfo info;
		readonly WeaponInfo defaultWeapon;
		readonly EquipmentSlot slot;
		WeaponItem weaponItem;

		public SlotArmament(Actor self, SlotArmamentInfo info)
			: base(self, info, false)
		{
			defaultWeapon = Weapon;
			slot = self.TraitsImplementing<EquipmentSlot>().First(slot => slot.Name == info.SlotBind);
			this.info = info;
		}

		protected override void Created(Actor self)
		{
			base.Created(self);
			if (slot.Item != null && slot.Item is WeaponItem)
			{
				Weapon = (slot.Item as WeaponItem).WeaponInfo;
			}
		}

		public override bool IsValidForArmamentChoose(in Target target, World world)
		{
			if (Weapon == defaultWeapon && !info.UseSlotDefaultWeapon)
				return false;

			return base.IsValidForArmamentChoose(target, world);
		}

		protected override WPos CalculateMuzzleWPos(Actor self, Barrel b)
		{
			if (slot.Item != null && slot.Item == weaponItem && weaponItem.UsingMuzzle != null)
			{
				return weaponItem.UsingMuzzle.MuzzleSkeleton.GetWPosFromBoneId(weaponItem.UsingMuzzle.MuzzleBoneId);
			}

			// Weapon offset in turret coordinates
			var localOffset = b.Offset + AdditionalLocalOffset() + new WVec(-Recoil, WDist.Zero, WDist.Zero);

			if (slot.Item != null && slot.Item == weaponItem)
			{
				localOffset += weaponItem.GetMuzzleOffset();
			}

			// Turret coordinates to body coordinates
			var bodyOrientation = coords.QuantizeOrientation(self.Orientation);
			if (turret != null)
			{
				localOffset = localOffset.Rotate(turret.WorldOrientation) + turret.Offset.Rotate(bodyOrientation);
				return self.CenterPosition + coords.LocalToWorld(localOffset);
			}
			else
			{
				localOffset = localOffset.Rotate(bodyOrientation);

				return self.CenterPosition + coords.LocalToWorld(localOffset);
			}
		}

		protected override void OnFire()
		{
			if (weaponItem != null)
				weaponItem.OnAttack();
		}

		bool INotifyEquip.CanEquip(Actor self, Item item)
		{
			return true;
		}

		void INotifyEquip.Equipped(Actor self, Item item)
		{
			if (slot.Item == item && item is WeaponItem)
			{
				Weapon = (item as WeaponItem).WeaponInfo;
				weaponItem = item as WeaponItem;
			}
		}

		bool INotifyEquip.CanUnequip(Actor self, Item item)
		{
			return true;
		}

		void INotifyEquip.Unequipped(Actor self, Item item)
		{
			if (slot.Item == null)
			{
				Weapon = defaultWeapon;
				weaponItem = null;
			}
		}
	}
}
