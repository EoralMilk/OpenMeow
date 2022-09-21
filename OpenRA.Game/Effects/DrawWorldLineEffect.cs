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
using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Primitives;
using OpenRA.Primitives.FixPoint;

namespace OpenRA.Effects
{
	public class DrawWorldLineEffect : IEffect
	{
		readonly World world;
		readonly Color color;
		readonly WDist width;
		readonly float3 start, end;
		readonly WPos pos;
		int delay;
		int duration;
		List<IRenderable> renderables = new List<IRenderable>();

		// Facing is last on these overloads partially for backwards compatibility with previous main ctor revision
		// and partially because most effects don't need it. The latter is also the reason for placement of 'delay'.
		public DrawWorldLineEffect(World world, WPos start, WPos end, WDist width, Color color, int duration)
			: this(world, start, end, width, color, duration, 0) { }

		public DrawWorldLineEffect(World world, WPos start, WPos end, WDist width, Color color, int duration, int delay = 0)
			: this(world, World3DCoordinate.WPosToFloat3(start), World3DCoordinate.WPosToFloat3(end), width, color, duration, delay) { }

		public DrawWorldLineEffect(World world, float3 start, float3 end, WDist width, Color color, int duration, int delay = 0)
		{
			this.world = world;
			this.start = start;
			this.end = end;
			this.color = color;
			this.duration = duration;
			this.width = width;
			this.delay = delay;
			pos = World3DCoordinate.Float3ToWPos(start);
		}

		public DrawWorldLineEffect(World world, WPos pos, float3 start, float3 end, WDist width, Color color, int duration, int delay = 0)
		{
			this.world = world;
			this.start = start;
			this.end = end;
			this.color = color;
			this.duration = duration;
			this.width = width;
			this.delay = delay;
			this.pos = pos;
		}

		public void Tick(World world)
		{
			delay--;
		}

		public IEnumerable<IRenderable> Render(WorldRenderer wr)
		{
			if (delay > 0 || duration-- < 0)
				return Array.Empty<IRenderable>();

			renderables.Clear();
			renderables.Add(new DebugLineRenderable(pos, 0, start, end, width, color, BlendMode.None));
			return renderables;
		}
	}
}
