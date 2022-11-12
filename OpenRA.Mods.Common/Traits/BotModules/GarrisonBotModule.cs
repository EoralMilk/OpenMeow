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
using System.Collections.Generic;
using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Manages AI repairing base buildings.")]
	public class GarrisonBotModuleInfo : ConditionalTraitInfo
	{
		public readonly HashSet<string> Transports = default;
		public readonly HashSet<string> Passengers = default;
		public readonly PlayerRelationship ValidTransportRelationship = PlayerRelationship.Ally;
		public readonly bool OnlyEnterSelf = true;
		public readonly string EnterOrderName = "EnterTransport";
		public readonly int ScanTick = 300;
		public override object Create(ActorInitializer init) { return new GarrisonBotModule(init.Self, this); }
	}

	public class GarrisonBotModule : ConditionalTrait<GarrisonBotModuleInfo>, IBotTick
	{
		readonly World world;
		readonly Player player;
		readonly PlayerRelationship transportRelationship;
		readonly Predicate<Actor> unitCannotBeOrderedOrIsBusy;
		readonly Predicate<Actor> invalidTransport;
		int minAssignRoleDelayTicks;

		public GarrisonBotModule(Actor self, GarrisonBotModuleInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;
			transportRelationship = info.ValidTransportRelationship;
			if (info.ValidTransportRelationship.HasRelationship(PlayerRelationship.Ally) && info.OnlyEnterSelf)
				invalidTransport = a => a == null || a.IsDead || !a.IsInWorld || a.Owner != player;
			else
				invalidTransport = a => a == null || a.IsDead || !a.IsInWorld || !transportRelationship.HasRelationship(a.Owner.RelationshipWith(player));
			unitCannotBeOrderedOrIsBusy = a => a == null || a.IsDead || !a.IsInWorld || a.Owner != player || !a.IsIdle;
		}

		protected override void TraitEnabled(Actor self)
		{
			// Avoid all AIs reevaluating assignments on the same tick, randomize their initial evaluation delay.
			minAssignRoleDelayTicks = world.LocalRandom.Next(0, Info.ScanTick);
		}

		void IBotTick.BotTick(IBot bot)
		{
			if (--minAssignRoleDelayTicks <= 0)
			{
				minAssignRoleDelayTicks = Info.ScanTick;

				var tcs = world.ActorsWithTrait<Cargo>().Where(at => Info.Transports.Contains(at.Actor.Info.Name)
				&& !invalidTransport(at.Actor) && at.Trait.HasSpace(1)).ToArray();

				if (tcs.Length == 0)
					return;

				var tc = tcs.Random(world.LocalRandom);
				var cargo = tc.Trait;
				var transport = tc.Actor;
				var space = cargo.Space();

				var passengers = world.ActorsWithTrait<Passenger>().Where(at => !unitCannotBeOrderedOrIsBusy(at.Actor) && Info.Passengers.Contains(at.Actor.Info.Name) && at.Trait.Info.Weight <= space)
					.OrderByDescending(at => (at.Actor.CenterPosition - transport.CenterPosition).HorizontalLengthSquared);

				var orderedActors = new List<Actor>();

				foreach (var p in passengers)
				{
					var mobile = p.Actor.TraitOrDefault<Mobile>();
					if (mobile == null || !mobile.PathFinder.PathExistsForLocomotor(mobile.Locomotor, p.Actor.Location, transport.Location))
						continue;

					if (space - p.Trait.Info.Weight >= 0)
					{
						space -= p.Trait.Info.Weight;
						orderedActors.Add(p.Actor);
					}

					if (space <= 0)
						break;
				}

				if (orderedActors.Count > 0)
					bot.QueueOrder(new Order(Info.EnterOrderName, null, Target.FromActor(transport), false, groupedActors: orderedActors.ToArray()));
			}
		}
	}
}
