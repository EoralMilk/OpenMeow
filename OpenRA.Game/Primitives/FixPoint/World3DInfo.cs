using System.Numerics;
using OpenRA.Graphics;
using TrueSync;

namespace OpenRA.Primitives
{
	public class World3DCoordinate
	{
		public const int WDistPerMeter = 256;
		public static TSVector Front = new TSVector(0, 1, 0);
		public static TSVector Up = new TSVector(0, 0, 1);

		public static TSVector WPosToTSVec3(WPos pos)
		{
			return new TSVector(-new FP(pos.X) / WDistPerMeter,
												new FP(pos.Y) / WDistPerMeter,
												new FP(pos.Z) / WDistPerMeter);
		}

		public static TSVector WVecToTSVec3(WVec vec)
		{
			return new TSVector(-new FP(vec.X) / WDistPerMeter,
												new FP(vec.Y) / WDistPerMeter,
												new FP(vec.Z) / WDistPerMeter);
		}

		public static WPos TSVec3ToWPos(in TSVector vec)
		{
			return new WPos(-(int)(vec.x * WDistPerMeter),
										(int)(vec.y * WDistPerMeter),
										(int)(vec.z * WDistPerMeter));
		}

		public static WVec TSVec3ToWVec(in TSVector vec)
		{
			return new WVec(-(int)(vec.x * WDistPerMeter),
										(int)(vec.y * WDistPerMeter),
										(int)(vec.z * WDistPerMeter));
		}

		public static Vector3 TSVec3ToVec3(TSVector vec)
		{
			return new Vector3(vec.x.AsFloat(), vec.y.AsFloat(), vec.z.AsFloat());
		}

		public static float3 TSVec3ToFloat3(TSVector vec)
		{
			return new float3(vec.x.AsFloat(), vec.y.AsFloat(), vec.z.AsFloat());
		}

		public static float3 Vec3toFloat3(Vector3 vec)
		{
			return new float3(vec.X, vec.Y, vec.Z);
		}

		public static Vector3 Float3toVec3(float3 f3)
		{
			return new Vector3(f3.X, f3.Y, f3.Z);
		}

		public static Vector3 WPosToVec3(WPos pos)
		{
			return new Vector3(-(float)pos.X / WDistPerMeter,
										(float)pos.Y / WDistPerMeter,
										(float)pos.Z / WDistPerMeter);
		}

		public static Vector3 WVecToVec3(WVec vec)
		{
			return new Vector3(-(float)vec.X / WDistPerMeter,
										(float)vec.Y / WDistPerMeter,
										(float)vec.Z / WDistPerMeter);
		}

		public static float3 WPosToFloat3(WPos pos)
		{
			return new float3(-(float)pos.X / WDistPerMeter,
										(float)pos.Y / WDistPerMeter,
										(float)pos.Z / WDistPerMeter);
		}

		public static float3 WVecToFloat3(WVec vec)
		{
			return new float3(-(float)vec.X / WDistPerMeter,
										(float)vec.Y / WDistPerMeter,
										(float)vec.Z / WDistPerMeter);
		}

		/// <summary>
		/// warning: only use for render
		/// </summary>
		public static WPos Float3ToWPosForRender(in float3 f3)
		{
			return new WPos(-(int)(f3.X * WDistPerMeter),
										(int)(f3.Y * WDistPerMeter),
										(int)(f3.Z * WDistPerMeter));
		}

		/// <summary>
		/// warning: only use for render
		/// </summary>
		public static WPos Vec3ToWPosForRender(in Vector3 v3)
		{
			return new WPos(-(int)(v3.X * WDistPerMeter),
										(int)(v3.Y * WDistPerMeter),
										(int)(v3.Z * WDistPerMeter));
		}

		public static WPos GetWPosFromMatrix(in TSMatrix4x4 matrix)
		{
			return new WPos(-(int)(matrix.M14 * WDistPerMeter),
										(int)(matrix.M24 * WDistPerMeter),
										(int)(matrix.M34 * WDistPerMeter));
		}

		public static WRot GetWRotFromBoneMatrix(in TSMatrix4x4 matrix)
		{
			var q = Transformation.MatRotation(in matrix);
			return WRot.FromBoneQuat(q);
		}

		public static TSQuaternion Get3DRotationFromWRot(in WRot rot)
		{
			return rot.ToQuat();
		}
	}
}
