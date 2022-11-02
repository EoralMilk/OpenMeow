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
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Activities
{
	public class FallDown : Activity
	{
		readonly IPositionable pos;
		WVec fallVector;
		readonly CarryableInfo info;

		readonly WPos dropPosition;
		WPos currentPosition;
		bool triggered = false;
		int gravity = 0;
		int gravityTick = 0;
		int speed = 0;

		public FallDown(Actor self, WPos dropPosition, int fallRate)
		{
			pos = self.TraitOrDefault<IPositionable>();
			IsInterruptible = false;
			fallVector = new WVec(0, 0, fallRate);
			this.dropPosition = dropPosition;
			info = null;
			speed = fallRate;
		}

		public FallDown(Actor self, WPos dropPosition, CarryableInfo info)
		{
			pos = self.TraitOrDefault<IPositionable>();
			IsInterruptible = false;
			this.dropPosition = dropPosition;
			this.info = info;
			if (info == null)
				throw new System.Exception("FallDown: FallsToEarthInfo can't be null");

			speed = info.Velocity.Length;
			fallVector = new WVec(0, 0, speed);
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

			if (info != null)
			{
				if (info.ExplosionWeapon != null)
				{
					info.ExplosionWeapon.Impact(Target.FromPos(self.CenterPosition), self);
				}

				var health = self.TraitOrDefault<Health>();
				var damage = health.MaxHP * speed / info.MaxVelocity.Length;
				health.InflictDamage(self, self, new Damage(damage, info.FallDamageTypes), true);

				var pilotPositionable = self.Info.TraitInfo<IPositionableInfo>();
				if (!pilotPositionable.CanEnterCell(self.World, null, self.Location))
					self.Kill(self, info.FallDamageTypes);
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

			if (info != null)
			{
				if (gravityTick++ >= info.GravityChangeInterval)
				{
					gravity = gravity >= info.MaxGravity.Length ? info.MaxGravity.Length : gravity + info.GravityAcceleration.Length;
					gravityTick = 0;
				}

				speed = speed >= info.MaxVelocity.Length ? info.MaxVelocity.Length : speed + gravity;
				fallVector = new WVec(0, 0, speed);
			}

			pos.SetCenterPosition(self, currentPosition);

			return false;
		}
	}
}
