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
using OpenRA.Mods.Common.Activities;
using OpenRA.Support;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Automatically transports harvesters with the AutoCarryable and CarryableHarvester between resource fields and refineries.")]
	public class AutoCarryallInfo : CarryallInfo
	{
		[ConsumedConditionReference]
		[Desc("Boolean expression defining the condition under which the auto carry behavior is enabled. Enabled at default.")]
		public readonly BooleanExpression AutoCarryCondition = null;

		public override object Create(ActorInitializer init) { return new AutoCarryall(init.Self, this); }
	}

	public class AutoCarryall : Carryall, INotifyBecomingIdle, IObservesVariables, IResolveOrder
	{
		readonly AutoCarryallInfo info;
		bool busy;
		bool underAutoCommand;

		public bool EnableAutoCarry { get; private set; }

		public AutoCarryall(Actor self, AutoCarryallInfo info)
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
			FindCarryableForTransport(self);
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
			if (ReserveCarryable(self, carryable))
			{
				self.QueueActivity(false, new FerryUnit(self, carryable));
				return true;
			}

			return false;
		}

		public override bool ReserveCarryable(Actor self, Actor carryable)
		{
			if (State == CarryallState.Reserved)
				UnreserveCarryable(self);

			var act = carryable.TraitOrDefault<AutoCarryable>();

			if (act != null)
			{
				if (State != CarryallState.Idle || !act.AutoReserve(self, underAutoCommand))
					return false;
			}
			else if (State != CarryallState.Idle || !carryable.Trait<Carryable>().Reserve(self))
				return false;

			Carryable = carryable;
			State = CarryallState.Reserved;
			return true;
		}

		static bool IsBestAutoCarryallForCargo(Actor self, Actor candidateCargo)
		{
			// Find carriers
			var carriers = self.World.ActorsHavingTrait<AutoCarryall>(c => !c.busy && c.EnableAutoCarry)
				.Where(a => a.Owner == self.Owner && a.IsInWorld);

			return carriers.ClosestTo(candidateCargo) == self;
		}

		void FindCarryableForTransport(Actor self)
		{
			if (!self.IsInWorld)
				return;

			// Get all carryables who want transport
			var carryables = self.World.ActorsWithTrait<AutoCarryable>().Where(p =>
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
				if (IsBestAutoCarryallForCargo(self, p.Actor) && ReserveCarryable(self, p.Actor))
				{
					busy = true;
					self.QueueActivity(false, new FerryUnit(self, p.Actor));
					break;
				}
			}
		}

		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString == "DeliverUnit")
			{
				var cell = self.World.Map.Clamp(self.World.Map.CellContaining(order.Target.CenterPosition));
				if (!AircraftInfo.MoveIntoShroud && !self.Owner.Shroud.IsExplored(cell))
					return;

				underAutoCommand = false;
				busy = true;
				self.QueueActivity(order.Queued, new DeliverUnit(self, order.Target, Info.DropRange, Info.TargetLineColor));
				self.ShowTargetLines();
			}
			else if (order.OrderString == "Unload")
			{
				if (!order.Queued && !CanUnload())
					return;

				underAutoCommand = false;
				busy = true;
				self.QueueActivity(order.Queued, new DeliverUnit(self, Info.DropRange, Info.TargetLineColor));
			}
			else if (order.OrderString == "PickupUnit")
			{
				if (order.Target.Type != TargetType.Actor)
					return;

				underAutoCommand = false;
				busy = true;
				self.QueueActivity(order.Queued, new PickupUnit(self, order.Target.Actor, Info.BeforeLoadDelay, Info.TargetLineColor));
				self.ShowTargetLines();
			}
		}

		class FerryUnit : Activity
		{
			readonly Actor cargo;
			readonly AutoCarryable carryable;
			readonly AutoCarryall carryall;

			public FerryUnit(Actor self, Actor cargo)
			{
				ActivityType = ActivityType.Move;
				this.cargo = cargo;
				carryable = cargo.Trait<AutoCarryable>();
				carryall = self.Trait<AutoCarryall>();
			}

			protected override void OnFirstRun(Actor self)
			{
				if (!cargo.IsDead)
					QueueChild(new PickupUnit(self, cargo, 0, carryall.Info.TargetLineColor));
			}

			public override bool Tick(Actor self)
			{
				if (cargo.IsDead)
					return true;

				var dropRange = carryall.Info.DropRange;
				var destination = carryable.Destination;
				if (destination != null)
					self.QueueActivity(true, new DeliverUnit(self, Target.FromCell(self.World, destination.Value), dropRange, carryall.Info.TargetLineColor));

				return true;
			}
		}
	}
}
