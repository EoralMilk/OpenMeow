using System;
using System.Collections.Generic;

namespace OpenRA.Graphics
{
	/// <summary>
	/// 储存动画，输出动画当前帧的变换矩阵
	/// 没有输出节点控制的话它不会更新帧数
	/// 基本的叶节点，一般只能有一个输出节点，没有输入节点
	/// </summary>
	public class AnimationNode : LeafNode
	{
		public string AnimationName { get { return animation.Name; } }
		public int CurrentFrame { get { return frame; } }

		bool backwards;
		SkeletalAnim animation;
		readonly Dictionary<int, List<Action>> frameActions = new Dictionary<int, List<Action>>();

		public void AddFrameAction(int frame, in Action action)
		{
			if (frameActions.ContainsKey(frame))
			{
				frameActions[frame].Add(action);
			}
			else
			{
				frameActions.Add(frame, new List<Action>
				{
					action
				});
			}
		}

		public AnimationNode(string name, uint id, BlendTree blendTree, AnimMask animMask, SkeletalAnim animation)
			: base(name, id, blendTree, animMask)
		{
			this.animation = animation;
			this.frame = 0;
			blendTree.AddLeaf(this);
		}

		public void ChangeAnimation(SkeletalAnim animation)
		{
			this.animation = animation;
			frame = 0;
			KeepingEnd = false;
		}

		public override void UpdateFrameTick()
		{
			if (!runThisTick || animation == null)
			{
				frame = 0;
				KeepingEnd = false;
			}
			else
			{
				if (NodePlayType == PlayType.Loop)
					frame = (frame + thisTickStep) % animation.Frames.Length;
				else if (NodePlayType == PlayType.Once)
				{
					KeepingEnd = frame == animation.Frames.Length - 1;
					frame = Math.Min(frame + thisTickStep, animation.Frames.Length - 1);
				}
				else if (NodePlayType == PlayType.PingPong)
				{
					// 这里的算法当step大于1时会稍微有些问题
					// 当帧数接近边界时，step再大，最终frame也会被钳制在边界
					// 应该问题不大，而且一般很少用PingPong播放模式
					if (backwards)
					{
						frame = Math.Max(frame - thisTickStep, 0);
						backwards = frame == 0 ? false : true;
					}
					else
					{
						frame = Math.Min(frame + thisTickStep, animation.Frames.Length - 1);
						backwards = frame == animation.Frames.Length - 1 ? true : false;
					}
				}

				if (frameActions.ContainsKey(frame))
				{
					foreach (var a in frameActions[frame])
					{
						a();
					}
				}
			}

			runThisTick = false;
			thisTickStep = 0;
		}

		bool runThisTick = false;
		int thisTickStep = 0;
		public override void UpdateTick(short optick, bool run, int step)
		{
			if (optick == tick)
				return;
			tick = optick;

			if (run && animation != null)
			{
				thisTickStep = step;
				runThisTick = true;
			}
		}

		public override BlendTreeNodeOutPut UpdateOutPut(short optick, bool resolve = true)
		{
			outPut = new BlendTreeNodeOutPut(animation.Frames[frame], animMask);
			return outPut;
		}

		public override int GetLength()
		{
			return animation.Length;
		}
	}
}
