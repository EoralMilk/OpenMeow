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
using System.Linq;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Spawn another actor immediately upon death.")]
	public class SpawnActorsOnDeathInfo : ConditionalTraitInfo
	{
		[ActorReference]
		[FieldLoader.Require]
		[Desc("Actor to spawn on death.")]
		public readonly string[] ActorTypes = null;

		[Desc("Probability the actor spawns.")]
		public readonly int Probability = 100;

		[Desc("Owner of the spawned actor. Allowed keywords:" +
			"'Victim', 'Killer' and 'InternalName'. " +
			"Falls back to 'InternalName' if 'Victim' is used " +
			"and the victim is defeated (see 'SpawnAfterDefeat').")]
		public readonly OwnerType OwnerType = OwnerType.Victim;

		[Desc("Map player to use when 'InternalName' is defined on 'OwnerType'.")]
		public readonly string InternalOwner = "Neutral";

		[Desc("Changes the effective (displayed) owner of the spawned actor to the old owner (victim).")]
		public readonly bool EffectiveOwnerFromOwner = false;

		[Desc("DeathType that triggers the actor spawn. " +
			"Leave empty to spawn an actor ignoring the DeathTypes.")]
		public readonly string DeathType = null;

		[Desc("Skips the spawned actor's make animations if true.")]
		public readonly bool SkipMakeAnimations = true;

		[Desc("Should the actors only be spawned when the 'Creeps' setting is true?")]
		public readonly bool RequiresLobbyCreeps = false;

		[Desc("Offset of the spawned actor relative to the dying actor's position.",
			"Warning: Spawning an actor outside the parent actor's footprint/influence might",
			"lead to unexpected behaviour.")]
		public readonly CVec Offset = CVec.Zero;

		[Desc("Should the actors spawn on the building cell?")]
		public readonly bool UseCell = false;

		[Desc("Should an actor spawn after the player has been defeated (e.g. after surrendering)?")]
		public readonly bool SpawnAfterDefeat = true;

		[Desc("Calculate the number of units generated according to their and self value")]
		public readonly bool SpawnCountCalculateAsValue = false;
		public readonly int ValuePercent = 40;

		public override object Create(ActorInitializer init) { return new SpawnActorsOnDeath(init, this); }
	}

	public class SpawnActorsOnDeath : ConditionalTrait<SpawnActorsOnDeathInfo>, INotifyCreated, INotifyKilled, INotifyRemovedFromWorld
	{
		readonly string faction;
		readonly bool enabled;

		Player attackingPlayer;
		BuildingInfo buildingInfo;
		ValuedInfo valued;
		int dudesValue = 99999;
		public SpawnActorsOnDeath(ActorInitializer init, SpawnActorsOnDeathInfo info)
			: base(info)
		{
			enabled = !info.RequiresLobbyCreeps || init.Self.World.WorldActor.Trait<MapCreeps>().Enabled;
			faction = init.GetValue<FactionInit, string>(init.Self.Owner.Faction.InternalName);
		}

		protected override void Created(Actor self)
		{
			if (Info.RequiresCondition == null)
				TraitEnabled(self);

			buildingInfo = self.Info.TraitInfoOrDefault<BuildingInfo>();
			if (Info.UseCell && buildingInfo == null)
				throw new Exception("The Actor " + self.Info.Name + " need BuildingInfo to spawn actors on death by cell");

			valued = self.Info.TraitInfoOrDefault<ValuedInfo>();
			if (Info.SpawnCountCalculateAsValue)
			{
				// if (valued == null)
				//	throw new Exception("SpawnActorsOnDeath need value when use SpawnCountCalculateAsValue");

				var csv = self.Info.TraitInfoOrDefault<CustomSellValueInfo>();
				var cost = csv?.Value ?? valued?.Cost ?? 0;
				dudesValue = Info.ValuePercent * cost / 100;
			}

		}

		void INotifyKilled.Killed(Actor self, AttackInfo e)
		{
			if (!enabled || IsTraitDisabled || !self.IsInWorld)
				return;

			if (Info.DeathType != null && !e.Damage.DamageTypes.Contains(Info.DeathType))
				return;

			attackingPlayer = e.Attacker.Owner;
		}

		// Don't add the new actor to the world before all RemovedFromWorld callbacks have run
		void INotifyRemovedFromWorld.RemovedFromWorld(Actor self)
		{
			if (attackingPlayer == null)
				return;

			var defeated = self.Owner.WinState == WinState.Lost;
			if (defeated && !Info.SpawnAfterDefeat)
				return;

			var td = new TypeDictionary
			{
				new ParentActorInit(self),
				// new LocationInit(self.Location + Info.Offset),
				// new CenterPositionInit(self.CenterPosition),
				new FactionInit(faction)
			};

			if (self.EffectiveOwner != null && self.EffectiveOwner.Disguised)
				td.Add(new EffectiveOwnerInit(self.EffectiveOwner.Owner));
			else if (Info.EffectiveOwnerFromOwner)
				td.Add(new EffectiveOwnerInit(self.Owner));

			if (Info.OwnerType == OwnerType.Victim)
			{
				// Fall back to InternalOwner if the Victim was defeated,
				// but only if InternalOwner is defined
				if (!defeated || string.IsNullOrEmpty(Info.InternalOwner))
					td.Add(new OwnerInit(self.Owner));
				else
				{
					td.Add(new OwnerInit(self.World.Players.First(p => p.InternalName == Info.InternalOwner)));
					if (!td.Contains<EffectiveOwnerInit>())
						td.Add(new EffectiveOwnerInit(self.Owner));
				}
			}
			else if (Info.OwnerType == OwnerType.Killer)
				td.Add(new OwnerInit(attackingPlayer));
			else
				td.Add(new OwnerInit(self.World.Players.First(p => p.InternalName == Info.InternalOwner)));

			if (Info.SkipMakeAnimations)
				td.Add(new SkipMakeAnimsInit());

			foreach (var modifier in self.TraitsImplementing<IDeathActorInitModifier>())
				modifier.ModifyDeathActorInit(self, td);

			var huskActor = self.TraitsImplementing<IHuskModifier>()
				.Select(ihm => ihm.HuskActor(self))
				.FirstOrDefault(a => a != null);

			var selfloc = self.Location;
			var selfpos = self.CenterPosition;
			self.World.AddFrameEndTask(w =>
			{
				if (Info.UseCell && buildingInfo != null)
				{
					var eligibleLocations = buildingInfo.Tiles(self.Location).ToList();
					if (eligibleLocations.Count == 0)
						return;

					foreach (var a in Info.ActorTypes)
					{
						// Console.WriteLine(self.Info.Name + self.ActorID + " death dudesValue: " + dudesValue);
						var ac = self.World.Map.Rules.Actors[a].TraitInfoOrDefault<ValuedInfo>();
						var at = ac?.Cost ?? 0;
						if ((Info.SpawnCountCalculateAsValue && at > dudesValue) || self.World.SharedRandom.Next(0, 100) > Info.Probability)
							continue;
						var loc = eligibleLocations.Random(self.World.SharedRandom);
						eligibleLocations.Remove(loc);
						if (eligibleLocations.Count == 0)
							eligibleLocations = buildingInfo.Tiles(self.Location).ToList();
						dudesValue -= at;
						var locinit = new LocationInit(loc);
						var pos = self.World.Map.CenterOfCell(loc);
						var posinit = new CenterPositionInit(pos);

						td.Add(locinit);
						td.Add(posinit);
						w.CreateActor(huskActor ?? a, td);
						td.Remove(locinit);
						td.Remove(posinit);
						// Console.WriteLine(self.Info.Name + self.ActorID + " death spawn: " + a + " at " + loc + " posinit: " + pos);

					}
				}
				else
				{
					td.Add(new LocationInit(selfloc));
					td.Add(new CenterPositionInit(selfpos));

					foreach (var a in Info.ActorTypes)
					{
						var ac = self.World.Map.Rules.Actors[a].TraitInfoOrDefault<ValuedInfo>();
						var at = ac?.Cost ?? 0;
						if ((Info.SpawnCountCalculateAsValue && at > dudesValue) || self.World.SharedRandom.Next(0, 100) > Info.Probability)
							continue;
						dudesValue -= at;

						w.CreateActor(huskActor ?? a, td);
					}
				}

			});
		}
	}
}
