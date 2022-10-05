using GlmSharp;
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
	}

	public struct CellInfo
	{
		public const float TU = 0f;
		public const float TV = 0f;

		public const float LU = 0f;
		public const float LV = 1f;

		public const float BU = 1f;
		public const float BV = 1f;

		public const float RU = 1f;
		public const float RV = 0f;

		public readonly WPos CellCenterPos;
		public readonly int CellMiniHeight;

		public readonly uint Type;

		public readonly int2 MiniCellTL, MiniCellTR, MiniCellBL, MiniCellBR;

		public readonly int M, T, B, L, R;

		public readonly bool Flat;

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
			int m, int t, int b, int l, int r, uint type, int2 ctl, int2 ctr, int2 cbl, int2 cbr, bool flat,
			TSVector tlnml, TSVector trnml, TSVector blnml, TSVector brnml)
		{
			CellCenterPos = center;
			CellMiniHeight = miniHeight;
			M = m;
			T = t;
			B = b;
			L = l;
			R = r;
			Type = type;

			MiniCellTL = ctl;
			MiniCellTR = ctr;
			MiniCellBL = cbl;
			MiniCellBR = cbr;

			Flat = flat;
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
