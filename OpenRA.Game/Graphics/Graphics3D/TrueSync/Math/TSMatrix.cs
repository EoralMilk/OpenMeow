/* Copyright (C) <2009-2011> <Thorben Linneweber, Jitter Physics>
* 
*  This software is provided 'as-is', without any express or implied
*  warranty.  In no event will the authors be held liable for any damages
*  arising from the use of this software.
*
*  Permission is granted to anyone to use this software for any purpose,
*  including commercial applications, and to alter it and redistribute it
*  freely, subject to the following restrictions:
*
*  1. The origin of this software must not be misrepresented; you must not
*      claim that you wrote the original software. If you use this software
*      in a product, an acknowledgment in the product documentation would be
*      appreciated but is not required.
*  2. Altered source versions must be plainly marked as such, and must not be
*      misrepresented as being the original software.
*  3. This notice may not be removed or altered from any source distribution. 
*/

namespace TrueSync
{

	/// <summary>
	/// 3x3 Matrix.
	/// </summary>
	public struct TSMatrix
	{
#pragma warning disable IDE1006 // 命名样式
		/// <summary>
		/// M11
		/// </summary>
		public FP m00; // 1st row vector

		/// <summary>
		/// M12
		/// </summary>
		public FP m10;

		/// <summary>
		/// M13
		/// </summary>
		public FP m20;

		/// <summary>
		/// M21
		/// </summary>
		public FP m01; // 2nd row vector

		/// <summary>
		/// M22
		/// </summary>
		public FP m11;

		/// <summary>
		/// M23
		/// </summary>
		public FP m21;

		/// <summary>
		/// M31
		/// </summary>
		public FP m02; // 3rd row vector

		/// <summary>
		/// M32
		/// </summary>
		public FP m12;

		/// <summary>
		/// M33
		/// </summary>
		public FP m22;
#pragma warning restore IDE1006 // 命名样式

		internal static TSMatrix InternalIdentity;

		/// <summary>
		/// Identity matrix.
		/// </summary>
		public static readonly TSMatrix Identity;
		public static readonly TSMatrix Zero;

		static TSMatrix()
		{
			Zero = new TSMatrix();

			Identity = new TSMatrix();
			Identity.m00 = FP.One;
			Identity.m11 = FP.One;
			Identity.m22 = FP.One;

			InternalIdentity = Identity;
		}

		public TSVector eulerAngles
		{
			get
			{
				TSVector result = new TSVector();

				result.x = TSMath.Atan2(m12, m22) * FP.Rad2Deg;
				result.y = TSMath.Atan2(-m02, TSMath.Sqrt(m12 * m12 + m22 * m22)) * FP.Rad2Deg;
				result.z = TSMath.Atan2(m01, m00) * FP.Rad2Deg;

				return result * -1;
			}
		}

		public static TSMatrix CreateFromYawPitchRoll(FP yaw, FP pitch, FP roll)
		{
			TSMatrix matrix;
			TSQuaternion quaternion;
			TSQuaternion.CreateFromYawPitchRoll(yaw, pitch, roll, out quaternion);
			CreateFromQuaternion(ref quaternion, out matrix);
			return matrix;
		}

		public static TSMatrix CreateRotationX(FP radians)
		{
			TSMatrix matrix;
			FP num2 = FP.Cos(radians);
			FP num = FP.Sin(radians);
			matrix.m00 = FP.One;
			matrix.m10 = FP.Zero;
			matrix.m20 = FP.Zero;
			matrix.m01 = FP.Zero;
			matrix.m11 = num2;
			matrix.m21 = num;
			matrix.m02 = FP.Zero;
			matrix.m12 = -num;
			matrix.m22 = num2;
			return matrix;
		}

		public static void CreateRotationX(FP radians, out TSMatrix result)
		{
			FP num2 = FP.Cos(radians);
			FP num = FP.Sin(radians);
			result.m00 = FP.One;
			result.m10 = FP.Zero;
			result.m20 = FP.Zero;
			result.m01 = FP.Zero;
			result.m11 = num2;
			result.m21 = num;
			result.m02 = FP.Zero;
			result.m12 = -num;
			result.m22 = num2;
		}

		public static TSMatrix CreateRotationY(FP radians)
		{
			TSMatrix matrix;
			FP num2 = FP.Cos(radians);
			FP num = FP.Sin(radians);
			matrix.m00 = num2;
			matrix.m10 = FP.Zero;
			matrix.m20 = -num;
			matrix.m01 = FP.Zero;
			matrix.m11 = FP.One;
			matrix.m21 = FP.Zero;
			matrix.m02 = num;
			matrix.m12 = FP.Zero;
			matrix.m22 = num2;
			return matrix;
		}

		public static void CreateRotationY(FP radians, out TSMatrix result)
		{
			FP num2 = FP.Cos(radians);
			FP num = FP.Sin(radians);
			result.m00 = num2;
			result.m10 = FP.Zero;
			result.m20 = -num;
			result.m01 = FP.Zero;
			result.m11 = FP.One;
			result.m21 = FP.Zero;
			result.m02 = num;
			result.m12 = FP.Zero;
			result.m22 = num2;
		}

		public static TSMatrix CreateRotationZ(FP radians)
		{
			TSMatrix matrix;
			FP num2 = FP.Cos(radians);
			FP num = FP.Sin(radians);
			matrix.m00 = num2;
			matrix.m10 = num;
			matrix.m20 = FP.Zero;
			matrix.m01 = -num;
			matrix.m11 = num2;
			matrix.m21 = FP.Zero;
			matrix.m02 = FP.Zero;
			matrix.m12 = FP.Zero;
			matrix.m22 = FP.One;
			return matrix;
		}


		public static void CreateRotationZ(FP radians, out TSMatrix result)
		{
			FP num2 = FP.Cos(radians);
			FP num = FP.Sin(radians);
			result.m00 = num2;
			result.m10 = num;
			result.m20 = FP.Zero;
			result.m01 = -num;
			result.m11 = num2;
			result.m21 = FP.Zero;
			result.m02 = FP.Zero;
			result.m12 = FP.Zero;
			result.m22 = FP.One;
		}

		/// <summary>
		/// Initializes a new instance of the matrix structure.
		/// </summary>
		/// <param name="m11">m11</param>
		/// <param name="m12">m12</param>
		/// <param name="m13">m13</param>
		/// <param name="m21">m21</param>
		/// <param name="m22">m22</param>
		/// <param name="m23">m23</param>
		/// <param name="m31">m31</param>
		/// <param name="m32">m32</param>
		/// <param name="m33">m33</param>
		#region public JMatrix(FP m11, FP m12, FP m13, FP m21, FP m22, FP m23,FP m31, FP m32, FP m33)
		public TSMatrix(FP m11, FP m12, FP m13, FP m21, FP m22, FP m23, FP m31, FP m32, FP m33)
		{
			this.m00 = m11;
			this.m10 = m12;
			this.m20 = m13;
			this.m01 = m21;
			this.m11 = m22;
			this.m21 = m23;
			this.m02 = m31;
			this.m12 = m32;
			this.m22 = m33;
		}
		#endregion

		/// <summary>
		/// Gets the determinant of the matrix.
		/// </summary>
		/// <returns>The determinant of the matrix.</returns>
		#region public FP Determinant()
		//public FP Determinant()
		//{
		//    return M11 * M22 * M33 -M11 * M23 * M32 -M12 * M21 * M33 +M12 * M23 * M31 + M13 * M21 * M32 - M13 * M22 * M31;
		//}
		#endregion

		/// <summary>
		/// Multiply two matrices. Notice: matrix multiplication is not commutative.
		/// </summary>
		/// <param name="matrix1">The first matrix.</param>
		/// <param name="matrix2">The second matrix.</param>
		/// <returns>The product of both matrices.</returns>
		#region public static JMatrix Multiply(JMatrix matrix1, JMatrix matrix2)
		public static TSMatrix Multiply(TSMatrix matrix1, TSMatrix matrix2)
		{
			TSMatrix result;
			TSMatrix.Multiply(ref matrix1, ref matrix2, out result);
			return result;
		}

		/// <summary>
		/// Multiply two matrices. Notice: matrix multiplication is not commutative.
		/// </summary>
		/// <param name="matrix1">The first matrix.</param>
		/// <param name="matrix2">The second matrix.</param>
		/// <param name="result">The product of both matrices.</param>
		public static void Multiply(ref TSMatrix matrix1, ref TSMatrix matrix2, out TSMatrix result)
		{
			FP num0 = ((matrix1.m00 * matrix2.m00) + (matrix1.m10 * matrix2.m01)) + (matrix1.m20 * matrix2.m02);
			FP num1 = ((matrix1.m00 * matrix2.m10) + (matrix1.m10 * matrix2.m11)) + (matrix1.m20 * matrix2.m12);
			FP num2 = ((matrix1.m00 * matrix2.m20) + (matrix1.m10 * matrix2.m21)) + (matrix1.m20 * matrix2.m22);
			FP num3 = ((matrix1.m01 * matrix2.m00) + (matrix1.m11 * matrix2.m01)) + (matrix1.m21 * matrix2.m02);
			FP num4 = ((matrix1.m01 * matrix2.m10) + (matrix1.m11 * matrix2.m11)) + (matrix1.m21 * matrix2.m12);
			FP num5 = ((matrix1.m01 * matrix2.m20) + (matrix1.m11 * matrix2.m21)) + (matrix1.m21 * matrix2.m22);
			FP num6 = ((matrix1.m02 * matrix2.m00) + (matrix1.m12 * matrix2.m01)) + (matrix1.m22 * matrix2.m02);
			FP num7 = ((matrix1.m02 * matrix2.m10) + (matrix1.m12 * matrix2.m11)) + (matrix1.m22 * matrix2.m12);
			FP num8 = ((matrix1.m02 * matrix2.m20) + (matrix1.m12 * matrix2.m21)) + (matrix1.m22 * matrix2.m22);

			result.m00 = num0;
			result.m10 = num1;
			result.m20 = num2;
			result.m01 = num3;
			result.m11 = num4;
			result.m21 = num5;
			result.m02 = num6;
			result.m12 = num7;
			result.m22 = num8;
		}
		#endregion

		/// <summary>
		/// Matrices are added.
		/// </summary>
		/// <param name="matrix1">The first matrix.</param>
		/// <param name="matrix2">The second matrix.</param>
		/// <returns>The sum of both matrices.</returns>
		#region public static JMatrix Add(JMatrix matrix1, JMatrix matrix2)
		public static TSMatrix Add(TSMatrix matrix1, TSMatrix matrix2)
		{
			TSMatrix result;
			TSMatrix.Add(ref matrix1, ref matrix2, out result);
			return result;
		}

		/// <summary>
		/// Matrices are added.
		/// </summary>
		/// <param name="matrix1">The first matrix.</param>
		/// <param name="matrix2">The second matrix.</param>
		/// <param name="result">The sum of both matrices.</param>
		public static void Add(ref TSMatrix matrix1, ref TSMatrix matrix2, out TSMatrix result)
		{
			result.m00 = matrix1.m00 + matrix2.m00;
			result.m10 = matrix1.m10 + matrix2.m10;
			result.m20 = matrix1.m20 + matrix2.m20;
			result.m01 = matrix1.m01 + matrix2.m01;
			result.m11 = matrix1.m11 + matrix2.m11;
			result.m21 = matrix1.m21 + matrix2.m21;
			result.m02 = matrix1.m02 + matrix2.m02;
			result.m12 = matrix1.m12 + matrix2.m12;
			result.m22 = matrix1.m22 + matrix2.m22;
		}
		#endregion

		/// <summary>
		/// Calculates the inverse of a give matrix.
		/// </summary>
		/// <param name="matrix">The matrix to invert.</param>
		/// <returns>The inverted JMatrix.</returns>
		#region public static JMatrix Inverse(JMatrix matrix)
		public static TSMatrix Inverse(TSMatrix matrix)
		{
			TSMatrix result;
			TSMatrix.Inverse(ref matrix, out result);
			return result;
		}

		public FP Determinant()
		{
			return m00 * m11 * m22 + m10 * m21 * m02 + m20 * m01 * m12 -
				   m02 * m11 * m20 - m12 * m21 * m00 - m22 * m01 * m10;
		}

		public static void Invert(ref TSMatrix matrix, out TSMatrix result)
		{
			FP determinantInverse = 1 / matrix.Determinant();
			FP m11 = (matrix.m11 * matrix.m22 - matrix.m21 * matrix.m12) * determinantInverse;
			FP m12 = (matrix.m20 * matrix.m12 - matrix.m22 * matrix.m10) * determinantInverse;
			FP m13 = (matrix.m10 * matrix.m21 - matrix.m11 * matrix.m20) * determinantInverse;

			FP m21 = (matrix.m21 * matrix.m02 - matrix.m01 * matrix.m22) * determinantInverse;
			FP m22 = (matrix.m00 * matrix.m22 - matrix.m20 * matrix.m02) * determinantInverse;
			FP m23 = (matrix.m20 * matrix.m01 - matrix.m00 * matrix.m21) * determinantInverse;

			FP m31 = (matrix.m01 * matrix.m12 - matrix.m11 * matrix.m02) * determinantInverse;
			FP m32 = (matrix.m10 * matrix.m02 - matrix.m00 * matrix.m12) * determinantInverse;
			FP m33 = (matrix.m00 * matrix.m11 - matrix.m10 * matrix.m01) * determinantInverse;

			result.m00 = m11;
			result.m10 = m12;
			result.m20 = m13;

			result.m01 = m21;
			result.m11 = m22;
			result.m21 = m23;

			result.m02 = m31;
			result.m12 = m32;
			result.m22 = m33;
		}

		/// <summary>
		/// Calculates the inverse of a give matrix.
		/// </summary>
		/// <param name="matrix">The matrix to invert.</param>
		/// <param name="result">The inverted JMatrix.</param>
		public static void Inverse(ref TSMatrix matrix, out TSMatrix result)
		{
			FP det = 1024 * matrix.m00 * matrix.m11 * matrix.m22 -
				1024 * matrix.m00 * matrix.m21 * matrix.m12 -
				1024 * matrix.m10 * matrix.m01 * matrix.m22 +
				1024 * matrix.m10 * matrix.m21 * matrix.m02 +
				1024 * matrix.m20 * matrix.m01 * matrix.m12 -
				1024 * matrix.m20 * matrix.m11 * matrix.m02;

			FP num11 = 1024 * matrix.m11 * matrix.m22 - 1024 * matrix.m21 * matrix.m12;
			FP num12 = 1024 * matrix.m20 * matrix.m12 - 1024 * matrix.m10 * matrix.m22;
			FP num13 = 1024 * matrix.m10 * matrix.m21 - 1024 * matrix.m11 * matrix.m20;

			FP num21 = 1024 * matrix.m21 * matrix.m02 - 1024 * matrix.m22 * matrix.m01;
			FP num22 = 1024 * matrix.m00 * matrix.m22 - 1024 * matrix.m02 * matrix.m20;
			FP num23 = 1024 * matrix.m20 * matrix.m01 - 1024 * matrix.m21 * matrix.m00;

			FP num31 = 1024 * matrix.m01 * matrix.m12 - 1024 * matrix.m02 * matrix.m11;
			FP num32 = 1024 * matrix.m10 * matrix.m02 - 1024 * matrix.m12 * matrix.m00;
			FP num33 = 1024 * matrix.m00 * matrix.m11 - 1024 * matrix.m01 * matrix.m10;

			if (det == 0)
			{
				result.m00 = FP.PositiveInfinity;
				result.m10 = FP.PositiveInfinity;
				result.m20 = FP.PositiveInfinity;
				result.m01 = FP.PositiveInfinity;
				result.m11 = FP.PositiveInfinity;
				result.m21 = FP.PositiveInfinity;
				result.m02 = FP.PositiveInfinity;
				result.m12 = FP.PositiveInfinity;
				result.m22 = FP.PositiveInfinity;
			}
			else
			{
				result.m00 = num11 / det;
				result.m10 = num12 / det;
				result.m20 = num13 / det;
				result.m01 = num21 / det;
				result.m11 = num22 / det;
				result.m21 = num23 / det;
				result.m02 = num31 / det;
				result.m12 = num32 / det;
				result.m22 = num33 / det;
			}

		}
		#endregion

		/// <summary>
		/// Multiply a matrix by a scalefactor.
		/// </summary>
		/// <param name="matrix1">The matrix.</param>
		/// <param name="scaleFactor">The scale factor.</param>
		/// <returns>A JMatrix multiplied by the scale factor.</returns>
		#region public static JMatrix Multiply(JMatrix matrix1, FP scaleFactor)
		public static TSMatrix Multiply(TSMatrix matrix1, FP scaleFactor)
		{
			TSMatrix result;
			TSMatrix.Multiply(ref matrix1, scaleFactor, out result);
			return result;
		}

		/// <summary>
		/// Multiply a matrix by a scalefactor.
		/// </summary>
		/// <param name="matrix1">The matrix.</param>
		/// <param name="scaleFactor">The scale factor.</param>
		/// <param name="result">A JMatrix multiplied by the scale factor.</param>
		public static void Multiply(ref TSMatrix matrix1, FP scaleFactor, out TSMatrix result)
		{
			FP num = scaleFactor;
			result.m00 = matrix1.m00 * num;
			result.m10 = matrix1.m10 * num;
			result.m20 = matrix1.m20 * num;
			result.m01 = matrix1.m01 * num;
			result.m11 = matrix1.m11 * num;
			result.m21 = matrix1.m21 * num;
			result.m02 = matrix1.m02 * num;
			result.m12 = matrix1.m12 * num;
			result.m22 = matrix1.m22 * num;
		}
		#endregion

		/// <summary>
		/// Creates a JMatrix representing an orientation from a quaternion.
		/// </summary>
		/// <param name="quaternion">The quaternion the matrix should be created from.</param>
		/// <returns>JMatrix representing an orientation.</returns>
		#region public static JMatrix CreateFromQuaternion(JQuaternion quaternion)

		public static TSMatrix CreateFromLookAt(TSVector position, TSVector target)
		{
			TSMatrix result;
			LookAt(target - position, TSVector.up, out result);
			return result;
		}

		public static TSMatrix LookAt(TSVector forward, TSVector upwards)
		{
			TSMatrix result;
			LookAt(forward, upwards, out result);

			return result;
		}

		public static void LookAt(TSVector forward, TSVector upwards, out TSMatrix result)
		{
			TSVector zaxis = forward; zaxis.Normalize();
			TSVector xaxis = TSVector.Cross(upwards, zaxis); xaxis.Normalize();
			TSVector yaxis = TSVector.Cross(zaxis, xaxis);

			result.m00 = xaxis.x;
			result.m01 = yaxis.x;
			result.m02 = zaxis.x;
			result.m10 = xaxis.y;
			result.m11 = yaxis.y;
			result.m12 = zaxis.y;
			result.m20 = xaxis.z;
			result.m21 = yaxis.z;
			result.m22 = zaxis.z;
		}

		public static TSMatrix CreateFromQuaternion(TSQuaternion quaternion)
		{
			TSMatrix result;
			TSMatrix.CreateFromQuaternion(ref quaternion, out result);
			return result;
		}

		/// <summary>
		/// Creates a JMatrix representing an orientation from a quaternion.
		/// </summary>
		/// <param name="quaternion">The quaternion the matrix should be created from.</param>
		/// <param name="result">JMatrix representing an orientation.</param>
		public static void CreateFromQuaternion(ref TSQuaternion quaternion, out TSMatrix result)
		{
			FP num9 = quaternion.x * quaternion.x;
			FP num8 = quaternion.y * quaternion.y;
			FP num7 = quaternion.z * quaternion.z;
			FP num6 = quaternion.x * quaternion.y;
			FP num5 = quaternion.z * quaternion.w;
			FP num4 = quaternion.z * quaternion.x;
			FP num3 = quaternion.y * quaternion.w;
			FP num2 = quaternion.y * quaternion.z;
			FP num = quaternion.x * quaternion.w;
			result.m00 = FP.One - (2 * (num8 + num7));
			result.m10 = 2 * (num6 + num5);
			result.m20 = 2 * (num4 - num3);
			result.m01 = 2 * (num6 - num5);
			result.m11 = FP.One - (2 * (num7 + num9));
			result.m21 = 2 * (num2 + num);
			result.m02 = 2 * (num4 + num3);
			result.m12 = 2 * (num2 - num);
			result.m22 = FP.One - (2 * (num8 + num9));
		}
		#endregion

		/// <summary>
		/// Creates the transposed matrix.
		/// </summary>
		/// <param name="matrix">The matrix which should be transposed.</param>
		/// <returns>The transposed JMatrix.</returns>
		#region public static JMatrix Transpose(JMatrix matrix)
		public static TSMatrix Transpose(TSMatrix matrix)
		{
			TSMatrix result;
			TSMatrix.Transpose(ref matrix, out result);
			return result;
		}

		/// <summary>
		/// Creates the transposed matrix.
		/// </summary>
		/// <param name="matrix">The matrix which should be transposed.</param>
		/// <param name="result">The transposed JMatrix.</param>
		public static void Transpose(ref TSMatrix matrix, out TSMatrix result)
		{
			result.m00 = matrix.m00;
			result.m10 = matrix.m01;
			result.m20 = matrix.m02;
			result.m01 = matrix.m10;
			result.m11 = matrix.m11;
			result.m21 = matrix.m12;
			result.m02 = matrix.m20;
			result.m12 = matrix.m21;
			result.m22 = matrix.m22;
		}
		#endregion

		/// <summary>
		/// Multiplies two matrices.
		/// </summary>
		/// <param name="value1">The first matrix.</param>
		/// <param name="value2">The second matrix.</param>
		/// <returns>The product of both values.</returns>
		#region public static JMatrix operator *(JMatrix value1,JMatrix value2)
		public static TSMatrix operator *(TSMatrix value1, TSMatrix value2)
		{
			TSMatrix result; TSMatrix.Multiply(ref value1, ref value2, out result);
			return result;
		}
		#endregion


		public FP Trace()
		{
			return this.m00 + this.m11 + this.m22;
		}

		/// <summary>
		/// Adds two matrices.
		/// </summary>
		/// <param name="value1">The first matrix.</param>
		/// <param name="value2">The second matrix.</param>
		/// <returns>The sum of both values.</returns>
		#region public static JMatrix operator +(JMatrix value1, JMatrix value2)
		public static TSMatrix operator +(TSMatrix value1, TSMatrix value2)
		{
			TSMatrix result; TSMatrix.Add(ref value1, ref value2, out result);
			return result;
		}
		#endregion

		/// <summary>
		/// Subtracts two matrices.
		/// </summary>
		/// <param name="value1">The first matrix.</param>
		/// <param name="value2">The second matrix.</param>
		/// <returns>The difference of both values.</returns>
		#region public static JMatrix operator -(JMatrix value1, JMatrix value2)
		public static TSMatrix operator -(TSMatrix value1, TSMatrix value2)
		{
			TSMatrix result; TSMatrix.Multiply(ref value2, -FP.One, out value2);
			TSMatrix.Add(ref value1, ref value2, out result);
			return result;
		}
		#endregion

		public static bool operator ==(TSMatrix value1, TSMatrix value2)
		{
			return value1.m00 == value2.m00 &&
				value1.m10 == value2.m10 &&
				value1.m20 == value2.m20 &&
				value1.m01 == value2.m01 &&
				value1.m11 == value2.m11 &&
				value1.m21 == value2.m21 &&
				value1.m02 == value2.m02 &&
				value1.m12 == value2.m12 &&
				value1.m22 == value2.m22;
		}

		public static bool operator !=(TSMatrix value1, TSMatrix value2)
		{
			return value1.m00 != value2.m00 ||
				value1.m10 != value2.m10 ||
				value1.m20 != value2.m20 ||
				value1.m01 != value2.m01 ||
				value1.m11 != value2.m11 ||
				value1.m21 != value2.m21 ||
				value1.m02 != value2.m02 ||
				value1.m12 != value2.m12 ||
				value1.m22 != value2.m22;
		}

		public override bool Equals(object obj)
		{
			if (!(obj is TSMatrix)) return false;
			TSMatrix other = (TSMatrix)obj;

			return this.m00 == other.m00 &&
				this.m10 == other.m10 &&
				this.m20 == other.m20 &&
				this.m01 == other.m01 &&
				this.m11 == other.m11 &&
				this.m21 == other.m21 &&
				this.m02 == other.m02 &&
				this.m12 == other.m12 &&
				this.m22 == other.m22;
		}

		public override int GetHashCode()
		{
			return m00.GetHashCode() ^
				m10.GetHashCode() ^
				m20.GetHashCode() ^
				m01.GetHashCode() ^
				m11.GetHashCode() ^
				m21.GetHashCode() ^
				m02.GetHashCode() ^
				m12.GetHashCode() ^
				m22.GetHashCode();
		}

		/// <summary>
		/// Creates a matrix which rotates around the given axis by the given angle.
		/// </summary>
		/// <param name="axis">The axis.</param>
		/// <param name="angle">The angle.</param>
		/// <param name="result">The resulting rotation matrix</param>
		#region public static void CreateFromAxisAngle(ref JVector axis, FP angle, out JMatrix result)
		public static void CreateFromAxisAngle(ref TSVector axis, FP angle, out TSMatrix result)
		{
			FP x = axis.x;
			FP y = axis.y;
			FP z = axis.z;
			FP num2 = FP.Sin(angle);
			FP num = FP.Cos(angle);
			FP num11 = x * x;
			FP num10 = y * y;
			FP num9 = z * z;
			FP num8 = x * y;
			FP num7 = x * z;
			FP num6 = y * z;
			result.m00 = num11 + (num * (FP.One - num11));
			result.m10 = (num8 - (num * num8)) + (num2 * z);
			result.m20 = (num7 - (num * num7)) - (num2 * y);
			result.m01 = (num8 - (num * num8)) - (num2 * z);
			result.m11 = num10 + (num * (FP.One - num10));
			result.m21 = (num6 - (num * num6)) + (num2 * x);
			result.m02 = (num7 - (num * num7)) + (num2 * y);
			result.m12 = (num6 - (num * num6)) - (num2 * x);
			result.m22 = num9 + (num * (FP.One - num9));
		}

		/// <summary>
		/// Creates a matrix which rotates around the given axis by the given angle.
		/// </summary>
		/// <param name="axis">The axis.</param>
		/// <param name="angle">The angle.</param>
		/// <returns>The resulting rotation matrix</returns>
		public static TSMatrix AngleAxis(FP angle, TSVector axis)
		{
			TSMatrix result; CreateFromAxisAngle(ref axis, angle, out result);
			return result;
		}

		#endregion

		public override string ToString()
		{
			return string.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}", m00.RawValue, m10.RawValue, m20.RawValue, m01.RawValue, m11.RawValue, m21.RawValue, m02.RawValue, m12.RawValue, m22.RawValue);
		}

	}

}
