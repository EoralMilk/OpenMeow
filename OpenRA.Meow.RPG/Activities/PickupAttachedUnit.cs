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

using System.Collections.Generic;
using OpenRA.Activities;
using OpenRA.Meow.RPG.Traits;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Meow.RPG.Activities
{
	public class PickupAttachedUnit : Activity
	{
		readonly Actor cargo;
		readonly AttachCarryall carryall;
		readonly AttachCarryable carryable;
		readonly IFacing carryableFacing;
		readonly BodyOrientation carryableBody;

		readonly int delay;
		readonly Color? targetLineColor;

		// TODO: Expose this to yaml
		readonly WDist targetLockRange = WDist.FromCells(4);

		enum PickupState { Intercept, LockAttachCarryable, Pickup }
		PickupState state = PickupState.Intercept;

		public PickupAttachedUnit(Actor self, Actor cargo, int delay, Color? targetLineColor)
		{
			ActivityType = ActivityType.Move;
			this.cargo = cargo;
			this.delay = delay;
			this.targetLineColor = targetLineColor;
			carryable = cargo.Trait<AttachCarryable>();
			carryableFacing = cargo.Trait<IFacing>();
			carryableBody = cargo.Trait<BodyOrientation>();

			carryall = self.Trait<AttachCarryall>();

			ChildHasPriority = false;
		}

		protected override void OnFirstRun(Actor self)
		{
			// The cargo might have become invalid while we were moving towards it.
			if (cargo.IsDead || carryable.IsTraitDisabled || (!carryall.Info.AttachCarryableAnyCamp && !cargo.AppearsFriendlyTo(self)))
				return;

			if (carryall.ReserveAttachCarryable(self, cargo))
			{
				// Fly to the target and wait for it to be locked for pickup
				// These activities will be cancelled and replaced by Land once the target has been locked
				QueueChild(new Fly(self, Target.FromActor(cargo)));
				QueueChild(new FlyIdle(self, idleTurn: false));
			}
		}

		public override bool Tick(Actor self)
		{
			if (IsCanceling)
			{
				if (carryall.State == AttachCarryall.AttachCarryallState.Reserved)
					carryall.UnreserveAttachCarryable(self);

				// Make sure we run the TakeOff activity if we are / have landed
				if (self.Trait<Aircraft>().HasInfluence())
				{
					ChildHasPriority = true;
					IsInterruptible = false;
					QueueChild(new TakeOff(self));
					return false;
				}

				return true;
			}

			if (cargo != carryall.AttachCarryable || cargo.IsDead || carryable.IsTraitDisabled || (!carryall.Info.AttachCarryableAnyCamp && !cargo.AppearsFriendlyTo(self)))
			{
				carryall.UnreserveAttachCarryable(self);
				Cancel(self, true);
				return false;
			}

			// Wait until we are near the target before we try to lock it
			var distSq = (cargo.CenterPosition - self.CenterPosition).HorizontalLengthSquared;
			if (state == PickupState.Intercept && distSq <= targetLockRange.LengthSquared)
				state = PickupState.LockAttachCarryable;

			if (state == PickupState.LockAttachCarryable)
			{
				var lockResponse = carryable.LockForPickup(self);
				if (lockResponse == LockResponse.Failed)
				{
					Cancel(self);
					return false;
				}
				else if (lockResponse == LockResponse.Success)
				{
					// Pickup position and facing are now known - swap the fly/wait activity with Land
					ChildActivity?.Cancel(self);

					var localOffset = carryall.OffsetForAttachCarryable(self, cargo).Rotate(carryableBody.QuantizeOrientation(cargo.Orientation));
					QueueChild(new Land(self, Target.FromActor(cargo), -carryableBody.LocalToWorld(localOffset), carryableFacing.Facing));

					// Pause briefly before attachment for visual effect
					if (delay > 0)
						QueueChild(new Wait(delay, false));

					// Remove our carryable from world
					QueueChild(new AttachAttachedUnit(self, cargo));
					QueueChild(new TakeOff(self));

					state = PickupState.Pickup;
				}
			}

			// We don't want to allow TakeOff to be cancelled
			if (ChildActivity is TakeOff)
				ChildHasPriority = true;

			// Return once we are in the pickup state and the pickup activities have completed
			return TickChild(self) && state == PickupState.Pickup;
		}

		public override IEnumerable<TargetLineNode> TargetLineNodes(Actor self)
		{
			if (targetLineColor != null)
				yield return new TargetLineNode(Target.FromActor(cargo), targetLineColor.Value);
		}

		class AttachAttachedUnit : Activity
		{
			readonly Actor cargo;
			readonly AttachCarryable carryable;
			readonly AttachCarryall carryall;

			public AttachAttachedUnit(Actor self, Actor cargo)
			{
				ActivityType = ActivityType.Move;
				this.cargo = cargo;
				carryable = cargo.Trait<AttachCarryable>();
				carryall = self.Trait<AttachCarryall>();
			}

			protected override void OnFirstRun(Actor self)
			{
				// The cargo might have become invalid while we were moving towards it.
				if (cargo == null || cargo.IsDead || carryable.IsTraitDisabled || (!carryall.Info.AttachCarryableAnyCamp && !cargo.AppearsFriendlyTo(self)))
					return;

				self.World.AddFrameEndTask(w =>
				{
					carryable.Attached();
					carryall.AttachAttachCarryable(self, cargo);
				});
			}
		}
	}
}