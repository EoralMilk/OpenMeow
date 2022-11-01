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
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Can be carried by actors with the `" + nameof(Carryall) + "` trait.")]
	public class CarryableInfo : ConditionalTraitInfo
	{
		[GrantedConditionReference]
		[Desc("The condition to grant to self while a carryall has been reserved.")]
		public readonly string ReservedCondition = null;

		[GrantedConditionReference]
		[Desc("The condition to grant to self while being carried.")]
		public readonly string CarriedCondition = null;

		[GrantedConditionReference]
		[Desc("The condition to grant to self while being locked for carry.")]
		public readonly string LockedCondition = null;

		[Desc("Carryall attachment point relative to body.")]
		public readonly WVec LocalOffset = WVec.Zero;

		public override object Create(ActorInitializer init) { return new Carryable(this); }
	}

	public enum LockResponse { Success, Pending, Failed }

	public interface IDelayCarryallPickup
	{
		bool TryLockForPickup(Actor self, Actor carrier);
	}

	public class Carryable : ConditionalTrait<CarryableInfo>
	{
		int reservedToken = Actor.InvalidConditionToken;
		int carriedToken = Actor.InvalidConditionToken;
		int lockedToken = Actor.InvalidConditionToken;

		IDelayCarryallPickup[] delayPickups;

		public Actor Carrier { get; private set; }

		public bool Reserved => state != State.Free;

		public Actor Self { get; private set; }
		public Mobile Mobile { get; private set; }
		protected enum State { Free, Reserved, Locked }
		protected State state = State.Free;
		protected bool attached;

		public Carryable(CarryableInfo info)
			: base(info) { }

		protected override void Created(Actor self)
		{
			Mobile = self.TraitOrDefault<Mobile>();
			delayPickups = self.TraitsImplementing<IDelayCarryallPickup>().ToArray();
			Self = self;

			base.Created(self);
		}

		public virtual void Attached()
		{
			if (attached)
				return;

			attached = true;
			Mobile.RemoveInfluence();
			Mobile.OccupySpace = false;
			Mobile.TerrainOrientationIgnore = true;

			if (carriedToken == Actor.InvalidConditionToken)
				carriedToken = Self.GrantCondition(Info.CarriedCondition);
		}

		// This gets called by carrier after we touched down
		public virtual void Detached()
		{
			if (!attached)
				return;

			attached = false;

			if (carriedToken != Actor.InvalidConditionToken)
				carriedToken = Self.RevokeCondition(carriedToken);
		}

		public virtual bool Reserve(Actor carrier)
		{
			if (Reserved || IsTraitDisabled)
				return false;

			state = State.Reserved;
			Carrier = carrier;

			if (reservedToken == Actor.InvalidConditionToken)
				reservedToken = Self.GrantCondition(Info.ReservedCondition);

			return true;
		}

		public virtual void UnReserve()
		{
			state = State.Free;
			Carrier = null;

			Mobile.OccupySpace = true;
			Mobile.AddInfluence();
			Mobile.TerrainOrientationIgnore = false;

			if (reservedToken != Actor.InvalidConditionToken)
				reservedToken = Self.RevokeCondition(reservedToken);

			if (lockedToken != Actor.InvalidConditionToken)
				lockedToken = Self.RevokeCondition(lockedToken);
		}

		// Prepare for transport pickup
		public virtual LockResponse LockForPickup(Actor carrier)
		{
			if (state == State.Locked && Carrier != carrier)
				return LockResponse.Failed;

			if (delayPickups.Any(d => d.IsTraitEnabled() && !d.TryLockForPickup(Self, carrier)))
				return LockResponse.Pending;

			if (Mobile != null && !Mobile.CanStayInCell(Self.Location))
				return LockResponse.Pending;

			if (state != State.Locked)
			{
				state = State.Locked;
				Carrier = carrier;

				if (lockedToken == Actor.InvalidConditionToken)
					lockedToken = Self.GrantCondition(Info.LockedCondition);
			}

			// Make sure we are not moving and at our normal position with respect to the cell grid
			if (Mobile != null && Mobile.IsMovingBetweenCells)
				return LockResponse.Pending;

			return LockResponse.Success;
		}
	}
}
