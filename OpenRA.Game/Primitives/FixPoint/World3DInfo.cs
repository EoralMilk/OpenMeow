using GlmSharp;
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

		public static vec3 TSVec3ToVec3(TSVector vec)
		{
			return new vec3(vec.x.AsFloat(), vec.y.AsFloat(), vec.z.AsFloat());
		}

		public static float3 TSVec3ToFloat3(TSVector vec)
		{
			return new float3(vec.x.AsFloat(), vec.y.AsFloat(), vec.z.AsFloat());
		}

		public static float3 Vec3toFloat3(vec3 vec)
		{
			return new float3(vec.x, vec.y, vec.z);
		}

		public static vec3 Float3toVec3(float3 f3)
		{
			return new vec3(f3.X, f3.Y, f3.Z);
		}

		public static vec3 WPosToVec3(WPos pos)
		{
			return new vec3(-(float)pos.X / WDistPerMeter,
										(float)pos.Y / WDistPerMeter,
										(float)pos.Z / WDistPerMeter);
		}

		public static vec3 WVecToVec3(WVec vec)
		{
			return new vec3(-(float)vec.X / WDistPerMeter,
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
		public static WPos Vec3ToWPosForRender(in vec3 v3)
		{
			return new WPos(-(int)(v3.x * WDistPerMeter),
										(int)(v3.y * WDistPerMeter),
										(int)(v3.z * WDistPerMeter));
		}

		public static WPos GetWPosFromMatrix(in TSMatrix4x4 matrix)
		{
			return new WPos(-(int)(matrix.M14 * WDistPerMeter),
										(int)(matrix.M24 * WDistPerMeter),
										(int)(matrix.M34 * WDistPerMeter));
		}

		public static WRot GetWRotFromMatrix(in TSMatrix4x4 matrix)
		{
			var q = Transformation.MatRotation(in matrix);
			var v = q.eulerAngles;
			return new WRot(
				new WAngle(0 + (int)(v.y * 512 / 180)),
				new WAngle(256 + (int)(v.x * 512 / 180)),
				new WAngle(0 + (int)(v.z * 512 / 180)));
		}

		public static TSQuaternion Get3DRotationFromWRot(in WRot rot)
		{
			return rot.ToQuat();
		}

		public static mat4 TSMatrix4x4ToMat4(TSMatrix4x4 tSMatrix4X4)
		{
			mat4 mat = mat4.Identity;
			var c0 = tSMatrix4X4.Column0;
			var c1 = tSMatrix4X4.Column1;
			var c2 = tSMatrix4X4.Column2;
			var c3 = tSMatrix4X4.Column3;
			mat[0] = (float)c0.x;
			mat[1] = (float)c0.y;
			mat[2] = (float)c0.z;
			mat[3] = (float)c0.w;
			mat[4] = (float)c1.x;
			mat[5] = (float)c1.y;
			mat[6] = (float)c1.z;
			mat[7] = (float)c1.w;
			mat[8] = (float)c2.x;
			mat[9] = (float)c2.y;
			mat[10] = (float)c2.z;
			mat[11] = (float)c2.w;
			mat[12] = (float)c3.x;
			mat[13] = (float)c3.y;
			mat[14] = (float)c3.z;
			mat[15] = (float)c3.w;
			return mat;
		}
	}
}
