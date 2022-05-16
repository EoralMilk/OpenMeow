using System;
using System.Collections.Generic;
using System.Text;

namespace OpenRA.Graphics.Graphics3D
{
	/// <summary>
	/// 混合树：
	/// 不同于一般的树状结构，以根节点开始向上生长。
	/// 混合树是以叶节点为起点，逐渐合并，直到最后的一个输出节点。
	/// 就像一颗倒置的树。
	/// 一般来讲一个节点如果接受后续节点控制，那么它只能有一个输出节点，但是可以有任意个输入节点（没输入节点也可以）。
	/// 如果一个节点不接受后续节点控制，那么它可以有任意个输入节点，也可以有任意个输出节点。
	/// 不过不论如何，一个节点一般只会有一种输出数据，即使有有多个节点接受其输出，它们获得的数据也是一样的。
	/// </summary>
	class BlendTree
	{
		string name;
		short tick;
		BlendTreeNode finalOutPut; // 最后的输出节点, 也就是根节点
		public BlendTreeNodeOutPut GetOutPut()
		{
			tick++;

			// step 可以用于跳步，当这一帧滞后时，可以选择跳过几帧动画
			return finalOutPut.UpdateOutPut(tick, true, 1);
		}
	}

	abstract class BlendTreeNode
	{
		public string Name { get { return name; } }
		public uint ID { get { return id; } }
		public BlendTree BlendTree { get { return blendTree; } }

		protected string name;
		protected uint id;
		protected BlendTree blendTree;
		protected AnimMask animMask;
		protected BlendTreeNodeOutPut outPut;

		// 用于判断是否需要Update节点数据
		protected short tick = -1;

		public BlendTreeNode(string name, uint id, BlendTree blendTree, AnimMask mask)
		{
			this.name = name;
			this.id = id;
			this.blendTree = blendTree;
			this.animMask = mask;
		}

		public abstract BlendTreeNodeOutPut UpdateOutPut(short tick, bool run, int step);
	}

	/// <summary>
	/// BlendTree做混合工作的节点基类
	/// </summary>
	abstract class BlendNode : BlendTreeNode
	{
		public BlendNode(string name, uint id, BlendTree blendTree, AnimMask mask)
			: base(name, id, blendTree, mask) { }
	}

	/// <summary>
	/// BlendTree叶节点基类
	/// </summary>
	abstract class LeafNode : BlendTreeNode
	{
		public enum PlayType
		{
			Loop,
			Once,
			PingPong,
		}

		public PlayType NodePlayType = PlayType.Loop;
		public bool KeepingEnd { get; protected set; }

		protected int frame;

		public LeafNode(string name, uint id, BlendTree blendTree, AnimMask mask)
			: base(name, id, blendTree, mask) { }

		public virtual int GetCurrentFrameLCM()
		{
			return GetLength();
		}

		public abstract int GetLength();

		public void ResetFrame()
		{
			frame = 0;
		}

		public float GetRatio()
		{
			return (float)frame / GetLength();
		}
	}

	/// <summary>
	/// 一个动画Asset，它应该作为资产存在，可以被BlendTreeNode引用
	/// 但是不能被其直接修改
	/// 因为BlendTreeNode应该自己在本地生成一个动画实例用来做更新数据等操作
	/// </summary>
	struct Animation
	{
		public string Name;
		public AnimFrame[] Frames;
		public int Length
		{
			get
			{
				return Frames.Length;
			}
		}
	}

	/// <summary>
	/// 动画帧
	/// </summary>
	struct AnimFrame
	{
		/// <summary>
		/// RestPose Based SRT
		/// </summary>
		public Transformation[] Transformations;

		public AnimFrame(int length)
		{
			Transformations = new Transformation[length];
		}

		public AnimFrame(Transformation[] srts)
		{
			Transformations = srts;
		}
	}

	/// <summary>
	/// 节点输出
	/// </summary>
	struct BlendTreeNodeOutPut
	{
		public Transformation[] OutPutTransform;
		public AnimMask AnimMask;

		// 到这个输出为止，全部动画长度的最小公倍数
		// public int FrameLCM;
		// public bool UpdateLCM;

		// 复制构造函数
		public BlendTreeNodeOutPut(BlendTreeNodeOutPut op)
		{
			OutPutTransform = op.OutPutTransform;
			AnimMask = op.AnimMask;

			// FrameLCM = op.FrameLCM;
			// UpdateLCM = op.UpdateLCM;
		}

		public BlendTreeNodeOutPut(Transformation[] srts, AnimMask animMask) //, int frameLCM = 0, bool updateLCM = false)
		{
			OutPutTransform = srts;
			AnimMask = animMask;

			// FrameLCM = frameLCM;
			// UpdateLCM = updateLCM;
		}
	}

	/// <summary>
	/// 动画遮罩，控制哪些骨骼播放动画，哪些不播放
	/// 它应该被BlendTreeNode作为修改输出动画Mat4数据的依据
	/// </summary>
	class AnimMask
	{
		public string Name;
		public bool[] Mask;

		public AnimMask(string name, bool[] mask)
		{
			Name = name;
			Mask = mask;
		}

		public AnimMask(string name, int length)
		{
			Name = name;

			// 全部初始化为true
			Mask = new bool[length];
			for (int i = 0; i < length; i++)
			{
				Mask[i] = true;
			}
		}
	}

	class BlendTreeUtil
	{
		// 计算动画长度最小公倍数
		public static int CalculateLCM(int length1, int length2)
		{
			int max = Math.Max(length1, length2);
			int min = Math.Min(length1, length2);
			int lcm = max;
			for (int i = max; i > 0; i--)
			{
				if (max % i == 0 && min % i == 0)
				{
					lcm = i;
					break;
				}
			}

			return lcm;
		}

		public static BlendTreeNodeOutPut Blend(BlendTreeNodeOutPut inPutValue1, BlendTreeNodeOutPut inPutValue2, float t, AnimMask animMask)
		{
			var outPutMat = new Transformation[inPutValue1.OutPutTransform.Length];

			for (int i = 0; i < inPutValue1.OutPutTransform.Length; i++)
			{
				if (inPutValue1.AnimMask.Mask[i] && inPutValue2.AnimMask.Mask[i])
					outPutMat[i] = Transformation.Blend(inPutValue1.OutPutTransform[i], inPutValue2.OutPutTransform[i], t);
				else if (inPutValue1.AnimMask.Mask[i])
					outPutMat[i] = inPutValue1.OutPutTransform[i];
				else if (inPutValue2.AnimMask.Mask[i])
					outPutMat[i] = inPutValue2.OutPutTransform[i];
				else
					outPutMat[i] = Transformation.Identify;
			}

			return new BlendTreeNodeOutPut(outPutMat, animMask);
		}

	}
}
