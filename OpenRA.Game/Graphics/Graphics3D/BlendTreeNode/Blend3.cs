using System;
using TrueSync;

namespace OpenRA.Graphics
{
	/// <summary>
	/// 基础的混合节点。
	/// 混合三个输入节点的Mat4[]数据，输出一个混合后的Mat4[]数据
	/// 它会试图在更新输出时对输入节点的UpdateFrame进行调用
	/// </summary>
	public class Blend3 : BlendNode
	{
		public BlendTreeNode InPutNodeCommon { get { return inPutNodeMid; } }
		public BlendTreeNode InPutNodeHigh { get { return inPutNodeHigh; } }
		public BlendTreeNode InPutNodeLow { get { return inPutNodeLow; } }

		public FP BlendValue = FP.Zero;
		BlendTreeNode inPutNodeMid;
		BlendTreeNode inPutNodeHigh;
		BlendTreeNode inPutNodeLow;

		public Blend3(string name, uint id, BlendTree blendTree, AnimMask animMask, BlendTreeNode inPutNodeMid, BlendTreeNode inPutNodeHigh, BlendTreeNode inPutNodeLow)
			: base(name, id, blendTree, animMask)
		{
			this.inPutNodeMid = inPutNodeMid;
			this.inPutNodeHigh = inPutNodeHigh;
			this.inPutNodeLow = inPutNodeLow;
		}

		public override void UpdateTick(short optick, bool run, int step)
		{
			if (optick == tick)
				return;
			updated = false;
			tick = optick;

			inPutNodeMid.UpdateTick(optick, run, step);
			inPutNodeHigh.UpdateTick(optick, run, step);
			inPutNodeLow.UpdateTick(optick, run, step);
		}

		public override BlendTreeNodeOutPut GetOutPut(short optick)
		{
			if (updated)
				return outPut;

			var inPutValueMid = inPutNodeMid.GetOutPut(optick);
			var inPutValueHigh = inPutNodeHigh.GetOutPut(optick);
			var inPutValueLow = inPutNodeLow.GetOutPut(optick);

			if (BlendValue > 0)
				outPut = blendTree.Blend(inPutValueMid, inPutValueHigh, BlendValue, animMask);
			else
				outPut = blendTree.Blend(inPutValueMid, inPutValueLow, -BlendValue, animMask);
			updated = true;
			return outPut;
		}

		public override BlendTreeNodeOutPutOne GetOutPutOnce(int animId, short tick)
		{
			if (updated)
				return new BlendTreeNodeOutPutOne(outPut.OutPutFrame[animId], outPut.AnimMask);

			var inPutValueMid = inPutNodeMid.GetOutPutOnce(animId, tick);
			var inPutValueHigh = inPutNodeHigh.GetOutPutOnce(animId, tick);
			var inPutValueLow = inPutNodeLow.GetOutPutOnce(animId, tick);

			if (BlendValue > 0)
				return blendTree.Blend(inPutValueMid, inPutValueHigh, BlendValue, animMask, animId);
			else
				return blendTree.Blend(inPutValueMid, inPutValueLow, -BlendValue, animMask, animId);
		}
	}
}
