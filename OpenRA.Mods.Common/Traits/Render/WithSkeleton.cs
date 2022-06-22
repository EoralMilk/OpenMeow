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
using OpenRA.Mods.Common.Activities;
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
		public readonly OrderedSkeleton OrderedSkeleton;
		public readonly SkeletonInstance Skeleton;
		readonly SkeletalAnim currentAnim;
		readonly SkeletalAnim targetAnim;
		World3DRenderer w3dr;
		AttachPointManager attachManager;

		readonly RenderMeshes rm;
		public bool Draw;
		int tick = 0;
		int frameTick = 0;
		int frameTick2 = 0;

		public readonly float Scale = 1;
		readonly Actor self;
		readonly IFacing myFacing;
		readonly BodyOrientation body;
		readonly IMove move;
		float blend;

		public WithSkeleton(Actor self, WithSkeletonInfo info)
			: base(info)
		{
			body = self.Trait<BodyOrientation>();
			move = self.Trait<IMove>();
			myFacing = self.Trait<IFacing>();
			this.self = self;

			rm = self.Trait<RenderMeshes>();
			Scale = rm.Info.Scale;
			OrderedSkeleton = self.World.SkeletonCache.GetOrderedSkeleton(rm.Image);
			if (OrderedSkeleton == null)
				throw new Exception("orderedSkeleton is null");

			Skeleton = OrderedSkeleton.CreateInstance();

			currentAnim = OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(rm.Image, info.Sequence);
			targetAnim = OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(rm.Image, info.Sequence2);

			if (OrderedSkeleton.SkeletonAsset.Animations.Count == 0)
			{
				throw new Exception("unit " + rm.Image + " has no animation");
			}
		}

		protected override void Created(Actor self)
		{
			w3dr = Game.Renderer.World3DRenderer;
			attachManager = self.TraitOrDefault<AttachPointManager>();
		}

		void ITick.Tick(Actor self)
		{
			SelfTick();
		}

		void SelfTick()
		{
			Draw = !IsTraitDisabled;

			Skeleton.UpdateLastPose();

			if (attachManager == null || !attachManager.HasParent)
				Skeleton.SetOffset(self.CenterPosition, myFacing.Orientation, Scale);

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
				Skeleton.UpdateOffset(result);
			}

			if (Draw)
			{
				// update my skeletonInstance drawId
				OrderedSkeleton.AddInstance(Skeleton);
				Skeleton.ProcessManagerData();
			}
			else
			{
				Skeleton.DrawID = -1;
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
			if (Skeleton.CanGetPose())
				return Skeleton.DrawID;
			else
				return -1;
		}

		public int GetBoneId(string boneName)
		{
			if (OrderedSkeleton.SkeletonAsset.BonesDict.ContainsKey(boneName))
				return OrderedSkeleton.SkeletonAsset.BonesDict[boneName].Id;
			else
				return -1;
		}

		public WPos GetWPosFromBoneId(int id)
		{
			return Skeleton.BoneWPos(id, w3dr);
		}

		public WRot GetWRotFromBoneId(int id)
		{
			return Skeleton.BoneWRot(id, w3dr);
		}
	}
}
