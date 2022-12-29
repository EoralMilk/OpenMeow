using System;
using System.Runtime.ConstrainedExecution;
using System.Numerics;
using OpenRA.Primitives;
using TrueSync;

namespace OpenRA
{
	public struct Vec3x3
	{
		public readonly Vector3 T;
		public readonly Vector3 B;
		public readonly Vector3 N;
		public Vec3x3(Vector3 t, Vector3 b, Vector3 n)
		{
			T = t; B = b; N = n;
		}

		bool Equals(Vec3x3 other)
		{
			return T == other.T && B == other.B && N == other.N;
		}

		public override bool Equals(object obj)
		{
			return obj is Vec3x3 x && Equals(x);
		}

		public override int GetHashCode()
		{
			var hashCode = default(HashCode);
			hashCode.Add(T.GetHashCode());
			hashCode.Add(B.GetHashCode());
			hashCode.Add(N.GetHashCode());
			return hashCode.ToHashCode();
		}

		public static bool operator ==(in Vec3x3 left, in Vec3x3 right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(in Vec3x3 left, in Vec3x3 right)
		{
			return !(left == right);
		}

		public static Vec3x3 operator +(in Vec3x3 left, in Vec3x3 right)
		{
			return new Vec3x3(left.T + right.T, left.B + right.B, left.N + right.N);
		}

		public static Vec3x3 operator *(in Vec3x3 left, in Vec3x3 right)
		{
			return new Vec3x3(left.T * right.T, left.B * right.B, left.N * right.N);
		}

		public static Vec3x3 operator *(in Vec3x3 left, in float right)
		{
			return new Vec3x3(left.T * right, left.B * right, left.N * right);
		}

		public static Vec3x3 operator *(in float left, in Vec3x3 right)
		{
			return new Vec3x3(left * right.T, left * right.B, left * right.N);
		}
	}

	public struct TerrainVertex
	{
		public float3 Pos;
		public WPos LogicPos;
		public Vec3x3 TBN;
		public float2 UV;
		public float2 MapUV;
		public float3 Color;

		public void UpdatePosHeight(int h)
		{
			LogicPos = new WPos(LogicPos.X, LogicPos.Y, h);
			Pos = new float3(-(float)LogicPos.X / 256,
										(float)LogicPos.Y / 256,
										(float)LogicPos.Z / 256);
		}

		public void UpdatePos()
		{
			Pos = new float3(-(float)LogicPos.X / 256,
										(float)LogicPos.Y / 256,
										(float)LogicPos.Z / 256);
		}
	}

	public class CellInfo
	{
		public const float TU = 0f;
		public const float TV = 0f;

		public const float LU = 0f;
		public const float LV = 1f;

		public const float BU = 1f;
		public const float BV = 1f;

		public const float RU = 1f;
		public const float RV = 0f;

		public WPos CellCenterPos;
		public int CellMiniHeight;

		/// <summary>
		/// use as MiniCells[CellInfos[cell].MiniCellTR.Y, CellInfos[cell].MiniCellTR.X]
		/// </summary>
		public readonly int2 MiniCellTL, MiniCellTR, MiniCellBL, MiniCellBR;

		public readonly int M, T, B, L, R;

		public Color ColorA;
		public Color ColorB;

		public ushort TileType;
		public byte TerrainType;

		public bool Flat;
		public bool AlmostFlat;

		public TSVector LogicNmlTL;
		public TSVector LogicNmlTR;
		public TSVector LogicNmlBL;
		public TSVector LogicNmlBR;
		public TSVector LogicNml;

		public WRot TerrainOrientationTL;
		public WRot TerrainOrientationTR;
		public WRot TerrainOrientationBL;
		public WRot TerrainOrientationBR;
		public WRot TerrainOrientationM;

		public CellInfo(WPos center, int miniHeight,
			int m, int t, int b, int l, int r, int2 ctl, int2 ctr, int2 cbl, int2 cbr, bool flat, bool almostFlat,
			TSVector tlnml, TSVector trnml, TSVector blnml, TSVector brnml, Map map)
		{
			CellCenterPos = center;
			CellMiniHeight = miniHeight;
			M = m;
			T = t;
			B = b;
			L = l;
			R = r;

			MiniCellTL = ctl;
			MiniCellTR = ctr;
			MiniCellBL = cbl;
			MiniCellBR = cbr;

			Flat = flat;
			AlmostFlat = almostFlat;
			LogicNmlTL = tlnml.normalized;
			LogicNmlTR = trnml.normalized;
			LogicNmlBL = blnml.normalized;
			LogicNmlBR = brnml.normalized;
			LogicNml = (LogicNmlTL + LogicNmlTR + LogicNmlBL + LogicNmlBR) / 4;
			LogicNml = LogicNml.normalized;

			TerrainOrientationTL = Map.NormalToTerrainOrientation(LogicNmlTL);
			TerrainOrientationTR = Map.NormalToTerrainOrientation(LogicNmlTR);
			TerrainOrientationBL = Map.NormalToTerrainOrientation(LogicNmlBL);
			TerrainOrientationBR = Map.NormalToTerrainOrientation(LogicNmlBR);
			TerrainOrientationM = Map.NormalToTerrainOrientation(LogicNml);

			ColorA = Color.FromFloat3(0.3f * map.TerrainVertices[T].Color + 0.3f * map.TerrainVertices[L].Color + 0.4f * map.TerrainVertices[M].Color);
			ColorB = Color.FromFloat3(0.3f * map.TerrainVertices[B].Color + 0.3f * map.TerrainVertices[R].Color + 0.4f * map.TerrainVertices[M].Color);
		}

		void UpdateNml(Map map)
		{
			var pm = map.TerrainVertices[M].Pos;
			var pt = map.TerrainVertices[T].Pos;
			var pl = map.TerrainVertices[L].Pos;
			var pb = map.TerrainVertices[B].Pos;
			var pr = map.TerrainVertices[R].Pos;

			var uvm = new float2(0.5f, 0.5f);
			var uvt = new float2(CellInfo.TU, CellInfo.TV);
			var uvb = new float2(CellInfo.BU, CellInfo.BV);
			var uvl = new float2(CellInfo.LU, CellInfo.LV);
			var uvr = new float2(CellInfo.RU, CellInfo.RV);

			var tlm = CalTBN(pt, pl, pm, uvt, uvl, uvm);
			var tmr = CalTBN(pt, pm, pr, uvt, uvm, uvr);
			var mlb = CalTBN(pm, pl, pb, uvm, uvl, uvb);
			var mbr = CalTBN(pm, pb, pr, uvm, uvb, uvr);

			map.TerrainVertices[M].TBN = tlm * 0.25f + tmr * 0.25f + mlb * 0.25f + mbr * 0.25f;
			map.TerrainVertices[T].TBN = 0.25f * (tlm * 0.5f + tmr * 0.5f) + 0.75f * map.TerrainVertices[T].TBN;
			map.TerrainVertices[B].TBN = 0.25f * (mlb * 0.5f + mbr * 0.5f) + 0.75f * map.TerrainVertices[B].TBN;
			map.TerrainVertices[L].TBN = 0.25f * (mlb * 0.5f + tlm * 0.5f) + 0.75f * map.TerrainVertices[L].TBN;
			map.TerrainVertices[R].TBN = 0.25f * (tmr * 0.5f + mbr * 0.5f) + 0.75f * map.TerrainVertices[R].TBN;

			WPos wposm = map.TerrainVertices[M].LogicPos;
			WPos wpost = map.TerrainVertices[T].LogicPos;
			WPos wposb = map.TerrainVertices[B].LogicPos;
			WPos wposl = map.TerrainVertices[L].LogicPos;
			WPos wposr = map.TerrainVertices[R].LogicPos;

			var tlNml = CalLogicNml(wposm, wpost, wposl);
			var trNml = CalLogicNml(wposm, wposr, wpost);
			var blNml = CalLogicNml(wposm, wposl, wposb);
			var brNml = CalLogicNml(wposm, wposb, wposl);

			LogicNmlTL = tlNml;
			LogicNmlTR = trNml;
			LogicNmlBL = blNml;
			LogicNmlBR = brNml;
			LogicNml = (LogicNmlTL + LogicNmlTR + LogicNmlBL + LogicNmlBR) / 4;
			LogicNml = LogicNml.normalized;

			TerrainOrientationTL = Map.NormalToTerrainOrientation(LogicNmlTL);
			TerrainOrientationTR = Map.NormalToTerrainOrientation(LogicNmlTR);
			TerrainOrientationBL = Map.NormalToTerrainOrientation(LogicNmlBL);
			TerrainOrientationBR = Map.NormalToTerrainOrientation(LogicNmlBR);
			TerrainOrientationM = Map.NormalToTerrainOrientation(LogicNml);
		}

		public void UpdateCell(Map map)
		{
			CellCenterPos = map.TerrainVertices[M].LogicPos;

			CellMiniHeight =
				Math.Min(
						Math.Min(
							Math.Min(
								Math.Min(map.TerrainVertices[M].LogicPos.Z,
									map.TerrainVertices[T].LogicPos.Z),
								map.TerrainVertices[B].LogicPos.Z),
							map.TerrainVertices[R].LogicPos.Z),
						map.TerrainVertices[L].LogicPos.Z);

			if (map.TerrainVertices[M].LogicPos.Z == map.TerrainVertices[T].LogicPos.Z &&
				map.TerrainVertices[M].LogicPos.Z == map.TerrainVertices[B].LogicPos.Z &&
				map.TerrainVertices[M].LogicPos.Z == map.TerrainVertices[R].LogicPos.Z &&
				map.TerrainVertices[M].LogicPos.Z == map.TerrainVertices[L].LogicPos.Z)
			{
				AlmostFlat = true;
				Flat = true;
			}

			map.TerrainVertices[M].UpdatePos();
			map.TerrainVertices[T].UpdatePos();
			map.TerrainVertices[L].UpdatePos();
			map.TerrainVertices[B].UpdatePos();
			map.TerrainVertices[R].UpdatePos();

			UpdateNml(map);
		}

		public void FlatCell(int h, Map map)
		{
			CellCenterPos = new WPos(CellCenterPos.X, CellCenterPos.Y, h);
			CellMiniHeight = h;

			map.TerrainVertices[M].UpdatePosHeight(h);
			map.TerrainVertices[T].UpdatePosHeight(h);
			map.TerrainVertices[L].UpdatePosHeight(h);
			map.TerrainVertices[B].UpdatePosHeight(h);
			map.TerrainVertices[R].UpdatePosHeight(h);

			UpdateNml(map);

			Flat = true;
			AlmostFlat = true;
		}

		public static Vec3x3 CalTBN(float3 a, float3 b, float3 c, float2 uva, float2 uvb, float2 uvc)
		{
			return CalTBN(a, b, c, uva, uvb, uvc, CalNormal(a, b, c));
		}

		public static Vec3x3 CalTBN(float3 a, float3 b, float3 c, float2 uva, float2 uvb, float2 uvc, Vector3 nm)
		{
			// positions
			Vector3 pos1 = World3DCoordinate.Float3toVec3(a);
			Vector3 pos2 = World3DCoordinate.Float3toVec3(b);
			Vector3 pos3 = World3DCoordinate.Float3toVec3(c);

			// texture coordinates
			Vector2 uv1 = new Vector2(uva.X, uva.Y);
			Vector2 uv2 = new Vector2(uvb.X, uvb.Y);
			Vector2 uv3 = new Vector2(uvc.X, uvc.Y);

			Vector3 edge1 = pos2 - pos1;
			Vector3 edge2 = pos3 - pos1;
			Vector2 deltaUV1 = uv2 - uv1;
			Vector2 deltaUV2 = uv3 - uv1;

			float f = 1.0f / (deltaUV1.X * deltaUV2.Y - deltaUV2.X * deltaUV1.Y);

			Vector3 tangent = Vector3.Zero;
			tangent.X = f * (deltaUV2.Y * edge1.X - deltaUV1.Y * edge2.X);
			tangent.Y = f * (deltaUV2.Y * edge1.Y - deltaUV1.Y * edge2.Y);
			tangent.Z = f * (deltaUV2.Y * edge1.Z - deltaUV1.Y * edge2.Z);
			tangent = Vector3.Normalize(tangent);

			Vector3 bitangent = Vector3.Zero;
			bitangent.X = f * (-deltaUV2.X * edge1.X + deltaUV1.X * edge2.X);
			bitangent.Y = f * (-deltaUV2.X * edge1.Y + deltaUV1.X * edge2.Y);
			bitangent.Z = f * (-deltaUV2.X * edge1.Z + deltaUV1.X * edge2.Z);
			bitangent = Vector3.Normalize(bitangent);

			return new Vec3x3(tangent, bitangent, nm);
		}

		public static Vector3 CalNormal(float3 a, float3 b, float3 c)
		{
			var va = new Vector3(a.X, a.Y, a.Z);
			var vb = new Vector3(b.X, b.Y, b.Z);
			var vc = new Vector3(c.X, c.Y, c.Z);
			var ab = vb - va;
			var ac = vc - va;
			return Vector3.Normalize(Vector3.Cross(ab, ac));
		}

		public static Vec3x3 NormalizeTBN(Vec3x3 tbn)
		{
			return new Vec3x3(Vector3.Normalize(tbn.T), Vector3.Normalize(tbn.B), Vector3.Normalize(tbn.N));
		}

		public static TSVector CalLogicNml(WPos a, WPos b, WPos c)
		{
			var va = TileTSVector(a);
			var vb = TileTSVector(b);
			var vc = TileTSVector(c);

			var ab = vb - va;
			var ac = vc - va;

			return TSVector.Cross(ab, ac).normalized;
		}

		public static TSVector TileTSVector(WPos pos)
		{
			return new TSVector(-(FP)pos.X / 256,
										(FP)pos.Y / 256,
										(FP)pos.Z / 256);
		}
	}

	public enum MiniCellType
	{
		/// <summary>
		/// /
		/// </summary>
		TRBL,

		/// <summary>
		/// \
		/// </summary>
		TLBR,
	}

	public struct MiniCell
	{
		public readonly int A1, B1, C1;
		public readonly int A2, B2, C2;

		/// <summary>
		/// ┌
		/// </summary>
		public readonly int TL;

		/// <summary>
		/// ┐
		/// </summary>
		public readonly int TR;

		/// <summary>
		/// └
		/// </summary>
		public readonly int BL;

		/// <summary>
		/// ┘
		/// </summary>
		public readonly int BR;

		public readonly MiniCellType Type;
		public MiniCell(
			int tl, int tr,
			int bl, int br,
			MiniCellType type)
		{
			TL = tl;
			TR = tr;
			BL = bl;
			BR = br;
			Type = type;
			if (Type == MiniCellType.TRBL)
			{
				A1 = TR;
				B1 = TL;
				C1 = BL;
				A2 = TR;
				B2 = BL;
				C2 = BR;
			}
			else
			{
				A1 = TL;
				B1 = BR;
				C1 = TR;
				A2 = TL;
				B2 = BL;
				C2 = BR;
			}
		}
	}
}
