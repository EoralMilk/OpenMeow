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

		public Vertex(in float3 xyz, float s, float t, float u, float v, float p, float c, float tintX, float tintY, float tintZ, float a)
			: this(xyz.X, xyz.Y, xyz.Z, s, t, u, v, p, c, tintX, tintY, tintZ, a) { }

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
		public readonly float X, Y;
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

		public MapVertex(int v)
		{
			X = 0; Y = 0; Z = 0;
			S = 0; T = 0;
			U = 0; V = 0;
			P = 0; C = 0;
			R = 0; G = 0; B = 0; A = 0;
			TX = 0; TY = 0; TZ = 0;
			BX = 0; BY = 0; BZ = 0;
			NX = 0; NY = 0; NZ = 0;
			TU = 0; TV = 0;
			DrawType = 0;
		}

		public MapVertex(in float3 xyz,
									in Vec3x3 tbn,
									float s, float t, float u, float v,
									float p, float c,
									in float3 tint, float a,
									float tu, float tv, uint type)
			: this(xyz.X, xyz.Y, xyz.Z,
				  s, t, u, v,
				  p, c,
				  tint.X, tint.Y, tint.Z, a,
				  tbn.T.X, tbn.T.Y, tbn.T.Z,
				  tbn.B.X, tbn.B.Y, tbn.B.Z,
				  tbn.N.X, tbn.N.Y, tbn.N.Z,
				  tu, tv, type)
		{ }

		public MapVertex(in float3 xyz,
									in Vec3x3 tbn, in float2 tuv,
									float s, float t, float u, float v,
									float p, float c)
			: this(xyz.X, xyz.Y, xyz.Z,
				  s, t, u, v,
				  p, c,
				  0, 0, 0, 1,
				  tbn.T.X, tbn.T.Y, tbn.T.Z,
				  tbn.B.X, tbn.B.Y, tbn.B.Z,
				  tbn.N.X, tbn.N.Y, tbn.N.Z,
				  tuv.X, tuv.Y, 100)
		{ }

		public MapVertex(in float3 xyz,
							in Vec3x3 tbn, in float2 tuv,
							float s, float t, float u, float v,
							float p, float c,
							in float3 tint, float a)
		: this(xyz.X, xyz.Y, xyz.Z,
			s, t, u, v,
			p, c,
			tint.X, tint.Y, tint.Z, a,
			tbn.T.X, tbn.T.Y, tbn.T.Z,
			tbn.B.X, tbn.B.Y, tbn.B.Z,
			tbn.N.X, tbn.N.Y, tbn.N.Z,
			tuv.X, tuv.Y, 100)
			{ }

		public MapVertex(in float3 xyz,
							in Vec3x3 tbn,
							float s, float t, float u, float v,
							float p, float c,
							in float3 tint, float a,
							float2 tuv, uint type)
	: this(xyz.X, xyz.Y, xyz.Z,
		 s, t, u, v,
		 p, c,
		 tint.X, tint.Y, tint.Z, a,
		 tbn.T.X, tbn.T.Y, tbn.T.Z,
		 tbn.B.X, tbn.B.Y, tbn.B.Z,
		 tbn.N.X, tbn.N.Y, tbn.N.Z,
		 tuv.X, tuv.Y, type)
		{ }

		public MapVertex(in OverlayVertex vertex,
					float a, uint type)
		: this(vertex.X, vertex.Y, vertex.Z,
		  0, 0, 0, 0,
		  0, 0,
		  0, 0, 0, a,
		  vertex.TX, vertex.TY, vertex.TZ,
		  vertex.BX, vertex.BY, vertex.BZ,
		  vertex.NX, vertex.NY, vertex.NZ,
		  vertex.U, vertex.V, type)
		{ }

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
				  tu, tv, type)
		{ }

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

		public MapVertex ChangeAlpha(float a)
		{
			return new MapVertex(X, Y, Z,
				S, T, U, V,
				P, C,
				R, G, B, a,
				TX, TY, TZ,
				BX, BY, BZ,
				NX, NY, NZ,
				TU, TV,
				DrawType);
		}
	};

	public struct OverlayVertex
	{
		public readonly float X, Y, Z;
		public readonly float U, V;

		// TBN
		public readonly float TX, TY, TZ;
		public readonly float BX, BY, BZ;
		public readonly float NX, NY, NZ;

		public OverlayVertex(in float3 xyz,
							in Vec3x3 tbn,
							float2 uv)
	: this(xyz.X, xyz.Y, xyz.Z,
			uv.X, uv.Y,
			tbn.T.X, tbn.T.Y, tbn.T.Z,
			tbn.B.X, tbn.B.Y, tbn.B.Z,
			tbn.N.X, tbn.N.Y, tbn.N.Z)
		{ }

		public OverlayVertex(float x, float y, float z,
			float u, float v,
			float tx, float ty, float tz,
			float bx, float by, float bz,
			float nx, float ny, float nz)
		{
			X = x; Y = y; Z = z;
			U = u; V = v;
			TX = tx; TY = ty; TZ = tz;
			BX = bx; BY = by; BZ = bz;
			NX = nx; NY = ny; NZ = nz;
		}
	}

	public readonly struct UITexArrayVertex
	{
		public readonly float X, Y;
		public readonly float U, V;
		public readonly int Type;
		public readonly int Layer; // reserve
		public readonly int Intensity; // reserve

		public UITexArrayVertex(float x, float y, float u, float v, int type = -1, int layer = -1, int intensity = -1)
		{
			X = x; Y = y;
			U = u; V = v;
			Type = type;
			Layer = layer;
			Intensity = intensity;
		}
	}

	public readonly struct TerrainMaskVertex
	{
		public readonly float X, Y;
		public readonly float U, V;
		public readonly int Type;
		public readonly int Layer;
		public readonly int Intensity;

		public TerrainMaskVertex(float x, float y, float u, float v, int type = -1, int layer = -1, int intensity = -1)
		{
			X = x; Y = y;
			U = u; V = v;
			Type = type;
			Layer = layer;
			Intensity = intensity;
		}
	}

	public struct TerrainBlendingVertex
	{
		public readonly float X, Y;
		public readonly float U, V;
		public readonly float MaskU, MaskV;

		// Color tint
		public readonly float R, G, B, A;

		// TBN
		public readonly float TX, TY, TZ;
		public readonly float BX, BY, BZ;
		public readonly float NX, NY, NZ;

		public readonly int TileType1, TileType2, TileType3, TileType4;
		public readonly int TileType5, TileType6, TileType7, TileType8;

		public TerrainBlendingVertex(
									in float2 xy,
									in float2 uv,
									in float2 maskuv,
									in Vec3x3 tbn,
									in float3 tint, float a,
									int t1 = 1, int t2 = 2, int t3 = 3, int t4 = 4,
									int t5 = 5, int t6 = 6, int t7 = 7, int t8 = 8)
			: this(
				  xy.X, xy.Y,
				  uv.X, uv.Y,
				  maskuv.X, maskuv.Y,
				  tint.X, tint.Y, tint.Z, a,
				  tbn.T.X, tbn.T.Y, tbn.T.Z,
				  tbn.B.X, tbn.B.Y, tbn.B.Z,
				  tbn.N.X, tbn.N.Y, tbn.N.Z,
				  t1, t2, t3, t4,
				  t5, t6, t7, t8)
		{ }

		public TerrainBlendingVertex(
							in float2 xy,
							in float2 uv,
							in float2 maskuv,
							in Vec3x3 tbn,
							in float3 tint, float a,
							TerrainBlendingVertex vert)
	: this(
			xy.X, xy.Y,
			uv.X, uv.Y,
			maskuv.X, maskuv.Y,
			tint.X, tint.Y, tint.Z, a,
			tbn.T.X, tbn.T.Y, tbn.T.Z,
			tbn.B.X, tbn.B.Y, tbn.B.Z,
			tbn.N.X, tbn.N.Y, tbn.N.Z,
			vert.TileType1, vert.TileType2, vert.TileType3, vert.TileType4,
			vert.TileType5, vert.TileType6, vert.TileType7, vert.TileType8)
		{ }

		public TerrainBlendingVertex(
									float x, float y,
									float u, float v,
									float masku, float maskv,
									float r, float g, float b, float a,
									float tx, float ty, float tz,
									float bx, float by, float bz,
									float nx, float ny, float nz,
									int t1, int t2, int t3, int t4,
									int t5, int t6, int t7, int t8)
		{
			X = x; Y = y;
			U = u; V = v;
			MaskU = masku; MaskV = maskv;
			R = r; G = g; B = b; A = a;
			TX = tx; TY = ty; TZ = tz;
			BX = bx; BY = by; BZ = bz;
			NX = nx; NY = ny; NZ = nz;
			TileType1 = t1; TileType2 = t2; TileType3 = t3; TileType4 = t4;
			TileType5 = t5; TileType6 = t6; TileType7 = t7; TileType8 = t8;
		}
	};

	public struct TerrainFinalVertex
	{
		public readonly float X, Y, Z;
		public readonly float U, V;

		public TerrainFinalVertex(in float3 xyz, in float2 uv)
			: this(
				  xyz.X, xyz.Y, xyz.Z,
				  uv.X, uv.Y)
		{ }

		public TerrainFinalVertex(
			float x, float y, float z,
			float u, float v
			)
		{
			X = x; Y = y; Z = z;
			U = u; V = v;
		}
	};

	public struct MeshVertex
	{
		public readonly float X, Y, Z;
		public readonly float NX, NY, NZ;
		public readonly float U, V;
		public readonly int BoneId1, BoneId2, BoneId3, BoneId4;
		public readonly float BoneWeight1, BoneWeight2, BoneWeight3, BoneWeight4;

		public MeshVertex(float x, float y, float z, float nx, float ny, float nz, float u, float v,
			int id1, int id2, int id3, int id4,
			float bw1, float bw2, float bw3, float bw4)
		{
			X = x;
			Y = y;
			Z = z;
			NX = nx;
			NY = ny;
			NZ = nz;
			U = u;
			V = v;
			BoneId1 = id1;
			BoneId2 = id2;
			BoneId3 = id3;
			BoneId4 = id4;
			BoneWeight1 = bw1;
			BoneWeight2 = bw2;
			BoneWeight3 = bw3;
			BoneWeight4 = bw4;
		}
	};

}
