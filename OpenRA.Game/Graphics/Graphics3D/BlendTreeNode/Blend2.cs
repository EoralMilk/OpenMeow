using System;
using TrueSync;

namespace OpenRA.Graphics
{
	/// <summary>
	/// 基础的混合节点。
	/// 混合两个输入节点的Mat4[]数据，输出一个混合后的Mat4[]数据
	/// 它会试图在更新输出时对输入节点的UpdateFrame进行调用
	/// </summary>
	public class Blend2 : BlendNode
	{
		public BlendTreeNode InPutNodeA { get { return inPutNode1; } }
		public BlendTreeNode InPutNodeB { get { return inPutNode2; } }

		public FP BlendValue = FP.Zero;
		BlendTreeNode inPutNode1;
		BlendTreeNode inPutNode2;

		public Blend2(string name, uint id, BlendTree blendTree, AnimMask animMask, BlendTreeNode inPutNode1, BlendTreeNode inPutNode2)
			: base(name, id, blendTree, animMask)
		{
			this.inPutNode1 = inPutNode1;
			this.inPutNode2 = inPutNode2;
		}

		public override void UpdateTick(short optick, bool run, int step)
		{
			if (optick == tick)
				return;
			updated = false;
			tick = optick;

			inPutNode1.UpdateTick(optick, run, step);
			inPutNode2.UpdateTick(optick, run, step);
		}

		public override BlendTreeNodeOutPut GetOutPut(short optick)
		{
			if (updated)
				return outPut;

			var inPutValue1 = inPutNode1.GetOutPut(optick);
			var inPutValue2 = inPutNode2.GetOutPut(optick);

			outPut = blendTree.Blend(inPutValue1, inPutValue2, BlendValue, animMask);
			updated = true;
			return outPut;
		}

		public override BlendTreeNodeOutPutOne GetOutPutOnce(int animId, short tick)
		{
			if (updated)
				return new BlendTreeNodeOutPutOne(outPut.OutPutFrame[animId], outPut.AnimMask);

			var inPutValue1 = inPutNode1.GetOutPutOnce(animId, tick);
			var inPutValue2 = inPutNode2.GetOutPutOnce(animId, tick);

			return blendTree.Blend(inPutValue1, inPutValue2, BlendValue, animMask, animId);
		}
	}
}
