using System;

namespace OpenRA.Graphics.Graphics3D
{
	/// <summary>
	/// 混合9个动画，主要用来实现8向移动动画混合
	/// 可以理解为9宫格的布局
	/// 9个输入依次为
	/// (-1,1) (0,1) (1,1)
	/// (-1,0) (0,0) (1,0)
	/// (-1,-1) (0,-1) (1,-1)
	/// 必须按预定顺序输入
	/// </summary>
	class Blend9Pos : BlendNode
	{
		public BlendTreeNode[] InPutNodes { get { return inPutNodes; } }
		BlendTreeNode[] inPutNodes = new BlendTreeNode[9];

		public float2 BlendPos;

		public Blend9Pos(string name, uint id, BlendTree blendTree, AnimMask animMask, BlendTreeNode[] inPutNodes)
			: base(name, id, blendTree, animMask)
		{
			this.inPutNodes = inPutNodes;
		}

		public override BlendTreeNodeOutPut UpdateOutPut(short optick, bool run, int step)
		{
			if (optick == tick)
				return outPut;
			tick = optick;

			int2 pos = new int2((int)(BlendPos.X * 1024), (int)(BlendPos.Y * 1024));
			var inPutValues = new BlendTreeNodeOutPut[9];

			for (int i = 0; i < 9; i++)
			{
				if (inPutNodes[i] != null)
					inPutValues[i] = inPutNodes[i].UpdateOutPut(optick, run, step);
				else
					throw new Exception("Blend9Pos error: inPutNodes[" + i + "] is null");
			}

			// 在顶点或水平或竖直线段上
			if (pos.X == MathF.Abs(1024) || pos.Y == MathF.Abs(1024) || pos.X == 0 || pos.Y == 0)
			{
				// 在顶点上
				if (pos == new int2(-1024, 1024))
					return outPut = inPutValues[0];
				if (pos == new int2(0, 1024))
					return outPut = inPutValues[1];
				if (pos == new int2(1024, 1024))
					return outPut = inPutValues[2];
				if (pos == new int2(-1024, 0))
					return outPut = inPutValues[3];
				if (pos == new int2(0, 0))
					return outPut = inPutValues[4];
				if (pos == new int2(1024, 0))
					return outPut = inPutValues[5];
				if (pos == new int2(-1024, -1024))
					return outPut = inPutValues[6];
				if (pos == new int2(0, -1024))
					return outPut = inPutValues[7];
				if (pos == new int2(1024, -1024))
					return outPut = inPutValues[8];

				// 在竖直与水平线上
				if (pos.X == -1024)
				{
					if (BlendPos.Y < 0)
						return outPut = BlendTreeUtil.Blend(inPutValues[3], inPutValues[6], MathF.Abs(BlendPos.Y), animMask);
					else
						return outPut = BlendTreeUtil.Blend(inPutValues[3], inPutValues[0], BlendPos.Y, animMask);
				}
				else if (pos.X == 0)
				{
					if (BlendPos.Y < 0)
						return outPut = BlendTreeUtil.Blend(inPutValues[4], inPutValues[7], MathF.Abs(BlendPos.Y), animMask);
					else
						return outPut = BlendTreeUtil.Blend(inPutValues[4], inPutValues[1], BlendPos.Y, animMask);
				}
				else if (pos.X == 1024)
				{
					if (BlendPos.Y < 0)
						return outPut = BlendTreeUtil.Blend(inPutValues[5], inPutValues[8], MathF.Abs(BlendPos.Y), animMask);
					else
						return outPut = BlendTreeUtil.Blend(inPutValues[5], inPutValues[2], BlendPos.Y, animMask);
				}
				else if (pos.Y == 1024)
				{
					if (BlendPos.X < 0)
						return outPut = BlendTreeUtil.Blend(inPutValues[1], inPutValues[0], MathF.Abs(BlendPos.X), animMask);
					else
						return outPut = BlendTreeUtil.Blend(inPutValues[1], inPutValues[2], BlendPos.X, animMask);
				}
				else if (pos.Y == 0)
				{
					if (BlendPos.X < 0)
						return outPut = BlendTreeUtil.Blend(inPutValues[4], inPutValues[3], MathF.Abs(BlendPos.X), animMask);
					else
						return outPut = BlendTreeUtil.Blend(inPutValues[4], inPutValues[5], BlendPos.X, animMask);
				}
				else if (pos.Y == -1024)
				{
					if (BlendPos.X < 0)
						return outPut = BlendTreeUtil.Blend(inPutValues[7], inPutValues[6], MathF.Abs(BlendPos.X), animMask);
					else
						return outPut = BlendTreeUtil.Blend(inPutValues[7], inPutValues[8], BlendPos.X, animMask);
				}
			}
			else if (-pos.X == pos.Y)
			{
				// 在斜对角线上
				if (BlendPos.X < 0)
					return outPut = BlendTreeUtil.Blend(inPutValues[4], inPutValues[0], BlendPos.Y, animMask);
				else
					return outPut = BlendTreeUtil.Blend(inPutValues[4], inPutValues[8], BlendPos.X, animMask);
			}
			else if (pos.X == pos.Y)
			{
				// 在斜对角线上
				if (BlendPos.X > 0)
					return outPut = BlendTreeUtil.Blend(inPutValues[4], inPutValues[2], BlendPos.X, animMask);
				else
					return outPut = BlendTreeUtil.Blend(inPutValues[4], inPutValues[6], BlendPos.Y, animMask);
			}
			else if (MathF.Abs(BlendPos.X) > MathF.Abs(BlendPos.Y))
			{
				if (BlendPos.X < 0)
				{
					if (BlendPos.Y < 0)
						return outPut = BlendTreeUtil.Blend(BlendTreeUtil.Blend(inPutValues[4], inPutValues[3], MathF.Abs(BlendPos.X), animMask), inPutValues[6], (BlendPos.Y * BlendPos.Y) / MathF.Abs(BlendPos.X), animMask);
					else
						return outPut = BlendTreeUtil.Blend(BlendTreeUtil.Blend(inPutValues[4], inPutValues[3], MathF.Abs(BlendPos.X), animMask), inPutValues[0], (BlendPos.Y * BlendPos.Y) / MathF.Abs(BlendPos.X), animMask);
				}
				else
				{
					if (BlendPos.Y < 0)
						return outPut = BlendTreeUtil.Blend(BlendTreeUtil.Blend(inPutValues[4], inPutValues[5], BlendPos.X, animMask), inPutValues[8], (BlendPos.Y * BlendPos.Y) / BlendPos.X, animMask);
					else
						return outPut = BlendTreeUtil.Blend(BlendTreeUtil.Blend(inPutValues[4], inPutValues[5], BlendPos.X, animMask), inPutValues[2], (BlendPos.Y * BlendPos.Y) / BlendPos.X, animMask);
				}
			}
			else
			{
				if (BlendPos.Y < 0)
				{
					if (BlendPos.X < 0)
						return outPut = BlendTreeUtil.Blend(BlendTreeUtil.Blend(inPutValues[4], inPutValues[7], MathF.Abs(BlendPos.Y), animMask), inPutValues[6], (BlendPos.X * BlendPos.X) / MathF.Abs(BlendPos.Y), animMask);
					else
						return outPut = BlendTreeUtil.Blend(BlendTreeUtil.Blend(inPutValues[4], inPutValues[7], MathF.Abs(BlendPos.Y), animMask), inPutValues[8], (BlendPos.X * BlendPos.X) / MathF.Abs(BlendPos.Y), animMask);
				}
				else
				{
					if (BlendPos.X < 0)
						return outPut = BlendTreeUtil.Blend(BlendTreeUtil.Blend(inPutValues[4], inPutValues[1], BlendPos.Y, animMask), inPutValues[0], (BlendPos.X * BlendPos.X) / BlendPos.Y, animMask);
					else
						return outPut = BlendTreeUtil.Blend(BlendTreeUtil.Blend(inPutValues[4], inPutValues[1], BlendPos.Y, animMask), inPutValues[2], (BlendPos.X * BlendPos.X) / BlendPos.Y, animMask);
				}
			}

			// 没有成功的输出，那么报错
			throw new Exception("Blend9Pos error");
		}
	}
}
