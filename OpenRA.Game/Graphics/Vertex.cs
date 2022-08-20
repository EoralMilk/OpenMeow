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

using System.Runtime.InteropServices;
using GlmSharp;

namespace OpenRA.Graphics
{
	[StructLayout(LayoutKind.Sequential)]
	public readonly struct Vertex
	{
		// 3d position
		public readonly float X, Y, Z;

		// Primary and secondary texture coordinates or RGBA color
		public readonly float S, T, U, V;

		// Palette and channel flags
		public readonly float P, C;

		// Color tint
		public readonly float R, G, B, A;

		public Vertex(in float3 xyz, float s, float t, float u, float v, float p, float c)
			: this(xyz.X, xyz.Y, xyz.Z, s, t, u, v, p, c, float3.Ones, 1f) { }

		public Vertex(in float3 xyz, float s, float t, float u, float v, float p, float c, in float3 tint, float a)
			: this(xyz.X, xyz.Y, xyz.Z, s, t, u, v, p, c, tint.X, tint.Y, tint.Z, a) { }

		public Vertex(float x, float y, float z, float s, float t, float u, float v, float p, float c, in float3 tint, float a)
			: this(x, y, z, s, t, u, v, p, c, tint.X, tint.Y, tint.Z, a) { }

		public Vertex(float x, float y, float z, float s, float t, float u, float v, float p, float c, float r, float g, float b, float a)
		{
			X = x; Y = y; Z = z;
			S = s; T = t;
			U = u; V = v;
			P = p; C = c;
			R = r; G = g; B = b; A = a;
		}
	}

	public readonly struct ScreenVertex
	{
		// 3d position
		public readonly float X, Y;

		// Primary and secondary texture coordinates or RGBA color
		public readonly float U, V;

		public ScreenVertex(float x, float y, float u, float v)
		{
			X = x; Y = y;
			U = u; V = v;
		}
	}

	public struct MapVertex
	{
		// 3d position
		public readonly float X, Y, Z;

		// Primary and secondary texture coordinates or RGBA color
		public readonly float S, T, U, V;

		// Palette and channel flags
		public readonly float P, C;

		// Color tint
		public readonly float R, G, B, A;

		// TBN
		public readonly float TX, TY, TZ;
		public readonly float BX, BY, BZ;
		public readonly float NX, NY, NZ;

		public readonly float TU, TV;

		public readonly uint DrawType;

		public MapVertex(in float3 xyz,
									in mat3 tbn,
									float s, float t, float u, float v,
									float p, float c,
									in float3 tint, float a,
									float tu, float tv, uint type)
			: this(xyz.X, xyz.Y, xyz.Z,
				  s, t, u, v,
				  p, c,
				  tint.X, tint.Y, tint.Z, a,
				  tbn.Column0.x, tbn.Column0.y, tbn.Column0.z,
				  tbn.Column1.x, tbn.Column1.y, tbn.Column1.z,
				  tbn.Column2.x, tbn.Column2.y, tbn.Column2.z,
				  tu, tv, type) { }

		public MapVertex(float x, float y, float z,
									float s, float t, float u, float v,
									float p, float c,
									in float3 tint, float a,
									float tx, float ty, float tz,
									float bx, float by, float bz,
									float nx, float ny, float nz,
									float tu, float tv, uint type)
			: this(x, y, z,
				  s, t, u, v,
				  p, c,
				  tint.X, tint.Y, tint.Z, a,
				  tx, ty, tz,
				  bx, by, bz,
				  nx, ny, nz,
				  tu, tv, type) { }

		public MapVertex(float x, float y, float z,
									float s, float t, float u, float v,
									float p, float c,
									float r, float g, float b, float a,
									float tx, float ty, float tz,
									float bx, float by, float bz,
									float nx, float ny, float nz,
									float tu, float tv, uint type)
		{
			X = x; Y = y; Z = z;
			S = s; T = t;
			U = u; V = v;
			P = p; C = c;
			R = r; G = g; B = b; A = a;
			TX = tx; TY = ty; TZ = tz;
			BX = bx; BY = by; BZ = bz;
			NX = nx; NY = ny; NZ = nz;
			TU = tu; TV = tv;
			DrawType = type;
		}

		public MapVertex ChangePal(float p)
		{
			return new MapVertex(X, Y, Z,
				S, T, U, V,
				p, C,
				R, G, B, A,
				TX, TY, TZ,
				BX, BY, BZ,
				NX, NY, NZ,
				TU, TV,
				DrawType);
		}
	};
}
