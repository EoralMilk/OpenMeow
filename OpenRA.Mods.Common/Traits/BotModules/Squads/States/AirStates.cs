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
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits.BotModules.Squads
{
	abstract class AirStateBase : StateBase
	{
		static readonly BitSet<TargetableType> AirTargetTypes = new BitSet<TargetableType>("Air");

		protected static int CountAntiAirUnits(IEnumerable<Actor> units)
		{
			if (!units.Any())
				return 0;

			var missileUnitsCount = 0;
			foreach (var unit in units)
			{
				if (unit == null)
					continue;

				foreach (var ab in unit.TraitsImplementing<AttackBase>())
				{
					if (ab.IsTraitDisabled || ab.IsTraitPaused)
						continue;

					foreach (var a in ab.Armaments)
					{
						if (a.Weapon.IsValidTarget(AirTargetTypes))
						{
							if (unit.Info.HasTraitInfo<AircraftInfo>())
								missileUnitsCount += 1;
							else
								missileUnitsCount += 3;
							break;
						}
					}
				}
			}

			return missileUnitsCount;
		}

		protected static Actor FindDefenselessTarget(Squad owner)
		{
			FindSafePlace(owner, out var target, true);
			return target;
		}

		protected static CPos? FindSafePlace(Squad owner, out Actor detectedEnemyTarget, bool needTarget)
		{
			var map = owner.World.Map;
			var dangerRadius = owner.SquadManager.Info.DangerScanRadius;
			detectedEnemyTarget = null;

			var columnCount = (map.MapSize.X + dangerRadius - 1) / dangerRadius;
			var rowCount = (map.MapSize.Y + dangerRadius - 1) / dangerRadius;

			var checkIndices = Exts.MakeArray(columnCount * rowCount, i => i).Shuffle(owner.World.LocalRandom);
			foreach (var i in checkIndices)
			{
				var pos = new MPos((i % columnCount) * dangerRadius + dangerRadius / 2, (i / columnCount) * dangerRadius + dangerRadius / 2).ToCPos(map);

				if (NearToPosSafely(owner, map.CenterOfCell(pos), out detectedEnemyTarget))
				{
					if (needTarget && detectedEnemyTarget == null)
						continue;

					return pos;
				}
			}

			return null;
		}

		protected static bool NearToPosSafely(Squad owner, WPos loc)
		{
			return NearToPosSafely(owner, loc, out _);
		}

		protected static bool NearToPosSafely(Squad owner, WPos loc, out Actor detectedEnemyTarget)
		{
			detectedEnemyTarget = null;
			var dangerRadius = owner.SquadManager.Info.DangerScanRadius;
			var unitsAroundPos = owner.World.FindActorsInCircle(loc, WDist.FromCells(dangerRadius))
				.Where(owner.SquadManager.IsPreferredEnemyUnit).ToList();

			if (unitsAroundPos.Count == 0)
				return true;

			if (CountAntiAirUnits(unitsAroundPos) < owner.Units.Count)
			{
				detectedEnemyTarget = unitsAroundPos.Random(owner.Random);
				return true;
			}

			return false;
		}

		// Checks the number of anti air enemies around units
		protected virtual bool ShouldFlee(Squad owner)
		{
			return ShouldFlee(owner, enemies => CountAntiAirUnits(enemies) > owner.Units.Count);
		}
	}

	class AirIdleState : AirStateBase, IState
	{
		public void Activate(Squad owner) { }

		public void Tick(Squad owner)
		{
			if (!owner.IsValid)
				return;

			if (ShouldFlee(owner))
			{
				owner.FuzzyStateMachine.ChangeState(owner, new AirFleeState(), false);
				return;
			}

			if (!owner.IsTargetValid)
			{
				var e = FindDefenselessTarget(owner);
				owner.TargetActor = e;
			}

			if (!owner.IsTargetValid)
			{
				Retreat(owner, flee: false, rearm: true, repair: true);
				return;
			}

			owner.FuzzyStateMachine.ChangeState(owner, new AirAttackState(), false);
		}

		public void Deactivate(Squad owner) { }
	}

	class AirAttackState : AirStateBase, IState
	{
		public void Activate(Squad owner) { }

		public void Tick(Squad owner)
		{
			if (!owner.IsValid)
				return;

			if (!owner.IsTargetValid)
			{
				var u = owner.Units.Random(owner.Random);
				var closestEnemy = owner.SquadManager.FindClosestEnemy(u.Actor);
				if (closestEnemy != null)
					owner.TargetActor = closestEnemy;
				else
				{
					owner.FuzzyStateMachine.ChangeState(owner, new AirFleeState(), false);
					return;
				}
			}

			var leader = owner.Units.Select(u => u.Actor).ClosestTo(owner.TargetActor.CenterPosition);

			var unitsAroundPos = owner.World.FindActorsInCircle(leader.CenterPosition, WDist.FromCells(owner.SquadManager.Info.DangerScanRadius))
				.Where(a => owner.SquadManager.IsPreferredEnemyUnit(a) && owner.SquadManager.IsNotHiddenUnit(a));

			// Check if get ambushed.
			if (CountAntiAirUnits(unitsAroundPos) > owner.Units.Count)
			{
				owner.FuzzyStateMachine.ChangeState(owner, new AirFleeState(), false);
				return;
			}

			var cannotRetaliate = true;
			var resupplyingUnits = new List<Actor>();
			var backingoffUnits = new List<Actor>();
			var attackingUnits = new List<Actor>();
			foreach (var u in owner.Units)
			{
				if (IsAttackingAndTryAttack(u.Actor).tryAttacking)
				{
					cannotRetaliate = false;
					continue;
				}

				var ammoPools = u.Actor.TraitsImplementing<AmmoPool>().ToArray();
				if (!ReloadsAutomatically(ammoPools, u.Actor.TraitOrDefault<Rearmable>()))
				{
					if (IsRearming(u.Actor))
						continue;

					if (!HasAmmo(ammoPools))
					{
						resupplyingUnits.Add(u.Actor);
						continue;
					}
				}

				if (CanAttackTarget(u.Actor, owner.TargetActor))
				{
					cannotRetaliate = false;
					attackingUnits.Add(u.Actor);
				}
				else
				{
					if (!FullAmmo(ammoPools))
					{
						resupplyingUnits.Add(u.Actor);
						continue;
					}

					backingoffUnits.Add(u.Actor);
				}
			}

			if (cannotRetaliate)
			{
				owner.FuzzyStateMachine.ChangeState(owner, new AirFleeState(), false);
				return;
			}

			owner.Bot.QueueOrder(new Order("ReturnToBase", null, false, groupedActors: resupplyingUnits.ToArray()));
			owner.Bot.QueueOrder(new Order("Attack", null, Target.FromActor(owner.TargetActor), false, groupedActors: attackingUnits.ToArray()));
			owner.Bot.QueueOrder(new Order("Move", null, Target.FromCell(owner.World, RandomBuildingLocation(owner)), false, groupedActors: backingoffUnits.ToArray()));
		}

		public void Deactivate(Squad owner) { }
	}

	class AirFleeState : AirStateBase, IState
	{
		public void Activate(Squad owner) { }

		public void Tick(Squad owner)
		{
			owner.TargetActor = null;

			if (!owner.IsValid)
				return;

			Retreat(owner, flee: true, rearm: true, repair: true);
			owner.FuzzyStateMachine.ChangeState(owner, new AirIdleState(), false);
		}

		public void Deactivate(Squad owner) { }
	}
}
