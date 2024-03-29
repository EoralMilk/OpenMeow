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

using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Grants a condition while the trait is active.")]
	class GrantConditionOnTimeInfo : ConditionalTraitInfo
	{
		[FieldLoader.Require]
		[GrantedConditionReference]
		[Desc("Condition to grant.")]
		public readonly string Condition = null;

		public readonly int Tick = 100;

		public override object Create(ActorInitializer init) { return new GrantConditionOnTime(this); }
	}

	class GrantConditionOnTime : ConditionalTrait<GrantConditionOnTimeInfo>, ITick
	{
		int conditionToken = Actor.InvalidConditionToken;
		int tick = 0;
		public GrantConditionOnTime(GrantConditionOnTimeInfo info)
			: base(info) { }

		protected override void TraitEnabled(Actor self)
		{
			tick = Info.Tick;
			if (conditionToken == Actor.InvalidConditionToken)
				conditionToken = self.GrantCondition(Info.Condition);
		}

		protected override void TraitDisabled(Actor self)
		{
		}

		public void Tick(Actor self)
		{
			if (tick-- > 0)
			{
				if (conditionToken == Actor.InvalidConditionToken)
					conditionToken = self.GrantCondition(Info.Condition);
			}
			else
			{
				tick = 0;
				if (conditionToken != Actor.InvalidConditionToken)
					conditionToken = self.RevokeCondition(conditionToken);
			}
		}
	}
}
