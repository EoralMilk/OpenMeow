#region Copyright & License Information
/*
 * Copyright 2007-2022 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Linq;
using OpenRA.GameRules;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{

	[Desc("This actor frequently explode a weapon.")]
	public class ExplodeWeaponInfo : ConditionalTraitInfo, Requires<IHealthInfo>
	{
		[WeaponReference]
		[FieldLoader.Require]
		[Desc("Default weapon to use for explosion if ammo/payload is loaded.")]
		public readonly string Weapon = null;

		[Desc("Chance that this actor will explode at all.")]
		public readonly int Chance = 100;

		[Desc("Interval that this actor will explode at all.")]
		public readonly int Interval = 100;

		[Desc("Possible values are CenterPosition (explosion at the actors' center) and ",
			"Footprint (explosion on each occupied cell).")]
		public readonly ExplosionType Type = ExplosionType.CenterPosition;

		[Desc("Offset of the explosion from the center of the exploding actor (or cell).")]
		public readonly WVec Offset = WVec.Zero;

		public WeaponInfo WeaponInfo { get; private set; }

		public override object Create(ActorInitializer init) { return new ExplodeWeapon(this, init.Self); }
		public override void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			if (!string.IsNullOrEmpty(Weapon))
			{
				var weaponToLower = Weapon.ToLowerInvariant();
				if (!rules.Weapons.TryGetValue(weaponToLower, out var weapon))
					throw new YamlException($"Weapons Ruleset does not contain an entry '{weaponToLower}'");
				WeaponInfo = weapon;
			}

			base.RulesetLoaded(rules, ai);
		}
	}

	public class ExplodeWeapon : ConditionalTrait<ExplodeWeaponInfo>, ITick
	{
		readonly IHealth health;
		BuildingInfo buildingInfo;
		int tick;

		public ExplodeWeapon(ExplodeWeaponInfo info, Actor self)
			: base(info)
		{
			health = self.Trait<IHealth>();
		}

		protected override void Created(Actor self)
		{
			buildingInfo = self.Info.TraitInfoOrDefault<BuildingInfo>();

			base.Created(self);
		}

		WeaponInfo ChooseWeaponForExplosion(Actor self)
		{
			return Info.WeaponInfo;
		}

		public void Tick(Actor self)
		{
			if (IsTraitDisabled || !self.IsInWorld || tick++ < Info.Interval)
				return;

			tick = 0;

			if (self.World.SharedRandom.Next(100) > Info.Chance)
				return;

			var weapon = ChooseWeaponForExplosion(self);
			if (weapon == null)
				return;

			if (weapon.Report != null && weapon.Report.Length > 0)
				Game.Sound.Play(SoundType.World, weapon.Report, self.World, self.CenterPosition);

			if (Info.Type == ExplosionType.Footprint && buildingInfo != null)
			{
				var cells = buildingInfo.OccupiedTiles(self.Location);
				foreach (var c in cells)
					weapon.Impact(Target.FromPos(self.World.Map.CenterOfCell(c) + Info.Offset), self);

				return;
			}

			// Use .FromPos since this actor is killed. Cannot use Target.FromActor
			weapon.Impact(Target.FromPos(self.CenterPosition + Info.Offset), self);
		}
	}
}
