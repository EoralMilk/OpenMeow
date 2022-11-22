#region Copyright & License Information
/*
 * Copyright 2015- OpenRA.Mods.AS Developers (see AUTHORS)
 * This file is a part of a third-party plugin for OpenRA, which is
 * free software. It is made available to you under the terms of the
 * GNU General Public License as published by the Free Software
 * Foundation. For more information, see COPYING.
 */
#endregion

using System;
using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Flags]
	public enum DeployTriggers
	{
		None = 0,
		Attack = 1,
		Damage = 2,
		Heal = 4,
		Periodically = 8,
		BecomingIdle = 16
	}

	[Desc("Allow this actor to automatically issue deploy orders on selected events. Require the AutoDeployManager trait on the palyer actor.")]
	public class AutoDeployerInfo : ConditionalTraitInfo
	{
		[Desc("Events leading to the actor getting uncloaked. Possible values are: None, Attack, Damage, Heal, Periodically, BecomingIdle.")]
		public readonly DeployTriggers DeployTrigger = DeployTriggers.Attack | DeployTriggers.Damage;

		[Desc("Chance of deploying when the trigger activates.")]
		public readonly int DeployChance = 50;

		[Desc("Delay between two successful deploy orders.")]
		public readonly int DeployTicks = 2500;

		[Desc("Delay to wait for the actor to undeploy (if capable to) after a successful deploy.")]
		public readonly int UndeployTicks = 450;

		public override object Create(ActorInitializer init) { return new AutoDeployer(this); }
	}

	// TO-DO: Pester OpenRA to allow INotifyDeployTrigger to be used for other traits besides WithMakeAnimation. Like this one.
	public class AutoDeployer : ConditionalTrait<AutoDeployerInfo>, INotifyAttack, ITick, INotifyDamage, INotifyCreated, ISync, INotifyOwnerChanged, INotifyDeployComplete, INotifyBecomingIdle
	{
		public const string PrimaryBuildingOrderID = "PrimaryProducer";
		int undeployTicks = -1, deployTicks;
		bool deployed;
		public bool PrimaryBuilding;
		public IIssueDeployOrder[] DeployTraits;
		AutoDeployManager autoDeployManager;

		public AutoDeployer(AutoDeployerInfo info)
			: base(info) { }

		protected override void Created(Actor self)
		{
			DeployTraits = self.TraitsImplementing<IIssueDeployOrder>().ToArray();
			PrimaryBuilding = self.Info.HasTraitInfo<PrimaryBuildingInfo>();
			autoDeployManager = self.Owner.PlayerActor.Trait<AutoDeployManager>();
		}

		void TryDeploy(Actor self)
		{
			if (!Game.IsHost || deployTicks > 0 || autoDeployManager.IsTraitDisabled)
				return;

			autoDeployManager.AddEntry(new TraitPair<AutoDeployer>(self, this));

			deployTicks = Info.DeployTicks;
			undeployTicks = Info.UndeployTicks;
		}

		void Undeploy(Actor self)
		{
			if (!Game.IsHost || autoDeployManager.IsTraitDisabled)
				return;

			autoDeployManager.AddUndeployOrders(new Order("GrantConditionOnDeploy", self, false));
		}

		void INotifyAttack.Attacking(Actor self, in Target target, Armament a, Barrel barrel)
		{
			if (!Game.IsHost || IsTraitDisabled || autoDeployManager.IsTraitDisabled)
				return;

			if (Info.DeployTrigger.HasFlag(DeployTriggers.Attack))
				TryDeploy(self);
		}

		void INotifyAttack.PreparingAttack(Actor self, in Target target, Armament a, Barrel barrel) { }

		void ITick.Tick(Actor self)
		{
			if (!Game.IsHost || IsTraitDisabled || autoDeployManager.IsTraitDisabled)
				return;

			if (deployed)
			{
				if (--undeployTicks < 0)
				{
					Undeploy(self);
					deployed = false;
				}

				return;
			}

			if (--deployTicks < 0 && Info.DeployTrigger.HasFlag(DeployTriggers.Periodically))
				TryDeploy(self);
		}

		void INotifyDamage.Damaged(Actor self, AttackInfo e)
		{
			if (!Game.IsHost || IsTraitDisabled || autoDeployManager.IsTraitDisabled)
				return;

			if (e.Damage.Value > 0 && Info.DeployTrigger.HasFlag(DeployTriggers.Damage))
				TryDeploy(self);

			if (e.Damage.Value < 0 && Info.DeployTrigger.HasFlag(DeployTriggers.Heal))
				TryDeploy(self);
		}

		void INotifyOwnerChanged.OnOwnerChanged(Actor self, Player oldOwner, Player newOwner)
		{
			autoDeployManager = newOwner.PlayerActor.Trait<AutoDeployManager>();
		}

		void INotifyDeployComplete.FinishedDeploy(Actor self)
		{
			deployed = true;
		}

		void INotifyDeployComplete.FinishedUndeploy(Actor self)
		{
			deployed = false;
		}

		void INotifyBecomingIdle.OnBecomingIdle(Actor self)
		{
			if (!Game.IsHost || IsTraitDisabled || autoDeployManager.IsTraitDisabled)
				return;

			if (Info.DeployTrigger.HasFlag(DeployTriggers.BecomingIdle))
				TryDeploy(self);
		}
	}
}
