#region Copyright & License Information
/*
 * Copyright 2007-2020 The OpenRA Developers (see AUTHORS)
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
	abstract class GuerrillaStatesBase : GroundStateBase
	{
	}

	class GuerrillaUnitsIdleState : GuerrillaStatesBase, IState
	{
		Actor leader;
		int squadsize;

		public void Activate(Squad owner) { }

		public void Tick(Squad owner)
		{
			if (!owner.IsValid)
				return;

			if (owner.SquadManager.UnitCannotBeOrdered(leader) || squadsize != owner.Units.Count)
			{
				leader = GetPathfindLeader(owner).Actor;
				squadsize = owner.Units.Count;
			}

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
				Retreat(owner, false, true, true);
				return;
			}

			if ((AttackOrFleeFuzzy.Default.CanAttack(owner.Units.Select(u => u.Actor), enemyUnits)))
			{
				// We have gathered sufficient units. Attack the nearest enemy unit.
				owner.BaseLocation = RandomBuildingLocation(owner);
				owner.FuzzyStateMachine.ChangeState(owner, new GuerrillaUnitsAttackMoveState(), false);
			}
			else
				Retreat(owner, true, true, true);
		}

		public void Deactivate(Squad owner) { }
	}

	// See detailed comments at GroundStates.cs
	// There is many in common
	class GuerrillaUnitsAttackMoveState : GuerrillaStatesBase, IState
	{
		const int MaxAttemptsToAdvance = 6;
		const int MakeWayTicks = 2;

		// Give tolerance for AI grouping team at start
		int failedAttempts = -(MaxAttemptsToAdvance * 2);
		int makeWay = MakeWayTicks;
		bool canMoveAfterMakeWay = true;
		long stuckDistThreshold;

		UnitWposWrapper leader = new UnitWposWrapper(null);
		WPos lastLeaderPos = WPos.Zero;
		int squadsize = 0;

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
			if (owner.SquadManager.UnitCannotBeOrdered(leader.Actor) || squadsize != owner.Units.Count)
			{
				leader = GetPathfindLeader(owner);
				squadsize = owner.Units.Count;
			}

			if (!owner.IsTargetValid)
			{
				var targetActor = owner.SquadManager.FindClosestEnemy(leader.Actor);
				if (targetActor != null)
					owner.TargetActor = targetActor;
				else
				{
					owner.FuzzyStateMachine.ChangeState(owner, new GuerrillaUnitsFleeState(), false);
					return;
				}
			}

			// Switch to attack state if we encounter enemy units like ground squad
			var attackScanRadius = WDist.FromCells(owner.SquadManager.Info.AttackScanRadius);

			var enemyActor = owner.SquadManager.FindClosestEnemy(leader.Actor, attackScanRadius);
			if (enemyActor != null)
			{
				owner.TargetActor = enemyActor;
				owner.FuzzyStateMachine.ChangeState(owner, new GuerrillaUnitsHitState(), false);
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

					// Check if it is the leader stuck
					if ((leader.Actor.CenterPosition - leader.WPos).HorizontalLengthSquared < stuckDistThreshold && !IsAttackingAndTryAttack(leader.Actor).isFiring)
					{
						stopUnits.Add(leader.Actor);
						owner.Units.Remove(leader);
					}

					// Check if it is the units stuck
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
						// Give some tolerance for AI regrouping when stuck at first time
						failedAttempts = 0 - MakeWayTicks;

						// Change target that may cause the stuck, which also makes Guerrilla Squad unpredictable
						owner.TargetActor = owner.SquadManager.FindClosestEnemy(leader.Actor);
						makeWay = MakeWayTicks;
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

			var unitsHurryUp = owner.Units.Where(u => (u.Actor.CenterPosition - leader.Actor.CenterPosition).HorizontalLengthSquared >= occupiedArea * 2).Select(u => u.Actor).ToArray();
			owner.Bot.QueueOrder(new Order("AttackMove", null, Target.FromCell(owner.World, leader.Actor.Location), false, groupedActors: unitsHurryUp));
		}

		public void Deactivate(Squad owner) { }
	}

	// See detailed comments at GroundStates.cs
	// There are many in common
	class GuerrillaUnitsHitState : GuerrillaStatesBase, IState
	{
		// Use it to find if entire squad cannot reach the attack position
		int tryAttackTick;

		Actor leader;
		int tryAttack = 0;
		bool isFirstTick = true; // Only record HP and do not retreat at first tick
		int squadsize = 0;

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
				leader = owner.Units.FirstOrDefault().Actor;

			var isDefaultLeader = true;

			// Rescan target to prevent being ambushed and die without fight
			// If there is no threat around, return to AttackMove state for formation
			var attackScanRadius = WDist.FromCells(owner.SquadManager.Info.AttackScanRadius);
			var closestEnemy = owner.SquadManager.FindClosestEnemy(leader, attackScanRadius);

			var healthChange = false;
			var cannotRetaliate = true;
			var followingUnits = new List<Actor>();
			var attackingUnits = new List<Actor>();

			if (closestEnemy == null)
			{
				owner.TargetActor = owner.SquadManager.FindClosestEnemy(leader);
				owner.FuzzyStateMachine.ChangeState(owner, new GuerrillaUnitsAttackMoveState(), false);
				return;
			}
			else
			{
				if (owner.TargetActor != closestEnemy)
				{
					// Refresh tryAttack when target switched
					tryAttack = 0;
					owner.TargetActor = closestEnemy;
				}

				for (var i = 0; i < owner.Units.Count; i++)
				{
					var u = owner.Units[i];
					var attackCondition = IsAttackingAndTryAttack(u.Actor);

					var health = u.Actor.TraitOrDefault<IHealth>();

					if (health != null)
					{
						var healthWPos = new WPos(0, 0, (int)health.DamageState); // HACK: use WPos.Z storage HP
						if (u.WPos.Z != healthWPos.Z)
						{
							if (u.WPos.Z < healthWPos.Z)
								healthChange = true;
							u.WPos = healthWPos;
						}
					}

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
			}

			// Because ShouldFlee(owner) cannot retreat units while they cannot even fight
			// a unit that they cannot target. Therefore, use `cannotRetaliate` here to solve this bug.
			if (cannotRetaliate)
				owner.FuzzyStateMachine.ChangeState(owner, new GuerrillaUnitsFleeState(), true);

			tryAttack++;

			var unitlost = squadsize > owner.Units.Count;
			squadsize = owner.Units.Count;

			if ((healthChange || unitlost) && !isFirstTick)
				owner.FuzzyStateMachine.ChangeState(owner, new GuerrillaUnitsRunState(), true);

			owner.Bot.QueueOrder(new Order("AttackMove", null, Target.FromCell(owner.World, leader.Location), false, groupedActors: followingUnits.ToArray()));
			owner.Bot.QueueOrder(new Order("Attack", null, Target.FromActor(owner.TargetActor), false, groupedActors: attackingUnits.ToArray()));

			isFirstTick = false;
		}

		public void Deactivate(Squad owner) { }
	}

	class GuerrillaUnitsRunState : GuerrillaStatesBase, IState
	{
		public const int HitTicks = 2;
		internal int Hit = HitTicks;
		bool ordered;

		public void Activate(Squad owner) { ordered = false; }

		public void Tick(Squad owner)
		{
			if (!owner.IsValid)
				return;

			if (Hit-- <= 0)
			{
				Hit = HitTicks;
				owner.FuzzyStateMachine.ChangeState(owner, new GuerrillaUnitsHitState(), true);
				return;
			}

			if (!ordered)
			{
				owner.Bot.QueueOrder(new Order("Move", null, Target.FromCell(owner.World, owner.BaseLocation), false, groupedActors: owner.Units.Select(u => u.Actor).ToArray()));
				ordered = true;
			}
		}

		public void Deactivate(Squad owner) { }
	}

	class GuerrillaUnitsFleeState : GuerrillaStatesBase, IState
	{
		public void Activate(Squad owner) { }

		public void Tick(Squad owner)
		{
			if (!owner.IsValid)
				return;

			Retreat(owner, true, true, true);
			owner.FuzzyStateMachine.ChangeState(owner, new GuerrillaUnitsIdleState(), false);
		}

		public void Deactivate(Squad owner) { }
	}
}
