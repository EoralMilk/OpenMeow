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
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace OpenRA
{
	[SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Mimic a built-in type alias.")]
	[StructLayout(LayoutKind.Sequential)]
	public readonly struct float4 : IEquatable<float4>
	{
		public readonly float X, Y, Z, W;
		public float4(float x, float y, float z, float w) { X = x; Y = y; Z = z; W = w; }

		public static implicit operator float4(int2 src) { return new float4(src.X, src.Y, 0, 1); }
		public static implicit operator float4(float2 src) { return new float4(src.X, src.Y, 0, 1); }

		public static float4 operator +(in float4 a, in float4 b) { return new float4(a.X + b.X, a.Y + b.Y, a.Z + b.Z, a.W + b.W); }
		public static float4 operator -(in float4 a, in float4 b) { return new float4(a.X - b.X, a.Y - b.Y, a.Z - b.Z, a.W - b.W); }
		public static float4 operator -(in float4 a) { return new float4(-a.X, -a.Y, -a.Z, -a.W); }
		public static float4 operator *(in float4 a, in float4 b) { return new float4(a.X * b.X, a.Y * b.Y, a.Z * b.Z, a.W * a.W); }
		public static float4 operator *(float a, in float4 b) { return new float4(a * b.X, a * b.Y, a * b.Z, a * b.W); }
		public static float4 operator /(in float4 a, in float4 b) { return new float4(a.X / b.X, a.Y / b.Y, a.Z / b.Z, a.W / b.W); }
		public static float4 operator /(in float4 a, float b) { return new float4(a.X / b, a.Y / b, a.Z / b, a.W / b); }

		public static float4 Lerp(float4 a, float4 b, float t)
		{
			return new float4(
				float2.Lerp(a.X, b.X, t),
				float2.Lerp(a.Y, b.Y, t),
				float2.Lerp(a.Z, b.Z, t),
				float2.Lerp(a.W, b.W, t));
		}

		public static bool operator ==(in float4 me, in float4 other) { return me.X == other.X && me.Y == other.Y && me.Z == other.Z && me.W == other.W; }
		public static bool operator !=(in float4 me, in float4 other) { return !(me == other); }
		public override int GetHashCode() { return X.GetHashCode() ^ Y.GetHashCode() ^ Z.GetHashCode() ^ W.GetHashCode(); }

		public bool Equals(float4 other)
		{
			return other == this;
		}

		public override bool Equals(object obj)
		{
			return obj is float4 o && (float4?)o == this;
		}

		public override string ToString() { return $"{X},{Y},{Z},{W}"; }

		public static readonly float4 Zero = new float4(0, 0, 0, 0);
		public static readonly float4 Ones = new float4(1, 1, 1, 1);
		public static readonly float4 Identity = new float4(0, 0, 0, 1);
	}
}
