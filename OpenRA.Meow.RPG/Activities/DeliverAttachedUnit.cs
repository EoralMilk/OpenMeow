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
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Meow.RPG.Activities
{
	public class DeliverAttachedUnit : Activity
	{
		readonly AttachCarryall carryall;
		readonly bool assignTargetOnFirstRun;
		readonly WDist deliverRange;
		readonly Color? targetLineColor;

		Target destination;

		public DeliverAttachedUnit(Actor self, WDist deliverRange, Color? targetLineColor)
			: this(self, Target.Invalid, deliverRange, targetLineColor)
		{
			ActivityType = ActivityType.Move;
			assignTargetOnFirstRun = true;
		}

		public DeliverAttachedUnit(Actor self, in Target destination, WDist deliverRange, Color? targetLineColor)
		{
			ActivityType = ActivityType.Move;
			this.destination = destination;
			this.deliverRange = deliverRange;
			this.targetLineColor = targetLineColor;

			carryall = self.Trait<AttachCarryall>();
		}

		protected override void OnFirstRun(Actor self)
		{
			// In case this activity was queued (either via queued order of via AutoAttachCarryall)
			// something might have happened to the cargo in the time between the activity being
			// queued and being run, so short out if it is no longer valid.
			if (carryall.AttachCarryable == null)
				return;

			if (assignTargetOnFirstRun)
				destination = Target.FromCell(self.World, self.Location);

			QueueChild(new Land(self, destination, deliverRange));
			QueueChild(new Wait(carryall.Info.BeforeUnloadDelay, false));
			QueueChild(new ReleaseUnit(self));
			QueueChild(new TakeOff(self));
		}

		public override IEnumerable<TargetLineNode> TargetLineNodes(Actor self)
		{
			if (targetLineColor != null)
				yield return new TargetLineNode(destination, targetLineColor.Value);
		}

		class ReleaseUnit : Activity
		{
			readonly AttachCarryall carryall;
			readonly BodyOrientation body;
			readonly IFacing facing;

			public ReleaseUnit(Actor self)
			{
				ActivityType = ActivityType.Move;
				facing = self.Trait<IFacing>();
				carryall = self.Trait<AttachCarryall>();
				body = self.Trait<BodyOrientation>();
			}

			protected override void OnFirstRun(Actor self)
			{
				// HACK: Activities still tick between the actor being killed and being disposed
				// Thus the carryable might have changed since queuing because the death handler set it to null
				if (carryall.AttachCarryable == null)
					return;

				var localOffset = carryall.AttachCarryableOffset.Rotate(body.QuantizeOrientation(self.Orientation));
				var targetPosition = self.CenterPosition + body.LocalToWorld(localOffset);
				var targetLocation = self.World.Map.CellContaining(targetPosition);
				carryall.AttachCarryable.Trait<IPositionable>().SetPosition(carryall.AttachCarryable, targetLocation, SubCell.FullCell);
				carryall.AttachCarryable.Trait<IFacing>().Facing = facing.Facing;

				// Put back into world
				self.World.AddFrameEndTask(w =>
				{
					if (self.IsDead)
						return;

					var cargo = carryall.AttachCarryable;
					if (cargo == null)
						return;

					var carryable = cargo.Trait<AttachCarryable>();
					carryall.DetachAttachCarryable(self);
					carryable.UnReserve();
					carryable.Detached();
				});
			}
		}
	}
}
