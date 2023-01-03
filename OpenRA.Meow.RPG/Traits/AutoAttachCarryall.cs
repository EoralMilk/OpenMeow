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
using System.Linq;
using OpenRA.Activities;
using OpenRA.Meow.RPG.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Support;
using OpenRA.Traits;

namespace OpenRA.Meow.RPG.Traits
{
	[Desc("Automatically transports harvesters with the AutoAttachCarryable and AttachCarryableHarvester between resource fields and refineries.")]
	public class AutoAttachCarryallInfo : AttachCarryallInfo
	{
		[ConsumedConditionReference]
		[Desc("Boolean expression defining the condition under which the auto carry behavior is enabled. Enabled at default.")]
		public readonly BooleanExpression AutoCarryCondition = null;

		public override object Create(ActorInitializer init) { return new AutoAttachCarryall(init.Self, this); }
	}

	public class AutoAttachCarryall : AttachCarryall, INotifyBecomingIdle, IObservesVariables, IResolveOrder
	{
		readonly AutoAttachCarryallInfo info;
		bool busy;
		bool underAutoCommand;

		public bool EnableAutoCarry { get; private set; }

		public AutoAttachCarryall(Actor self, AutoAttachCarryallInfo info)
			: base(self, info)
		{
			this.info = info;
			EnableAutoCarry = true;
		}

		void INotifyBecomingIdle.OnBecomingIdle(Actor self)
		{
			if (!EnableAutoCarry)
				return;

			busy = false;
			FindAttachCarryableForTransport(self);
		}

		IEnumerable<VariableObserver> IObservesVariables.GetVariableObservers()
		{
			if (info.AutoCarryCondition != null)
				yield return new VariableObserver(AutoCarryConditionsChanged, info.AutoCarryCondition.Variables);
		}

		void AutoCarryConditionsChanged(Actor self, IReadOnlyDictionary<string, int> conditions)
		{
			EnableAutoCarry = info.AutoCarryCondition.Evaluate(conditions);
		}

		// A carryable notifying us that he'd like to be carried
		public bool RequestTransportNotify(Actor self, Actor carryable)
		{
			if (busy || !EnableAutoCarry)
				return false;

			underAutoCommand = true;
			if (ReserveAttachCarryable(self, carryable))
			{
				self.QueueActivity(false, new FerryUnit(self, carryable));
				return true;
			}

			return false;
		}

		public override bool ReserveAttachCarryable(Actor self, Actor carryable)
		{
			if (State == AttachCarryallState.Reserved)
				UnreserveAttachCarryable(self);

			if (State != AttachCarryallState.Idle)
				return false;

			var act = carryable.TraitOrDefault<AutoAttachCarryable>();

			if (act != null)
			{
				if (!act.AutoReserve(self, underAutoCommand))
					return false;
			}
			else if (!carryable.Trait<AttachCarryable>().Reserve(self))
				return false;

			AttachCarryable = carryable;
			State = AttachCarryallState.Reserved;
			return true;
		}

		static bool IsBestAutoAttachCarryallForCargo(Actor self, Actor candidateCargo)
		{
			// Find carriers
			var carriers = self.World.ActorsHavingTrait<AutoAttachCarryall>(c => !c.busy && c.EnableAutoCarry)
				.Where(a => a.Owner == self.Owner && a.IsInWorld);

			return carriers.ClosestTo(candidateCargo) == self;
		}

		void FindAttachCarryableForTransport(Actor self)
		{
			if (!self.IsInWorld)
				return;

			// Get all carryables who want transport
			var carryables = self.World.ActorsWithTrait<AutoAttachCarryable>().Where(p =>
			{
				var actor = p.Actor;
				if (actor == null)
					return false;

				if (actor.Owner != self.Owner)
					return false;

				if (actor.IsDead)
					return false;

				var trait = p.Trait;
				if (trait.Reserved)
					return false;

				if (!trait.WantsTransport)
					return false;

				if (actor.IsIdle)
					return false;

				return true;
			}).OrderBy(p => (self.Location - p.Actor.Location).LengthSquared);

			foreach (var p in carryables)
			{
				// Check if its actually me who's the best candidate
				underAutoCommand = true;
				if (IsBestAutoAttachCarryallForCargo(self, p.Actor) && ReserveAttachCarryable(self, p.Actor))
				{
					busy = true;
					self.QueueActivity(false, new FerryUnit(self, p.Actor));
					break;
				}
			}
		}

		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString == "DeliverAttachedUnit")
			{
				var cell = self.World.Map.Clamp(self.World.Map.CellContaining(order.Target.CenterPosition));
				if (!AircraftInfo.MoveIntoShroud && !self.Owner.Shroud.IsExplored(cell))
					return;

				if (underAutoCommand)
				{
					underAutoCommand = false;
					self.CancelActivity();
				}

				busy = true;
				self.QueueActivity(order.Queued, new DeliverAttachedUnit(self, order.Target, Info.DropRange, Info.TargetLineColor));
				self.ShowTargetLines();
			}
			else if (order.OrderString == "Unload")
			{
				if (!order.Queued && !CanUnload())
					return;

				if (underAutoCommand)
				{
					underAutoCommand = false;
					self.CancelActivity();
				}

				busy = true;
				self.QueueActivity(order.Queued, new DeliverAttachedUnit(self, Info.DropRange, Info.TargetLineColor));
			}
			else if (order.OrderString == "PickupAttachedUnit")
			{
				if (order.Target.Type != TargetType.Actor)
					return;

				if (underAutoCommand)
				{
					underAutoCommand = false;
					self.CancelActivity();
				}
				busy = true;
				self.QueueActivity(order.Queued, new PickupAttachedUnit(self, order.Target.Actor, Info.BeforeLoadDelay, Info.TargetLineColor));
				self.ShowTargetLines();
			}
		}

		class FerryUnit : Activity
		{
			readonly Actor cargo;
			readonly AutoAttachCarryable carryable;
			readonly AutoAttachCarryall carryall;

			public FerryUnit(Actor self, Actor cargo)
			{
				ActivityType = ActivityType.Move;
				this.cargo = cargo;
				carryable = cargo.Trait<AutoAttachCarryable>();
				carryall = self.Trait<AutoAttachCarryall>();
			}

			protected override void OnFirstRun(Actor self)
			{
				if (!cargo.IsDead)
					QueueChild(new PickupAttachedUnit(self, cargo, 0, carryall.Info.TargetLineColor));
			}

			public override bool Tick(Actor self)
			{
				if (cargo.IsDead)
				{
					carryall.UnreserveAttachCarryable(self);
					return true;
				}

				var dropRange = carryall.Info.DropRange;
				var destination = carryable.Destination;
				if (destination != null)
					self.QueueActivity(true, new DeliverAttachedUnit(self, Target.FromCell(self.World, destination.Value), dropRange, carryall.Info.TargetLineColor));

				return true;
			}
		}
	}
}
