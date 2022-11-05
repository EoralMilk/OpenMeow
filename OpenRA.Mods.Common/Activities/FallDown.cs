#region Copyright & License Information
/*
 * Copyright 2015- OpenRA.Mods.AS Developers (see AUTHORS)
 * This file is a part of a third-party plugin for OpenRA, which is
 * free software. It is made available to you under the terms of the
 * GNU General Public License as published by the Free Software
 * Foundation. For more information, see COPYING.
 */
#endregion

using OpenRA.Activities;
using OpenRA.GameRules;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Activities
{
	public class FallDown : Activity
	{
		readonly bool enableAdvanceFallDown;
		readonly IPositionable pos;

		WVec fallVector;
		readonly int maxVelocity;

		readonly int gravityChangeInterval;
		readonly int maxGravity;
		readonly int gravityAcceleration;

		readonly WeaponInfo weapon;
		readonly BitSet<DamageType> fallDamageTypes;

		readonly WPos dropPosition;
		WPos currentPosition;
		bool triggered = false;
		int gravity = 0;
		int gravityTick = 0;
		int speed = 0;

		public FallDown(Actor self, WPos dropPosition, int fallRate)
		{
			enableAdvanceFallDown = false;
			pos = self.TraitOrDefault<IPositionable>();
			IsInterruptible = false;
			fallVector = new WVec(0, 0, fallRate);
			speed = fallRate;
			this.dropPosition = dropPosition;
			weapon = null;
		}

		public FallDown(Actor self, WPos dropPosition, int fallRate, int maxVelocity, WeaponInfo weapon, int gravityChangeInterval,	int maxGravity,	int gravityAcceleration, BitSet<DamageType> fallDamageTypes)
		{
			enableAdvanceFallDown = true;
			pos = self.TraitOrDefault<IPositionable>();
			IsInterruptible = false;
			this.dropPosition = dropPosition;
			speed = fallRate;
			fallVector = new WVec(0, 0, speed);
			this.weapon = weapon;
			this.maxVelocity = maxVelocity;
			this.gravityChangeInterval = gravityChangeInterval;
			this.maxGravity = maxGravity;
			this.gravityAcceleration = gravityAcceleration;
			this.fallDamageTypes = fallDamageTypes;
		}

		bool FirstTick(Actor self)
		{
			triggered = true;

			// Place the actor and retrieve its visual position (CenterPosition)
			if (dropPosition != WPos.Zero)
				pos.SetPosition(self, dropPosition);
			currentPosition = self.CenterPosition;

			return false;
		}

		bool LastTick(Actor self)
		{
			var dat = self.World.Map.DistanceAboveTerrain(currentPosition);
			pos.SetPosition(self, currentPosition - new WVec(WDist.Zero, WDist.Zero, dat));

			if (enableAdvanceFallDown)
			{
				if (weapon != null)
					weapon.Impact(Target.FromPos(self.CenterPosition), self);

				var health = self.TraitOrDefault<Health>();
				if (health != null && maxVelocity > 0)
				{
					var damage = health.MaxHP * speed / maxVelocity;
					health.InflictDamage(self, self, new Damage(damage, fallDamageTypes), true);

					var pilotPositionable = self.Info.TraitInfo<IPositionableInfo>();
					if (!pilotPositionable.CanEnterCell(self.World, null, self.Location))
						self.Kill(self, fallDamageTypes);
				}
			}

			return true;
		}

		public override bool Tick(Actor self)
		{
			// If this is the first tick
			if (!triggered)
				return FirstTick(self);

			currentPosition -= fallVector;

			// If the unit has landed, this will be the last tick
			if (self.World.Map.DistanceAboveTerrain(currentPosition).Length <= 0)
				return LastTick(self);

			if (enableAdvanceFallDown)
			{
				if (gravityTick++ >= gravityChangeInterval)
				{
					gravity = gravity >= maxGravity ? maxGravity : gravity + gravityAcceleration;
					gravityTick = 0;
				}

				speed = speed >= maxVelocity ? maxVelocity : speed + gravity;
				fallVector = new WVec(0, 0, speed);
			}

			pos.SetCenterPosition(self, currentPosition);

			return false;
		}
	}
}
