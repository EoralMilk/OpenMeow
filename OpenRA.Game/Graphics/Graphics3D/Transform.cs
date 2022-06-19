using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using GlmSharp;

namespace OpenRA.Graphics
{
	/// <summary>
	/// Transform用于存储变化信息和最重要的变换矩阵
	/// </summary>
	public struct Transformation
	{
		public vec3 Scale;
		public quat Rotation;
		public vec3 Position;
		public static Transformation Identify { get { return new Transformation(vec3.Ones, quat.Identity, vec3.Zero); } }

		public Transformation(vec3 s, quat r, vec3 t)
		{
			Scale = s;
			Rotation = r;
			Position = t;
		}

		public Transformation(in mat4 matrix)
		{
			Scale = MatScale(matrix);
			Rotation = new quat(ExtractRotationMatrix(matrix, Scale));
			Position = MatPosition(matrix);
		}

		public static mat4 ExtractRotationMatrix(in mat4 matrix, in vec3 scale)
		{
			return new mat4(matrix.Column0 / scale.x, matrix.Column1 / scale.y, matrix.Column2 / scale.z, new vec4(0, 0, 0, 1.0f));
		}

		public static vec3 MatPosition(in mat4 matrix)
		{
			return new vec3(matrix.Column3.x, matrix.Column3.y, matrix.Column3.z);
		}

		public static vec3 MatScale(in mat4 matrix)
		{
			return new vec3(matrix.Column0.xyz.Length, matrix.Column1.xyz.Length, matrix.Column2.xyz.Length);
		}

		// 获取旋转四元数
		public static quat MatRotation(in mat4 matrix)
		{
			return new quat(ExtractRotationMatrix(matrix, MatScale(matrix)));
		}

		public static quat MatRotation(in mat4 matrix, in vec3 scale)
		{
			return new quat(ExtractRotationMatrix(matrix, scale));
		}

		// 应用一个旋转到自身
		// 两个四元数 q1 和 q2 的乘积表示一个旋转。q1q2表示先施加旋转q2，再施加旋转q1
		public void Rotated(in quat rotate)
		{
			Rotation = rotate * Rotation;
		}

		public void Rotated(float angle, in vec3 axis)
		{
			Rotation.Rotated(angle, axis);
		}

		// 应用一个缩放到自身
		public void Scaled(in vec3 scale)
		{
			Scale *= scale;
		}

		// 应用位移
		public void Translated(in vec3 offset)
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
			var m = Mat4by(then) * Mat4by(this);

			Scale = MatScale(m);
			Rotation = new quat(ExtractRotationMatrix(m, Scale));
			Position = MatPosition(m);
			return this;
		}

		// 变换混合，分离SRT依次进行混合
		public static Transformation Blend(in Transformation a, in Transformation b, float blend)
		{
			var s = vec3.Lerp(a.Scale, b.Scale, blend);

			// Milk: GlmSharp的SLerp有问题，不清楚是怎么回事，总之不要用quat.SLerp
			var qs = Quaternion.Slerp(new Quaternion(a.Rotation.x, a.Rotation.y, a.Rotation.z, a.Rotation.w),
										new Quaternion(b.Rotation.x, b.Rotation.y, b.Rotation.z, b.Rotation.w),
										blend);
			var r = new quat(qs.X, qs.Y, qs.Z, qs.W);

			//var r = quat.Lerp(a.Rotation, b.Rotation, blend).Normalized;
			var p = vec3.Lerp(a.Position, b.Position, blend);

			return new Transformation(s, r, p);
		}

		public static mat4 Mat4by(in Transformation trans)
		{
			mat4 m = mat4.Scale(trans.Scale) * (new mat4(trans.Rotation));
			m.Column3 = new vec4(trans.Position, 1.0f);
			return m;
		}
	}
}
