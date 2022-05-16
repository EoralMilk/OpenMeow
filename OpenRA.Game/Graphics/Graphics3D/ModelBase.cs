using System;
using System.Collections.Generic;
using GlmSharp;

/// <summary>
/// WIP
/// </summary>
namespace OpenRA.Graphics.Graphics3D
{
	/// <summary>
	/// Transform用于存储变化信息和最重要的变换矩阵
	/// </summary>
	struct Transformation
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

		public Transformation(mat4 matrix)
		{
			Scale = MatScale(matrix);
			Rotation = new quat(ExtractRotationMatrix(matrix, Scale));
			Position = MatPosition(matrix);
		}

		public static mat4 ExtractRotationMatrix(mat4 matrix, vec3 scale)
		{
			return new mat4(matrix.Column0 / scale.x, matrix.Column1 / scale.y, matrix.Column2 / scale.z, new vec4(0,0,0,1.0f));
		}

		public static vec3 MatPosition(mat4 matrix)
		{
			return new vec3(matrix.Column3.x, matrix.Column3.y, matrix.Column3.z);
		}

		public static vec3 MatScale(mat4 matrix)
		{
			return new vec3(matrix.Column0.xyz.Length, matrix.Column1.xyz.Length, matrix.Column2.xyz.Length);
		}

		// 获取旋转四元数
		public static quat MatRotation(mat4 matrix)
		{
			return new quat(ExtractRotationMatrix(matrix, MatScale(matrix)));
		}

		/// <summary>
		/// 需要计算，不建议大量使用
		/// </summary>
		public vec3 GetRotationEuler()
		{
			return new vec3(Rotation);
		}

		// 获取旋转矩阵
		public mat4 GetRotationMatrix()
		{
			return new mat4(Rotation);
		}

		// 应用一个旋转到自身
		// 两个四元数 q1 和 q2 的乘积表示一个旋转。q1q2表示先施加旋转q2，再施加旋转q1
		public void Rotated(quat rotate)
		{
			Rotation = rotate * Rotation;
		}

		public void Rotated(float angle, vec3 axis)
		{
			Rotation.Rotated(angle, axis);
		}

		// 应用一个缩放到自身
		public void Scaled(vec3 scale)
		{
			Scale *= scale;
		}

		// 应用位移
		public void Translated(vec3 offset)
		{
			Position += offset;
		}

		public Transformation Then(Transformation then)
		{
			Rotation = then.Rotation * Rotation;
			Scale *= then.Scale;
			Position += then.Position;
			return this;
		}

		// 变换混合，分离SRT依次进行混合
		public static Transformation Blend(Transformation a, Transformation b, float blend)
		{
			var s = vec3.Lerp(a.Scale, b.Scale, blend);
			var r = quat.SLerp(a.Rotation, b.Rotation, blend);
			var p = vec3.Lerp(a.Position, b.Position, blend);

			return new Transformation(s, r, p);
		}

		public static mat4 Mat4by(Transformation trans)
		{
			mat4 m = mat4.Scale(trans.Scale) * (new mat4(trans.Rotation));
			m.Column3 = new vec4(trans.Position,1.0f);
			return m;
		}
	}

	/// <summary>
	/// 3D节点：这可能是个mesh，可能是个骨骼，也可能是个Actor, 也许不是所有Actor都有Transform，所以或许INode要知道自己是否有Transform
	/// </summary>
	class Node3D
	{
		public string Name { get { return name; } }
		public Transformation Transform { get { return transform; } }
		public Transformation RestTransform { get { return restTransform; } }
		public Node3D Parent { get { return parent; } }
		public List<Node3D> Children { get { return children; } }

		protected string name;
		protected Node3D parent;
		protected List<Node3D> children;
		public bool HasTransform { get; protected set; }
		protected Transformation transform; // 世界空间下的坐标
		protected Transformation restTransform; // 父级空间下的初始坐标

		public Node3D(string name, Node3D parent, bool hasTransform)
		{
			this.name = name;
			this.parent = parent;
			this.HasTransform = hasTransform;
			if (HasTransform)
			{
				transform = parent.Transform;
				restTransform = Transformation.Identify;
			}

			children = new List<Node3D>();
		}

		public Node3D(string name, Node3D parent, Transformation restTransform)
		{
			this.name = name;
			this.parent = parent;
			this.HasTransform = true;
			this.restTransform = restTransform;
			this.transform = restTransform.Then(parent.Transform);
			children = new List<Node3D>();
		}

		public virtual void ChangeParent(Node3D parent)
		{
			this.parent = parent;
		}

		public virtual void AddChild(Node3D child)
		{
			children.Append(child);
		}

		public virtual void RemoveChild(Node3D child)
		{
			children.Remove(child);
		}

		/// <summary>
		/// 处理父级带来的影响
		/// </summary>
		public virtual void HandleInfluence()
		{
			if (HasTransform)
			{
				transform.Then(parent.Transform);
			}

			foreach (var child in children)
			{
				child.HandleInfluence();
			}
		}
	}

	/// <summary>
	/// 一根骨骼，或者说是一个关节。
	/// 它是组成骨架的基本单元。
	/// 它是一种特殊的Node3D。
	/// </summary>
	class Bone : Node3D
	{
		public uint ID { get { return id; } }
		public new bool HasTransform { get { return true; } }
		uint id;
		Skeleton skeleton;

		public Bone(string name, Node3D parent, Transformation transform, uint id, Skeleton skeleton)
			: base(name, parent, transform)
		{
			this.id = id;
			this.skeleton = skeleton;
		}

		// HandleInfluence
		public override void HandleInfluence()
		{
			// 这里是最后的混合，一般骨骼是都有动画的，最终输出的mask通常没有用
			if (skeleton.CurrentAnimMask.Mask[id])
			{
				transform.Scale = restTransform.Scale * skeleton.CurrentAnimTransformation[id].Scale * parent.Transform.Scale;
				transform.Rotation = parent.Transform.Rotation * skeleton.CurrentAnimTransformation[id].Rotation * restTransform.Rotation;
				transform.Position = restTransform.Position + skeleton.CurrentAnimTransformation[id].Position + parent.Transform.Position;
			}
			else
			{
				transform.Scale = restTransform.Scale * parent.Transform.Scale;
				transform.Rotation = parent.Transform.Rotation * restTransform.Rotation;
				transform.Position = restTransform.Position + parent.Transform.Position;
			}

			foreach (var child in children)
			{
				child.HandleInfluence(); // 如果子节点是bone，应该执行Bone重写的HandleInfluence。窝不清楚会不会这样
			}

			skeleton.UpdateBoneState(id, transform);
		}
	}

	/// <summary>
	/// 骨架：
	/// 一个骨架包含一组骨骼， 它的结构应该是确定的。
	/// 骨架结构应该从资产文件中导入，当每个使用骨架的Actor初始化时同步初始化骨架实例，并且在游戏运行时不应该被修改。
	/// 骨架应该被ActorTrait生成与控制更新。
	/// 骨架状态应该在LogicTick更新。
	/// 对，没错，它也是个Node3D。
	/// </summary>
	class Skeleton : Node3D
	{
		Bone[] bones;
		Bone rootBone;

		// 每帧更新的currentPose，至于是渲染帧更新还是逻辑帧更新，这可能是个问题，
		// 我觉得应该以逻辑帧为主，在逻辑帧更新，因为更新时可以同时处理其他节点的父子关系影响。
		// 如果在渲染帧更新，逻辑帧需要获取父子关系影响下的Node状态时可能需要再次更新，得不偿失。
		// 不管是否渲染，这个数组都会被更新，因为他是在解算Node父子关系影响时顺便处理的,
		// 而逻辑更新需要Node的状态始终保持正确可预见，因此每个逻辑帧都会更新它。
		// 注意：这是用于传递给GPU的数据集合，同一时间所有要渲染的骨骼都要把这个currentPose拿出来，想办法合并在一起传递给GPU。一般应该不会有其他的用处
		Transformation[] currentPose; // 当前骨骼的姿态，以世界空间为准
		public Transformation[] CurrentAnimTransformation { get; private set; } // 用于混合动画，这个是基于父级空间的
		public AnimMask CurrentAnimMask { get; private set; } // 用于混合动画
		Dictionary<string, int> nameDict;
		public uint Size { get { return (uint)bones.Length; } }

		/// <summary>
		/// 由于混合树需要根据骨架结构来设计，一般一个混合树只能对应一种骨架
		/// 而一种骨架可以选择多个适用于它的混合树中的一个来使用
		/// 一个骨架同一时间只能也必须存在一个BlendTree实例
		/// </summary>
		public BlendTree BlendTree;

		public Skeleton(string name, Node3D parent, Transformation restTransform, Bone[] bones)
			: base(name, parent, restTransform)
		{
			this.bones = bones;
			this.rootBone = bones[0];
			children.Add(rootBone);
			currentPose = new Transformation[bones.Length];
			CurrentAnimTransformation = new Transformation[bones.Length];
			nameDict = new Dictionary<string, int>();
			for (int i = 0; i < bones.Length; i++)
			{
				currentPose[i] = bones[i].Transform;
				nameDict.Add(bones[i].Name, i);
			}
		}

		// 当这个骨架作为子级节点时，它会在处理父级影响时直接处理动画逻辑
		public override void HandleInfluence()
		{
			base.HandleInfluence();

			UpdateSkeletonBone();

			foreach (var child in children)
			{
				if (child == rootBone)
					continue;
				child.HandleInfluence();
			}
		}

		/// <summary>
		/// 根据动画混合树更新整个骨架的骨骼状态
		/// </summary>
		/// <exception cref="Exception">BlendTree == null时</exception>
		public void UpdateSkeletonBone()
		{
			// 如果没有blendtree 产生报错
			if (BlendTree == null)
			{
				throw new Exception("Skeleton " + name + " has no blendTree");
			}

			var blendResult = BlendTree.GetOutPut();
			CurrentAnimTransformation = blendResult.OutPutTransform;
			CurrentAnimMask = blendResult.AnimMask;

			// 从根骨骼开始递归所有子骨骼，更新骨架结构
			rootBone.HandleInfluence();
		}

		/// <summary>
		/// 由骨骼调用，更新骨架的数据
		/// </summary>
		/// <param name="id">骨骼id</param>
		/// <param name="transform">骨骼transform状态</param>
		public void UpdateBoneState(uint id, Transformation trans)
		{
			currentPose[id] = trans;
		}

		/// <summary>
		/// 根据骨骼名称尝试获取骨骼id
		/// 如果返回值是-1，代表没有找到。
		/// </summary>
		/// <param name="name">骨骼名称</param>
		/// <returns>骨骼id，如果是 -1代表没找到</returns>
		public int GetBoneId(string name)
		{
			int id;
			if (nameDict.TryGetValue(name, out id))
				return id;
			else
				return -1;
		}
	}
}
