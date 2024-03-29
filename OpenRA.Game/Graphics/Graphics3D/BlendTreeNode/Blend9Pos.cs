﻿using System;
using TrueSync;

namespace OpenRA.Graphics
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
	public class Blend9Pos : BlendNode
	{
		public BlendTreeNode[] InPutNodes { get { return inPutNodes; } }
		BlendTreeNode[] inPutNodes = new BlendTreeNode[9];

		public TSVector2 BlendPos;

		public Blend9Pos(string name, uint id, BlendTree blendTree, AnimMask animMask, BlendTreeNode[] inPutNodes)
			: base(name, id, blendTree, animMask)
		{
			this.inPutNodes = inPutNodes;
		}

		int2 pos;
		public override void UpdateTick(short optick, bool run, int step)
		{
			if (optick == tick)
				return;
			updated = false;
			tick = optick;

			pos = new int2((int)(BlendPos.x * 1024), (int)(BlendPos.y * 1024));

			for (int i = 0; i < 9; i++)
			{
				if (inPutNodes[i] != null)
					inPutNodes[i].UpdateTick(optick, run, step);
				else
					throw new Exception("Blend9Pos error: inPutNodes[" + i + "] is null");
			}
		}

		readonly BlendTreeNodeOutPut[] inPutValues = new BlendTreeNodeOutPut[9];

		public override BlendTreeNodeOutPut GetOutPut(short optick)
		{
			if (updated)
				return outPut;
			updated = true;

			for (int i = 0; i < 9; i++)
			{
				if (inPutNodes[i] != null)
					inPutValues[i] = inPutNodes[i].GetOutPut(optick);
				else
					throw new Exception("Blend9Pos error: inPutNodes[" + i + "] is null");
			}

			if (pos.X > 0)
			{
				if (pos.Y > 0)
				{
					var a = blendTree.Blend(inPutValues[4], inPutValues[5], BlendPos.x, animMask);
					var b = blendTree.Blend(inPutValues[1], inPutValues[2], BlendPos.x, animMask);
					return outPut = blendTree.Blend(a, b, BlendPos.y, animMask);
				}
				else
				{
					var a = blendTree.Blend(inPutValues[4], inPutValues[5], BlendPos.x, animMask);
					var b = blendTree.Blend(inPutValues[7], inPutValues[8], BlendPos.x, animMask);
					return outPut = blendTree.Blend(a, b, -BlendPos.y, animMask);
				}
			}
			else
			{
				if (pos.Y > 0)
				{
					var a = blendTree.Blend(inPutValues[4], inPutValues[3], -BlendPos.x, animMask);
					var b = blendTree.Blend(inPutValues[1], inPutValues[0], -BlendPos.x, animMask);
					return outPut = blendTree.Blend(a, b, BlendPos.y, animMask);
				}
				else
				{
					var a = blendTree.Blend(inPutValues[4], inPutValues[3], -BlendPos.x, animMask);
					var b = blendTree.Blend(inPutValues[7], inPutValues[6], -BlendPos.x, animMask);
					return outPut = blendTree.Blend(a, b, -BlendPos.y, animMask);
				}
			}

			/*
			// 在顶点或水平或竖直线段上
			if (Math.Abs(pos.X) == 1024 || Math.Abs(pos.Y) == 1024 || pos.X == 0 || pos.Y == 0)
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
					if (BlendPos.y < 0)
						return outPut = blendTree.Blend(inPutValues[3], inPutValues[6], FP.Abs(BlendPos.y), animMask);
					else
						return outPut = blendTree.Blend(inPutValues[3], inPutValues[0], BlendPos.y, animMask);
				}
				else if (pos.X == 0)
				{
					if (BlendPos.y < 0)
						return outPut = blendTree.Blend(inPutValues[4], inPutValues[7], FP.Abs(BlendPos.y), animMask);
					else
						return outPut = blendTree.Blend(inPutValues[4], inPutValues[1], BlendPos.y, animMask);
				}
				else if (pos.X == 1024)
				{
					if (BlendPos.y < 0)
						return outPut = blendTree.Blend(inPutValues[5], inPutValues[8], FP.Abs(BlendPos.y), animMask);
					else
						return outPut = blendTree.Blend(inPutValues[5], inPutValues[2], BlendPos.y, animMask);
				}
				else if (pos.Y == 1024)
				{
					if (BlendPos.x < 0)
						return outPut = blendTree.Blend(inPutValues[1], inPutValues[0], FP.Abs(BlendPos.x), animMask);
					else
						return outPut = blendTree.Blend(inPutValues[1], inPutValues[2], BlendPos.x, animMask);
				}
				else if (pos.Y == 0)
				{
					if (BlendPos.x < 0)
						return outPut = blendTree.Blend(inPutValues[4], inPutValues[3], FP.Abs(BlendPos.x), animMask);
					else
						return outPut = blendTree.Blend(inPutValues[4], inPutValues[5], BlendPos.x, animMask);
				}
				else if (pos.Y == -1024)
				{
					if (BlendPos.x < 0)
						return outPut = blendTree.Blend(inPutValues[7], inPutValues[6], FP.Abs(BlendPos.x), animMask);
					else
						return outPut = blendTree.Blend(inPutValues[7], inPutValues[8], BlendPos.x, animMask);
				}
			}
			else if (-pos.X == pos.Y)
			{
				// 在斜对角线上
				if (BlendPos.x < 0)
					return outPut = blendTree.Blend(inPutValues[4], inPutValues[0], BlendPos.y, animMask);
				else
					return outPut = blendTree.Blend(inPutValues[4], inPutValues[8], BlendPos.x, animMask);
			}
			else if (pos.X == pos.Y)
			{
				// 在斜对角线上
				if (BlendPos.x > 0)
					return outPut = blendTree.Blend(inPutValues[4], inPutValues[2], BlendPos.x, animMask);
				else
					return outPut = blendTree.Blend(inPutValues[4], inPutValues[6], BlendPos.y, animMask);
			}
			else if (FP.Abs(BlendPos.x) > FP.Abs(BlendPos.y))
			{
				if (BlendPos.x < 0)
				{
					if (BlendPos.y < 0)
						return outPut = blendTree.Blend(blendTree.Blend(inPutValues[4], inPutValues[3], FP.Abs(BlendPos.x), animMask), inPutValues[6], (BlendPos.y * BlendPos.y) / FP.Abs(BlendPos.x), animMask);
					else
						return outPut = blendTree.Blend(blendTree.Blend(inPutValues[4], inPutValues[3], FP.Abs(BlendPos.x), animMask), inPutValues[0], (BlendPos.y * BlendPos.y) / FP.Abs(BlendPos.x), animMask);
				}
				else
				{
					if (BlendPos.y < 0)
						return outPut = blendTree.Blend(blendTree.Blend(inPutValues[4], inPutValues[5], BlendPos.x, animMask), inPutValues[8], (BlendPos.y * BlendPos.y) / BlendPos.x, animMask);
					else
						return outPut = blendTree.Blend(blendTree.Blend(inPutValues[4], inPutValues[5], BlendPos.x, animMask), inPutValues[2], (BlendPos.y * BlendPos.y) / BlendPos.x, animMask);
				}
			}
			else
			{
				if (BlendPos.y < 0)
				{
					if (BlendPos.x < 0)
						return outPut = blendTree.Blend(blendTree.Blend(inPutValues[4], inPutValues[7], FP.Abs(BlendPos.y), animMask), inPutValues[6], (BlendPos.x * BlendPos.x) / FP.Abs(BlendPos.y), animMask);
					else
						return outPut = blendTree.Blend(blendTree.Blend(inPutValues[4], inPutValues[7], FP.Abs(BlendPos.y), animMask), inPutValues[8], (BlendPos.x * BlendPos.x) / FP.Abs(BlendPos.y), animMask);
				}
				else
				{
					if (BlendPos.x < 0)
						return outPut = blendTree.Blend(blendTree.Blend(inPutValues[4], inPutValues[1], BlendPos.y, animMask), inPutValues[0], (BlendPos.x * BlendPos.x) / BlendPos.y, animMask);
					else
						return outPut = blendTree.Blend(blendTree.Blend(inPutValues[4], inPutValues[1], BlendPos.y, animMask), inPutValues[2], (BlendPos.x * BlendPos.x) / BlendPos.y, animMask);
				}
			}
			*/

			// 没有成功的输出，那么报错
			throw new Exception("Blend9Pos error");
		}

		readonly BlendTreeNodeOutPutOne[] inPutValueOnces = new BlendTreeNodeOutPutOne[9];
		public override BlendTreeNodeOutPutOne GetOutPutOnce(int animId, short tick)
		{
			if (updated)
				return new BlendTreeNodeOutPutOne(outPut.OutPutFrame[animId], outPut.AnimMask);

			for (int i = 0; i < 9; i++)
			{
				if (inPutNodes[i] != null)
					inPutValueOnces[i] = inPutNodes[i].GetOutPutOnce(animId, tick);
				else
					throw new Exception("Blend9Pos error: inPutNodes[" + i + "] is null");
			}

			if (pos.X > 0)
			{
				if (pos.Y > 0)
				{
					var a = blendTree.Blend(inPutValueOnces[4], inPutValueOnces[5], BlendPos.x, animMask, animId);
					var b = blendTree.Blend(inPutValueOnces[1], inPutValueOnces[2], BlendPos.x, animMask, animId);
					return blendTree.Blend(a, b, BlendPos.y, animMask, animId);
				}
				else
				{
					var a = blendTree.Blend(inPutValueOnces[4], inPutValueOnces[5], BlendPos.x, animMask, animId);
					var b = blendTree.Blend(inPutValueOnces[7], inPutValueOnces[8], BlendPos.x, animMask, animId);
					return blendTree.Blend(a, b, -BlendPos.y, animMask, animId);
				}
			}
			else
			{
				if (pos.Y > 0)
				{
					var a = blendTree.Blend(inPutValueOnces[4], inPutValueOnces[3], -BlendPos.x, animMask, animId);
					var b = blendTree.Blend(inPutValueOnces[1], inPutValueOnces[0], -BlendPos.x, animMask, animId);
					return blendTree.Blend(a, b, BlendPos.y, animMask, animId);
				}
				else
				{
					var a = blendTree.Blend(inPutValueOnces[4], inPutValueOnces[3], -BlendPos.x, animMask, animId);
					var b = blendTree.Blend(inPutValueOnces[7], inPutValueOnces[6], -BlendPos.x, animMask, animId);
					return blendTree.Blend(a, b, -BlendPos.y, animMask, animId);
				}
			}
		}
	}
}
