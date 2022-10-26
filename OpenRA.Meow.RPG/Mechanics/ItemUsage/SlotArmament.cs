using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GlmSharp;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Mods.Common.Traits.Trait3D;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;
using TrueSync;
using OpenRA.GameRules;
using TagLib.Ape;

namespace OpenRA.Meow.RPG.Mechanics
{
	public class SlotArmamentInfo : ArmamentInfo, Requires<EquipmentSlotInfo>
	{
		[FieldLoader.Require]
		public readonly string SlotBind = null;

		public override object Create(ActorInitializer init) { return new SlotArmament(init.Self, this); }

	}

	public class SlotArmament : Armament, INotifyEquip
	{
		readonly WeaponInfo defaultWeapon;
		readonly EquipmentSlot slot;
		public SlotArmament(Actor self, SlotArmamentInfo info)
			: base(self, info, false)
		{
			defaultWeapon = Weapon;
			slot = self.TraitsImplementing<EquipmentSlot>().First(slot => slot.Name == info.SlotBind);
		}

		protected override void Created(Actor self)
		{
			base.Created(self);
		}

		public override bool IsValidForArmamentChoose(in Target target, World world)
		{
			if (Weapon == defaultWeapon)
				return false;

			return base.IsValidForArmamentChoose(target, world);
		}

		protected override WPos CalculateMuzzleWPos(Actor self, Barrel b)
		{
			// Weapon offset in turret coordinates
			var localOffset = b.Offset + new WVec(-Recoil, WDist.Zero, WDist.Zero);

			if (slot.Item != null && slot.Item is WeaponItem)
			{
				localOffset += (slot.Item as WeaponItem).GetMuzzleOffset();
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

		bool INotifyEquip.CanEquip(Actor self, Item item)
		{
			return true;
		}

		void INotifyEquip.Equipped(Actor self, Item item)
		{
			if (slot.Item == item && item is WeaponItem)
			{
				Weapon = (item as WeaponItem).WeaponInfo;
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
			}
		}
	}
}
