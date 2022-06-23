using System;
using TrueSync;

namespace OpenRA.Graphics
{
	/// <summary>
	/// 混合一个动画，一次激活播放一遍Shot动画。
	/// 播放完毕后，状态分为两种：
	/// Recover：恢复到Shot之前的状态，Shot动画不会继续覆盖输入
	/// Keep：Shot播放完毕后保持Shot的最后一帧不变，继续覆盖输入
	/// </summary>
	public class OneShot : BlendNode
	{
		public enum ShotEndType
		{
			Recover,
			Keep
		}

		public BlendTreeNode InPutNode { get { return inPutNode; } }
		public BlendTreeNode ShotNode { get { return shot; } }

		public int FadeTick = 10;
		BlendTreeNode inPutNode;
		LeafNode shot;

		bool runShot = false;
		int shotTick = 0;
		FP fadeBlend = 0.0f;

		ShotEndType shotEndType = ShotEndType.Recover;

		public OneShot(string name, uint id, BlendTree blendTree, AnimMask animMask, BlendTreeNode inPutNode, LeafNode shot, ShotEndType shotEndType, int fadeTick)
			: base(name, id, blendTree, animMask)
		{
			this.inPutNode = inPutNode;
			this.shot = shot;
			shot.NodePlayType = LeafNode.PlayType.Once;

			this.shotEndType = shotEndType;
			FadeTick = fadeTick;
		}

		public void StartShot()
		{
			runShot = true;
			shotTick = 0;
			fadeBlend = 0;
		}

		public void StopShot()
		{
			runShot = false;
			shotTick = 0;
			fadeBlend = 0;
		}

		public override BlendTreeNodeOutPut UpdateOutPut(short optick, bool run, int step)
		{
			if (optick == tick)
				return outPut;
			tick = optick;

			var shotValue = shot.UpdateOutPut(optick, runShot, step);

			if (runShot && shotEndType == ShotEndType.Recover)
			{
				if (shot.KeepingEnd)
				{
					shotTick = Math.Max(shotTick - 1, 0);
				}
				else
				{
					shotTick = Math.Min(shotTick + 1, FadeTick);
				}

				fadeBlend = (FP)shotTick / FadeTick;
				outPut = blendTree.Blend(inPutNode.UpdateOutPut(optick, run, step), shotValue, fadeBlend, animMask);
			}
			else if (runShot && shotEndType == ShotEndType.Keep)
			{
				if (shot.KeepingEnd)
				{
					fadeBlend = (FP)1.0f;
					shotTick = FadeTick;
				}
				else
				{
					shotTick = Math.Min(shotTick + 1, FadeTick);
					fadeBlend = (FP)shotTick / FadeTick;
					outPut = blendTree.Blend(inPutNode.UpdateOutPut(optick, run, step), shotValue, fadeBlend, animMask);
				}
			}

			runShot = shotTick != 0;

			return outPut;
		}
	}
}
