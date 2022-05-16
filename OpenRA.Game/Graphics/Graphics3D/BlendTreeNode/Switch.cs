using System;

namespace OpenRA.Graphics.Graphics3D
{
	/// <summary>
	/// 在两个输入之间切换
	/// 选择一个作为输出
	/// 有过渡的动画混合效果
	/// 可以设定切换速度
	/// </summary>
	class Switch : BlendNode
	{
		public BlendTreeNode InPutNodeA { get { return inPutNode1; } }
		public BlendTreeNode InPutNodeB { get { return inPutNode2; } }

		bool flag = false;
		float blendValue = 0.0f;
		public int SwitchTick = 10;
		BlendTreeNode inPutNode1;
		BlendTreeNode inPutNode2;

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

			var inPutValue1 = inPutNode1.UpdateOutPut(optick, run, step);
			var inPutValue2 = inPutNode2.UpdateOutPut(optick, run, step);

			if (flag)
			{
				blendValue = MathF.Min(blendValue + 1.0f / SwitchTick, 1.0f);
			}
			else
			{
				blendValue = MathF.Max(blendValue - 1.0f / SwitchTick, 0.0f);
			}

			outPut = BlendTreeUtil.Blend(inPutValue1, inPutValue2, blendValue, animMask);
			return outPut;
		}
	}
}
