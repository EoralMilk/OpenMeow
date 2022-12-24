using System;
using System.Runtime.ConstrainedExecution;
using GlmSharp;
using OpenRA.Primitives;
using TrueSync;

namespace OpenRA
{
	public struct TerrainVertex
	{
		public float3 Pos;
		public WPos LogicPos;
		public mat3 TBN;
		public float2 UV;
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
			TSVector tlnml, TSVector trnml, TSVector blnml, TSVector brnml)
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

		public static mat3 CalTBN(float3 a, float3 b, float3 c, float2 uva, float2 uvb, float2 uvc)
		{
			// positions
			vec3 pos1 = World3DCoordinate.Float3toVec3(a);
			vec3 pos2 = World3DCoordinate.Float3toVec3(b);
			vec3 pos3 = World3DCoordinate.Float3toVec3(c);

			// texture coordinates
			vec2 uv1 = new vec2(uva.X, uva.Y);
			vec2 uv2 = new vec2(uvb.X, uvb.Y);
			vec2 uv3 = new vec2(uvc.X, uvc.Y);

			// normal vector
			vec3 nm = CalNormal(a, b, c);

			vec3 edge1 = pos2 - pos1;
			vec3 edge2 = pos3 - pos1;
			vec2 deltaUV1 = uv2 - uv1;
			vec2 deltaUV2 = uv3 - uv1;

			float f = 1.0f / (deltaUV1.x * deltaUV2.y - deltaUV2.x * deltaUV1.y);

			vec3 tangent = vec3.Zero;
			tangent.x = f * (deltaUV2.y * edge1.x - deltaUV1.y * edge2.x);
			tangent.y = f * (deltaUV2.y * edge1.y - deltaUV1.y * edge2.y);
			tangent.z = f * (deltaUV2.y * edge1.z - deltaUV1.y * edge2.z);
			tangent = tangent.Normalized;

			vec3 bitangent = vec3.Zero;
			bitangent.x = f * (-deltaUV2.x * edge1.x + deltaUV1.x * edge2.x);
			bitangent.y = f * (-deltaUV2.x * edge1.y + deltaUV1.x * edge2.y);
			bitangent.z = f * (-deltaUV2.x * edge1.z + deltaUV1.x * edge2.z);
			bitangent = bitangent.Normalized;

			return new mat3(tangent, bitangent, nm);
		}

		public static vec3 CalNormal(float3 a, float3 b, float3 c)
		{
			var va = new vec3(a.X, a.Y, a.Z);
			var vb = new vec3(b.X, b.Y, b.Z);
			var vc = new vec3(c.X, c.Y, c.Z);
			var ab = vb - va;
			var ac = vc - va;
			return vec3.Cross(ab, ac).Normalized;
		}

		public static mat3 NormalizeTBN(mat3 tbn)
		{
			return new mat3(tbn.Column0.NormalizedSafe, tbn.Column1.NormalizedSafe, tbn.Column2.NormalizedSafe);
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
