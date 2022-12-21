using System;
using System.Collections.Generic;

namespace OpenRA.Graphics
{
	/// <summary>
	/// 输出一个储存好的BlendTreeNodeOutPut
	/// </summary>
	public class DirectResultNode : LeafNode
	{
		public DirectResultNode(string name, uint id, BlendTree blendTree, BlendTreeNodeOutPut outPut)
			: base(name, id, blendTree, outPut.AnimMask)
		{
			this.outPut = outPut;
			blendTree.AddLeaf(this);
			KeepingEnd = true;
		}

		public override void UpdateFrameTick()
		{
			return;
		}

		public override void UpdateTick(short optick, bool run, int step)
		{
			return;
		}

		public override BlendTreeNodeOutPut GetOutPut(short optick)
		{
			return outPut;
		}

		public override BlendTreeNodeOutPutOne GetOutPutOnce(int animId, short tick)
		{
			return new BlendTreeNodeOutPutOne(outPut.OutPutFrame[animId], outPut.AnimMask);
		}

		public override int GetLength()
		{
			return 1;
		}
	}
}
