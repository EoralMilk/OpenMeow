﻿using System;
using System.Numerics;
using OpenRA.Primitives;
using TrueSync;

namespace OpenRA.Graphics
{

	public struct DualQuaternion
	{
		public Quaternion Real;
		public Quaternion Dual;

		public DualQuaternion(Quaternion real, Quaternion dual)
		{
			Real = real;
			Dual = dual;
		}

		public static DualQuaternion CombineDualQuaternions(in DualQuaternion a, in DualQuaternion b)
		{
			Quaternion real = a.Real * b.Real;
			Quaternion dual = (a.Real * b.Dual) + (a.Dual * b.Real);
			return new DualQuaternion(real, dual);
		}
	}

	public struct DQTransform
	{
		public DualQuaternion DQ;
		public Vector3 Scale;

		public DQTransform(DualQuaternion dq, Vector3 scale)
		{
			DQ = dq;
			Scale = scale;
		}

		public DQTransform Then(in DQTransform next)
		{
			var dq = DualQuaternion.CombineDualQuaternions(next.DQ, this.DQ);
			var s = next.Scale * Scale;
			return new DQTransform(dq, s);
		}
	}

	/// <summary>
	/// Transform用于存储变化信息和最重要的变换矩阵
	/// </summary>
	public struct Transformation
	{
		bool scaleUpdated;
		bool rotationUpdated;
		bool positionUpdated;
		bool matrixUpdated;

		TSVector scale;
		TSQuaternion rotation;
		TSVector position;
		TSMatrix4x4 matrix;

		TSVector UpdateScale()
		{
			if (!matrixUpdated)
				throw new Exception("Can't update scale");

			scale = MatScale(matrix);
			scaleUpdated = true;
			return scale;
		}

		TSVector UpdatePosition()
		{
			if (!matrixUpdated)
				throw new Exception("Can't update position");

			position = MatPosition(matrix);
			positionUpdated = true;
			return position;
		}

		TSQuaternion UpdateRotation()
		{
			if (!matrixUpdated)
				throw new Exception("Can't update rotation");

			if (scaleUpdated)
				rotation = MatRotation(matrix, scale);
			else
				rotation = MatRotation(matrix);
			rotationUpdated = true;
			return rotation;
		}

		TSMatrix4x4 UpdateMatrix()
		{
			if (!scaleUpdated || !rotationUpdated || !positionUpdated)
				throw new Exception("Can't update matrix");

			matrix = TSMatrix4x4.Rotate(Rotation) * TSMatrix4x4.Scale(Scale);
			matrix.M14 = Position.x;
			matrix.M24 = Position.y;
			matrix.M34 = Position.z;
			matrixUpdated = true;
			return matrix;
		}

		public TSVector Scale {
			get
			{
				if (scaleUpdated)
					return scale;
				else
					return UpdateScale();
			}
			set
			{
				scale = value;
				scaleUpdated = true;
			}
		}

		public TSQuaternion Rotation
		{
			get
			{
				if (rotationUpdated)
					return rotation;
				else
					return UpdateRotation();
			}
			set
			{
				rotation = value;
				rotationUpdated = true;
			}
		}

		public TSVector Position
		{
			get
			{
				if (positionUpdated)
					return position;
				else
					return UpdatePosition();
			}
			set
			{
				position = value;
				positionUpdated = true;
			}
		}

		public TSMatrix4x4 Matrix {
			get {
				if (matrixUpdated)
					return matrix;
				else
					return UpdateMatrix();
			}
			set {
				matrix = value;
				matrixUpdated = true;
			}
		}

		// public quat DQrot => new quat((float)Rotation.x, (float)Rotation.y, (float)Rotation.z, (float)Rotation.w);
		// public quat DQtrans => new quat((float)Position.x, (float)Position.y, (float)Position.z, 0.0f) * DQrot * 0.5f;
		// public DualQuaternion DQ => new DualQuaternion(DQrot, DQtrans);
		// public DQTransform DQT => new DQTransform(DQ, new vec3((float)Scale.x, (float)Scale.y, (float)Scale.z));

		public static Transformation Identity { get { return new Transformation(TSVector.one, TSQuaternion.identity, TSVector.zero); } }

		public Transformation(TSVector s, TSQuaternion r, TSVector t)
		{
			scale = s;
			scaleUpdated = true;
			rotation = r;
			rotationUpdated = true;
			position = t;
			positionUpdated = true;
			matrix = TSMatrix4x4.Identity;
			matrixUpdated = false;
		}

		public Transformation(TSMatrix4x4 mat)
		{
			scale = TSVector.zero;
			scaleUpdated = false;
			rotation = TSQuaternion.identity;
			rotationUpdated = false;
			position = TSVector.zero;
			positionUpdated = false;
			matrix = mat;
			matrixUpdated = true;
		}

		/// <summary>
		/// for render
		/// </summary>
		public Transformation(DQTransform dqt)
		{
			scale = new TSVector(dqt.Scale);
			scaleUpdated = true;
			rotation = new TSQuaternion(dqt.DQ.Real);
			rotationUpdated = true;

			var v = NumericUtil.QuaternionMul(2.0f,dqt.DQ.Dual) * new Quaternion(-dqt.DQ.Real.X, -dqt.DQ.Real.Y, -dqt.DQ.Real.Z, dqt.DQ.Real.W);
			position = new TSVector(v.X, v.Y, v.Z);
			positionUpdated = true;

			matrix = TSMatrix4x4.Identity;
			matrixUpdated = false;
		}

		public static TSMatrix4x4 ExtractRotationMatrix(in TSMatrix4x4 matrix, in TSVector scale)
		{
			return new TSMatrix4x4(matrix.Column0 / scale.x, matrix.Column1 / scale.y, matrix.Column2 / scale.z, new TSVector4(0, 0, 0, FP.FromFloat(1.0f)));
		}

		public static TSVector MatPosition(in TSMatrix4x4 matrix)
		{
			return new TSVector(matrix.Column3.x, matrix.Column3.y, matrix.Column3.z);
		}

		public static Vector3 MatPosition(in Matrix4x4 matrix)
		{
			return matrix.Translation;
		}

		public static TSVector MatScale(in TSMatrix4x4 matrix)
		{
			return new TSVector(matrix.Column0.xyz.magnitude, matrix.Column1.xyz.magnitude, matrix.Column2.xyz.magnitude);
		}

		public static Vector3 MatScale(in Matrix4x4 matrix)
		{
			return new Vector3(NumericUtil.Column0xyz(matrix).Length(), NumericUtil.Column1xyz(matrix).Length(), NumericUtil.Column2xyz(matrix).Length());
		}

		public static TSMatrix4x4 MatWithOutScale(TSMatrix4x4 matrix)
		{
			var s = new TSVector(matrix.Column0.xyz.magnitude, matrix.Column1.xyz.magnitude, matrix.Column2.xyz.magnitude);
			matrix.M11 /= s.x; matrix.M12 /= s.y; matrix.M13 /= s.z;
			matrix.M21 /= s.x; matrix.M22 /= s.y; matrix.M23 /= s.z;
			matrix.M31 /= s.x; matrix.M32 /= s.y; matrix.M33 /= s.z;
			return matrix;
		}

		public static Matrix4x4 MatWithOutScale(in Matrix4x4 matrix)
		{
			var s = MatScale(matrix);
			return new Matrix4x4(
				matrix.M11 / s.X, matrix.M12 / s.Y, matrix.M13 / s.Z, matrix.M14,
				matrix.M21 / s.X, matrix.M22 / s.Y, matrix.M23 / s.Z, matrix.M24,
				matrix.M31 / s.X, matrix.M32 / s.Y, matrix.M33 / s.Z, matrix.M34,
				matrix.M41, matrix.M42, matrix.M43, matrix.M44);
		}

		public static Matrix4x4 MatWithNewScale(Matrix4x4 matrix, float scale)
		{
			var c1 = Vector3.Normalize(NumericUtil.Column0xyz(matrix)) * scale;
			var c2 = Vector3.Normalize(NumericUtil.Column1xyz(matrix)) * scale;
			var c3 = Vector3.Normalize(NumericUtil.Column2xyz(matrix)) * scale;

			return new Matrix4x4(
				c1.X, c2.X, c3.X, matrix.M14,
				c1.Y, c2.Y, c3.Y, matrix.M24,
				c1.Z, c2.Z, c3.Z, matrix.M34,
				matrix.M41, matrix.M42, matrix.M43, matrix.M44);
		}

		public static TSMatrix4x4 MatWithNewScale(TSMatrix4x4 matrix, FP scale)
		{
			var s = new TSVector(matrix.Column0.xyz.magnitude, matrix.Column1.xyz.magnitude, matrix.Column2.xyz.magnitude) / scale;
			matrix.M11 /= s.x; matrix.M12 /= s.y; matrix.M13 /= s.z;
			matrix.M21 /= s.x; matrix.M22 /= s.y; matrix.M23 /= s.z;
			matrix.M31 /= s.x; matrix.M32 /= s.y; matrix.M33 /= s.z;
			return matrix;
		}

		public static Matrix4x4 MatWithNewScale(Matrix4x4 matrix, Vector3 scale)
		{
			var c1 = Vector3.Normalize(NumericUtil.Column0xyz(matrix)) * scale.X;
			var c2 = Vector3.Normalize(NumericUtil.Column1xyz(matrix)) * scale.Y;
			var c3 = Vector3.Normalize(NumericUtil.Column2xyz(matrix)) * scale.Z;

			return new Matrix4x4(
				c1.X, c2.X, c3.X, matrix.M14,
				c1.Y, c2.Y, c3.Y, matrix.M24,
				c1.Z, c2.Z, c3.Z, matrix.M34,
				matrix.M41, matrix.M42, matrix.M43, matrix.M44);
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
			//var fq = quat.FromMat4(ExtractRotationMatrix(matrix, MatScale(matrix)).ToMat4());
			//return new TSQuaternion(fq.x, fq.y, fq.z, fq.w);
			return TSQuaternion.CreateFromMatrix(ExtractRotationMatrix(matrix, MatScale(matrix)).Matrix3x3);
		}

		public static TSQuaternion MatRotation(in TSMatrix4x4 matrix, in TSVector scale)
		{
			//var fq = quat.FromMat4(ExtractRotationMatrix(matrix, scale).ToMat4());
			//return new TSQuaternion(fq.x, fq.y, fq.z, fq.w);
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

		public static TSMatrix4x4 LerpMatrix(in Transformation a, in Transformation b, FP t)
		{
			var s = TSVector.Lerp(a.Scale, b.Scale, t);

			var r = TSQuaternion.Slerp(a.Rotation, b.Rotation, t);

			var p = TSVector.Lerp(a.Position, b.Position, t);

			return new Transformation(s, r, p).Matrix;
		}

		public static TSMatrix4x4 LerpMatrix(in TSMatrix4x4 a, in TSMatrix4x4 b, FP t)
		{
			return LerpMatrix(new Transformation(a), new Transformation(b), t);
		}

		public static TSMatrix4x4 Mat4by(in Transformation trans)
		{
			return trans.Matrix;
		}
	}
}
