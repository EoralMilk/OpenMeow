using System;
using TrueSync;

namespace OpenRA.Graphics
{
	/// <summary>
	/// 在两个输入之间切换
	/// 选择一个作为输出
	/// 有过渡的动画混合效果
	/// 可以设定切换速度
	/// </summary>
	public class Switch : BlendNode
	{
		public BlendTreeNode InPutNodeA { get { return inPutNode1; } }
		public BlendTreeNode InPutNodeB { get { return inPutNode2; } }

		bool flag = false;
		FP blendValue = 0.0f;
		public int SwitchTick = 10;
		readonly BlendTreeNode inPutNode1;
		readonly BlendTreeNode inPutNode2;

		public Switch(string name, uint id, BlendTree blendTree, AnimMask animMask, BlendTreeNode inPutNode1, BlendTreeNode inPutNode2, int switchTick)
			: base(name, id, blendTree, animMask)
		{
			this.inPutNode1 = inPutNode1;
			this.inPutNode2 = inPutNode2;
			this.SwitchTick = switchTick;
		}

		public void SetFlag(bool flag)
		{
			this.flag = flag;
		}

		public override BlendTreeNodeOutPut UpdateOutPut(short optick, bool run, int step)
		{
			if (optick == tick)
				return outPut;
			tick = optick;

			if (flag)
			{
				blendValue = TSMath.Min(blendValue + (FP)1.0f / SwitchTick, (FP)1.0f);
			}
			else
			{
				blendValue = TSMath.Max(blendValue - (FP)1.0f / SwitchTick, (FP)0.0f);
			}

			if (blendValue > (FP)0.999f)
			{
				outPut = inPutNode2.UpdateOutPut(optick, run, step);
				inPutNode1.UpdateOutPut(optick, false, step);
			}
			else if (blendValue < (FP)0.001f)
			{
				outPut = inPutNode1.UpdateOutPut(optick, run, step);
				inPutNode2.UpdateOutPut(optick, false, step);
			}
			else
			{
				var inPutValue1 = inPutNode1.UpdateOutPut(optick, run, step);
				var inPutValue2 = inPutNode2.UpdateOutPut(optick, run, step);
				outPut = blendTree.Blend(inPutValue1, inPutValue2, blendValue, animMask);
			}

			return outPut;
		}
	}
}
