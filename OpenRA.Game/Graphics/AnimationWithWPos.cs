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
using OpenRA.Primitives;

namespace OpenRA.Graphics
{
	public class AnimationWithWPos
	{
		public readonly Animation Animation;
		public readonly Func<WPos> PosFunc;
		public readonly Func<bool> DisableFunc;
		public readonly Func<WPos, int> ZOffset;

		public AnimationWithWPos(Animation a, Func<WPos> pos, Func<bool> disable)
			: this(a, pos, disable, null) { }

		public AnimationWithWPos(Animation a, Func<WPos> pos, Func<bool> disable, int zOffset)
			: this(a, pos, disable, _ => zOffset) { }

		public AnimationWithWPos(Animation a, Func<WPos> pos, Func<bool> disable, Func<WPos, int> zOffset)
		{
			Animation = a;
			PosFunc = pos;
			DisableFunc = disable;
			ZOffset = zOffset;
		}

		public IRenderable[] Render(Actor self, PaletteReference pal)
		{
			var center = PosFunc?.Invoke() ?? self.CenterPosition;

			var z = ZOffset?.Invoke(center) ?? 0;
			return Animation.Render(center, WVec.Zero, z, pal);
		}

		public Rectangle ScreenBounds(Actor self, WorldRenderer wr)
		{
			var center = PosFunc?.Invoke() ?? self.CenterPosition;

			return Animation.ScreenBounds(wr, center, WVec.Zero);
		}

		public static implicit operator AnimationWithWPos(Animation a)
		{
			return new AnimationWithWPos(a, null, null, null);
		}
	}
}
