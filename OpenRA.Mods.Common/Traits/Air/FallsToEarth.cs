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

using OpenRA.GameRules;
using OpenRA.Mods.Common.Activities;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Causes aircraft husks that are spawned in the air to crash to the ground.")]
	public class FallsToEarthInfo : TraitInfo, IRulesetLoaded, Requires<AircraftInfo>
	{
		[WeaponReference]
		[Desc("Explosion weapon that triggers when hitting ground.")]
		public readonly string Explosion = "UnitExplode";

		[Desc("Limit the maximum spin (in angle units per tick) that can be achieved while crashing.",
			"0 disables spinning. Leave undefined for no limit.")]
		public readonly WAngle? MaximumSpinSpeed = null;

		[Desc("Init Spin Speed.")]
		public readonly int SpinSpeed = 0;

		public readonly int SpinChangeInterval = 5;

		[Desc("Spin Acceleration.")]
		public readonly int SpinAcceleration = 1;

		[Desc("Does the aircraft (husk) move forward?")]
		public readonly bool Moves = false;

		[Desc("Does the aircraft (husk) move forward at aircraft speed? No to calculate speed as actor init info")]
		public readonly bool UseAircraftSpeed = false;

		[Desc("Init Gravity at which aircraft falls to ground.")]
		public readonly WDist Gravity = new WDist(0);

		[Desc("Gravity (effect gravity per GravityChangeInterval tick) at which aircraft falls to ground.")]
		public readonly WDist GravityAcceleration = new WDist(1);

		public readonly int GravityChangeInterval = 1;

		[Desc("Max Gravity at which aircraft falls to ground.")]
		public readonly WDist MaxGravity = new WDist(18);

		[Desc("Init velocity at which aircraft falls to ground.")]
		public readonly WDist Velocity = WDist.Zero;

		[Desc("Velocity (per tick) at which aircraft falls to ground.")]
		public readonly WDist MaxVelocity = new WDist(512);

		public WeaponInfo ExplosionWeapon { get; private set; }

		public override object Create(ActorInitializer init) { return new FallsToEarth(init, this); }
		public void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			if (string.IsNullOrEmpty(Explosion))
				return;

			var weaponToLower = Explosion.ToLowerInvariant();
			if (!rules.Weapons.TryGetValue(weaponToLower, out var weapon))
				throw new YamlException($"Weapons Ruleset does not contain an entry '{weaponToLower}'");

			ExplosionWeapon = weapon;
		}
	}

	public class FallsToEarth : IEffectiveOwner, INotifyCreated
	{
		readonly FallsToEarthInfo info;
		readonly Player effectiveOwner;

		public FallsToEarth(ActorInitializer init, FallsToEarthInfo info)
		{
			this.info = info;
			effectiveOwner = init.GetValue<EffectiveOwnerInit, Player>(info, init.Self.Owner);
		}

		// We return init.Self.Owner if there's no effective owner
		bool IEffectiveOwner.Disguised => true;
		Player IEffectiveOwner.Owner => effectiveOwner;

		void INotifyCreated.Created(Actor self)
		{
			self.QueueActivity(false, new FallToEarth(self, info));
		}
	}
}
