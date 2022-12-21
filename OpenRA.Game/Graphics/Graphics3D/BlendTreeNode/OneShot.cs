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

		public Action ShotEndAction;

		public Action ShotEndBlendAction;

		public bool Playing { get
			{
				return runShot;
			}
		}

		public int FadeTick = 10;
		readonly BlendTreeNode inPutNode;
		readonly LeafNode shot;

		bool runShot = false;
		bool initShot = false;
		bool toActEndBlend = false;

		int shotTick = 0;
		FP fadeBlend = 0.0f;

		readonly ShotEndType shotEndType = ShotEndType.Recover;

		public OneShot(string name, uint id, BlendTree blendTree, AnimMask animMask, BlendTreeNode inPutNode, LeafNode shot, ShotEndType shotEndType, int fadeTick)
			: base(name, id, blendTree, animMask)
		{
			this.inPutNode = inPutNode;
			this.shot = shot;
			shot.NodePlayType = LeafNode.PlayType.Once;

			this.shotEndType = shotEndType;
			FadeTick = fadeTick;
		}

		public void ForceShotTickToFadeTick()
		{
			shotTick = FadeTick;
		}

		public void StartShot()
		{
			runShot = true;
			shotTick = 0;
			fadeBlend = 0;
			initShot = true;
			toActEndBlend = true;
			interrupted = false;
		}

		bool interrupted = false;
		public void Interrupt()
		{
			if (runShot)
				interrupted = true;
		}

		public void StopShot()
		{
			runShot = false;
			shotTick = 0;
			fadeBlend = 0;
			initShot = false;
			toActEndBlend = false;
			interrupted = false;
		}

		public override void UpdateTick(short optick, bool run, int step)
		{
			if (optick == tick)
				return;
			tick = optick;
			updated = false;

			inPutNode.UpdateTick(optick, shotTick != FadeTick, step);

			if (runShot && shotEndType == ShotEndType.Recover)
			{
				if (shot.KeepingEnd || interrupted)
				{
					if (initShot)
					{
						initShot = false;
						if (ShotEndAction != null)
						{
							ShotEndAction();
						}

						// inPutNode.UpdateTick(optick, false, 0); // start from frame 0
					}

					shotTick = Math.Max(shotTick - 1, 0);
				}
				else
				{
					shotTick = Math.Min(shotTick + 1, FadeTick);
				}

				fadeBlend = (FP)shotTick / FadeTick;
			}
			else if (runShot && shotEndType == ShotEndType.Keep)
			{
				if (shot.KeepingEnd)
				{
					if (initShot)
					{
						initShot = false;
						if (ShotEndAction != null)
						{
							ShotEndAction();
						}
					}

					fadeBlend = FP.One;
					shotTick = FadeTick;
					// inPutNode.UpdateTick(optick, false, 0); // keep
				}
				else
				{
					shotTick = Math.Min(shotTick + 1, FadeTick);
					fadeBlend = (FP)shotTick / FadeTick;
				}
			}

			if (shotTick == 0)
			{
				if (toActEndBlend && ShotEndBlendAction != null)
					ShotEndBlendAction();

				StopShot();
			}

			shot.UpdateTick(optick, runShot, step);
		}

		public override BlendTreeNodeOutPut GetOutPut(short optick)
		{
			if (updated)
				return outPut;

			if (runShot)
				outPut = blendTree.Blend(inPutNode.GetOutPut(optick), shot.GetOutPut(optick), fadeBlend, animMask);
			else
				outPut = inPutNode.GetOutPut(optick);

			updated = true;
			return outPut;
		}

		public override BlendTreeNodeOutPutOne GetOutPutOnce(int animId, short tick)
		{
			if (updated)
				return new BlendTreeNodeOutPutOne(outPut.OutPutFrame[animId], outPut.AnimMask);

			if (runShot)
				return blendTree.Blend(inPutNode.GetOutPutOnce(animId, tick), shot.GetOutPutOnce(animId, tick), fadeBlend, animMask, animId);
			else
				return inPutNode.GetOutPutOnce(animId, tick);
		}
	}
}
