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
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits.BotModules.Squads
{
	abstract class NavyStateBase : StateBase
	{
		protected Actor FindClosestEnemy(Squad owner, Actor leader)
		{
			// Navy squad AI can exploit enemy naval production to find path, if any.
			// (Way better than finding a nearest target which is likely to be on Ground)
			// You might be tempted to move these lookups into Activate() but that causes null reference exception.
			var mobile = leader.Trait<Mobile>();

			var navalProductions = owner.World.ActorsHavingTrait<Building>().Where(a
				=> owner.SquadManager.Info.NavalProductionTypes.Contains(a.Info.Name)
				&& mobile.PathFinder.PathExistsForLocomotor(mobile.Locomotor, leader.Location, a.Location)
				&& a.AppearsHostileTo(leader));

			if (navalProductions.Any())
			{
				var nearest = navalProductions.ClosestTo(leader);

				// Return nearest when it is FAR enough.
				// If the naval production is within MaxBaseRadius, it implies that
				// this squad is close to enemy territory and they should expect a naval combat;
				// closest enemy makes more sense in that case.
				if ((nearest.Location - leader.Location).LengthSquared > owner.SquadManager.Info.MaxBaseRadius * owner.SquadManager.Info.MaxBaseRadius)
					return nearest;
			}

			return owner.SquadManager.FindClosestEnemy(leader);
		}
	}

	class NavyUnitsIdleState : NavyStateBase, IState
	{
		Actor leader;

		public void Activate(Squad owner) { }

		public void Tick(Squad owner)
		{
			if (!owner.IsValid)
				return;

			leader = GetPathfindLeader(owner).Actor;

			if (!owner.IsTargetValid)
			{
				var closestEnemy = FindClosestEnemy(owner, leader);
				if (closestEnemy == null)
					return;

				owner.TargetActor = closestEnemy;
			}

			var enemyUnits = owner.World.FindActorsInCircle(owner.TargetActor.CenterPosition, WDist.FromCells(owner.SquadManager.Info.IdleScanRadius))
				.Where(owner.SquadManager.IsPreferredEnemyUnit).ToList();

			if (enemyUnits.Count == 0)
			{
				Retreat(owner, flee: false, rearm: true, repair: true);
				return;
			}

			if (AttackOrFleeFuzzy.Default.CanAttack(owner.Units.Select(u => u.Actor), enemyUnits))
			{
				// We have gathered sufficient units. Attack the nearest enemy unit.
				owner.FuzzyStateMachine.ChangeState(owner, new NavyUnitsAttackMoveState(), false);
			}
			else
				owner.FuzzyStateMachine.ChangeState(owner, new NavyUnitsFleeState(), false);
		}

		public void Deactivate(Squad owner) { }
	}

	// See detailed comments at GroundStates.cs
	// There is many in common
	class NavyUnitsAttackMoveState : NavyStateBase, IState
	{
		const int MaxAttemptsToAdvance = 6;
		const int MakeWayTicks = 2;

		// Give tolerance for AI grouping team at start
		int failedAttempts = -(MaxAttemptsToAdvance * 2);
		int makeWay = MakeWayTicks;
		bool canMoveAfterMakeWay = true;
		long stuckDistThreshold;
		WPos lastLeaderPos = WPos.Zero;

		public void Activate(Squad owner)
		{
			stuckDistThreshold = 142179L * owner.SquadManager.Info.AttackForceInterval;
		}

		public void Tick(Squad owner)
		{
			// Basic check
			if (!owner.IsValid)
				return;

			// Initialize leader. Optimize pathfinding by using leader.
			// Drop former "owner.Units.ClosestTo(owner.TargetActor.CenterPosition)",
			// which is the shortest geometric distance, but it has no relation to pathfinding distance in map.
			var leader = GetPathfindLeader(owner);

			if (!owner.IsTargetValid)
			{
				var targetActor = FindClosestEnemy(owner, leader.Actor);
				if (targetActor != null)
					owner.TargetActor = targetActor;
				else
				{
					owner.FuzzyStateMachine.ChangeState(owner, new NavyUnitsFleeState(), false);
					return;
				}
			}

			// Switch to attack state if we encounter enemy units like ground squad
			var attackScanRadius = WDist.FromCells(owner.SquadManager.Info.AttackScanRadius);

			var enemyActor = owner.SquadManager.FindClosestEnemy(leader.Actor, attackScanRadius);
			if (enemyActor != null)
			{
				owner.TargetActor = enemyActor;
				owner.FuzzyStateMachine.ChangeState(owner, new NavyUnitsAttackState(), false);
				return;
			}

			// Solve squad stuck by two method: if canMoveAfterMakeWay is true, use regular method,
			// otherwise try kick units in squad that cannot move at all.
			var occupiedArea = (long)WDist.FromCells(owner.Units.Count).Length * 1024;
			if (failedAttempts >= MaxAttemptsToAdvance)
			{
				// Kick stuck units: Kick stuck units that cannot move at all
				if (!canMoveAfterMakeWay)
				{
					var stopUnits = new List<Actor>();

					// Check if it is the units stuck
					if ((leader.Actor.CenterPosition - leader.WPos).HorizontalLengthSquared < stuckDistThreshold && !IsAttackingAndTryAttack(leader.Actor).isFiring)
					{
						stopUnits.Add(leader.Actor);
						owner.Units.Remove(leader);
					}
					else
					{
						for (var i = 0; i < owner.Units.Count; i++)
						{
							var u = owner.Units[i];
							var dist = (u.Actor.CenterPosition - leader.Actor.CenterPosition).HorizontalLengthSquared;
							if ((u.Actor.CenterPosition - u.WPos).HorizontalLengthSquared <= stuckDistThreshold
								&& dist >= (u.WPos - leader.WPos).HorizontalLengthSquared
								&& dist >= 5 * occupiedArea
								&& !IsAttackingAndTryAttack(u.Actor).isFiring)
							{
								stopUnits.Add(u.Actor);
								owner.Units.RemoveAt(i);
							}
							else
								u.WPos = u.Actor.CenterPosition;
						}
					}

					if (owner.Units.Count == 0)
						return;
					failedAttempts = MaxAttemptsToAdvance - 2;
					leader = owner.Units.FirstOrDefault();
					owner.Bot.QueueOrder(new Order("AttackMove", leader.Actor, Target.FromCell(owner.World, owner.TargetActor.Location), false));
					owner.Bot.QueueOrder(new Order("Stop", null, false, groupedActors: stopUnits.ToArray()));

					makeWay = 0;
				}

				// Make way for leader
				if (makeWay > 0)
				{
					owner.Bot.QueueOrder(new Order("AttackMove", leader.Actor, Target.FromCell(owner.World, owner.TargetActor.Location), false));

					var others = owner.Units.Where(u => u.Actor != leader.Actor).Select(u => u.Actor);
					owner.Bot.QueueOrder(new Order("Scatter", null, false, groupedActors: others.ToArray()));
					if (makeWay == 1)
					{
						// Give some tolerance for AI regrouping when stuck at first time
						failedAttempts = 0 - MakeWayTicks;

						// To prevent ground target causing the stuck
						owner.TargetActor = FindClosestEnemy(owner, leader.Actor);
						canMoveAfterMakeWay = false;
						owner.Bot.QueueOrder(new Order("AttackMove", null, Target.FromCell(owner.World, leader.Actor.Location), true, groupedActors: others.ToArray()));
					}

					makeWay--;
				}

				return;
			}

			// Check if the leader is waiting for squad too long. Skips when just after a stuck-solving process.
			if (makeWay > 0)
			{
				if ((leader.Actor.CenterPosition - lastLeaderPos).HorizontalLengthSquared < stuckDistThreshold / 2) // Becuase compared to kick leader check, lastLeaderPos every squad ticks so we reduce the threshold
					failedAttempts++;
				else
				{
					failedAttempts = 0;
					canMoveAfterMakeWay = true;
					lastLeaderPos = leader.Actor.CenterPosition;
				}
			}
			else
			{
				makeWay = MakeWayTicks;
				lastLeaderPos = leader.Actor.CenterPosition;
			}

			// The same as ground squad regroup
			var leaderWaitCheck = owner.Units.Any(u => (u.Actor.CenterPosition - leader.Actor.CenterPosition).HorizontalLengthSquared > occupiedArea * 5);

			if (leaderWaitCheck)
				owner.Bot.QueueOrder(new Order("Stop", leader.Actor, false));
			else
				owner.Bot.QueueOrder(new Order("AttackMove", leader.Actor, Target.FromCell(owner.World, owner.TargetActor.Location), false));

			var unitsHurryUp = owner.Units.Where(u => (u.Actor.CenterPosition - leader.Actor.CenterPosition).HorizontalLengthSquared >= occupiedArea * 2).Select(u => u.Actor);
			owner.Bot.QueueOrder(new Order("AttackMove", null, Target.FromCell(owner.World, leader.Actor.Location), false, groupedActors: unitsHurryUp.ToArray()));
		}

		public void Deactivate(Squad owner) { }
	}

	// See detailed comments at GroundStates.cs
	// There are many in common
	class NavyUnitsAttackState : NavyStateBase, IState
	{
		// Use it to find if entire squad cannot reach the attack position
		int tryAttackTick;
		int tryAttack = 0;

		public void Activate(Squad owner)
		{
			tryAttackTick = owner.SquadManager.Info.AttackScanRadius;
		}

		public void Tick(Squad owner)
		{
			// Basic check
			if (!owner.IsValid)
				return;

			var leader = owner.Units.First().Actor;
			var isDefaultLeader = true;

			// Rescan target to prevent being ambushed and die without fight
			// If there is no threat around, return to AttackMove state for formation
			var attackScanRadius = WDist.FromCells(owner.SquadManager.Info.AttackScanRadius);
			var closestEnemy = owner.SquadManager.FindClosestEnemy(leader, attackScanRadius);

			if (closestEnemy == null)
			{
				owner.TargetActor = FindClosestEnemy(owner, leader);
				owner.FuzzyStateMachine.ChangeState(owner, new NavyUnitsAttackMoveState(), false);
				return;
			}
			else if (owner.TargetActor != closestEnemy)
			{
				// Refresh tryAttack when target switched
				tryAttack = 0;
				owner.TargetActor = closestEnemy;
			}

			var cannotRetaliate = true;
			var followingUnits = new List<Actor>();
			var attackingUnits = new List<Actor>();

			foreach (var u in owner.Units)
			{
				var attackCondition = IsAttackingAndTryAttack(u.Actor);

				if ((attackCondition.tryAttacking || attackCondition.isFiring) &&
					(u.Actor.CenterPosition - owner.TargetActor.CenterPosition).HorizontalLengthSquared <
					(leader.CenterPosition - owner.TargetActor.CenterPosition).HorizontalLengthSquared)
				{
					isDefaultLeader = false;
					leader = u.Actor;
				}

				if (attackCondition.isFiring && tryAttack != 0)
				{
					// Make there is at least one follow and attack target, AFTER first trying on attack
					if (isDefaultLeader)
					{
						leader = u.Actor;
						isDefaultLeader = false;
					}

					cannotRetaliate = false;
				}
				else if (CanAttackTarget(u.Actor, owner.TargetActor))
				{
					if (tryAttack > tryAttackTick && attackCondition.tryAttacking)
					{
						// Make there is at least one follow and attack target even when approach max tryAttackTick
						if (isDefaultLeader)
						{
							leader = u.Actor;
							isDefaultLeader = false;
							attackingUnits.Add(u.Actor);
							continue;
						}

						followingUnits.Add(u.Actor);
						continue;
					}

					attackingUnits.Add(u.Actor);
					cannotRetaliate = false;
				}
				else
					followingUnits.Add(u.Actor);
			}

			// Because ShouldFlee(owner) cannot retreat units while they cannot even fight
			// a unit that they cannot target. Therefore, use `cannotRetaliate` here to solve this bug.
			if (ShouldFleeSimple(owner) || cannotRetaliate)
			{
				owner.FuzzyStateMachine.ChangeState(owner, new NavyUnitsFleeState(), false);
				return;
			}

			tryAttack++;

			owner.Bot.QueueOrder(new Order("AttackMove", null, Target.FromCell(owner.World, leader.Location), false, groupedActors: followingUnits.ToArray()));
			owner.Bot.QueueOrder(new Order("Attack", null, Target.FromActor(owner.TargetActor), false, groupedActors: attackingUnits.ToArray()));
		}

		public void Deactivate(Squad owner) { }
	}

	class NavyUnitsFleeState : NavyStateBase, IState
	{
		public void Activate(Squad owner) { }

		public void Tick(Squad owner)
		{
			if (!owner.IsValid)
				return;

			Retreat(owner, flee: true, rearm: true, repair: true);
			owner.FuzzyStateMachine.ChangeState(owner, new NavyUnitsIdleState(), false);
		}

		public void Deactivate(Squad owner) { }
	}
}
