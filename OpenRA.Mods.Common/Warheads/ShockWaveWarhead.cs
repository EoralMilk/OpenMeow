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
using OpenRA.GameRules;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Warheads
{
	[Desc("Create a Shockwave Effect.")]
	public class ShockWaveWarhead : Warhead
	{
		public readonly WDist Radius = new WDist(1024);

		public readonly int LifeTime = 13;

		public readonly float Width = 0.1f;

		public override void DoImpact(in Target target, WarheadArgs args)
		{
			if (target.Type == TargetType.Invalid)
				return;
			var firedBy = args.SourceActor;
			var pos = target.CenterPosition;
			firedBy.World.AddFrameEndTask(w => { firedBy.World.Add(new OpenRA.Graphics.ShockWaveEffect(pos, LifeTime, (float)Radius.Length / 256, Width)); });
		}
	}
}
