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

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Can be carried by units with the trait `Carryall`.")]
	public class AutoCarryableInfo : CarryableInfo
	{
		[Desc("Required distance away from destination before requesting a pickup. Default is 6 cells.")]
		public readonly WDist MinDistance = WDist.FromCells(6);

		public override object Create(ActorInitializer init) { return new AutoCarryable(this); }
	}

	public class AutoCarryable : Carryable, ICallForTransport
	{
		readonly AutoCarryableInfo info;
		bool autoCommandReserved = false;

		public CPos? Destination { get; private set; }
		public bool WantsTransport => Destination != null && !IsTraitDisabled;

		public AutoCarryable(AutoCarryableInfo info)
			: base(info)
		{
			this.info = info;
		}

		public WDist MinimumDistance => info.MinDistance;

		// No longer want to be carried
		void ICallForTransport.MovementCancelled(Actor self) { MovementCancelled(); }
		void ICallForTransport.RequestTransport(Actor self, CPos destination) { RequestTransport(destination); }

		void MovementCancelled()
		{
			if (state == State.Locked)
				return;

			Destination = null;
			autoCommandReserved = false;

			// TODO: We could implement something like a carrier.Trait<Carryall>().CancelTransportNotify(self) and call it here
		}

		void RequestTransport(CPos destination)
		{
			if (!IsValidAutoCarryDistance(destination))
			{
				Destination = null;
				return;
			}

			Destination = destination;

			if (state != State.Free)
				return;

			// Inform all idle carriers
			var carriers = Self.World.ActorsWithTrait<AutoCarryall>()
				.Where(c => c.Trait.EnableAutoCarry && c.Trait.State == Carryall.CarryallState.Idle && !c.Actor.IsDead && c.Actor.Owner == Self.Owner && c.Actor.IsInWorld)
				.OrderBy(p => (Self.Location - p.Actor.Location).LengthSquared);

			// Enumerate idle carriers to find the first that is able to transport us
			foreach (var carrier in carriers)
				if (carrier.Trait.RequestTransportNotify(carrier.Actor, Self))
					return;
		}

		// This gets called by carrier after we touched down
		public override void Detached()
		{
			if (!attached)
				return;

			Destination = null;

			base.Detached();
		}

		public bool AutoReserve(Actor carrier, bool fromAutoCommand)
		{
			// When "fromAutoCommand" is true, it means the carrying request
			// is given by auto command, we need to check the validity of "Destination"
			// for an effective trip.
			if (fromAutoCommand)
			{
				if (Reserved || !WantsTransport)
					return false;

				if (!IsValidAutoCarryDistance(Destination.Value))
				{
					// Cancel pickup
					MovementCancelled();
					return false;
				}
			}

			if (Reserve(carrier))
			{
				// When successfully reserved by auto command,
				// set the "autoCommandReserved" to true
				if (fromAutoCommand)
					autoCommandReserved = true;
				return true;
			}

			return false;
		}

		// Prepare for transport pickup
		public override LockResponse LockForPickup(Actor carrier)
		{
			if (state == State.Locked && Carrier != carrier)
				return LockResponse.Failed;

			// When "autoCommandReserved" is true, the carrying operation is given by auto command
			// we still need to check the validity of "Destination" to ensure an effective trip.
			if (autoCommandReserved)
			{
				if (!WantsTransport)
				{
					// Cancel pickup
					MovementCancelled();
					return LockResponse.Failed;
				}

				if (!IsValidAutoCarryDistance(Destination.Value))
				{
					// Cancel pickup
					MovementCancelled();
					return LockResponse.Failed;
				}

				// Reset "AutoCommandReserved" as we finished the check
				autoCommandReserved = false;
			}

			return base.LockForPickup(carrier);
		}

		bool IsValidAutoCarryDistance(CPos destination)
		{
			if (Mobile == null)
				return false;

			// TODO: change the check here to pathfinding distance in the future
			return ((Self.World.Map.CenterOfCell(destination) - Self.CenterPosition).HorizontalLengthSquared >= info.MinDistance.LengthSquared
				|| !Mobile.PathFinder.PathExistsForLocomotor(Mobile.Locomotor, Self.Location, destination));
		}
	}
}
