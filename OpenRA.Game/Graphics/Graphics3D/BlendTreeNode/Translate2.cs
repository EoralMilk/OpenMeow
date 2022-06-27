using System;
using TrueSync;

namespace OpenRA.Graphics
{
	/// <summary>
	/// 在A与B两个状态之间做切换，分别有一个过渡动画用于在从A到B或者从B到A的过程中播放
	/// 切换状态的过程中不接受新的切换指令
	/// </summary>
	public class Translate2 : BlendNode
	{
		public BlendTreeNode InPutNodeA { get { return inPutNode1; } }
		public BlendTreeNode InPutNodeB { get { return inPutNode2; } }
		public LeafNode TransAtoB { get { return transAtoB; } }
		public LeafNode TransBtoA { get { return transBtoA; } }
		public FP TranslateBlendRatio = 0.1f;

		bool flag = false;
		bool translating = false;
		FP blendValue = 0.0f;
		BlendTreeNode inPutNode1;
		LeafNode transAtoB;
		BlendTreeNode inPutNode2;
		LeafNode transBtoA;

		public Translate2(string name, uint id, BlendTree blendTree, AnimMask animMask, BlendTreeNode inPutNode1, BlendTreeNode inPutNode2, LeafNode transAtoB, LeafNode transBtoA)
			: base(name, id, blendTree, animMask)
		{
			this.inPutNode1 = inPutNode1;
			this.inPutNode2 = inPutNode2;

			this.transAtoB = transAtoB;
			this.transBtoA = transBtoA;
			transAtoB.NodePlayType = LeafNode.PlayType.Once;
			transBtoA.NodePlayType = LeafNode.PlayType.Once;
		}

		public void SetFlag(bool flag)
		{
			if (translating)
				return;

			if (this.flag != flag)
			{
				this.flag = flag;
				translating = true;
				transAtoB.ResetFrame();
				transBtoA.ResetFrame();
			}
		}

		public override BlendTreeNodeOutPut UpdateOutPut(short optick, bool run, int step, bool resolve = true)
		{
			if (optick == tick)
				return outPut;
			tick = optick;

			if (translating)
			{
				if (flag)
				{
					var ratio = transAtoB.GetRatio();

					var inPutValue1 = ratio < TranslateBlendRatio ? inPutNode1.UpdateOutPut(optick, run, step) : transAtoB.UpdateOutPut(optick, run, step);
					var inPutValue2 = ratio < TranslateBlendRatio ? transAtoB.UpdateOutPut(optick, run, step) : inPutNode2.UpdateOutPut(optick, run, step);

					if (transAtoB.KeepingEnd)
					{
						translating = false;
					}

					blendValue = ratio < TranslateBlendRatio ? ratio / TranslateBlendRatio : (1.0f - ratio) < TranslateBlendRatio ? (1.0f - ratio) / TranslateBlendRatio : 1.0f;
					if (resolve)
						outPut = blendTree.Blend(inPutValue1, inPutValue2, blendValue, animMask);
					return outPut;
				}
				else
				{
					var ratio = transBtoA.GetRatio();
					var inPutValue1 = ratio < TranslateBlendRatio ? inPutNode2.UpdateOutPut(optick, run, step) : transBtoA.UpdateOutPut(optick, run, step);
					var inPutValue2 = ratio < TranslateBlendRatio ? transBtoA.UpdateOutPut(optick, run, step) : inPutNode1.UpdateOutPut(optick, run, step);

					if (transAtoB.KeepingEnd)
					{
						translating = false;
					}

					blendValue = ratio < TranslateBlendRatio ? ratio / TranslateBlendRatio : (1.0f - ratio) < TranslateBlendRatio ? (1.0f - ratio) / TranslateBlendRatio : 1.0f;
					if (resolve)
						outPut = blendTree.Blend(inPutValue1, inPutValue2, blendValue, animMask);
					return outPut;
				}
			}
			else
			{
				if (flag)
				{
					var inPutValue = inPutNode2.UpdateOutPut(optick, run, step);
					if (resolve)
						outPut = new BlendTreeNodeOutPut(inPutValue.OutPutFrame, animMask);
					return outPut;
				}
				else
				{
					var inPutValue = inPutNode1.UpdateOutPut(optick, run, step);
					if (resolve)
						outPut = new BlendTreeNodeOutPut(inPutValue.OutPutFrame, animMask);
					return outPut;
				}
			}
		}

	}
}
