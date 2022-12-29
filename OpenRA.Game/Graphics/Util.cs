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
using OpenRA.FileFormats;
using OpenRA.Primitives;

namespace OpenRA.Graphics
{
	public static class Util
	{
		// yes, our channel order is nuts.
		static readonly int[] ChannelMasks = { 2, 1, 0, 3 };

		// Meow TODO: use the "rotation" on our graphic 
		public static int FastCreateCard(Vertex[] vertices,
			in WPos inPos, in Vector3 viewOffset,
			Sprite r, int2 samplers, float paletteTextureIndex, float scale,
			in float3 tint, float alpha, int nv, float rotation = 0f)
		{
			if (r.HasMeshCreateInfo)
			{
				if (!r.UpdateMeshInfo())
					throw new Exception("invalide create mesh time: sprite has not create mesh data");
			}

			if (r.SpriteMeshType != SpriteMeshType.Card)
			{
				throw new Exception("sprite's mesh type is not card");
			}

			if (scale < 0)
			{
				throw new Exception("invalide create mesh scale: only positve value supported");
			}

			float3 ssziehalf = scale * r.Ssizehalf;
			float3 soffset = scale * r.Soffset;
			float2 leftRight = scale * r.LeftRight;
			float2 topBottom = scale * r.TopBottom; // In general, both top and bottom are positive

			var position = Game.Renderer.World3DRenderer.Get3DRenderPositionFromWPos(inPos);
			position += viewOffset;

			// sprite only has horizental part
			if (topBottom.X < 0)
			{
				float3 leftBack = new float3(position.X + scale * r.leftBack.X, position.Y + scale * r.leftBack.Y, position.Z);
				float3 rightBack = new float3(position.X + leftRight.Y, leftBack.Y, position.Z);
				float3 leftFront = new float3(leftBack.X, position.Y + scale * r.leftFront.Y, position.Z);
				float3 rightFront = new float3(rightBack.X, leftFront.Y, position.Z);

				float sl = 0;
				float st = 0;
				float sr = 0;
				float sb = 0;

				// See combined.vert for documentation on the channel attribute format
				var attribC = r.Channel == TextureChannel.RGBA ? 0x02 : ((byte)r.Channel) << 1 | 0x01;
				attribC |= samplers.X << 6;
				if (r is SpriteWithSecondaryData ss)
				{
					sl = ss.SecondaryLeft;
					st = ss.SecondaryTop;
					sr = ss.SecondaryRight;
					sb = ss.SecondaryBottom;

					attribC |= ((byte)ss.SecondaryChannel) << 4 | 0x08;
					attribC |= samplers.Y << 9;
				}

				var fAttribC = (float)attribC;

				vertices[nv] = new Vertex(leftBack, r.Left, r.Top, sl, st, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[nv + 1] = new Vertex(rightBack, r.Right, r.Top, sr, st, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[nv + 2] = new Vertex(rightFront, r.Right, r.Bottom, sr, sb, paletteTextureIndex, fAttribC, tint, alpha);

				vertices[nv + 3] = new Vertex(rightFront, r.Right, r.Bottom, sr, sb, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[nv + 4] = new Vertex(leftFront, r.Left, r.Bottom, sl, sb, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[nv + 5] = new Vertex(leftBack, r.Left, r.Top, sl, st, paletteTextureIndex, fAttribC, tint, alpha);

				return 6;
			}
			else if (topBottom.Y < 0)
			{
				// sprite only has vertical part
				float3 leftTop = new float3(position.X + scale * r.leftTop.X, position.Y, position.Z + scale * r.leftTop.Z);
				float3 rightTop = new float3(position.X + leftRight.Y, position.Y, leftTop.Z);
				float3 leftBottom = new float3(leftTop.X, position.Y, position.Z + scale * r.leftBottom.Z);
				float3 rightBottom = new float3(rightTop.X, position.Y, leftBottom.Z);

				float sl = 0;
				float st = 0;
				float sr = 0;
				float sb = 0;

				// See combined.vert for documentation on the channel attribute format
				var attribC = r.Channel == TextureChannel.RGBA ? 0x02 : ((byte)r.Channel) << 1 | 0x01;
				attribC |= samplers.X << 6;
				if (r is SpriteWithSecondaryData ss)
				{
					sl = ss.SecondaryLeft;
					st = ss.SecondaryTop;
					sr = ss.SecondaryRight;
					sb = ss.SecondaryBottom;

					attribC |= ((byte)ss.SecondaryChannel) << 4 | 0x08;
					attribC |= samplers.Y << 9;
				}

				var fAttribC = (float)attribC;

				vertices[nv] = new Vertex(leftTop, r.Left, r.Top, sl, st, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[nv + 1] = new Vertex(rightTop, r.Right, r.Top, sr, st, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[nv + 2] = new Vertex(rightBottom, r.Right, r.Bottom, sr, sb, paletteTextureIndex, fAttribC, tint, alpha);

				vertices[nv + 3] = new Vertex(rightBottom, r.Right, r.Bottom, sr, sb, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[nv + 4] = new Vertex(leftBottom, r.Left, r.Bottom, sl, sb, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[nv + 5] = new Vertex(leftTop, r.Left, r.Top, sl, st, paletteTextureIndex, fAttribC, tint, alpha);

				return 6;
			}
			else
			{
				float3 leftTop = new float3(position.X + scale * r.leftTop.X, position.Y, position.Z + scale * r.leftTop.Z);
				float3 rightTop = new float3(position.X + leftRight.Y, position.Y, leftTop.Z);
				float3 leftBase = new float3(leftTop.X, position.Y, position.Z);
				float3 rightBase = new float3(rightTop.X, position.Y, position.Z);
				float3 leftFront = new float3(leftTop.X, position.Y + scale * r.leftFront.Y, position.Z);
				float3 rightFront = new float3(rightTop.X, leftFront.Y, position.Z);

				float ycut = topBottom.X / (ssziehalf.Y * 2);

				float sl = 0;
				float st = 0;
				float sbase = 0;
				float sr = 0;
				float sb = 0;

				// See combined.vert for documentation on the channel attribute format
				var attribC = r.Channel == TextureChannel.RGBA ? 0x02 : ((byte)r.Channel) << 1 | 0x01;
				attribC |= samplers.X << 6;
				if (r is SpriteWithSecondaryData ss)
				{
					sl = ss.SecondaryLeft;
					st = ss.SecondaryTop;
					sr = ss.SecondaryRight;
					sb = ss.SecondaryBottom;

					sbase = st - (st - sb) * ycut;

					attribC |= ((byte)ss.SecondaryChannel) << 4 | 0x08;
					attribC |= samplers.Y << 9;
				}

				var fAttribC = (float)attribC;
				float baseY = r.Top - (r.Top - r.Bottom) * ycut;

				vertices[nv] = new Vertex(leftTop, r.Left, r.Top, sl, st, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[nv + 1] = new Vertex(rightTop, r.Right, r.Top, sr, st, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[nv + 2] = new Vertex(rightBase, r.Right, baseY, sr, sbase, paletteTextureIndex, fAttribC, tint, alpha);

				vertices[nv + 3] = new Vertex(rightBase, r.Right, baseY, sr, sbase, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[nv + 4] = new Vertex(leftBase, r.Left, baseY, sl, sbase, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[nv + 5] = new Vertex(leftTop, r.Left, r.Top, sl, st, paletteTextureIndex, fAttribC, tint, alpha);

				vertices[nv + 6] = new Vertex(leftBase, r.Left, baseY, sl, sbase, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[nv + 7] = new Vertex(rightBase, r.Right, baseY, sr, sbase, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[nv + 8] = new Vertex(rightFront, r.Right, r.Bottom, sr, sb, paletteTextureIndex, fAttribC, tint, alpha);

				vertices[nv + 9] = new Vertex(rightFront, r.Right, r.Bottom, sr, sb, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[nv + 10] = new Vertex(leftFront, r.Left, r.Bottom, sl, sb, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[nv + 11] = new Vertex(leftBase, r.Left, baseY, sl, sbase, paletteTextureIndex, fAttribC, tint, alpha);
				return 12;
			}
		}

		public static int FastCreateFloatBoard(Vertex[] vertices,
			in WPos inPos, in Vector3 viewOffset,
			Sprite r, int2 samplers, float paletteTextureIndex, float scale,
			in float3 tint, float alpha, int nv, float rotation = 0f)
		{
			if (r.HasMeshCreateInfo)
			{
				if (!r.UpdateMeshInfo())
					throw new Exception("invalide create mesh time: sprite has not create mesh data");
			}

			if (r.SpriteMeshType != SpriteMeshType.FloatBoard)
			{
				throw new Exception("sprite's mesh type is not card");
			}

			if (scale < 0)
			{
				throw new Exception("invalide create mesh scale: only positve value supported");
			}

			float3 ssziehalf = scale * r.Ssizehalf;
			float3 soffset = scale * r.Soffset;
			float2 leftRight = scale * r.LeftRight;
			float2 topBottom = scale * r.TopBottom; // In general, both top and bottom are positive

			var position = Game.Renderer.World3DRenderer.Get3DRenderPositionFromWPos(inPos);
			position += viewOffset;

			// sprite only has horizental part
			if (topBottom.X < 0)
			{
				float3 leftBack = new float3(position.X + scale * r.leftBack.X, position.Y + scale * r.leftBack.Y, position.Z);
				float3 rightBack = new float3(position.X + leftRight.Y, leftBack.Y, position.Z);
				float3 leftFront = new float3(leftBack.X, position.Y + scale * r.leftFront.Y, position.Z);
				float3 rightFront = new float3(rightBack.X, leftFront.Y, position.Z);

				float sl = 0;
				float st = 0;
				float sr = 0;
				float sb = 0;

				// See combined.vert for documentation on the channel attribute format
				var attribC = r.Channel == TextureChannel.RGBA ? 0x02 : ((byte)r.Channel) << 1 | 0x01;
				attribC |= samplers.X << 6;
				if (r is SpriteWithSecondaryData ss)
				{
					sl = ss.SecondaryLeft;
					st = ss.SecondaryTop;
					sr = ss.SecondaryRight;
					sb = ss.SecondaryBottom;

					attribC |= ((byte)ss.SecondaryChannel) << 4 | 0x08;
					attribC |= samplers.Y << 9;
				}

				var fAttribC = (float)attribC;

				vertices[nv] = new Vertex(leftBack, r.Left, r.Top, sl, st, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[nv + 1] = new Vertex(rightBack, r.Right, r.Top, sr, st, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[nv + 2] = new Vertex(rightFront, r.Right, r.Bottom, sr, sb, paletteTextureIndex, fAttribC, tint, alpha);

				vertices[nv + 3] = new Vertex(rightFront, r.Right, r.Bottom, sr, sb, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[nv + 4] = new Vertex(leftFront, r.Left, r.Bottom, sl, sb, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[nv + 5] = new Vertex(leftBack, r.Left, r.Top, sl, st, paletteTextureIndex, fAttribC, tint, alpha);

				return 6;
			}
			else if (topBottom.Y < 0) // sprite only has vertical part
			{
				//float3 leftTop = new float3(position.X + leftRight.X, position.Y, position.Z + (topBottom.X) / Game.Renderer.World3DRenderer.SinCameraPitch);
				//float3 rightTop = new float3(position.X + leftRight.Y, position.Y, leftTop.Z);
				//float3 leftBottom = new float3(leftTop.X, position.Y, position.Z - (topBottom.Y) / Game.Renderer.World3DRenderer.SinCameraPitch);
				//float3 rightBottom = new float3(rightTop.X, position.Y, leftBottom.Z);

				float3 leftTop = new float3(position.X + scale * r.leftTop.X, position.Y, position.Z + scale * r.leftTop.Z);
				float3 rightTop = new float3(position.X + leftRight.Y, position.Y, leftTop.Z);
				float3 leftBottom = new float3(leftTop.X, position.Y, position.Z + scale * r.leftBottom.Z);
				float3 rightBottom = new float3(rightTop.X, position.Y, leftBottom.Z);

				float sl = 0;
				float st = 0;
				float sr = 0;
				float sb = 0;

				// See combined.vert for documentation on the channel attribute format
				var attribC = r.Channel == TextureChannel.RGBA ? 0x02 : ((byte)r.Channel) << 1 | 0x01;
				attribC |= samplers.X << 6;
				if (r is SpriteWithSecondaryData ss)
				{
					sl = ss.SecondaryLeft;
					st = ss.SecondaryTop;
					sr = ss.SecondaryRight;
					sb = ss.SecondaryBottom;

					attribC |= ((byte)ss.SecondaryChannel) << 4 | 0x08;
					attribC |= samplers.Y << 9;
				}

				var fAttribC = (float)attribC;

				vertices[nv] = new Vertex(leftTop, r.Left, r.Top, sl, st, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[nv + 1] = new Vertex(rightTop, r.Right, r.Top, sr, st, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[nv + 2] = new Vertex(rightBottom, r.Right, r.Bottom, sr, sb, paletteTextureIndex, fAttribC, tint, alpha);

				vertices[nv + 3] = new Vertex(rightBottom, r.Right, r.Bottom, sr, sb, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[nv + 4] = new Vertex(leftBottom, r.Left, r.Bottom, sl, sb, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[nv + 5] = new Vertex(leftTop, r.Left, r.Top, sl, st, paletteTextureIndex, fAttribC, tint, alpha);

				return 6;
			}
			else
			{
				float3 leftTop = new float3(position.X + scale * r.leftTop.X, position.Y, position.Z + scale * r.leftTop.Z);
				float3 rightTop = new float3(position.X + leftRight.Y, position.Y, leftTop.Z);
				float3 leftFront = new float3(leftTop.X, position.Y + scale * r.leftFront.Y, position.Z);
				float3 rightFront = new float3(rightTop.X, leftFront.Y, position.Z);

				float sl = 0;
				float st = 0;
				float sr = 0;
				float sb = 0;

				// See combined.vert for documentation on the channel attribute format
				var attribC = r.Channel == TextureChannel.RGBA ? 0x02 : ((byte)r.Channel) << 1 | 0x01;
				attribC |= samplers.X << 6;
				if (r is SpriteWithSecondaryData ss)
				{
					sl = ss.SecondaryLeft;
					st = ss.SecondaryTop;
					sr = ss.SecondaryRight;
					sb = ss.SecondaryBottom;

					attribC |= ((byte)ss.SecondaryChannel) << 4 | 0x08;
					attribC |= samplers.Y << 9;
				}

				var fAttribC = (float)attribC;

				vertices[nv] = new Vertex(leftTop, r.Left, r.Top, sl, st, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[nv + 1] = new Vertex(rightTop, r.Right, r.Top, sr, st, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[nv + 2] = new Vertex(rightFront, r.Right, r.Bottom, sr, sb, paletteTextureIndex, fAttribC, tint, alpha);

				vertices[nv + 3] = new Vertex(leftTop, r.Left, r.Top, sl, st, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[nv + 4] = new Vertex(rightFront, r.Right, r.Bottom, sr, sb, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[nv + 5] = new Vertex(leftFront, r.Left, r.Bottom, sl, sb, paletteTextureIndex, fAttribC, tint, alpha);

				return 6;
			}
		}

		public static void FastCreatePlane(Vertex[] vertices,
			in WPos inPos, in Vector3 viewOffset,
			Sprite r, int2 samplers, float paletteTextureIndex, float scale,
			in float3 tint, float alpha, int nv, float rotation = 0f)
		{
			if (r.HasMeshCreateInfo)
			{
				if (!r.UpdateMeshInfo())
					throw new Exception("invalide create mesh time: sprite has not create mesh data");
			}

			if (r.SpriteMeshType != SpriteMeshType.Plane)
			{
				throw new Exception("sprite's mesh type is not plane");
			}

			if (scale < 0)
			{
				throw new Exception("invalide create mesh scale: only positve value supported");
			}

			float3 ssziehalf = scale * r.Ssizehalf;
			float3 soffset = scale * r.Soffset;
			float2 leftRight = scale * r.LeftRight;
			float2 topBottom = scale * r.TopBottom; // In general, both top and bottom are positive

			var position = Game.Renderer.World3DRenderer.Get3DRenderPositionFromWPos(inPos);
			position += viewOffset;

			float3 leftBack = new float3(position.X + leftRight.X, position.Y + scale * r.leftBack.Y, position.Z);
			float3 rightBack = new float3(position.X + leftRight.Y, leftBack.Y, position.Z);
			float3 leftFront = new float3(leftBack.X, position.Y + scale * r.leftFront.Y, position.Z);
			float3 rightFront = new float3(rightBack.X, leftFront.Y, position.Z);

			float sl = 0;
			float st = 0;
			float sr = 0;
			float sb = 0;

			// See combined.vert for documentation on the channel attribute format
			var attribC = r.Channel == TextureChannel.RGBA ? 0x02 : ((byte)r.Channel) << 1 | 0x01;
			attribC |= samplers.X << 6;
			if (r is SpriteWithSecondaryData ss)
			{
				sl = ss.SecondaryLeft;
				st = ss.SecondaryTop;
				sr = ss.SecondaryRight;
				sb = ss.SecondaryBottom;

				attribC |= ((byte)ss.SecondaryChannel) << 4 | 0x08;
				attribC |= samplers.Y << 9;
			}

			var fAttribC = (float)attribC;

			vertices[nv] = new Vertex(leftBack, r.Left, r.Top, sl, st, paletteTextureIndex, fAttribC, tint, alpha);
			vertices[nv + 1] = new Vertex(rightBack, r.Right, r.Top, sr, st, paletteTextureIndex, fAttribC, tint, alpha);
			vertices[nv + 2] = new Vertex(rightFront, r.Right, r.Bottom, sr, sb, paletteTextureIndex, fAttribC, tint, alpha);

			vertices[nv + 3] = new Vertex(rightFront, r.Right, r.Bottom, sr, sb, paletteTextureIndex, fAttribC, tint, alpha);
			vertices[nv + 4] = new Vertex(leftFront, r.Left, r.Bottom, sl, sb, paletteTextureIndex, fAttribC, tint, alpha);
			vertices[nv + 5] = new Vertex(leftBack, r.Left, r.Top, sl, st, paletteTextureIndex, fAttribC, tint, alpha);
		}

		public static void FastCreateBoard(Vertex[] vertices,
			in WPos inPos, in Vector3 viewOffset,
			Sprite r, int2 samplers, float paletteTextureIndex,
			float scale, in float3 tint, float alpha, int nv, float rotation = 0f)
		{
			if (r.HasMeshCreateInfo)
			{
				if (!r.UpdateMeshInfo())
					throw new Exception("invalide create mesh time: sprite has not create mesh data");
			}

			if (r.SpriteMeshType != SpriteMeshType.Board)
			{
				throw new Exception("sprite's mesh type is not board");
			}

			if (scale < 0)
			{
				throw new Exception("invalide create mesh scale: only positve value supported");
			}

			float3 ssziehalf = scale * r.Ssizehalf;
			float3 soffset = scale * r.Soffset;
			float2 leftRight = scale * r.LeftRight;
			float2 topBottom = scale * r.TopBottom; // In general, both top and bottom are positive

			var position = Game.Renderer.World3DRenderer.Get3DRenderPositionFromWPos(inPos);
			position += viewOffset;

			float3 leftTop = new float3(position.X + leftRight.X, position.Y, position.Z + scale * r.leftTop.Z);
			float3 rightTop = new float3(position.X + leftRight.Y, position.Y, leftTop.Z);
			float3 leftBottom = new float3(leftTop.X, position.Y, position.Z + scale * r.leftBottom.Z);
			float3 rightBottom = new float3(rightTop.X, position.Y, leftBottom.Z);

			float sl = 0;
			float st = 0;
			float sr = 0;
			float sb = 0;

			// See combined.vert for documentation on the channel attribute format
			var attribC = r.Channel == TextureChannel.RGBA ? 0x02 : ((byte)r.Channel) << 1 | 0x01;
			attribC |= samplers.X << 6;
			if (r is SpriteWithSecondaryData ss)
			{
				sl = ss.SecondaryLeft;
				st = ss.SecondaryTop;
				sr = ss.SecondaryRight;
				sb = ss.SecondaryBottom;

				attribC |= ((byte)ss.SecondaryChannel) << 4 | 0x08;
				attribC |= samplers.Y << 9;
			}

			var fAttribC = (float)attribC;

			vertices[nv] = new Vertex(leftTop, r.Left, r.Top, sl, st, paletteTextureIndex, fAttribC, tint, alpha);
			vertices[nv + 1] = new Vertex(rightTop, r.Right, r.Top, sr, st, paletteTextureIndex, fAttribC, tint, alpha);
			vertices[nv + 2] = new Vertex(rightBottom, r.Right, r.Bottom, sr, sb, paletteTextureIndex, fAttribC, tint, alpha);

			vertices[nv + 3] = new Vertex(rightBottom, r.Right, r.Bottom, sr, sb, paletteTextureIndex, fAttribC, tint, alpha);
			vertices[nv + 4] = new Vertex(leftBottom, r.Left, r.Bottom, sl, sb, paletteTextureIndex, fAttribC, tint, alpha);
			vertices[nv + 5] = new Vertex(leftTop, r.Left, r.Top, sl, st, paletteTextureIndex, fAttribC, tint, alpha);
		}

		public static void FastCreateNormalBoard(Vertex[] vertices,
			in WPos inPos, in Vector3 viewOffset, in Vector3 leftDir, in Vector3 upDir,
			Sprite r, int2 samplers, float paletteTextureIndex,
			float scale, in float3 tint, float alpha, int nv, float rotation = 0f)
		{
			if (r.HasMeshCreateInfo)
			{
				if (!r.UpdateMeshInfo())
					throw new Exception("invalide create mesh time: sprite has not create mesh data");
			}

			if (r.SpriteMeshType != SpriteMeshType.Board)
			{
				throw new Exception("sprite's mesh type is not board");
			}

			float2 leftRight = scale * r.LeftRight;

			var position = Game.Renderer.World3DRenderer.Get3DRenderPositionFromWPos(inPos);
			position += viewOffset;

			var leftTop = position + leftRight.X * leftDir + scale * r.leftTop.Z * upDir;
			var rightBottom = position - leftRight.X * leftDir - scale * r.leftTop.Z * upDir;
			var rightTop = new float3(rightBottom.X, rightBottom.Y, leftTop.Z);
			var leftBottom = new float3(leftTop.X, leftTop.Y, rightBottom.Z);

			float sl = 0;
			float st = 0;
			float sr = 0;
			float sb = 0;

			// See combined.vert for documentation on the channel attribute format
			var attribC = r.Channel == TextureChannel.RGBA ? 0x02 : ((byte)r.Channel) << 1 | 0x01;
			attribC |= samplers.X << 6;
			if (r is SpriteWithSecondaryData ss)
			{
				sl = ss.SecondaryLeft;
				st = ss.SecondaryTop;
				sr = ss.SecondaryRight;
				sb = ss.SecondaryBottom;

				attribC |= ((byte)ss.SecondaryChannel) << 4 | 0x08;
				attribC |= samplers.Y << 9;
			}

			var fAttribC = (float)attribC;

			vertices[nv] = new Vertex(new float3(leftTop), r.Left, r.Top, sl, st, paletteTextureIndex, fAttribC, tint, alpha);
			vertices[nv + 1] = new Vertex(rightTop, r.Right, r.Top, sr, st, paletteTextureIndex, fAttribC, tint, alpha);
			vertices[nv + 2] = new Vertex(new float3(rightBottom), r.Right, r.Bottom, sr, sb, paletteTextureIndex, fAttribC, tint, alpha);

			vertices[nv + 3] = new Vertex(new float3(rightBottom), r.Right, r.Bottom, sr, sb, paletteTextureIndex, fAttribC, tint, alpha);
			vertices[nv + 4] = new Vertex(leftBottom, r.Left, r.Bottom, sl, sb, paletteTextureIndex, fAttribC, tint, alpha);
			vertices[nv + 5] = new Vertex(new float3(leftTop), r.Left, r.Top, sl, st, paletteTextureIndex, fAttribC, tint, alpha);
		}

		public static void FastCreateDirection(Vertex[] vertices,
			in float3 leftTop, in float3 rightTop, in float3 leftBottom, in float3 rightBottom,
			Sprite r, int2 samplers, float paletteTextureIndex,
			in float3 tint, float alpha,
			in float3 endtint, float endalpha,
			int nv,
			float uvOffsetStart = 0, float uvOffsetEnd = 1)
		{
			if (r.HasMeshCreateInfo)
			{
				if (!r.UpdateMeshInfo())
					throw new Exception("invalide create mesh time: sprite has not create mesh data");
			}

			float sl = 0;
			float st = 0;
			float sr = 0;
			float sb = 0;

			// See combined.vert for documentation on the channel attribute format
			var attribC = r.Channel == TextureChannel.RGBA ? 0x02 : ((byte)r.Channel) << 1 | 0x01;
			attribC |= samplers.X << 6;
			if (r is SpriteWithSecondaryData ss)
			{
				sl = ss.SecondaryLeft;
				st = ss.SecondaryTop;
				sr = ss.SecondaryRight;
				sb = ss.SecondaryBottom;

				attribC |= ((byte)ss.SecondaryChannel) << 4 | 0x08;
				attribC |= samplers.Y << 9;
			}

			//var uvLT = new float2(r.Left, r.Top);
			//var uvRT = new float2(r.Right, r.Top);
			//var uvLB = new float2(r.Left, r.Bottom);
			//var uvRB = new float2(r.Right, r.Bottom);

			var stl = r.Left;
			var str = r.Right;
			var stt = r.Top + r.TB * uvOffsetStart;
			var stb = r.Top + r.TB * uvOffsetEnd;

			var fAttribC = (float)attribC;

			vertices[nv] = new Vertex(leftTop, stl, stt, sl, st, paletteTextureIndex, fAttribC, tint, alpha);
			vertices[nv + 1] = new Vertex(rightTop, str, stt, sr, st, paletteTextureIndex, fAttribC, tint, alpha);
			vertices[nv + 2] = new Vertex(rightBottom, str, stb, sr, sb, paletteTextureIndex, fAttribC, endtint, endalpha);

			vertices[nv + 3] = new Vertex(rightBottom, str, stb, sr, sb, paletteTextureIndex, fAttribC, endtint, endalpha);
			vertices[nv + 4] = new Vertex(leftBottom, stl, stb, sl, sb, paletteTextureIndex, fAttribC, endtint, endalpha);
			vertices[nv + 5] = new Vertex(leftTop, stl, stt, sl, st, paletteTextureIndex, fAttribC, tint, alpha);
		}

		static float2 CalUV(WPos vpos, int TLX, int TLY, int width, int height)
		{
			return new float2((float)(vpos.X - TLX) / width, (float)(vpos.Y - TLY) / height);
		}

		public static Vertex[] FastCreateTileOverlay(
			in WPos pos, in float3 viewOffset,
			Sprite r, int2 samplers, float paletteTextureIndex,
			float scale, in float3 tint, float alpha,
			Map map)
		{
			var width = (int)(r.Bounds.Width * Game.Renderer.World3DRenderer.WDistPerPix * scale);

			var height = (int)(r.Bounds.Height * Game.Renderer.World3DRenderer.WDistPerPix * scale);

			// height * 2 for isometric tile
			if (r.SpriteMeshType == SpriteMeshType.TileOverlay)
				 height *= 2;

			var w = width / 2;
			var h = height / 2;
			var TL = new int2((pos.X - w - MapGrid.MapMiniCellWidth) / MapGrid.MapMiniCellWidth + 1,
				(pos.Y - h - MapGrid.MapMiniCellWidth) / MapGrid.MapMiniCellWidth + 1);
			var BR = new int2((pos.X + w + MapGrid.MapMiniCellWidth) / MapGrid.MapMiniCellWidth,
				(pos.Y + h + MapGrid.MapMiniCellWidth) / MapGrid.MapMiniCellWidth);

			// 6 vertex one minicell
			int count = (BR.X - TL.X) * (BR.Y - TL.Y) * 6;
			var TLX = pos.X - w;
			var TLY = pos.Y - h;

			float sl = 0;
			float st = 0;
			float lr = 0;
			float tb = 0;

			Vertex[] vertices = new Vertex[count];

			// See combined.vert for documentation on the channel attribute format
			var attribC = r.Channel == TextureChannel.RGBA ? 0x02 : ((byte)r.Channel) << 1 | 0x01;
			attribC |= samplers.X << 6;
			if (r is SpriteWithSecondaryData ss)
			{
				sl = ss.SecondaryLeft;
				st = ss.SecondaryTop;
				lr = ss.SecondaryRight - sl;
				tb = ss.SecondaryBottom - st;

				attribC |= ((byte)ss.SecondaryChannel) << 4 | 0x08;
				attribC |= samplers.Y << 9;
			}

			var fAttribC = (float)attribC;

			{
				int i = 0;
				for (int y = TL.Y; y < BR.Y; y++)
					for (int x = TL.X; x < BR.X; x++)
					{
						var iLT = Math.Clamp(x + y * map.VertexArrayWidth, 0, map.TerrainVertices.Length - 1);
						var iRT = Math.Clamp(x + 1 + y * map.VertexArrayWidth, 0, map.TerrainVertices.Length - 1);
						var iLB = Math.Clamp(x + (y + 1) * map.VertexArrayWidth, 0, map.TerrainVertices.Length - 1);
						var iRB = Math.Clamp(x + 1 + (y + 1) * map.VertexArrayWidth, 0, map.TerrainVertices.Length - 1);
						var index = iRT;

						MiniCell cell = map.MiniCells[y, x];

						float2 uv;
						if (cell.Type == MiniCellType.TLBR)
						{
							// ------------
							// |  \          |
							// |      \      |
							// |          \  |
							// ------------
							index = iRT;
							uv = CalUV(map.TerrainVertices[index].LogicPos, TLX, TLY, width, height);
							vertices[i] = new Vertex(map.TerrainVertices[index].Pos + viewOffset, r.Left + r.LR * uv.X, r.Top + r.TB * uv.Y, sl + lr * uv.X, st + tb * uv.Y, paletteTextureIndex, fAttribC, uv.X, uv.Y, -2f, alpha);
							index = iLT;
							uv = CalUV(map.TerrainVertices[index].LogicPos, TLX, TLY, width, height);
							vertices[i + 1] = new Vertex(map.TerrainVertices[index].Pos + viewOffset, r.Left + r.LR * uv.X, r.Top + r.TB * uv.Y, sl + lr * uv.X, st + tb * uv.Y, paletteTextureIndex, fAttribC, uv.X, uv.Y, -2f, alpha);
							index = iRB;
							uv = CalUV(map.TerrainVertices[index].LogicPos, TLX, TLY, width, height);
							vertices[i + 2] = new Vertex(map.TerrainVertices[index].Pos + viewOffset, r.Left + r.LR * uv.X, r.Top + r.TB * uv.Y, sl + lr * uv.X, st + tb * uv.Y, paletteTextureIndex, fAttribC, uv.X, uv.Y, -2f, alpha);

							index = iLT;
							uv = CalUV(map.TerrainVertices[index].LogicPos, TLX, TLY, width, height);
							vertices[i + 3] = new Vertex(map.TerrainVertices[index].Pos + viewOffset, r.Left + r.LR * uv.X, r.Top + r.TB * uv.Y, sl + lr * uv.X, st + tb * uv.Y, paletteTextureIndex, fAttribC, uv.X, uv.Y, -2f, alpha);
							index = iLB;
							uv = CalUV(map.TerrainVertices[index].LogicPos, TLX, TLY, width, height);
							vertices[i + 4] = new Vertex(map.TerrainVertices[index].Pos + viewOffset, r.Left + r.LR * uv.X, r.Top + r.TB * uv.Y, sl + lr * uv.X, st + tb * uv.Y, paletteTextureIndex, fAttribC, uv.X, uv.Y, -2f, alpha);
							index = iRB;
							uv = CalUV(map.TerrainVertices[index].LogicPos, TLX, TLY, width, height);
							vertices[i + 5] = new Vertex(map.TerrainVertices[index].Pos + viewOffset, r.Left + r.LR * uv.X, r.Top + r.TB * uv.Y, sl + lr * uv.X, st + tb * uv.Y, paletteTextureIndex, fAttribC, uv.X, uv.Y, -2f, alpha);

						}
						else
						{
							// ------------
							// |           / |
							// |      /      |
							// |  /          |
							// ------------
							index = iRT;
							uv = CalUV(map.TerrainVertices[index].LogicPos, TLX, TLY, width, height);
							vertices[i] = new Vertex(map.TerrainVertices[index].Pos + viewOffset, r.Left + r.LR * uv.X, r.Top + r.TB * uv.Y, sl + lr * uv.X, st + tb * uv.Y, paletteTextureIndex, fAttribC, uv.X, uv.Y, -2f, alpha);
							index = iLT;
							uv = CalUV(map.TerrainVertices[index].LogicPos, TLX, TLY, width, height);
							vertices[i + 1] = new Vertex(map.TerrainVertices[index].Pos + viewOffset, r.Left + r.LR * uv.X, r.Top + r.TB * uv.Y, sl + lr * uv.X, st + tb * uv.Y, paletteTextureIndex, fAttribC, uv.X, uv.Y, -2f, alpha);
							index = iLB;
							uv = CalUV(map.TerrainVertices[index].LogicPos, TLX, TLY, width, height);
							vertices[i + 2] = new Vertex(map.TerrainVertices[index].Pos + viewOffset, r.Left + r.LR * uv.X, r.Top + r.TB * uv.Y, sl + lr * uv.X, st + tb * uv.Y, paletteTextureIndex, fAttribC, uv.X, uv.Y, -2f, alpha);

							index = iRT;
							uv = CalUV(map.TerrainVertices[index].LogicPos, TLX, TLY, width, height);
							vertices[i + 3] = new Vertex(map.TerrainVertices[index].Pos + viewOffset, r.Left + r.LR * uv.X, r.Top + r.TB * uv.Y, sl + lr * uv.X, st + tb * uv.Y, paletteTextureIndex, fAttribC, uv.X, uv.Y, -2f, alpha);
							index = iLB;
							uv = CalUV(map.TerrainVertices[index].LogicPos, TLX, TLY, width, height);
							vertices[i + 4] = new Vertex(map.TerrainVertices[index].Pos + viewOffset, r.Left + r.LR * uv.X, r.Top + r.TB * uv.Y, sl + lr * uv.X, st + tb * uv.Y, paletteTextureIndex, fAttribC, uv.X, uv.Y, -2f, alpha);
							index = iRB;
							uv = CalUV(map.TerrainVertices[index].LogicPos, TLX, TLY, width, height);
							vertices[i + 5] = new Vertex(map.TerrainVertices[index].Pos + viewOffset, r.Left + r.LR * uv.X, r.Top + r.TB * uv.Y, sl + lr * uv.X, st + tb * uv.Y, paletteTextureIndex, fAttribC, uv.X, uv.Y, -2f, alpha);
						}

						i += 6;
					}

				return vertices;
			}
		}

		public static MapVertex[] FastCreateTileActor(
			in WPos pos, in float3 viewOffset,
			Sprite r, int2 samplers, float paletteTextureIndex,
			float scale, in float3 tint, float alpha, 
			Map map)
		{
			var width = (int)(r.Bounds.Width * Game.Renderer.World3DRenderer.WDistPerPix * scale);

			var height = (int)(r.Bounds.Height * Game.Renderer.World3DRenderer.WDistPerPix * scale);

			// height * 2 for isometric tile
			if (r.SpriteMeshType == SpriteMeshType.TerrainCovering)
				height *= 2;

			var w = width / 2;
			var h = height / 2;
			var TL = new int2((pos.X - w - MapGrid.MapMiniCellWidth) / MapGrid.MapMiniCellWidth + 1,
				(pos.Y - h - MapGrid.MapMiniCellWidth) / MapGrid.MapMiniCellWidth + 1);
			var BR = new int2((pos.X + w + MapGrid.MapMiniCellWidth) / MapGrid.MapMiniCellWidth,
				(pos.Y + h + MapGrid.MapMiniCellWidth) / MapGrid.MapMiniCellWidth);

			// 6 vertex one minicell
			int count = (BR.X - TL.X) * (BR.Y - TL.Y) * 6;
			var TLX = pos.X - w;
			var TLY = pos.Y - h;

			float sl = 0;
			float st = 0;
			float lr = 0;
			float tb = 0;

			MapVertex[] vertices = new MapVertex[count];

			// See combined.vert for documentation on the channel attribute format
			var attribC = r.Channel == TextureChannel.RGBA ? 0x02 : ((byte)r.Channel) << 1 | 0x01;
			attribC |= samplers.X << 6;
			if (r is SpriteWithSecondaryData ss)
			{
				sl = ss.SecondaryLeft;
				st = ss.SecondaryTop;
				lr = ss.SecondaryRight - sl;
				tb = ss.SecondaryBottom - st;

				attribC |= ((byte)ss.SecondaryChannel) << 4 | 0x08;
				attribC |= samplers.Y << 9;
			}

			var fAttribC = (float)attribC;

			{
				int i = 0;
				for (int y = TL.Y; y < BR.Y; y++)
					for (int x = TL.X; x < BR.X; x++)
					{
						var iLT = Math.Clamp(x + y * map.VertexArrayWidth, 0, map.TerrainVertices.Length - 1);
						var iRT = Math.Clamp(x + 1 + y * map.VertexArrayWidth, 0, map.TerrainVertices.Length - 1);
						var iLB = Math.Clamp(x + (y + 1) * map.VertexArrayWidth, 0, map.TerrainVertices.Length - 1);
						var iRB = Math.Clamp(x + 1 + (y + 1) * map.VertexArrayWidth, 0, map.TerrainVertices.Length - 1);
						var index = iRT;

						MiniCell cell = map.MiniCells[y, x];

						float2 uv;
						if (cell.Type == MiniCellType.TLBR)
						{
							// ------------
							// |  \          |
							// |      \      |
							// |          \  |
							// ------------
							index = iRT;
							uv = CalUV(map.TerrainVertices[index].LogicPos, TLX, TLY, width, height);
							vertices[i] = new MapVertex(map.TerrainVertices[index].Pos + viewOffset, map.TerrainVertices[index].TBN, uv,
								r.Left + r.LR * uv.X, r.Top + r.TB * uv.Y, sl + lr * uv.X, st + tb * uv.Y, paletteTextureIndex, fAttribC, tint, alpha);
							index = iLT;
							uv = CalUV(map.TerrainVertices[index].LogicPos, TLX, TLY, width, height);
							vertices[i + 1] = new MapVertex(map.TerrainVertices[index].Pos + viewOffset, map.TerrainVertices[index].TBN, uv,
								r.Left + r.LR * uv.X, r.Top + r.TB * uv.Y, sl + lr * uv.X, st + tb * uv.Y, paletteTextureIndex, fAttribC, tint, alpha);
							index = iRB;
							uv = CalUV(map.TerrainVertices[index].LogicPos, TLX, TLY, width, height);
							vertices[i + 2] = new MapVertex(map.TerrainVertices[index].Pos + viewOffset, map.TerrainVertices[index].TBN, uv,
								r.Left + r.LR * uv.X, r.Top + r.TB * uv.Y, sl + lr * uv.X, st + tb * uv.Y, paletteTextureIndex, fAttribC, tint, alpha);

							index = iLT;
							uv = CalUV(map.TerrainVertices[index].LogicPos, TLX, TLY, width, height);
							vertices[i + 3] = new MapVertex(map.TerrainVertices[index].Pos + viewOffset, map.TerrainVertices[index].TBN, uv,
								r.Left + r.LR * uv.X, r.Top + r.TB * uv.Y, sl + lr * uv.X, st + tb * uv.Y, paletteTextureIndex, fAttribC, tint, alpha);
							index = iLB;
							uv = CalUV(map.TerrainVertices[index].LogicPos, TLX, TLY, width, height);
							vertices[i + 4] = new MapVertex(map.TerrainVertices[index].Pos + viewOffset, map.TerrainVertices[index].TBN, uv,
								r.Left + r.LR * uv.X, r.Top + r.TB * uv.Y, sl + lr * uv.X, st + tb * uv.Y, paletteTextureIndex, fAttribC, tint, alpha);
							index = iRB;
							uv = CalUV(map.TerrainVertices[index].LogicPos, TLX, TLY, width, height);
							vertices[i + 5] = new MapVertex(map.TerrainVertices[index].Pos + viewOffset, map.TerrainVertices[index].TBN, uv,
								r.Left + r.LR * uv.X, r.Top + r.TB * uv.Y, sl + lr * uv.X, st + tb * uv.Y, paletteTextureIndex, fAttribC, tint, alpha);
						}
						else
						{
							// ------------
							// |           / |
							// |      /      |
							// |  /          |
							// ------------
							index = iRT;
							uv = CalUV(map.TerrainVertices[index].LogicPos, TLX, TLY, width, height);
							vertices[i] = new MapVertex(map.TerrainVertices[index].Pos + viewOffset, map.TerrainVertices[index].TBN, uv,
								r.Left + r.LR * uv.X, r.Top + r.TB * uv.Y, sl + lr * uv.X, st + tb * uv.Y, paletteTextureIndex, fAttribC, tint, alpha);
							index = iLT;
							uv = CalUV(map.TerrainVertices[index].LogicPos, TLX, TLY, width, height);
							vertices[i + 1] = new MapVertex(map.TerrainVertices[index].Pos + viewOffset, map.TerrainVertices[index].TBN, uv,
								r.Left + r.LR * uv.X, r.Top + r.TB * uv.Y, sl + lr * uv.X, st + tb * uv.Y, paletteTextureIndex, fAttribC, tint, alpha);
							index = iLB;
							uv = CalUV(map.TerrainVertices[index].LogicPos, TLX, TLY, width, height);
							vertices[i + 2] = new MapVertex(map.TerrainVertices[index].Pos + viewOffset, map.TerrainVertices[index].TBN, uv,
								r.Left + r.LR * uv.X, r.Top + r.TB * uv.Y, sl + lr * uv.X, st + tb * uv.Y, paletteTextureIndex, fAttribC, tint, alpha);

							index = iRT;
							uv = CalUV(map.TerrainVertices[index].LogicPos, TLX, TLY, width, height);
							vertices[i + 3] = new MapVertex(map.TerrainVertices[index].Pos + viewOffset, map.TerrainVertices[index].TBN, uv,
								r.Left + r.LR * uv.X, r.Top + r.TB * uv.Y, sl + lr * uv.X, st + tb * uv.Y, paletteTextureIndex, fAttribC, tint, alpha);
							index = iLB;
							uv = CalUV(map.TerrainVertices[index].LogicPos, TLX, TLY, width, height);
							vertices[i + 4] = new MapVertex(map.TerrainVertices[index].Pos + viewOffset, map.TerrainVertices[index].TBN, uv,
								r.Left + r.LR * uv.X, r.Top + r.TB * uv.Y, sl + lr * uv.X, st + tb * uv.Y, paletteTextureIndex, fAttribC, tint, alpha);
							index = iRB;
							uv = CalUV(map.TerrainVertices[index].LogicPos, TLX, TLY, width, height);
							vertices[i + 5] = new MapVertex(map.TerrainVertices[index].Pos + viewOffset, map.TerrainVertices[index].TBN, uv,
								r.Left + r.LR * uv.X, r.Top + r.TB * uv.Y, sl + lr * uv.X, st + tb * uv.Y, paletteTextureIndex, fAttribC, tint, alpha);
						}

						i += 6;
					}

				return vertices;
			}
		}

		public static MapVertex[] FastCreateTile(
			Map map, in CellInfo cellinfo,
			in float3 mColorOffset, in float3 tColorOffset, in float3 bColorOffset, in float3 lColorOffset, in float3 rColorOffset,
			uint type,
			Sprite r, int2 samplers, float paletteTextureIndex, in Vector3 ZOffset,
			float alpha, bool rotation = true)
		{
			var viewOffset = new float3(ZOffset.X, ZOffset.Y, ZOffset.Z);

			var mpos = map.TerrainVertices[cellinfo.M].Pos + viewOffset;
			var tpos = map.TerrainVertices[cellinfo.T].Pos + viewOffset;
			var bpos = map.TerrainVertices[cellinfo.B].Pos + viewOffset;
			var lpos = map.TerrainVertices[cellinfo.L].Pos + viewOffset;
			var rpos = map.TerrainVertices[cellinfo.R].Pos + viewOffset;

			float sl = 0;
			float st = 0;
			float sr = 0;
			float sb = 0;

			float sbaseTB = 0;
			float sbaseRL = 0;

			// See combined.vert for documentation on the channel attribute format
			var attribC = r.Channel == TextureChannel.RGBA ? 0x02 : ((byte)r.Channel) << 1 | 0x01;
			attribC |= samplers.X << 6;
			if (r is SpriteWithSecondaryData ss)
			{
				sl = ss.SecondaryLeft;
				st = ss.SecondaryTop;
				sr = ss.SecondaryRight;
				sb = ss.SecondaryBottom;

				sbaseTB = st - (st - sb) * 0.5f;
				sbaseRL = sr - (sr - sl) * 0.5f;

				attribC |= ((byte)ss.SecondaryChannel) << 4 | 0x08;
				attribC |= samplers.Y << 9;
			}

			var fAttribC = (float)attribC;
			float baseY = r.Top - (r.Top - r.Bottom) * 0.5f;
			float baseX = r.Right - (r.Right - r.Left) * 0.5f;
			MapVertex[] vertices;

			// rotate isomatric tile to rect
			if (rotation)
			{
				float top = r.Top + (r.Bottom - r.Top) * 0.035f;
				float bottom = r.Bottom + (r.Top - r.Bottom) * 0.035f;
				float left = r.Left + (r.Right - r.Left) * 0.035f;
				float right = r.Right + (r.Left - r.Right) * 0.035f;

				if (cellinfo.Flat)
				{
					vertices = new MapVertex[6];
					vertices[0] = new MapVertex(tpos, map.TerrainVertices[cellinfo.T].TBN, baseX, top, sbaseRL, st, paletteTextureIndex, fAttribC, tColorOffset, alpha, map.TerrainVertices[cellinfo.T].UV, type);
					vertices[1] = new MapVertex(bpos, map.TerrainVertices[cellinfo.B].TBN, baseX, bottom, sbaseRL, sb, paletteTextureIndex, fAttribC, bColorOffset, alpha, map.TerrainVertices[cellinfo.B].UV, type);
					vertices[2] = new MapVertex(rpos, map.TerrainVertices[cellinfo.R].TBN, right, baseY, sr, sbaseTB, paletteTextureIndex, fAttribC, rColorOffset, alpha, map.TerrainVertices[cellinfo.R].UV, type);

					vertices[3] = new MapVertex(tpos, map.TerrainVertices[cellinfo.T].TBN, baseX, top, sbaseRL, st, paletteTextureIndex, fAttribC, tColorOffset, alpha, map.TerrainVertices[cellinfo.T].UV, type);
					vertices[4] = new MapVertex(lpos, map.TerrainVertices[cellinfo.L].TBN, left, baseY, sl, sbaseTB, paletteTextureIndex, fAttribC, lColorOffset, alpha, map.TerrainVertices[cellinfo.L].UV, type);
					vertices[5] = new MapVertex(bpos, map.TerrainVertices[cellinfo.B].TBN, baseX, bottom, sbaseRL, sb, paletteTextureIndex, fAttribC, bColorOffset, alpha, map.TerrainVertices[cellinfo.B].UV, type);
				}
				else
				{
					vertices = new MapVertex[12];

					vertices[0] = new MapVertex(tpos, map.TerrainVertices[cellinfo.T].TBN, baseX, top, sbaseRL, st, paletteTextureIndex, fAttribC, tColorOffset, alpha, map.TerrainVertices[cellinfo.T].UV, type);
					vertices[1] = new MapVertex(mpos, map.TerrainVertices[cellinfo.M].TBN, baseX, baseY, sbaseRL, sbaseTB, paletteTextureIndex, fAttribC, mColorOffset, alpha, map.TerrainVertices[cellinfo.M].UV, type);
					vertices[2] = new MapVertex(rpos, map.TerrainVertices[cellinfo.R].TBN, right, baseY, sr, sbaseTB, paletteTextureIndex, fAttribC, rColorOffset, alpha, map.TerrainVertices[cellinfo.R].UV, type);

					vertices[3] = new MapVertex(mpos, map.TerrainVertices[cellinfo.M].TBN, baseX, baseY, sbaseRL, sbaseTB, paletteTextureIndex, fAttribC, mColorOffset, alpha, map.TerrainVertices[cellinfo.M].UV, type);
					vertices[4] = new MapVertex(bpos, map.TerrainVertices[cellinfo.B].TBN, baseX, bottom, sbaseRL, sb, paletteTextureIndex, fAttribC, bColorOffset, alpha, map.TerrainVertices[cellinfo.B].UV, type);
					vertices[5] = new MapVertex(rpos, map.TerrainVertices[cellinfo.R].TBN, right, baseY, sr, sbaseTB, paletteTextureIndex, fAttribC, rColorOffset, alpha, map.TerrainVertices[cellinfo.R].UV, type);

					vertices[6] = new MapVertex(tpos, map.TerrainVertices[cellinfo.T].TBN, baseX, top, sbaseRL, st, paletteTextureIndex, fAttribC, tColorOffset, alpha, map.TerrainVertices[cellinfo.T].UV, type);
					vertices[7] = new MapVertex(lpos, map.TerrainVertices[cellinfo.L].TBN, left, baseY, sl, sbaseTB, paletteTextureIndex, fAttribC, lColorOffset, alpha, map.TerrainVertices[cellinfo.L].UV, type);
					vertices[8] = new MapVertex(mpos, map.TerrainVertices[cellinfo.M].TBN, baseX, baseY, sbaseRL, sbaseTB, paletteTextureIndex, fAttribC, mColorOffset, alpha, map.TerrainVertices[cellinfo.M].UV, type);

					vertices[9] = new MapVertex(mpos, map.TerrainVertices[cellinfo.M].TBN, baseX, baseY, sbaseRL, sbaseTB, paletteTextureIndex, fAttribC, mColorOffset, alpha, map.TerrainVertices[cellinfo.M].UV, type);
					vertices[10] = new MapVertex(lpos, map.TerrainVertices[cellinfo.L].TBN, left, baseY, sl, sbaseTB, paletteTextureIndex, fAttribC, lColorOffset, alpha, map.TerrainVertices[cellinfo.L].UV, type);
					vertices[11] = new MapVertex(bpos, map.TerrainVertices[cellinfo.B].TBN, baseX, bottom, sbaseRL, sb, paletteTextureIndex, fAttribC, bColorOffset, alpha, map.TerrainVertices[cellinfo.B].UV, type);

				}
			}
			else
			{
				if (cellinfo.Flat)
				{
					vertices = new MapVertex[6];
					vertices[0] = new MapVertex(tpos, map.TerrainVertices[cellinfo.T].TBN, r.Left, r.Top, sl, st, paletteTextureIndex, fAttribC, tColorOffset, alpha, map.TerrainVertices[cellinfo.T].UV, type);
					vertices[1] = new MapVertex(bpos, map.TerrainVertices[cellinfo.B].TBN, r.Right, r.Bottom, sr, sb, paletteTextureIndex, fAttribC, bColorOffset, alpha, map.TerrainVertices[cellinfo.B].UV, type);
					vertices[2] = new MapVertex(rpos, map.TerrainVertices[cellinfo.R].TBN, r.Right, r.Top, sr, st, paletteTextureIndex, fAttribC, rColorOffset, alpha, map.TerrainVertices[cellinfo.R].UV, type);

					vertices[3] = new MapVertex(tpos, map.TerrainVertices[cellinfo.T].TBN, r.Left, r.Top, sl, st, paletteTextureIndex, fAttribC, tColorOffset, alpha, map.TerrainVertices[cellinfo.T].UV, type);
					vertices[4] = new MapVertex(lpos, map.TerrainVertices[cellinfo.L].TBN, r.Left, r.Bottom, sl, sb, paletteTextureIndex, fAttribC, lColorOffset, alpha, map.TerrainVertices[cellinfo.L].UV, type);
					vertices[5] = new MapVertex(bpos, map.TerrainVertices[cellinfo.B].TBN, r.Right, r.Bottom, sr, sb, paletteTextureIndex, fAttribC, bColorOffset, alpha, map.TerrainVertices[cellinfo.B].UV, type);
				}
				else
				{
					vertices = new MapVertex[12];
					vertices[0] = new MapVertex(tpos, map.TerrainVertices[cellinfo.T].TBN, r.Left, r.Top, sl, st, paletteTextureIndex, fAttribC, tColorOffset, alpha, map.TerrainVertices[cellinfo.T].UV, type);
					vertices[1] = new MapVertex(mpos, map.TerrainVertices[cellinfo.M].TBN, baseX, baseY, sbaseRL, sbaseTB, paletteTextureIndex, fAttribC, mColorOffset, alpha, map.TerrainVertices[cellinfo.M].UV, type);
					vertices[2] = new MapVertex(rpos, map.TerrainVertices[cellinfo.R].TBN, r.Right, r.Top, sr, st, paletteTextureIndex, fAttribC, rColorOffset, alpha, map.TerrainVertices[cellinfo.R].UV, type);

					vertices[3] = new MapVertex(mpos, map.TerrainVertices[cellinfo.M].TBN, baseX, baseY, sbaseRL, sbaseTB, paletteTextureIndex, fAttribC, mColorOffset, alpha, map.TerrainVertices[cellinfo.M].UV, type);
					vertices[4] = new MapVertex(bpos, map.TerrainVertices[cellinfo.B].TBN, r.Right, r.Bottom, sr, sb, paletteTextureIndex, fAttribC, bColorOffset, alpha, map.TerrainVertices[cellinfo.B].UV, type);
					vertices[5] = new MapVertex(rpos, map.TerrainVertices[cellinfo.R].TBN, r.Right, r.Top, sr, st, paletteTextureIndex, fAttribC, rColorOffset, alpha, map.TerrainVertices[cellinfo.R].UV, type);

					vertices[6] = new MapVertex(tpos, map.TerrainVertices[cellinfo.T].TBN, r.Left, r.Top, sl, st, paletteTextureIndex, fAttribC, tColorOffset, alpha, map.TerrainVertices[cellinfo.T].UV, type);
					vertices[7] = new MapVertex(lpos, map.TerrainVertices[cellinfo.L].TBN, r.Left, r.Bottom, sl, sb, paletteTextureIndex, fAttribC, lColorOffset, alpha, map.TerrainVertices[cellinfo.L].UV, type);
					vertices[8] = new MapVertex(mpos, map.TerrainVertices[cellinfo.M].TBN, baseX, baseY, sbaseRL, sbaseTB, paletteTextureIndex, fAttribC, mColorOffset, alpha, map.TerrainVertices[cellinfo.M].UV, type);

					vertices[9] = new MapVertex(mpos, map.TerrainVertices[cellinfo.M].TBN, baseX, baseY, sbaseRL, sbaseTB, paletteTextureIndex, fAttribC, mColorOffset, alpha, map.TerrainVertices[cellinfo.M].UV, type);
					vertices[10] = new MapVertex(lpos, map.TerrainVertices[cellinfo.L].TBN, r.Left, r.Bottom, sl, sb, paletteTextureIndex, fAttribC, lColorOffset, alpha, map.TerrainVertices[cellinfo.L].UV, type);
					vertices[11] = new MapVertex(bpos, map.TerrainVertices[cellinfo.B].TBN, r.Right, r.Bottom, sr, sb, paletteTextureIndex, fAttribC, bColorOffset, alpha, map.TerrainVertices[cellinfo.B].UV, type);
				}
			}

			return vertices;
		}

		public static Vertex[] FastCreateCell(
			in CPos cell,
			in float3 viewOffset,
			Sprite r, int2 samplers, float paletteTextureIndex,
			float scale, in float3 tint, float alpha,
			Map map)
		{
			CellInfo cellinfo = map.CellInfos[cell];

			var mpos = map.TerrainVertices[cellinfo.M].Pos + viewOffset;
			var tpos = map.TerrainVertices[cellinfo.T].Pos + viewOffset;
			var bpos = map.TerrainVertices[cellinfo.B].Pos + viewOffset;
			var lpos = map.TerrainVertices[cellinfo.L].Pos + viewOffset;
			var rpos = map.TerrainVertices[cellinfo.R].Pos + viewOffset;

			float sl = 0;
			float st = 0;
			float sr = 0;
			float sb = 0;

			float sbaseTB = 0;
			float sbaseRL = 0;

			// See combined.vert for documentation on the channel attribute format
			var attribC = r.Channel == TextureChannel.RGBA ? 0x02 : ((byte)r.Channel) << 1 | 0x01;
			attribC |= samplers.X << 6;
			if (r is SpriteWithSecondaryData ss)
			{
				sl = ss.SecondaryLeft;
				st = ss.SecondaryTop;
				sr = ss.SecondaryRight;
				sb = ss.SecondaryBottom;

				sbaseTB = st - (st - sb) * 0.5f;
				sbaseRL = sr - (sr - sl) * 0.5f;

				attribC |= ((byte)ss.SecondaryChannel) << 4 | 0x08;
				attribC |= samplers.Y << 9;
			}

			var fAttribC = (float)attribC;
			float baseY = r.Top - (r.Top - r.Bottom) * 0.5f;
			float baseX = r.Right - (r.Right - r.Left) * 0.5f;
			Vertex[] vertices;

			if (cellinfo.Flat)
			{
				vertices = new Vertex[6];
				vertices[0] = new Vertex(tpos, r.Left, r.Top, sl, st, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[1] = new Vertex(bpos, r.Right, r.Bottom, sr, sb, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[2] = new Vertex(rpos, r.Right, r.Top, sr, st, paletteTextureIndex, fAttribC, tint, alpha);

				vertices[3] = new Vertex(tpos, r.Left, r.Top, sl, st, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[4] = new Vertex(lpos, r.Left, r.Bottom, sl, sb, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[5] = new Vertex(bpos, r.Right, r.Bottom, sr, sb, paletteTextureIndex, fAttribC, tint, alpha);
			}
			else
			{
				vertices = new Vertex[12];
				vertices[0] = new Vertex(tpos, r.Left, r.Top, sl, st, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[1] = new Vertex(mpos, baseX, baseY, sbaseRL, sbaseTB, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[2] = new Vertex(rpos, r.Right, r.Top, sr, st, paletteTextureIndex, fAttribC, tint, alpha);

				vertices[3] = new Vertex(mpos, baseX, baseY, sbaseRL, sbaseTB, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[4] = new Vertex(bpos, r.Right, r.Bottom, sr, sb, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[5] = new Vertex(rpos, r.Right, r.Top, sr, st, paletteTextureIndex, fAttribC, tint, alpha);

				vertices[6] = new Vertex(tpos, r.Left, r.Top, sl, st, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[7] = new Vertex(lpos, r.Left, r.Bottom, sl, sb, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[8] = new Vertex(mpos, baseX, baseY, sbaseRL, sbaseTB, paletteTextureIndex, fAttribC, tint, alpha);

				vertices[9] = new Vertex(mpos, baseX, baseY, sbaseRL, sbaseTB, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[10] = new Vertex(lpos, r.Left, r.Bottom, sl, sb, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[11] = new Vertex(bpos, r.Right, r.Bottom, sr, sb, paletteTextureIndex, fAttribC, tint, alpha);
			}

			return vertices;
		}

		public static MapVertex[] FastCreateTilePlane(
				in Vec3x3 tbn,
				in WPos inPos, in Vector3 viewOffset,
				Sprite r, int2 samplers, float paletteTextureIndex, float scale,
				in float3 colorOffset, float alpha)
		{
			if (r.HasMeshCreateInfo)
			{
				if (!r.UpdateMeshInfo())
					throw new Exception("invalide create mesh time: sprite has not create mesh data");
			}

			if (r.SpriteMeshType != SpriteMeshType.Plane)
			{
				throw new Exception("sprite's mesh type is not plane");
			}

			if (scale < 0)
			{
				throw new Exception("invalide create mesh scale: only positve value supported");
			}

			float2 leftRight = scale * r.LeftRight;

			var position = Game.Renderer.World3DRenderer.Get3DRenderPositionFromWPos(inPos);
			position += viewOffset;

			float3 leftBack = new float3(position.X + leftRight.X, position.Y + scale * r.leftBack.Y, position.Z);
			float3 rightBack = new float3(position.X + leftRight.Y, leftBack.Y, position.Z);
			float3 leftFront = new float3(leftBack.X, position.Y + scale * r.leftFront.Y, position.Z);
			float3 rightFront = new float3(rightBack.X, leftFront.Y, position.Z);

			float sl = 0;
			float st = 0;
			float sr = 0;
			float sb = 0;

			// See combined.vert for documentation on the channel attribute format
			var attribC = r.Channel == TextureChannel.RGBA ? 0x02 : ((byte)r.Channel) << 1 | 0x01;
			attribC |= samplers.X << 6;
			if (r is SpriteWithSecondaryData ss)
			{
				sl = ss.SecondaryLeft;
				st = ss.SecondaryTop;
				sr = ss.SecondaryRight;
				sb = ss.SecondaryBottom;

				attribC |= ((byte)ss.SecondaryChannel) << 4 | 0x08;
				attribC |= samplers.Y << 9;
			}

			var fAttribC = (float)attribC;
			var vertices = new MapVertex[6];
			vertices[2] = new MapVertex(leftBack, tbn, r.Left, r.Top, sl, st, paletteTextureIndex, fAttribC, colorOffset, alpha, CellInfo.TU, CellInfo.TV, 99);
			vertices[1] = new MapVertex(rightBack, tbn, r.Right, r.Top, sr, st, paletteTextureIndex, fAttribC, colorOffset, alpha, CellInfo.RU, CellInfo.RV, 99);
			vertices[0] = new MapVertex(rightFront, tbn, r.Right, r.Bottom, sr, sb, paletteTextureIndex, fAttribC, colorOffset, alpha, CellInfo.BU, CellInfo.BV, 99);

			vertices[5] = new MapVertex(rightFront, tbn, r.Right, r.Bottom, sr, sb, paletteTextureIndex, fAttribC, colorOffset, alpha, CellInfo.BU, CellInfo.BV, 99);
			vertices[4] = new MapVertex(leftFront, tbn, r.Left, r.Bottom, sl, sb, paletteTextureIndex, fAttribC, colorOffset, alpha, CellInfo.LU, CellInfo.LV, 99);
			vertices[3] = new MapVertex(leftBack, tbn, r.Left, r.Top, sl, st, paletteTextureIndex, fAttribC, colorOffset, alpha, CellInfo.TU, CellInfo.TV, 99);
			return vertices;
		}

		public static void FastCreateQuad(Vertex[] vertices, in float3 o, Sprite r, int2 samplers, float paletteTextureIndex, int nv,
					in float3 size, in float3 tint, float alpha, float rotation = 0f)
		{
			float3 a, b, c, d;

			// Rotate sprite if rotation angle is not equal to 0
			if (rotation != 0f)
			{
				var center = o + 0.5f * size;
				var angleSin = (float)Math.Sin(-rotation);
				var angleCos = (float)Math.Cos(-rotation);

				// Rotated offset for +/- x with +/- y
				var ra = 0.5f * new float3(
					size.X * angleCos - size.Y * angleSin,
					size.X * angleSin + size.Y * angleCos,
					(size.X * angleSin + size.Y * angleCos) * size.Z / size.Y);

				// Rotated offset for +/- x with -/+ y
				var rb = 0.5f * new float3(
					size.X * angleCos + size.Y * angleSin,
					size.X * angleSin - size.Y * angleCos,
					(size.X * angleSin - size.Y * angleCos) * size.Z / size.Y);

				a = center - ra;
				b = center + rb;
				c = center + ra;
				d = center - rb;
			}
			else
			{
				a = o;
				b = new float3(o.X + size.X, o.Y, o.Z);
				c = new float3(o.X + size.X, o.Y + size.Y, o.Z + size.Z);
				d = new float3(o.X, o.Y + size.Y, o.Z + size.Z);
			}

			FastCreateQuad(vertices, a, b, c, d, r, samplers, paletteTextureIndex, tint, alpha, nv);
		}

		public static void FastCreateQuad(Vertex[] vertices,
			in float3 a, in float3 b, in float3 c, in float3 d,
			Sprite r, int2 samplers, float paletteTextureIndex,
			in float3 tint, float alpha, int nv)
		{
			float sl = 0;
			float st = 0;
			float sr = 0;
			float sb = 0;

			// See combined.vert for documentation on the channel attribute format
			var attribC = r.Channel == TextureChannel.RGBA ? 0x02 : ((byte)r.Channel) << 1 | 0x01;
			attribC |= samplers.X << 6;
			if (r is SpriteWithSecondaryData ss)
			{
				sl = ss.SecondaryLeft;
				st = ss.SecondaryTop;
				sr = ss.SecondaryRight;
				sb = ss.SecondaryBottom;

				attribC |= ((byte)ss.SecondaryChannel) << 4 | 0x08;
				attribC |= samplers.Y << 9;
			}

			var fAttribC = (float)attribC;
			vertices[nv] = new Vertex(a, r.Left, r.Top, sl, st, paletteTextureIndex, fAttribC, tint, alpha);
			vertices[nv + 1] = new Vertex(b, r.Right, r.Top, sr, st, paletteTextureIndex, fAttribC, tint, alpha);
			vertices[nv + 2] = new Vertex(c, r.Right, r.Bottom, sr, sb, paletteTextureIndex, fAttribC, tint, alpha);
			vertices[nv + 3] = new Vertex(c, r.Right, r.Bottom, sr, sb, paletteTextureIndex, fAttribC, tint, alpha);
			vertices[nv + 4] = new Vertex(d, r.Left, r.Bottom, sl, sb, paletteTextureIndex, fAttribC, tint, alpha);
			vertices[nv + 5] = new Vertex(a, r.Left, r.Top, sl, st, paletteTextureIndex, fAttribC, tint, alpha);
		}

		public static void FastCopyIntoChannel(Sprite dest, byte[] src, SpriteFrameType srcType)
		{
			var destData = dest.Sheet.GetData();
			var width = dest.Bounds.Width;
			var height = dest.Bounds.Height;

			if (dest.Channel == TextureChannel.RGBA)
			{
				var destStride = dest.Sheet.Size.Width;
				unsafe
				{
					// Cast the data to an int array so we can copy the src data directly
					fixed (byte* bd = &destData[0])
					{
						var data = (int*)bd;
						var x = dest.Bounds.Left;
						var y = dest.Bounds.Top;

						var k = 0;
						for (var j = 0; j < height; j++)
						{
							for (var i = 0; i < width; i++)
							{
								byte r, g, b, a;
								switch (srcType)
								{
									case SpriteFrameType.Bgra32:
									case SpriteFrameType.Bgr24:
										{
											b = src[k++];
											g = src[k++];
											r = src[k++];
											a = srcType == SpriteFrameType.Bgra32 ? src[k++] : (byte)255;
											break;
										}

									case SpriteFrameType.Rgba32:
									case SpriteFrameType.Rgb24:
										{
											r = src[k++];
											g = src[k++];
											b = src[k++];
											a = srcType == SpriteFrameType.Rgba32 ? src[k++] : (byte)255;
											break;
										}

									default:
										throw new InvalidOperationException($"Unknown SpriteFrameType {srcType}");
								}

								var cc = Color.FromArgb(a, r, g, b);
								data[(y + j) * destStride + x + i] = PremultiplyAlpha(cc).ToArgb();
							}
						}
					}
				}
			}
			else
			{
				var destStride = dest.Sheet.Size.Width * 4;
				var destOffset = destStride * dest.Bounds.Top + dest.Bounds.Left * 4 + ChannelMasks[(int)dest.Channel];
				var destSkip = destStride - 4 * width;

				var srcOffset = 0;
				for (var j = 0; j < height; j++)
				{
					for (var i = 0; i < width; i++, srcOffset++)
					{
						destData[destOffset] = src[srcOffset];
						destOffset += 4;
					}

					destOffset += destSkip;
				}
			}
		}

		public static void FastCopyIntoSprite(Sprite dest, Png src)
		{
			var destData = dest.Sheet.GetData();
			var destStride = dest.Sheet.Size.Width;
			var width = dest.Bounds.Width;
			var height = dest.Bounds.Height;

			unsafe
			{
				// Cast the data to an int array so we can copy the src data directly
				fixed (byte* bd = &destData[0])
				{
					var data = (int*)bd;
					var x = dest.Bounds.Left;
					var y = dest.Bounds.Top;

					var k = 0;
					for (var j = 0; j < height; j++)
					{
						for (var i = 0; i < width; i++)
						{
							Color cc;
							switch (src.Type)
							{
								case SpriteFrameType.Indexed8:
									{
										cc = src.Palette[src.Data[k++]];
										break;
									}

								case SpriteFrameType.Rgba32:
								case SpriteFrameType.Rgb24:
									{
										var r = src.Data[k++];
										var g = src.Data[k++];
										var b = src.Data[k++];
										var a = src.Type == SpriteFrameType.Rgba32 ? src.Data[k++] : (byte)255;
										cc = Color.FromArgb(a, r, g, b);
										break;
									}

								// Pngs don't support BGR[A], so no need to include them here
								default:
									throw new InvalidOperationException($"Unknown SpriteFrameType {src.Type}");
							}

							data[(y + j) * destStride + x + i] = PremultiplyAlpha(cc).ToArgb();
						}
					}
				}
			}
		}

		/// <summary>Rotates a quad about its center in the x-y plane.</summary>
		/// <param name="tl">The top left vertex of the quad</param>
		/// <param name="size">A float3 containing the X, Y, and Z lengths of the quad</param>
		/// <param name="rotation">The number of radians to rotate by</param>
		/// <returns>An array of four vertices representing the rotated quad (top-left, top-right, bottom-right, bottom-left)</returns>
		public static float3[] RotateQuad(float3 tl, float3 size, float rotation)
		{
			var center = tl + 0.5f * size;
			var angleSin = (float)Math.Sin(-rotation);
			var angleCos = (float)Math.Cos(-rotation);

			// Rotated offset for +/- x with +/- y
			var ra = 0.5f * new float3(
				size.X * angleCos - size.Y * angleSin,
				size.X * angleSin + size.Y * angleCos,
				(size.X * angleSin + size.Y * angleCos) * size.Z / size.Y);

			// Rotated offset for +/- x with -/+ y
			var rb = 0.5f * new float3(
				size.X * angleCos + size.Y * angleSin,
				size.X * angleSin - size.Y * angleCos,
				(size.X * angleSin - size.Y * angleCos) * size.Z / size.Y);

			return new float3[]
			{
				center - ra,
				center + rb,
				center + ra,
				center - rb
			};
		}

		/// <summary>
		/// Returns the bounds of an object. Used for determining which objects need to be rendered on screen, and which do not.
		/// </summary>
		/// <param name="offset">The top left vertex of the object</param>
		/// <param name="size">A float 3 containing the X, Y, and Z lengths of the object</param>
		/// <param name="rotation">The angle to rotate the object by (use 0f if there is no rotation)</param>
		public static Rectangle BoundingRectangle(float3 offset, float3 size, float rotation)
		{
			if (rotation == 0f)
				return new Rectangle((int)offset.X, (int)offset.Y, (int)size.X, (int)size.Y);

			var rotatedQuad = Util.RotateQuad(offset, size, rotation);
			var minX = rotatedQuad[0].X;
			var maxX = rotatedQuad[0].X;
			var minY = rotatedQuad[0].Y;
			var maxY = rotatedQuad[0].Y;
			for (var i = 1; i < rotatedQuad.Length; i++)
			{
				minX = Math.Min(rotatedQuad[i].X, minX);
				maxX = Math.Max(rotatedQuad[i].X, maxX);
				minY = Math.Min(rotatedQuad[i].Y, minY);
				maxY = Math.Max(rotatedQuad[i].Y, maxY);
			}

			return new Rectangle(
				(int)minX,
				(int)minY,
				(int)Math.Ceiling(maxX) - (int)minX,
				(int)Math.Ceiling(maxY) - (int)minY);
		}

		public static Color PremultiplyAlpha(Color c)
		{
			if (c.A == byte.MaxValue)
				return c;
			var a = c.A / 255f;
			return Color.FromArgb(c.A, (byte)(c.R * a + 0.5f), (byte)(c.G * a + 0.5f), (byte)(c.B * a + 0.5f));
		}

		public static Color PremultipliedColorLerp(float t, Color c1, Color c2)
		{
			// Colors must be lerped in a non-multiplied color space
			var a1 = 255f / c1.A;
			var a2 = 255f / c2.A;
			return PremultiplyAlpha(Color.FromArgb(
				(int)(t * c2.A + (1 - t) * c1.A),
				(int)((byte)(t * a2 * c2.R + 0.5f) + (1 - t) * (byte)(a1 * c1.R + 0.5f)),
				(int)((byte)(t * a2 * c2.G + 0.5f) + (1 - t) * (byte)(a1 * c1.G + 0.5f)),
				(int)((byte)(t * a2 * c2.B + 0.5f) + (1 - t) * (byte)(a1 * c1.B + 0.5f))));
		}

		public static float[] IdentityMatrix()
		{
			return Exts.MakeArray(16, j => (j % 5 == 0) ? 1.0f : 0);
		}

		public static float[] ScaleMatrix(float sx, float sy, float sz)
		{
			var mtx = IdentityMatrix();
			mtx[0] = sx;
			mtx[5] = sy;
			mtx[10] = sz;
			return mtx;
		}

		public static float[] TranslationMatrix(float x, float y, float z)
		{
			var mtx = IdentityMatrix();
			mtx[12] = x;
			mtx[13] = y;
			mtx[14] = z;
			return mtx;
		}

		public static float[] MatrixMultiply(float[] lhs, float[] rhs)
		{
			var mtx = new float[16];
			for (var i = 0; i < 4; i++)
				for (var j = 0; j < 4; j++)
				{
					mtx[4 * i + j] = 0;
					for (var k = 0; k < 4; k++)
						mtx[4 * i + j] += lhs[4 * k + j] * rhs[4 * i + k];
				}

			return mtx;
		}

		public static float[] MatrixVectorMultiply(float[] mtx, float[] vec)
		{
			var ret = new float[4];
			for (var j = 0; j < 4; j++)
			{
				ret[j] = 0;
				for (var k = 0; k < 4; k++)
					ret[j] += mtx[4 * k + j] * vec[k];
			}

			return ret;
		}

		public static float[] MatrixInverse(float[] m)
		{
			var mtx = new float[16];

			mtx[0] = m[5] * m[10] * m[15] -
				m[5] * m[11] * m[14] -
				m[9] * m[6] * m[15] +
				m[9] * m[7] * m[14] +
				m[13] * m[6] * m[11] -
				m[13] * m[7] * m[10];

			mtx[4] = -m[4] * m[10] * m[15] +
				m[4] * m[11] * m[14] +
				m[8] * m[6] * m[15] -
				m[8] * m[7] * m[14] -
				m[12] * m[6] * m[11] +
				m[12] * m[7] * m[10];

			mtx[8] = m[4] * m[9] * m[15] -
				m[4] * m[11] * m[13] -
				m[8] * m[5] * m[15] +
				m[8] * m[7] * m[13] +
				m[12] * m[5] * m[11] -
				m[12] * m[7] * m[9];

			mtx[12] = -m[4] * m[9] * m[14] +
				m[4] * m[10] * m[13] +
				m[8] * m[5] * m[14] -
				m[8] * m[6] * m[13] -
				m[12] * m[5] * m[10] +
				m[12] * m[6] * m[9];

			mtx[1] = -m[1] * m[10] * m[15] +
				m[1] * m[11] * m[14] +
				m[9] * m[2] * m[15] -
				m[9] * m[3] * m[14] -
				m[13] * m[2] * m[11] +
				m[13] * m[3] * m[10];

			mtx[5] = m[0] * m[10] * m[15] -
				m[0] * m[11] * m[14] -
				m[8] * m[2] * m[15] +
				m[8] * m[3] * m[14] +
				m[12] * m[2] * m[11] -
				m[12] * m[3] * m[10];

			mtx[9] = -m[0] * m[9] * m[15] +
				m[0] * m[11] * m[13] +
				m[8] * m[1] * m[15] -
				m[8] * m[3] * m[13] -
				m[12] * m[1] * m[11] +
				m[12] * m[3] * m[9];

			mtx[13] = m[0] * m[9] * m[14] -
				m[0] * m[10] * m[13] -
				m[8] * m[1] * m[14] +
				m[8] * m[2] * m[13] +
				m[12] * m[1] * m[10] -
				m[12] * m[2] * m[9];

			mtx[2] = m[1] * m[6] * m[15] -
				m[1] * m[7] * m[14] -
				m[5] * m[2] * m[15] +
				m[5] * m[3] * m[14] +
				m[13] * m[2] * m[7] -
				m[13] * m[3] * m[6];

			mtx[6] = -m[0] * m[6] * m[15] +
				m[0] * m[7] * m[14] +
				m[4] * m[2] * m[15] -
				m[4] * m[3] * m[14] -
				m[12] * m[2] * m[7] +
				m[12] * m[3] * m[6];

			mtx[10] = m[0] * m[5] * m[15] -
				m[0] * m[7] * m[13] -
				m[4] * m[1] * m[15] +
				m[4] * m[3] * m[13] +
				m[12] * m[1] * m[7] -
				m[12] * m[3] * m[5];

			mtx[14] = -m[0] * m[5] * m[14] +
				m[0] * m[6] * m[13] +
				m[4] * m[1] * m[14] -
				m[4] * m[2] * m[13] -
				m[12] * m[1] * m[6] +
				m[12] * m[2] * m[5];

			mtx[3] = -m[1] * m[6] * m[11] +
				m[1] * m[7] * m[10] +
				m[5] * m[2] * m[11] -
				m[5] * m[3] * m[10] -
				m[9] * m[2] * m[7] +
				m[9] * m[3] * m[6];

			mtx[7] = m[0] * m[6] * m[11] -
				m[0] * m[7] * m[10] -
				m[4] * m[2] * m[11] +
				m[4] * m[3] * m[10] +
				m[8] * m[2] * m[7] -
				m[8] * m[3] * m[6];

			mtx[11] = -m[0] * m[5] * m[11] +
				m[0] * m[7] * m[9] +
				m[4] * m[1] * m[11] -
				m[4] * m[3] * m[9] -
				m[8] * m[1] * m[7] +
				m[8] * m[3] * m[5];

			mtx[15] = m[0] * m[5] * m[10] -
				m[0] * m[6] * m[9] -
				m[4] * m[1] * m[10] +
				m[4] * m[2] * m[9] +
				m[8] * m[1] * m[6] -
				m[8] * m[2] * m[5];

			var det = m[0] * mtx[0] + m[1] * mtx[4] + m[2] * mtx[8] + m[3] * mtx[12];
			if (det == 0)
				return null;

			for (var i = 0; i < 16; i++)
				mtx[i] *= 1 / det;

			return mtx;
		}

		public static float[] MakeFloatMatrix(Int32Matrix4x4 imtx)
		{
			var multipler = 1f / imtx.M44;
			return new[]
			{
				imtx.M11 * multipler,
				imtx.M12 * multipler,
				imtx.M13 * multipler,
				imtx.M14 * multipler,

				imtx.M21 * multipler,
				imtx.M22 * multipler,
				imtx.M23 * multipler,
				imtx.M24 * multipler,

				imtx.M31 * multipler,
				imtx.M32 * multipler,
				imtx.M33 * multipler,
				imtx.M34 * multipler,

				imtx.M41 * multipler,
				imtx.M42 * multipler,
				imtx.M43 * multipler,
				imtx.M44 * multipler,
			};
		}

		public static float[] MatrixAABBMultiply(float[] mtx, float[] bounds)
		{
			// Corner offsets
			var ix = new uint[] { 0, 0, 0, 0, 3, 3, 3, 3 };
			var iy = new uint[] { 1, 1, 4, 4, 1, 1, 4, 4 };
			var iz = new uint[] { 2, 5, 2, 5, 2, 5, 2, 5 };

			// Vectors to opposing corner
			var ret = new[]
			{
				float.MaxValue, float.MaxValue, float.MaxValue,
				float.MinValue, float.MinValue, float.MinValue
			};

			// Transform vectors and find new bounding box
			for (var i = 0; i < 8; i++)
			{
				var vec = new[] { bounds[ix[i]], bounds[iy[i]], bounds[iz[i]], 1 };
				var tvec = MatrixVectorMultiply(mtx, vec);

				ret[0] = Math.Min(ret[0], tvec[0] / tvec[3]);
				ret[1] = Math.Min(ret[1], tvec[1] / tvec[3]);
				ret[2] = Math.Min(ret[2], tvec[2] / tvec[3]);
				ret[3] = Math.Max(ret[3], tvec[0] / tvec[3]);
				ret[4] = Math.Max(ret[4], tvec[1] / tvec[3]);
				ret[5] = Math.Max(ret[5], tvec[2] / tvec[3]);
			}

			return ret;
		}
	}
}
