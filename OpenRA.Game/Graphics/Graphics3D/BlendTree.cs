using System;
using System.Collections.Generic;
using System.Text;
using TrueSync;

namespace OpenRA.Graphics
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
	public class BlendTree
	{
		/// <summary>
		/// 仅用于控制整棵树的所有节点只更新一次
		/// </summary>
		short currentTick;
		public BlendTreeNode FinalOutPut { get; private set; } // 最后的输出节点, 也就是根节点
		public bool RunBlend = true;

		public BlendTree()
		{
			RunBlend = true;
			currentTick = 0;
		}

		public void InitTree(BlendTreeNode finalOutPut)
		{
			FinalOutPut = finalOutPut;
			RunBlend = true;
			currentTick = 0;
		}

		public BlendTreeNodeOutPut GetOutPut()
		{
			currentTick++;

			// step 可以用于跳步，当这一帧滞后时，可以选择跳过几帧动画
			return FinalOutPut.UpdateOutPut(currentTick, true, 1);
		}

		public BlendTreeNodeOutPut Blend(in BlendTreeNodeOutPut inPutValue1, in BlendTreeNodeOutPut inPutValue2, FP t, in AnimMask animMask)
		{
			if (!RunBlend)
				return inPutValue1;

			var outPut = new Frame(inPutValue1.OutPutFrame.Length);
			//var outPut = new Transformation[inPutValue1.OutPutTransform.Length];

			for (int i = 0; i < inPutValue1.OutPutFrame.Length; i++)
			{
				if (inPutValue1.AnimMask.Mask[i] && inPutValue2.AnimMask.Mask[i])
					outPut[i] = Transformation.Blend(inPutValue1.OutPutFrame[i], inPutValue2.OutPutFrame[i], t);
				else if (inPutValue1.AnimMask.Mask[i])
					outPut[i] = inPutValue1.OutPutFrame[i];
				else if (inPutValue2.AnimMask.Mask[i])
					outPut[i] = inPutValue2.OutPutFrame[i];
				else
					outPut[i] = Transformation.Identify;
			}

			return new BlendTreeNodeOutPut(outPut, animMask);
		}
	}

	public abstract class BlendTreeNode
	{
		public string Name { get { return name; } }
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
	public abstract class BlendNode : BlendTreeNode
	{
		public BlendNode(string name, uint id, BlendTree blendTree, AnimMask mask)
			: base(name, id, blendTree, mask) { }
	}

	/// <summary>
	/// BlendTree叶节点基类
	/// </summary>
	public abstract class LeafNode : BlendTreeNode
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
	/// 节点输出
	/// </summary>
	public struct BlendTreeNodeOutPut
	{
		public Frame OutPutFrame;
		public AnimMask AnimMask;

		// 到这个输出为止，全部动画长度的最小公倍数
		// public int FrameLCM;
		// public bool UpdateLCM;

		// 复制构造函数
		public BlendTreeNodeOutPut(BlendTreeNodeOutPut op)
		{
			OutPutFrame = op.OutPutFrame;
			AnimMask = op.AnimMask;

			// FrameLCM = op.FrameLCM;
			// UpdateLCM = op.UpdateLCM;
		}

		public BlendTreeNodeOutPut(in Frame srts, in AnimMask animMask)
		{
			OutPutFrame = srts;
			AnimMask = animMask;
		}
	}

	/// <summary>
	/// 动画遮罩，控制哪些骨骼播放动画，哪些不播放
	/// 它应该被BlendTreeNode作为修改输出动画Mat4数据的依据
	/// </summary>
	public class AnimMask
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

	}
}
