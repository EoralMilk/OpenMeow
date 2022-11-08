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

using System;
using OpenRA.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Activities
{
	public class FallToEarth : Activity
	{
		readonly Aircraft aircraft;
		readonly FallsToEarthInfo info;

		readonly int rot;
		int rotSpeed = 0;
		int speed = 0;
		int gravity = 0;
		int gravityTick = 0;
		int rotTick = 0;

		int spin;

		public FallToEarth(Actor self, FallsToEarthInfo info)
		{
			ActivityType = ActivityType.Move;
			this.info = info;
			IsInterruptible = false;
			aircraft = self.Trait<Aircraft>();
			if (!info.MaximumSpinSpeed.HasValue || info.MaximumSpinSpeed.Value != WAngle.Zero)
				rot = self.World.SharedRandom.Next(2) * 2 - 1;
			rotSpeed = info.SpinSpeed;
			speed = info.Velocity.Length;
			gravity = info.Gravity.Length;
			gravityTick = 0;
		}

		public override bool Tick(Actor self)
		{
			var h = self.World.Map.HeightOfTerrain(self.CenterPosition);
			if (self.CenterPosition.Z <= h)
			{
				aircraft.SetPosition(self, new WPos(self.CenterPosition.X, self.CenterPosition.Y, h));

				if (info.ExplosionWeapon != null)
				{
					// Use .FromPos since this actor is killed. Cannot use Target.FromActor
					info.ExplosionWeapon.Impact(Target.FromPos(self.CenterPosition), self);
				}

				self.Kill(self);
				Cancel(self);
				return true;
			}

			if (rot != 0)
			{
				if (rotTick++ >= info.SpinChangeInterval)
				{
					rotSpeed = rotSpeed + info.SpinAcceleration;
					rotTick = 0;
				}

				if (!info.MaximumSpinSpeed.HasValue || Math.Abs(spin) < info.MaximumSpinSpeed.Value.Angle)
					spin += rot * rotSpeed;

				// Allow for negative spin values and convert from facing to angle units
				aircraft.Facing = new WAngle(aircraft.Facing.Angle + spin);
			}

			var move = info.Moves ? (info.UseAircraftSpeed ? new WVec(0, -aircraft.Info.Speed, 0).Rotate(WRot.FromYaw(aircraft.Facing)) : aircraft.InitSpeed) : WVec.Zero;
			if (gravityTick++ >= info.GravityChangeInterval)
			{
				gravity = gravity >= info.MaxGravity.Length ? info.MaxGravity.Length : gravity + info.GravityAcceleration.Length;
				gravityTick = 0;
			}

			speed = speed >= info.MaxVelocity.Length ? info.MaxVelocity.Length : speed + gravity;
			move -= new WVec(WDist.Zero, WDist.Zero, new WDist(speed));
			aircraft.SetPosition(self, aircraft.CenterPosition + move);

			return false;
		}
	}
}
