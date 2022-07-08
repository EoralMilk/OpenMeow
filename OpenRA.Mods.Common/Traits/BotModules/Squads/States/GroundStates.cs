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
	abstract class GroundStateBase : StateBase
	{
	}

	class GroundUnitsIdleState : GroundStateBase, IState
	{
		Actor leader;

		public void Activate(Squad owner) { }

		public void Tick(Squad owner)
		{
			if (!owner.IsValid)
				return;

			if (owner.SquadManager.UnitCannotBeOrdered(leader))
				leader = GetPathfindLeader(owner).Actor;

			if (!owner.IsTargetValid)
			{
				var closestEnemy = owner.SquadManager.FindClosestEnemy(leader);
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

			if ((AttackOrFleeFuzzy.Default.CanAttack(owner.Units.Select(u => u.Actor), enemyUnits)))
				owner.FuzzyStateMachine.ChangeState(owner, new GroundUnitsAttackMoveState(), false);
			else
				Retreat(owner, flee: true, rearm: true, repair: true);
		}

		public void Deactivate(Squad owner) { }
	}

	// This version AI forcus on solving pathfinding problem for AI
	// 1. use a leader to guide the entire squad to target, solve stuck on twisted road and saving performance on pathfinding
	// 2. have two methods to solve entire squad stuck. First, try make way for leader. Second, kick stuck units
	class GroundUnitsAttackMoveState : GroundStateBase, IState
	{
		const int MaxAttemptsToAdvance = 6;
		const int MakeWayTicks = 2;

		// failedAttempts: squad is considered to be stuck when it is reduced to 0
		// makeWay: the remaining tick for squad on make way behaviour
		// canMoveAfterMakeWay: to find if make way is enough for solve stuck problem, if not, will kick stuck unit when
		// stuckDistThreshold:
		int failedAttempts = -(MaxAttemptsToAdvance * 2); // Give tolerance for AI grouping team at start, so it is not zero
		int makeWay = MakeWayTicks;
		bool canMoveAfterMakeWay = true;
		long stuckDistThreshold;

		UnitWposWrapper leader = new UnitWposWrapper(null);
		WPos lastLeaderPos = WPos.Zero; // Record leader location at every bot tick, to find if leader/squad is stuck

		public void Activate(Squad owner)
		{
			stuckDistThreshold = 142179L * owner.SquadManager.Info.AttackForceInterval;
		}

		public void Tick(Squad owner)
		{
			// Basic check
			if (!owner.IsValid)
				return;

			// Initialize leader. Optimize pathfinding by using a leader with specific locomotor.
			// Drop former "owner.Units.ClosestTo(owner.TargetActor.CenterPosition)",
			// which is the shortest geometric distance, but it has no relation to pathfinding distance in map.
			if (owner.SquadManager.UnitCannotBeOrdered(leader.Actor))
				leader = GetPathfindLeader(owner);

			if (!owner.IsTargetValid)
			{
				var targetActor = owner.SquadManager.FindClosestEnemy(leader.Actor);
				if (targetActor != null)
					owner.TargetActor = targetActor;
				else
				{
					owner.FuzzyStateMachine.ChangeState(owner, new GroundUnitsFleeState(), false);
					return;
				}
			}

			// Switch to "GroundUnitsAttackState" if we encounter enemy units.
			var attackScanRadius = WDist.FromCells(owner.SquadManager.Info.AttackScanRadius);

			var enemyActor = owner.SquadManager.FindClosestEnemy(leader.Actor, attackScanRadius);
			if (enemyActor != null)
			{
				owner.TargetActor = enemyActor;
				owner.FuzzyStateMachine.ChangeState(owner, new GroundUnitsAttackState(), false);
				return;
			}

			// Since units have different movement speeds, they get separated while approaching the target.
			// Let them regroup into tighter formation towards "leader".
			//
			// "occupiedArea" means the space the squad units will occupy (if 1 per Cell).
			// leader only stop when scope of "lookAround" is not covered all units;
			// units in "unitsHurryUp"  will catch up, which keep the team tight while not stuck.
			//
			// Imagining "occupiedArea" takes up a a place shape like square,
			// we need to draw a circle to cover the the enitire circle.
			var occupiedArea = (long)WDist.FromCells(owner.Units.Count).Length * 1024;

			// Solve squad stuck by two method: if canMoveAfterMakeWay is true, try make way for leader,
			// otherwise try kick units in squad that cannot move at all.
			if (failedAttempts >= MaxAttemptsToAdvance)
			{
				// Kick stuck units: Kick stuck units that cannot move at all
				if (!canMoveAfterMakeWay)
				{
					var stopUnits = new List<Actor>();

					// Check if it is the leader stuck
					if ((leader.Actor.CenterPosition - leader.WPos).HorizontalLengthSquared < stuckDistThreshold && !IsAttackingAndTryAttack(leader.Actor).isFiring)
					{
						stopUnits.Add(leader.Actor);
						owner.Units.Remove(leader);
					}

					// If not, check and record all units position
					else
					{
						for (var i = 0; i < owner.Units.Count; i++)
						{
							var u = owner.Units[i];
							var dist = (u.Actor.CenterPosition - leader.Actor.CenterPosition).HorizontalLengthSquared;
							if ((u.Actor.CenterPosition - u.WPos).HorizontalLengthSquared <= stuckDistThreshold // Check if unit cannot move
								&& dist >= (u.WPos - leader.WPos).HorizontalLengthSquared // Check if unit are further from leader than before
								&& dist >= 5 * occupiedArea // Ckeck if unit in valid distance from leader
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
					leader = GetPathfindLeader(owner);
					owner.Bot.QueueOrder(new Order("AttackMove", leader.Actor, Target.FromCell(owner.World, owner.TargetActor.Location), false));
					owner.Bot.QueueOrder(new Order("Stop", null, false, groupedActors: stopUnits.ToArray()));
					makeWay = 0;
				}

				// Make way for leader: Make sure the guide unit has not been blocked by the rest of the squad.
				// If canMoveAfterMakeWay is not reset to true after this, will try kick unit
				if (makeWay > 0)
				{
					owner.Bot.QueueOrder(new Order("AttackMove", leader.Actor, Target.FromCell(owner.World, owner.TargetActor.Location), false));

					var others = owner.Units.Where(u => u.Actor != leader.Actor).Select(u => u.Actor);
					owner.Bot.QueueOrder(new Order("Scatter", null, false, groupedActors: others.ToArray()));
					if (makeWay == 1)
					{
						// Give some tolerance for AI regrouping when make way
						failedAttempts = 0 - MakeWayTicks;

						// Change target that may cause the stuck
						owner.TargetActor = owner.SquadManager.FindClosestEnemy(leader.Actor);
						canMoveAfterMakeWay = false;
						owner.Bot.QueueOrder(new Order("AttackMove", null, Target.FromCell(owner.World, leader.Actor.Location), true, groupedActors: others.ToArray()));
					}

					makeWay--;
				}

				return;
			}

			// Stuck check: by using "failedAttempts" to get if the leader is waiting for squad too long .
			// When just after a stuck-solving process, only record position and skip the stuck check.
			if (makeWay > 0)
			{
				if ((leader.Actor.CenterPosition - lastLeaderPos).HorizontalLengthSquared < stuckDistThreshold / 2) // Becuase compared to kick leader check, lastLeaderPos record every ticks so we reduce the threshold
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

			// "Leader" will check how many squad members are around
			// to decide if it needs to continue.
			//
			// Units that need hurry up ("unitsHurryUp") will try catch up before Leader waiting,
			// which can make squad members follows relatively tight without stucking "Leader".
			var leaderWaitCheck = owner.Units.Any(u => (u.Actor.CenterPosition - leader.Actor.CenterPosition).HorizontalLengthSquared > occupiedArea * 5);

			if (leaderWaitCheck)
				owner.Bot.QueueOrder(new Order("Stop", leader.Actor, false));
			else
				owner.Bot.QueueOrder(new Order("AttackMove", leader.Actor, Target.FromCell(owner.World, owner.TargetActor.Location), false));

			var unitsHurryUp = owner.Units.Where(u => (u.Actor.CenterPosition - leader.Actor.CenterPosition).HorizontalLengthSquared >= occupiedArea * 2).Select(u => u.Actor).ToArray();
			owner.Bot.QueueOrder(new Order("AttackMove", null, Target.FromCell(owner.World, leader.Actor.Location), false, groupedActors: unitsHurryUp));
		}

		public void Deactivate(Squad owner) { }
	}

	class GroundUnitsAttackState : GroundStateBase, IState
	{
		// Use it to find if entire squad cannot reach the attack position
		int tryAttackTick;

		Actor leader;
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

			if (owner.SquadManager.UnitCannotBeOrdered(leader))
				leader = owner.Units.First().Actor;

			var isDefaultLeader = true;

			// Rescan target to prevent being ambushed and die without fight
			// If there is no threat around, return to AttackMove state for formation
			var attackScanRadius = WDist.FromCells(owner.SquadManager.Info.AttackScanRadius);
			var closestEnemy = owner.SquadManager.FindClosestEnemy(leader, attackScanRadius);

			// Becuase MoveWithinRange can cause huge lag when stuck
			// we only allow free attack behaivour within TryAttackTick
			// then the squad will gather to a certain leader
			if (closestEnemy == null)
			{
				owner.TargetActor = owner.SquadManager.FindClosestEnemy(leader);
				owner.FuzzyStateMachine.ChangeState(owner, new GroundUnitsAttackMoveState(), false);
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
				owner.FuzzyStateMachine.ChangeState(owner, new GroundUnitsFleeState(), false);
				return;
			}

			tryAttack++;

			owner.Bot.QueueOrder(new Order("AttackMove", null, Target.FromCell(owner.World, leader.Location), false, groupedActors: followingUnits.ToArray()));
			owner.Bot.QueueOrder(new Order("Attack", null, Target.FromActor(owner.TargetActor), false, groupedActors: attackingUnits.ToArray()));
		}

		public void Deactivate(Squad owner) { }
	}

	class GroundUnitsFleeState : GroundStateBase, IState
	{
		public void Activate(Squad owner) { }

		public void Tick(Squad owner)
		{
			if (!owner.IsValid)
				return;

			Retreat(owner, flee: true, rearm: true, repair: true);
			owner.FuzzyStateMachine.ChangeState(owner, new GroundUnitsIdleState(), false);
		}

		public void Deactivate(Squad owner) { owner.SquadManager.DismissSquad(owner); }
	}
}
