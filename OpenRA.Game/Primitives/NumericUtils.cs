using System;
using System.Numerics;

namespace OpenRA.Primitives
{
	public static class NumericUtil
	{
		public static Vector4 Column0(in Matrix4x4 mat4) { return new Vector4(mat4.M11, mat4.M21, mat4.M31, mat4.M41); }
		public static Vector4 Column1(in Matrix4x4 mat4) { return new Vector4(mat4.M12, mat4.M22, mat4.M32, mat4.M42); }
		public static Vector4 Column2(in Matrix4x4 mat4) { return new Vector4(mat4.M13, mat4.M23, mat4.M33, mat4.M43); }
		public static Vector4 Column3(in Matrix4x4 mat4) { return new Vector4(mat4.M14, mat4.M24, mat4.M34, mat4.M44); }

		public static Vector3 Column0xyz(in Matrix4x4 mat4) { return new Vector3(mat4.M11, mat4.M21, mat4.M31); }
		public static Vector3 Column1xyz(in Matrix4x4 mat4) { return new Vector3(mat4.M12, mat4.M22, mat4.M32); }
		public static Vector3 Column2xyz(in Matrix4x4 mat4) { return new Vector3(mat4.M13, mat4.M23, mat4.M33); }
		public static Vector3 Column3xyz(in Matrix4x4 mat4) { return new Vector3(mat4.M14, mat4.M24, mat4.M34); }

		public static float ToRadians(float degrees)
		{
			return (degrees * MathF.PI / 180);
		}

		public static float ToDegrees(float radians)
		{
			return (radians * 180 / MathF.PI);
		}

		public static Quaternion QuaternionMul(Quaternion q, float x)
		{
			return new Quaternion(x * q.X, x * q.Y, x * q.Z, x * q.W);
		}

		public static Quaternion QuaternionMul(float x, Quaternion q)
		{
			return new Quaternion(x * q.X, x * q.Y, x * q.Z, x * q.W);
		}

		/// <summary>
		/// mat4 as col
		/// </summary>
		public static float[] MatRenderValues(Matrix4x4 matrix)
		{
			return new float[16]
			{
				matrix.M11,
				matrix.M21,
				matrix.M31,
				matrix.M41,

				matrix.M12,
				matrix.M22,
				matrix.M32,
				matrix.M42,

				matrix.M13,
				matrix.M23,
				matrix.M33,
				matrix.M43,

				matrix.M14,
				matrix.M24,
				matrix.M34,
				matrix.M44
			};
		}


	}
}
