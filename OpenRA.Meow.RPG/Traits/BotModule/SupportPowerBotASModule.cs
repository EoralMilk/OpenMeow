#region Copyright & License Information
/*
 * Copyright 2015- OpenRA.Mods.AS Developers (see AUTHORS)
 * This file is a part of a third-party plugin for OpenRA, which is
 * free software. It is made available to you under the terms of the
 * GNU General Public License as published by the Free Software
 * Foundation. For more information, see COPYING.
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.AS.Traits
{
	[Desc("Manages bot support power handling. From AS")]
	public class SupportPowerBotASModuleInfo : ConditionalTraitInfo, Requires<SupportPowerManagerInfo>
	{
		[Desc("Tells the AI how to use its support powers.")]
		[FieldLoader.LoadUsing("LoadDecisions")]
		public readonly List<SupportPowerDecisionAS> Decisions = new List<SupportPowerDecisionAS>();

		static object LoadDecisions(MiniYaml yaml)
		{
			var ret = new List<SupportPowerDecisionAS>();
			var decisions = yaml.Nodes.FirstOrDefault(n => n.Key == "Decisions");
			if (decisions != null)
				foreach (var d in decisions.Value.Nodes)
					ret.Add(new SupportPowerDecisionAS(d.Value));

			return ret;
		}

		public override object Create(ActorInitializer init) { return new SupportPowerBotASModule(init.Self, this); }
	}

	public class SupportPowerBotASModule : ConditionalTrait<SupportPowerBotASModuleInfo>, IBotTick, IGameSaveTraitData
	{
		readonly World world;
		readonly Player player;
		readonly Dictionary<SupportPowerInstance, int> waitingPowers = new Dictionary<SupportPowerInstance, int>();
		readonly Dictionary<string, SupportPowerDecisionAS> powerDecisions = new Dictionary<string, SupportPowerDecisionAS>();
		readonly List<SupportPowerInstance> stalePowers = new List<SupportPowerInstance>();
		SupportPowerManager supportPowerManager;

		public SupportPowerBotASModule(Actor self, SupportPowerBotASModuleInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;
		}

		protected override void TraitEnabled(Actor self)
		{
			supportPowerManager = player.PlayerActor.Trait<SupportPowerManager>();
			foreach (var decision in Info.Decisions)
			{
				if (!powerDecisions.ContainsKey(decision.OrderName))
					powerDecisions.Add(decision.OrderName, decision);
			}
		}

		void IBotTick.BotTick(IBot bot)
		{
			if (IsTraitDisabled)
				return;

			foreach (var sp in supportPowerManager.Powers.Values)
			{
				if (sp.Disabled)
					continue;

				// Add power to dictionary if not in delay dictionary yet
				if (!waitingPowers.ContainsKey(sp))
					waitingPowers.Add(sp, 0);

				if (waitingPowers[sp] > 0)
					waitingPowers[sp]--;

				// If we have recently tried and failed to find a use location for a power, then do not try again until later
				var isDelayed = waitingPowers[sp] > 0;
				if (sp.Ready && !isDelayed && powerDecisions.ContainsKey(sp.Info.OrderName))
				{
					var powerDecision = powerDecisions[sp.Info.OrderName];
					if (powerDecision == null)
					{
						AIUtils.BotDebug("Bot Bug: FindAttackLocationToSupportPower, couldn't find powerDecision for {0}", sp.Info.OrderName);
						continue;
					}

					var attackLocation = FindAttackLocationToSupportPower(sp);
					if (attackLocation == null)
					{
						AIUtils.BotDebug("AI: {1} can't find suitable attack location for support power {0}. Delaying rescan.", sp.Info.OrderName, player.PlayerName);
						waitingPowers[sp] += powerDecision.GetNextScanTime(world);

						continue;
					}

					// Valid target found, delay by a few ticks to avoid rescanning before power fires via order
					AIUtils.BotDebug("AI: {2} found new target location {0} for support power {1}.", attackLocation, sp.Info.OrderName, player.PlayerName);
					waitingPowers[sp] += 10;
					bot.QueueOrder(new Order(sp.Key, supportPowerManager.Self, Target.FromCell(world, attackLocation.Value), false) { SuppressVisualFeedback = true });
				}
			}

			// Remove stale powers
			stalePowers.AddRange(waitingPowers.Keys.Where(wp => !supportPowerManager.Powers.ContainsKey(wp.Key)));
			foreach (var p in stalePowers)
				waitingPowers.Remove(p);

			stalePowers.Clear();
		}

		/// <summary>Detail scans an area, evaluating positions.</summary>
		CPos? FindAttackLocationToSupportPower(SupportPowerInstance readyPower)
		{
			CPos? bestLocation = null;
			var bestAttractiveness = 0;
			var powerDecision = powerDecisions[readyPower.Info.OrderName];
			if (powerDecision == null)
			{
				AIUtils.BotDebug("Bot Bug: FindAttackLocationToSupportPower, couldn't find powerDecision for {0}", readyPower.Info.OrderName);
				return null;
			}

			var availableTargets = world.ActorsHavingTrait<IOccupySpace>().Where(x => x.IsInWorld && !x.IsDead &&
				(powerDecision.IgnoreVisibility || x.CanBeViewedByPlayer(player)) &&
				powerDecision.Against.HasRelationship(player.RelationshipWith(x.Owner)) &&
				powerDecision.Types.Overlaps(x.GetEnabledTargetTypes()));

			foreach (var a in availableTargets)
			{
				var pos = a.CenterPosition;
				var consideredAttractiveness = 0;
				consideredAttractiveness += powerDecision.GetAttractiveness(pos, player);

				if (consideredAttractiveness <= bestAttractiveness || consideredAttractiveness < powerDecision.MinimumAttractiveness)
					continue;

				bestAttractiveness = consideredAttractiveness;
				bestLocation = world.Map.CellContaining(pos);
			}

			return bestLocation;
		}

		List<MiniYamlNode> IGameSaveTraitData.IssueTraitData(Actor self)
		{
			if (IsTraitDisabled)
				return null;

			var waitingPowersNodes = waitingPowers
				.Select(kv => new MiniYamlNode(kv.Key.Key, FieldSaver.FormatValue(kv.Value)))
				.ToList();

			return new List<MiniYamlNode>()
			{
				new MiniYamlNode("WaitingPowers", "", waitingPowersNodes)
			};
		}

		void IGameSaveTraitData.ResolveTraitData(Actor self, List<MiniYamlNode> data)
		{
			if (self.World.IsReplay)
				return;

			var waitingPowersNode = data.FirstOrDefault(n => n.Key == "WaitingPowers");
			if (waitingPowersNode != null)
			{
				foreach (var n in waitingPowersNode.Value.Nodes)
				{
					if (supportPowerManager.Powers.TryGetValue(n.Key, out var instance))
						waitingPowers[instance] = FieldLoader.GetValue<int>("WaitingPowers", n.Value.Value);
				}
			}
		}
	}
}
