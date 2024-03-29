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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Primitives;
using TagLib.Riff;

namespace OpenRA.Mods.Common.Graphics
{
	public class ContrailRenderable : IRenderable, IFinalizedRenderable
	{
		const int MaxSmoothLength = 4;

		public int Length => trail.Length;

		readonly World world;
		readonly Color startcolorOuter;
		readonly Color endcolorOuter;
		readonly Color startcolor;
		readonly Color endcolor;
		readonly int zOffset;
		readonly float widthFadeRate;

		// Store trail positions in a circular buffer
		readonly WPos[] trail;
		readonly WDist width;

		readonly bool useInnerOuterColor;

		readonly Animation anim;
		readonly string palette;
		readonly bool flipSprite;

		struct ContrailPart
		{
			public float UVOffsetStart;
			public float UVOffsetEnd;

			public Vector3 Start;
			public Vector3 End;

			public Color StartColor;
			public Color EndColor;
			public Color StartColorOuter;
			public Color EndColorOuter;

			public Vector3 StartUp;
			public Vector3 StartDown;
			public Vector3 EndUp;
			public Vector3 EndDown;
		}

		readonly List<ContrailPart> partToRender = new List<ContrailPart>();

		int next;
		int length;
		readonly int skip;

		public ContrailRenderable(Animation anim, string palette, bool flipSprite, World world, Color startcolor, Color endcolor, WDist width, int length, int skip, int zOffset, float widthFadeRate, BlendMode blendMode)
			: this(anim, palette, flipSprite, world, new WPos[length], width, 0, 0, skip, startcolor, endcolor, zOffset, widthFadeRate, blendMode) { }

		public ContrailRenderable(Animation anim, string palette, bool flipSprite, World world, Color startcolor, Color endcolor, WDist width, int length, int skip, int zOffset, float widthFadeRate, BlendMode blendMode, Color startcolorOuter, Color endcolorOuter)
			: this(anim, palette, flipSprite, world, new WPos[length], width, 0, 0, skip, startcolor, endcolor, zOffset, widthFadeRate, blendMode, startcolorOuter, endcolorOuter) { }

		ContrailRenderable(Animation anim, string palette, bool flipSprite, World world, WPos[] trail, WDist width, int next, int length, int skip, Color startcolor, Color endcolor, int zOffset, float widthFadeRate, BlendMode blendMode)
		{
			this.world = world;
			this.trail = trail;
			this.width = width;
			this.next = next;
			this.length = length;
			this.skip = 0;
			this.startcolor = startcolor;
			this.endcolor = endcolor;
			this.zOffset = zOffset;
			this.widthFadeRate = widthFadeRate;
			this.blendMode = blendMode;
			this.useInnerOuterColor = false;

			if (anim != null)
			{
				this.anim = anim;
				this.palette = palette;
				this.flipSprite = flipSprite;
			}
		}

		ContrailRenderable(Animation anim, string palette, bool flipSprite, World world, WPos[] trail, WDist width, int next, int length, int skip, Color startcolor, Color endcolor, int zOffset, float widthFadeRate, BlendMode blendMode, Color startcolorOuter, Color endcolorOuter)
		{
			this.world = world;
			this.trail = trail;
			this.width = width;
			this.next = next;
			this.length = length;
			this.skip = 0;
			this.startcolor = startcolor;
			this.endcolor = endcolor;
			this.zOffset = zOffset;
			this.widthFadeRate = widthFadeRate;
			this.blendMode = blendMode;
			this.useInnerOuterColor = true;
			this.startcolorOuter = startcolorOuter;
			this.endcolorOuter = endcolorOuter;

			if (anim != null)
			{
				this.anim = anim;
				this.palette = palette;
				this.flipSprite = flipSprite;
			}
		}

		public WPos Pos => trail[Index(next - 1)];
		public int ZOffset => zOffset;
		public bool IsDecoration => true;

		readonly BlendMode blendMode;
		public BlendMode BlendMode => blendMode;

		public IRenderable WithZOffset(int newOffset) {
			if (useInnerOuterColor)
				return new ContrailRenderable(anim, palette, flipSprite, world, (WPos[])trail.Clone(), width, next, length, skip, startcolor, endcolor, newOffset, widthFadeRate, blendMode, startcolorOuter, endcolorOuter);
			else
				return new ContrailRenderable(anim, palette, flipSprite, world, (WPos[])trail.Clone(), width, next, length, skip, startcolor, endcolor, newOffset, widthFadeRate, blendMode);
		}

		public IRenderable OffsetBy(in WVec vec)
		{
			// Lambdas can't use 'in' variables, so capture a copy for later
			var offset = vec;
			if (useInnerOuterColor)
				return new ContrailRenderable(anim, palette, flipSprite, world, trail.Select(pos => pos + offset).ToArray(), width, next, length, skip, startcolor, endcolor, zOffset, widthFadeRate, blendMode, startcolorOuter, endcolorOuter);
			else
				return new ContrailRenderable(anim, palette, flipSprite, world, trail.Select(pos => pos + offset).ToArray(), width, next, length, skip, startcolor, endcolor, zOffset, widthFadeRate, blendMode);
		}

		public IRenderable AsDecoration() { return this; }

		public IFinalizedRenderable PrepareRender(WorldRenderer wr) { return this; }
		public void Render(WorldRenderer wr)
		{
			var palette = wr.Palette(this.palette);

			var w3dr = Game.Renderer.World3DRenderer;
			var cam = w3dr.InverseCameraFront;

			// Note: The length of contrail is now actually the number of the points to draw the contrail
			// and we require at least two points to draw a tail
			var renderLength = length - skip;
			if (renderLength <= 1)
				return;

			var screenWidth = (float)width.Length / World3DCoordinate.WDistPerMeter;
			var wcr = Game.Renderer.WorldRgbaColorRenderer;

			// Start of the first line segment is the tail of the list - don't smooth it.
			var curPos = trail[Index(next - skip - 1)];

			var curColor = startcolor;
			var curColorOuter = startcolorOuter;
			float curUVOffset = 0;

			for (var i = 1; i < renderLength; i++)
			{
				var j = next - skip - 1 - i;

				var nextUVOffset = Math.Clamp(i * 1f / (renderLength - 1), 0, 1);

				var nextColor = Exts.ColorLerp(nextUVOffset, startcolor, endcolor);
				var nextColorOuter = Exts.ColorLerp(nextUVOffset, startcolorOuter, endcolorOuter);

				var nextX = 0L;
				var nextY = 0L;
				var nextZ = 0L;
				var k = 0;
				for (; k < renderLength - i && k < MaxSmoothLength; k++)
				{
					var prepos = trail[Index(j - k)];
					nextX += prepos.X;
					nextY += prepos.Y;
					nextZ += prepos.Z;
				}

				var nextPos = new WPos((int)(nextX / k), (int)(nextY / k), (int)(nextZ / k));

				if (!world.FogObscures(curPos) && !world.FogObscures(nextPos))
				{
					var wfade = (renderLength * 1f - (i - 1) * widthFadeRate) / renderLength;
					if (wfade > 0 && curPos != nextPos)
					{
						Vector3 dir = Vector3.Normalize(w3dr.Get3DRenderVecFromWVec(nextPos - curPos));
						Vector3 cross;
						if (dir == cam)
							cross = Game.Renderer.World3DRenderer.CameraUp;
						else
							cross = Vector3.Normalize(Vector3.Cross(cam, dir));
						var widthOffset = (screenWidth * wfade / 2) * cross;
						var start = w3dr.Get3DRenderPositionFromWPos(curPos);
						var end = w3dr.Get3DRenderPositionFromWPos(nextPos);

						partToRender.Add(new ContrailPart()
						{
							UVOffsetStart = curUVOffset,
							UVOffsetEnd = nextUVOffset,

							Start = start,
							End = end,
							StartUp = start + widthOffset,
							StartDown = start - widthOffset,
							EndUp = end + widthOffset,
							EndDown = end - widthOffset,
							StartColor = curColor,
							EndColor = nextColor,
							StartColorOuter = curColorOuter,
							EndColorOuter = nextColorOuter,
						});
					}
				}

				curPos = nextPos;
				curColor = nextColor;
				curColorOuter = nextColorOuter;
				curUVOffset = nextUVOffset;
			}

			for (int i = 0; i < partToRender.Count; i++)
			{
				var startUp = i == 0 ? partToRender[i].StartUp : Vector3.Lerp(partToRender[i - 1].EndUp, partToRender[i].StartUp, 0.5f);
				var startDown = i == 0 ? partToRender[i].StartDown : Vector3.Lerp(partToRender[i - 1].EndDown, partToRender[i].StartDown, 0.5f);
				var endUp = i == partToRender.Count - 1 ? partToRender[i].EndUp : Vector3.Lerp(partToRender[i].EndUp, partToRender[i + 1].StartUp, 0.5f);
				var endDown = i == partToRender.Count - 1 ? partToRender[i].EndDown : Vector3.Lerp(partToRender[i].EndDown, partToRender[i + 1].StartDown, 0.5f);

				if (anim != null)
				{
					var startColor = Color.PremultiplyAlpha(partToRender[i].StartColor);
					var endColor = Color.PremultiplyAlpha(partToRender[i].EndColor);

					var sprite = anim.Image;
					var spriteAlpha = anim.CurrentSequence.GetAlpha(anim.CurrentFrame);
					if (BlendMode == BlendMode.Alpha)
						Game.Renderer.WorldSpriteRenderer.DrawDirectionSprite(sprite, palette,
						new float3(flipSprite ? startUp : endUp), new float3(flipSprite ? startDown : endDown),
						new float3(flipSprite ? endUp : startUp), new float3(flipSprite ? endDown : startDown),
						new float3(startColor.R / 255.0f, startColor.G / 255.0f, startColor.B / 255.0f), startColor.A / 255.0f * spriteAlpha,
						new float3(endColor.R / 255.0f, endColor.G / 255.0f, endColor.B / 255.0f), endColor.A / 255.0f * spriteAlpha
						, flipSprite ? partToRender[i].UVOffsetStart : partToRender[i].UVOffsetEnd,
						flipSprite ? partToRender[i].UVOffsetEnd : partToRender[i].UVOffsetStart);
					else
						Game.Renderer.WorldSpriteRenderer.DrawDirectionSprite(sprite, palette,
						new float3(flipSprite ? startUp : endUp), new float3(flipSprite ? startDown : endDown),
						new float3(flipSprite ? endUp : startUp), new float3(flipSprite ? endDown : startDown),
						spriteAlpha * new float3(startColor.R / 255.0f, startColor.G / 255.0f, startColor.B / 255.0f), startColor.A / 255.0f * spriteAlpha,
						spriteAlpha * new float3(endColor.R / 255.0f, endColor.G / 255.0f, endColor.B / 255.0f), endColor.A / 255.0f * spriteAlpha
						, flipSprite ? partToRender[i].UVOffsetStart : partToRender[i].UVOffsetEnd,
						flipSprite ? partToRender[i].UVOffsetEnd : partToRender[i].UVOffsetStart);
				}
				else if (useInnerOuterColor)
					wcr.DrawWorldLine(World3DCoordinate.Vec3toFloat3(startUp),
																	World3DCoordinate.Vec3toFloat3(partToRender[i].Start),
																	World3DCoordinate.Vec3toFloat3(startDown),
																	World3DCoordinate.Vec3toFloat3(endUp),
																	World3DCoordinate.Vec3toFloat3(partToRender[i].End),
																	World3DCoordinate.Vec3toFloat3(endDown),
																	partToRender[i].StartColor,
																	partToRender[i].EndColor,
																	partToRender[i].StartColorOuter,
																	partToRender[i].EndColorOuter,
																	blendMode);
				else
					wcr.DrawWorldLine(World3DCoordinate.Vec3toFloat3(startUp),
													World3DCoordinate.Vec3toFloat3(startDown),
													World3DCoordinate.Vec3toFloat3(endUp),
													World3DCoordinate.Vec3toFloat3(endDown),
													partToRender[i].StartColor,
													partToRender[i].EndColor,
													blendMode);

			}

			partToRender.Clear();
		}

		public void RenderDebugGeometry(WorldRenderer wr) { }
		public Rectangle ScreenBounds(WorldRenderer wr) { return Rectangle.Empty; }

		// Array index modulo length
		int Index(int i)
		{
			var j = i % trail.Length;
			return j < 0 ? j + trail.Length : j;
		}

		public void Update(WPos pos)
		{
			trail[next] = pos;
			next = Index(next + 1);
			anim?.Tick();

			if (length < trail.Length)
				length++;
		}
	}
}
