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
using GlmSharp;
using OpenRA.FileFormats;
using OpenRA.Primitives;

namespace OpenRA.Graphics
{
	public static class Util
	{
		// yes, our channel order is nuts.
		static readonly int[] ChannelMasks = { 2, 1, 0, 3 };

		public static void FastCreateCard(Vertex[] vertices,
			WPos inPos,
			Sprite r, int2 samplers, float paletteTextureIndex, float scale,
			in float3 tint, float alpha, int nv)
		{
			var position = new vec3((float)inPos.X / Game.Renderer.Standalone3DRenderer.WPosPerMeter, (float)inPos.Y / Game.Renderer.Standalone3DRenderer.WPosPerMeter, (float)inPos.Z / Game.Renderer.Standalone3DRenderer.WPosPerMeterHeight);
			var ssziehalf = Game.Renderer.Standalone3DRenderer.meterPerPix * scale * r.Size / 2;
			var soffset = Game.Renderer.Standalone3DRenderer.meterPerPix * scale * r.Offset;

			float2 leftRight = new float2(soffset.X - ssziehalf.X, soffset.X + ssziehalf.X);
			float2 topBottom = new float2(ssziehalf.Y - soffset.Y, soffset.Y + ssziehalf.Y);

			float3 leftTop = new float3(position.x + leftRight.X, position.y, position.z + (topBottom.X) / Game.Renderer.Standalone3DRenderer.SinCameraPitch);
			float3 rightTop = new float3(position.x + leftRight.Y, position.y, leftTop.Z);
			float3 leftBase = new float3(leftTop.X, position.y, position.z);
			float3 rightBase = new float3(rightTop.X, position.y, position.z);
			float3 leftFront = new float3(leftTop.X, position.y + topBottom.Y / Game.Renderer.Standalone3DRenderer.CosCameraPitch, position.z);
			float3 rightFront = new float3(rightTop.X, leftFront.Y, position.z);

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

			//vertices[nv] = new Vertex(a, r.Left, r.Top, sl, st, paletteTextureIndex, fAttribC, tint, alpha);
			//vertices[nv + 1] = new Vertex(b, r.Right, r.Top, sr, st, paletteTextureIndex, fAttribC, tint, alpha);
			//vertices[nv + 2] = new Vertex(c, r.Right, r.Bottom, sr, sb, paletteTextureIndex, fAttribC, tint, alpha);
			//vertices[nv + 3] = new Vertex(c, r.Right, r.Bottom, sr, sb, paletteTextureIndex, fAttribC, tint, alpha);
			//vertices[nv + 4] = new Vertex(d, r.Left, r.Bottom, sl, sb, paletteTextureIndex, fAttribC, tint, alpha);
			//vertices[nv + 5] = new Vertex(a, r.Left, r.Top, sl, st, paletteTextureIndex, fAttribC, tint, alpha);
		}

		public static void FastCreatePlane(Vertex[] vertices,
			WPos inPos,
			Sprite r, int2 samplers, float paletteTextureIndex, float scale,
			in float3 tint, float alpha, int nv)
		{
			var position = new vec3((float)inPos.X / Game.Renderer.Standalone3DRenderer.WPosPerMeter, (float)inPos.Y / Game.Renderer.Standalone3DRenderer.WPosPerMeter, (float)inPos.Z / Game.Renderer.Standalone3DRenderer.WPosPerMeterHeight);
			var ssziehalf = Game.Renderer.Standalone3DRenderer.meterPerPix * scale * r.Size / 2;
			var soffset = Game.Renderer.Standalone3DRenderer.meterPerPix * scale * r.Offset;

			float2 leftRight = new float2(soffset.X - ssziehalf.X, soffset.X + ssziehalf.X);
			float2 topBottom = new float2(ssziehalf.Y - soffset.Y, soffset.Y + ssziehalf.Y);

			float3 leftBack = new float3(position.x + leftRight.X, position.y - topBottom.X / Game.Renderer.Standalone3DRenderer.CosCameraPitch, position.z);
			float3 rightBack = new float3(position.x + leftRight.Y, leftBack.Y, position.z);
			float3 leftFront = new float3(leftBack.X, position.y + topBottom.Y / Game.Renderer.Standalone3DRenderer.CosCameraPitch, position.z);
			float3 rightFront = new float3(rightBack.X, leftFront.Y, position.z);

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

		public static void FastCreateQuad(Vertex[] vertices, in float3 o, Sprite r, int2 samplers, float paletteTextureIndex, int nv, in float3 size, in float3 tint, float alpha)
		{
			var b = new float3(o.X + size.X, o.Y, o.Z);
			var c = new float3(o.X + size.X, o.Y + size.Y, o.Z + size.Z);
			var d = new float3(o.X, o.Y + size.Y, o.Z + size.Z);
			FastCreateQuad(vertices, o, b, c, d, r, samplers, paletteTextureIndex, tint, alpha, nv);
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
