using GlmSharp;
using TrueSync;

namespace OpenRA.Primitives.FixPoint
{
	public class World3DCoordinate
	{
		public const int WPosPerMeter = 256;
		public static TSVector Front = new TSVector(0, 1, 0);
		public static TSVector Up = new TSVector(0, 0, 1);

		public static TSVector WPosToTSVec3(WPos pos)
		{
			return new TSVector(-new FP(pos.X) / WPosPerMeter,
												new FP(pos.Y) / WPosPerMeter,
												new FP(pos.Z) / WPosPerMeter);
		}

		public static TSVector WVecToTSVec3(WVec vec)
		{
			return new TSVector(-new FP(vec.X) / WPosPerMeter,
												new FP(vec.Y) / WPosPerMeter,
												new FP(vec.Z) / WPosPerMeter);
		}

		public static WPos TSVec3ToWPos(in TSVector vec)
		{
			return new WPos(-(int)(vec.x * WPosPerMeter),
										(int)(vec.y * WPosPerMeter),
										(int)(vec.z * WPosPerMeter));
		}

		public static WVec TSVec3ToWVec(in TSVector vec)
		{
			return new WVec(-(int)(vec.x * WPosPerMeter),
										(int)(vec.y * WPosPerMeter),
										(int)(vec.z * WPosPerMeter));
		}

		public static vec3 TSVec3ToRVec3(TSVector vec)
		{
			return new vec3(vec.x.AsFloat(), vec.y.AsFloat(), vec.z.AsFloat());
		}

		public static float3 Vec3toFloat3(vec3 vec)
		{
			return new float3(vec.x, vec.y, vec.z);
		}

		public static vec3 Float3toVec3(float3 f3)
		{
			return new vec3(f3.X, f3.Y, f3.Z);
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
