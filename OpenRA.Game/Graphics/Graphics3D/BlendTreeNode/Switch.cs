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
		public FP BlendValue { get => blendValue; }
		FP blendValue = FP.Zero;
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

		public override void UpdateTick(short optick, bool run, int step)
		{
			if (optick == tick)
				return;
			tick = optick;
			updated = false;

			if (flag)
			{
				blendValue = TSMath.Min(blendValue + FP.One / SwitchTick, FP.One);
			}
			else
			{
				blendValue = TSMath.Max(blendValue - FP.One / SwitchTick, FP.Zero);
			}

			if (blendValue >= FP.One)
			{
				inPutNode2.UpdateTick(optick, run, step);
				inPutNode1.UpdateTick(optick, false, step);
			}
			else if (blendValue <= FP.Zero)
			{
				inPutNode1.UpdateTick(optick, run, step);
				inPutNode2.UpdateTick(optick, false, step);
			}
			else
			{
				inPutNode1.UpdateTick(optick, run, step);
				inPutNode2.UpdateTick(optick, run, step);
			}
		}

		public override BlendTreeNodeOutPut UpdateOutPut(short optick, bool resolve = true)
		{
			if (!resolve || updated)
				return outPut;

			if (blendValue >= FP.One)
			{
				outPut = inPutNode2.UpdateOutPut(optick, resolve);
				inPutNode1.UpdateOutPut(optick, resolve);
			}
			else if (blendValue <= FP.Zero)
			{
				outPut = inPutNode1.UpdateOutPut(optick, resolve);
				inPutNode2.UpdateOutPut(optick, resolve);
			}
			else
			{
				var inPutValue1 = inPutNode1.UpdateOutPut(optick, resolve);
				var inPutValue2 = inPutNode2.UpdateOutPut(optick, resolve);
				outPut = blendTree.Blend(inPutValue1, inPutValue2, blendValue, animMask);
			}

			updated = true;
			return outPut;
		}
	}
}
