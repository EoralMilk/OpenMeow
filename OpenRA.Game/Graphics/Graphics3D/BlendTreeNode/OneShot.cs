using System;

namespace OpenRA.Graphics.Graphics3D
{
	/// <summary>
	/// 混合一个动画，一次激活播放一遍Shot动画。
	/// 播放完毕后，状态分为两种：
	/// Recover：恢复到Shot之前的状态，Shot动画不会继续覆盖输入
	/// Keep：Shot播放完毕后保持Shot的最后一帧不变，继续覆盖输入
	/// </summary>
	class OneShot : BlendNode
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
		float fadeBlend = 0.0f;

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

			var inPutValue = inPutNode.UpdateOutPut(optick, run, step);

			var shotValue = shot.UpdateOutPut(optick, runShot, step);

			if (!run)
			{
				shotTick = 0;
			}
			else
			{
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

					fadeBlend = (float)shotTick / FadeTick;
				}
				else if (runShot && shotEndType == ShotEndType.Keep)
				{
					if (shot.KeepingEnd)
					{
						fadeBlend = 1.0f;
						shotTick = FadeTick;
					}
					else
					{
						shotTick = Math.Min(shotTick + 1, FadeTick);
						fadeBlend = (float)shotTick / FadeTick;
					}
				}

				runShot = shotTick != 0;
			}

			outPut = BlendTreeUtil.Blend(inPutValue, shotValue, fadeBlend, animMask);

			return outPut;
		}
	}
}
