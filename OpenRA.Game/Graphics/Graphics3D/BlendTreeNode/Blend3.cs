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

		public FP BlendValue = 0.0f;
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

		public override BlendTreeNodeOutPut UpdateOutPut(short optick, bool run, int step, bool resolve = true)
		{
			if (optick == tick)
				return outPut;
			tick = optick;
			if (!resolve)
				return outPut;

			var inPutValueMid = inPutNodeMid.UpdateOutPut(optick, run, step);
			var inPutValueHigh = inPutNodeHigh.UpdateOutPut(optick, run, step);
			var inPutValueLow = inPutNodeLow.UpdateOutPut(optick, run, step);

			if (BlendValue > 0)
				outPut = blendTree.Blend(inPutValueMid, inPutValueHigh, BlendValue, animMask);
			else
				outPut = blendTree.Blend(inPutValueMid, inPutValueLow, -BlendValue, animMask);

			return outPut;
		}
	}
}
