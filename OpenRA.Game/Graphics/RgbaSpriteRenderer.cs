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
using System.Numerics;
using OpenRA.Primitives;

namespace OpenRA.Graphics
{
	public class RgbaSpriteRenderer
	{
		public readonly SpriteRenderer Parent;

		public RgbaSpriteRenderer(SpriteRenderer parent)
		{
			this.Parent = parent;
		}

		public void DrawSprite(Sprite s, in float3 location, in float3 scale, float rotation = 0f)
		{
			if (s.Channel != TextureChannel.RGBA)
				throw new InvalidOperationException("DrawRGBASprite requires a RGBA sprite.");

			Parent.DrawSprite(s, 0, location, scale, rotation);
		}

		public void DrawSprite(Sprite s, in float3 location, float scale = 1f, float rotation = 0f)
		{
			if (s.Channel != TextureChannel.RGBA)
				throw new InvalidOperationException("DrawRGBASprite requires a RGBA sprite.");

			Parent.DrawSprite(s, 0, location, scale, rotation);
		}

		public void DrawCardSprite(Sprite s, in WPos wpos, in Vector3 offset, float scale, in float3 tint, float alpha, float rotation = 0f)
		{
			if (s.Channel != TextureChannel.RGBA)
				throw new InvalidOperationException("DrawRGBASprite requires a RGBA sprite.");

			Parent.DrawCardSprite(s, 0, wpos, offset, scale, tint, alpha, rotation);
		}

		public void DrawSprite(Sprite s, in float3 location, float scale, in float3 tint, float alpha, float rotation = 0f)
		{
			if (s.Channel != TextureChannel.RGBA)
				throw new InvalidOperationException("DrawRGBASprite requires a RGBA sprite.");

			Parent.DrawSprite(s, 0, location, scale, tint, alpha, rotation);
		}

		public void DrawSprite(Sprite s, in float3 a, in float3 b, in float3 c, in float3 d, in float3 tint, float alpha)
		{
			if (s.Channel != TextureChannel.RGBA)
				throw new InvalidOperationException("DrawRGBASprite requires a RGBA sprite.");

			Parent.DrawSprite(s, 0, a, b, c, d, tint, alpha);
		}
	}
}
