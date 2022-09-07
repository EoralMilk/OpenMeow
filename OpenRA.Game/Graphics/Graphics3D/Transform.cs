using TrueSync;

namespace OpenRA.Graphics
{
	/// <summary>
	/// Transform用于存储变化信息和最重要的变换矩阵
	/// </summary>
	public struct Transformation
	{
		public TSVector Scale;
		public TSQuaternion Rotation;
		public TSVector Position;
		public static Transformation Identity { get { return new Transformation(TSVector.one, TSQuaternion.identity, TSVector.zero); } }
		public TSMatrix4x4 Matrix { get { return TSMatrix4x4.TRS(Position, Rotation, Scale); } }
		public Transformation(TSVector s, TSQuaternion r, TSVector t)
		{
			Scale = s;
			Rotation = r;
			Position = t;
		}

		public Transformation(TSMatrix4x4 mat)
		{
			Scale = MatScale(mat);
			Rotation = MatRotation(mat, Scale);
			Position = MatPosition(mat);
		}

		public static TSMatrix4x4 ExtractRotationMatrix(in TSMatrix4x4 matrix, in TSVector scale)
		{
			return new TSMatrix4x4(matrix.Column0 / scale.x, matrix.Column1 / scale.y, matrix.Column2 / scale.z, new TSVector4(0, 0, 0, FP.FromFloat(1.0f)));
		}

		public static TSVector MatPosition(in TSMatrix4x4 matrix)
		{
			return new TSVector(matrix.Column3.x, matrix.Column3.y, matrix.Column3.z);
		}

		public static TSVector MatScale(in TSMatrix4x4 matrix)
		{
			return new TSVector(matrix.Column0.xyz.magnitude, matrix.Column1.xyz.magnitude, matrix.Column2.xyz.magnitude);
		}

		public static TSMatrix4x4 MatWithOutScale(TSMatrix4x4 matrix)
		{
			var s = new TSVector(matrix.Column0.xyz.magnitude, matrix.Column1.xyz.magnitude, matrix.Column2.xyz.magnitude);
			matrix.M11 /= s.x; matrix.M12 /= s.y; matrix.M13 /= s.z;
			matrix.M21 /= s.x; matrix.M22 /= s.y; matrix.M23 /= s.z;
			matrix.M31 /= s.x; matrix.M32 /= s.y; matrix.M33 /= s.z;
			return matrix;
		}

		public static TSMatrix4x4 MatWithNewScale(TSMatrix4x4 matrix, FP scale)
		{
			var s = new TSVector(matrix.Column0.xyz.magnitude, matrix.Column1.xyz.magnitude, matrix.Column2.xyz.magnitude) / scale;
			matrix.M11 /= s.x; matrix.M12 /= s.y; matrix.M13 /= s.z;
			matrix.M21 /= s.x; matrix.M22 /= s.y; matrix.M23 /= s.z;
			matrix.M31 /= s.x; matrix.M32 /= s.y; matrix.M33 /= s.z;
			return matrix;
		}

		public static TSMatrix4x4 MatWithNewScale(TSMatrix4x4 matrix, TSVector scale)
		{
			var s = new TSVector(matrix.Column0.xyz.magnitude / scale.x, matrix.Column1.xyz.magnitude / scale.y, matrix.Column2.xyz.magnitude / scale.z);
			matrix.M11 /= s.x; matrix.M12 /= s.y; matrix.M13 /= s.z;
			matrix.M21 /= s.x; matrix.M22 /= s.y; matrix.M23 /= s.z;
			matrix.M31 /= s.x; matrix.M32 /= s.y; matrix.M33 /= s.z;
			return matrix;
		}

		// 获取旋转四元数
		public static TSQuaternion MatRotation(in TSMatrix4x4 matrix)
		{
			return TSQuaternion.CreateFromMatrix(ExtractRotationMatrix(matrix, MatScale(matrix)).Matrix3x3);
		}

		public static TSQuaternion MatRotation(in TSMatrix4x4 matrix, in TSVector scale)
		{
			return TSQuaternion.CreateFromMatrix(ExtractRotationMatrix(matrix, scale).Matrix3x3);
		}

		// 应用一个旋转到自身
		// 两个四元数 q1 和 q2 的乘积表示一个旋转。q1q2表示先施加旋转q2，再施加旋转q1
		public void Rotated(in TSQuaternion rotate)
		{
			Rotation = rotate * Rotation;
		}

		// 应用一个缩放到自身
		public void Scaled(in TSVector scale)
		{
			Scale.Scale(scale);
		}

		// 应用位移
		public void Translated(in TSVector offset)
		{
			Position += offset;
		}

		/// <summary>
		/// 计算量较大，不应该大量使用。
		/// </summary>
		/// <param name="then">要应用的变换</param>
		/// <returns>变换结果</returns>
		public Transformation Then(in Transformation then)
		{
			var m = then.Matrix * Matrix;

			Scale = MatScale(m);
			Rotation = MatRotation(m, Scale);
			Position = MatPosition(m);
			return this;
		}

		// 变换混合，分离SRT依次进行混合
		public static Transformation Blend(in Transformation a, in Transformation b, FP blend)
		{
			var s = TSVector.Lerp(a.Scale, b.Scale, blend);

			var r = TSQuaternion.FastSlerp(a.Rotation, b.Rotation, blend);

			var p = TSVector.Lerp(a.Position, b.Position, blend);

			return new Transformation(s, r, p);
		}

		public static TSMatrix4x4 Mat4by(in Transformation trans)
		{
			return trans.Matrix;
		}
	}
}
