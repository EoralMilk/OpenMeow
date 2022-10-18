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
	public class Sprite
	{
		public readonly Rectangle Bounds;
		public readonly Sheet Sheet;
		BlendMode blendMode;
		public BlendMode BlendMode => blendMode;
		public readonly TextureChannel Channel;
		public readonly float ZRamp;
		public readonly float3 Size;
		public readonly float3 Offset;
		public readonly float Top, Left, Bottom, Right, TB, LR;

		public readonly SpriteMeshType SpriteMeshType;
		public bool HasMeshCreateInfo;
		public float3 Ssizehalf;
		public float3 Soffset;
		public float2 LeftRight;
		public float2 TopBottom;

		public float3 leftBack;
		public float3 rightBack;
		public float3 leftFront;
		public float3 rightFront;
		public float3 leftTop;
		public float3 rightTop;
		public float3 leftBottom;
		public float3 rightBottom;
		public float3 leftBase;
		public float3 rightBase;

		public void ChangeBlendMode(BlendMode mode)
		{
			blendMode = mode;
		}

		public Sprite(Sheet sheet, Rectangle bounds, TextureChannel channel, float scale = 1, SpriteMeshType spriteMeshType = SpriteMeshType.UI)
			: this(sheet, bounds, 0, float2.Zero, channel, BlendMode.Alpha, scale, spriteMeshType) { }

		public Sprite(Sheet sheet, Rectangle bounds, float zRamp, in float3 offset, TextureChannel channel, BlendMode blendMode = BlendMode.Alpha, float scale = 1f, SpriteMeshType spriteMeshType = SpriteMeshType.UI)
		{
			Sheet = sheet;
			Bounds = bounds;
			Offset = offset;
			ZRamp = zRamp;
			Channel = channel;
			Size = scale * new float3(bounds.Size.Width, bounds.Size.Height, bounds.Size.Height * zRamp);
			this.blendMode = blendMode;
			SpriteMeshType = spriteMeshType;

			// Some GPUs suffer from precision issues when rendering into non 1:1 framebuffers that result
			// in rendering a line of texels that sample outside the sprite rectangle.
			// Insetting the texture coordinates by a small fraction of a pixel avoids this
			// with negligible impact on the 1:1 rendering case.
			var inset = 1 / 128f;
			Left = (Math.Min(bounds.Left, bounds.Right) + inset) / sheet.Size.Width;
			Top = (Math.Min(bounds.Top, bounds.Bottom) + inset) / sheet.Size.Height;
			Right = (Math.Max(bounds.Left, bounds.Right) - inset) / sheet.Size.Width;
			Bottom = (Math.Max(bounds.Top, bounds.Bottom) - inset) / sheet.Size.Height;
			TB = Bottom - Top;
			LR = Right - Left;
			UpdateMeshInfo();
		}

		public bool UpdateMeshInfo()
		{
			if (Game.Renderer != null && Game.Renderer.World3DRenderer != null)
			{
				HasMeshCreateInfo = true;

				Ssizehalf = Game.Renderer.World3DRenderer.MeterPerPix * Size / 2;
				Soffset = Game.Renderer.World3DRenderer.MeterPerPix * Offset;

				LeftRight = new float2(-(Soffset.X - Ssizehalf.X), -(Soffset.X + Ssizehalf.X));
				TopBottom = new float2(Ssizehalf.Y - Soffset.Y, Soffset.Y + Ssizehalf.Y);

				if (SpriteMeshType == SpriteMeshType.Plane || (SpriteMeshType == SpriteMeshType.Card && TopBottom.X < 0))
				{
					leftBack = new float3(LeftRight.X, -TopBottom.X / Game.Renderer.World3DRenderer.CosCameraPitch, 0);
					rightBack = new float3(LeftRight.Y, leftBack.Y, 0);
					leftFront = new float3(leftBack.X, TopBottom.Y / Game.Renderer.World3DRenderer.CosCameraPitch, 0);
					rightFront = new float3(rightBack.X, leftFront.Y, 0);
				}
				else if (SpriteMeshType == SpriteMeshType.Board || (SpriteMeshType == SpriteMeshType.Card && TopBottom.Y < 0))
				{
					leftTop = new float3(LeftRight.X, 0, TopBottom.X / Game.Renderer.World3DRenderer.SinCameraPitch);
					rightTop = new float3(LeftRight.Y, 0, leftTop.Z);
					leftBottom = new float3(leftTop.X, 0, -TopBottom.Y / Game.Renderer.World3DRenderer.SinCameraPitch);
					rightBottom = new float3(rightTop.X, 0, leftBottom.Z);
				}
				else if (SpriteMeshType == SpriteMeshType.Card || SpriteMeshType == SpriteMeshType.FloatBoard)
				{
					leftTop = new float3(LeftRight.X, 0, TopBottom.X / Game.Renderer.World3DRenderer.SinCameraPitch);
					rightTop = new float3(LeftRight.Y, 0, leftTop.Z);
					leftBase = new float3(leftTop.X, 0, 0);
					rightBase = new float3(rightTop.X, 0, 0);
					leftFront = new float3(leftTop.X, TopBottom.Y / Game.Renderer.World3DRenderer.CosCameraPitch, 0);
					rightFront = new float3(rightTop.X, leftFront.Y, 0);
				}
				else
				{
					// ...
				}

			}
			else
			{
				HasMeshCreateInfo = false;
			}

			return HasMeshCreateInfo;
		}
	}

	public class SpriteWithSecondaryData : Sprite
	{
		public readonly Sheet SecondarySheet;
		public readonly Rectangle SecondaryBounds;
		public readonly TextureChannel SecondaryChannel;
		public readonly float SecondaryTop, SecondaryLeft, SecondaryBottom, SecondaryRight;

		public SpriteWithSecondaryData(Sprite s, Sheet secondarySheet, Rectangle secondaryBounds, TextureChannel secondaryChannel)
			: base(s.Sheet, s.Bounds, s.ZRamp, s.Offset, s.Channel, s.BlendMode, spriteMeshType: s.SpriteMeshType)
		{
			SecondarySheet = secondarySheet;
			SecondaryBounds = secondaryBounds;
			SecondaryChannel = secondaryChannel;
			SecondaryLeft = (float)Math.Min(secondaryBounds.Left, secondaryBounds.Right) / s.Sheet.Size.Width;
			SecondaryTop = (float)Math.Min(secondaryBounds.Top, secondaryBounds.Bottom) / s.Sheet.Size.Height;
			SecondaryRight = (float)Math.Max(secondaryBounds.Left, secondaryBounds.Right) / s.Sheet.Size.Width;
			SecondaryBottom = (float)Math.Max(secondaryBounds.Top, secondaryBounds.Bottom) / s.Sheet.Size.Height;
		}
	}

	public enum TextureChannel : byte
	{
		Red = 0,
		Green = 1,
		Blue = 2,
		Alpha = 3,
		RGBA = 4
	}
}
