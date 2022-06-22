#region Copyright & License Information
/*
 * Copyright 2007-2022 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits.Render
{
	public class WithSkeletonInfo : ConditionalTraitInfo, Requires<RenderMeshesInfo>
	{
		public readonly string Sequence = "rest";
		public readonly string Sequence2 = "test";
		public readonly float BlendSpeed = 0.03f;

		public override object Create(ActorInitializer init) { return new WithSkeleton(init.Self, this); }
	}

	public class WithSkeleton : ConditionalTrait<WithSkeletonInfo>, ITick
	{
		readonly OrderedSkeleton orderedSkeleton;
		readonly SkeletonInstance skeleton;
		readonly SkeletalAnim[] skeletalAnims;
		readonly SkeletalAnim currentAnim;
		readonly SkeletalAnim targetAnim;
		World3DRenderer w3dr;

		readonly RenderMeshes rm;
		public bool Draw;
		int tick = 0;
		int frameTick = 0;
		int frameTick2 = 0;

		readonly float scale = 1;
		readonly Actor self;
		readonly BodyOrientation body;
		readonly IMove move;
		float blend;

		public WithSkeleton(Actor self, WithSkeletonInfo info)
			: base(info)
		{
			body = self.Trait<BodyOrientation>();
			move = self.Trait<IMove>();
			this.self = self;

			rm = self.Trait<RenderMeshes>();
			scale = rm.Info.Scale;
			orderedSkeleton = self.World.SkeletonCache.GetOrderedSkeleton(rm.Image);
			if (orderedSkeleton == null)
				throw new Exception("orderedSkeleton is null");

			skeleton = orderedSkeleton.CreateInstance();

			currentAnim = orderedSkeleton.SkeletonAsset.GetSkeletalAnim(rm.Image, info.Sequence);
			targetAnim = orderedSkeleton.SkeletonAsset.GetSkeletalAnim(rm.Image, info.Sequence2);

			if (orderedSkeleton.SkeletonAsset.Animations.Count == 0)
			{
				throw new Exception("unit " + rm.Image + " has no animation");
			}
		}

		protected override void Created(Actor self)
		{
			w3dr = Game.Renderer.World3DRenderer;
		}

		void ITick.Tick(Actor self)
		{
			SelfTick();
		}

		void SelfTick()
		{
			Draw = !IsTraitDisabled;

			skeleton.UpdateLastPose();

			skeleton.SetOffset(self.CenterPosition, body.QuantizeOrientation(self.Orientation), scale);

			if (currentAnim != null && targetAnim != null)
			{
				if (move.CurrentMovementTypes != MovementType.None)
				{
					blend = blend >= 1.0 ? 1.0f : blend + Info.BlendSpeed;
				}
				else
				{
					blend = blend <= 0.0 ? 0.0f : blend - Info.BlendSpeed;
				}

				if (blend == 1.0)
				{
					frameTick = 0;
					frameTick2 = (frameTick2 + 1) % targetAnim.Frames.Length;
				}
				else if (blend == 0)
				{
					frameTick2 = 0;
					frameTick = (frameTick + 1) % currentAnim.Frames.Length;
				}
				else
				{
					frameTick = (frameTick + 1) % currentAnim.Frames.Length;
					frameTick2 = (frameTick2 + 1) % targetAnim.Frames.Length;
				}

				Frame result = new Frame(currentAnim.Frames[frameTick].Length);
				BlendFrame(currentAnim.Frames[frameTick], targetAnim.Frames[frameTick2], blend, ref result);
				skeleton.UpdateOffset(result);
			}

			if (Draw)
			{
				// update my skeletonInstance drawId
				orderedSkeleton.AddInstance(skeleton);
				skeleton.ProcessManagerData();
			}
			else
			{
				skeleton.DrawID = -1;
			}

			tick++;
		}

		void BlendFrame(in Frame frameA, in Frame frameB, float alpha, ref Frame result)
		{
			if (alpha == 0)
			{
				result = frameA;
			}
			else if (alpha == 1.0)
			{
				result = frameB;
			}

			for (int i = 0; i < frameA.Length; i++)
			{
				result.Trans[i] = Transformation.Blend(frameA.Trans[i], frameB.Trans[i], alpha);
			}
		}

		public int GetDrawId()
		{
			if (skeleton.CanGetPose())
				return skeleton.DrawID;
			else
				return -1;
		}

		public int GetBoneId(string boneName)
		{
			if (orderedSkeleton.SkeletonAsset.BonesDict.ContainsKey(boneName))
				return orderedSkeleton.SkeletonAsset.BonesDict[boneName].Id;
			else
				return -1;
		}

		public WPos GetWPosFromBoneId(int id)
		{
			return skeleton.BoneWPos(id, w3dr);
		}
	}
}
