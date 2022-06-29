using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Primitives;
using OpenRA.Traits;
using TrueSync;

namespace OpenRA.Mods.Common.Traits.Trait3D
{
	public class BlendTreeHandlerInfo : TraitInfo, Requires<WithSkeletonInfo>
	{
		public readonly string SkeletonToUse = null;
		public readonly string Stand = null;
		public readonly string Walk = null;
		public readonly string Guard = null;

		public readonly int Stand2WalkTick = 10;
		public override object Create(ActorInitializer init) { return new BlendTreeHandler(init.Self, this); }
	}

	public class BlendTreeHandler : IBlendTreeHandler, IPrepareForAttack, ITick
	{
		readonly BlendTree blendTree;
		readonly BlendTreeHandlerInfo info;
		readonly WithSkeleton withSkeleton;
		readonly Actor self;
		readonly IFacing myFacing;
		readonly BodyOrientation body;
		readonly IMove move;

		// nodes
		readonly Switch moveSwitch;
		readonly AnimationNode standAnim;
		readonly AnimationNode walkAnim;
		readonly AnimationNode guardAnim;

		readonly Blend2 guardBlend2;

		// temp test
		readonly SkeletalAnim stand;
		readonly SkeletalAnim walk;
		readonly SkeletalAnim guard;

		public BlendTreeHandler(Actor self, BlendTreeHandlerInfo info)
		{
			this.info = info;
			this.self = self;
			body = self.Trait<BodyOrientation>();
			move = self.Trait<IMove>();
			myFacing = self.Trait<IFacing>();

			if (info.SkeletonToUse == null)
				throw new YamlException("BlendTreeHandler must define a SkeletonToUse for get animations");
			withSkeleton = self.TraitsImplementing<WithSkeleton>().Single(w => w.Info.Name == info.SkeletonToUse);

			stand = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.Stand);
			walk = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.Walk);
			guard = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.Guard);
			if (withSkeleton.OrderedSkeleton.SkeletonAsset.Animations.Count == 0)
			{
				throw new Exception("unit " + withSkeleton.Image + " has no animation");
			}

			blendTree = new BlendTree();
			var animMask = new AnimMask("all", withSkeleton.OrderedSkeleton.SkeletonAsset.BoneNameAnimIndex.Count);
			standAnim = new AnimationNode(info.Stand, 0, blendTree, animMask, stand);
			walkAnim = new AnimationNode(info.Walk, 1, blendTree, animMask, walk);
			guardAnim = new AnimationNode(info.Guard, 2, blendTree, animMask, guard);
			moveSwitch = new Switch("Stand2Walk", 10, blendTree, animMask, standAnim, walkAnim, info.Stand2WalkTick);
			guardBlend2 = new Blend2("GuardBlend", 11, blendTree, animMask, moveSwitch, guardAnim);
			blendTree.InitTree(guardBlend2);

			withSkeleton.BlendTreeHandler = this;
		}

		public BlendTreeNodeOutPut GetResult()
		{
			return blendTree.GetOutPut();
		}

		void IBlendTreeHandler.UpdateTick()
		{
			blendTree.UpdateTick();
		}

		FP guardBlendSpeed = 0.1f;
		int guardTick = 0;
		readonly int guardTime = 50;
		public bool PrepareForAttack()
		{
			guardTick = 0;
			if (guardBlend2.BlendValue < FP.One)
			{
				guardBlend2.BlendValue += guardBlendSpeed;
				return false;
			}
			else
			{
				guardBlend2.BlendValue = FP.One;
				return true;
			}
		}

		public void Tick(Actor self)
		{
			if (guardTick == guardTime)
			{
				if (guardBlend2.BlendValue > FP.Zero)
					guardBlend2.BlendValue -= guardBlendSpeed;
				else
					guardBlend2.BlendValue = FP.Zero;
			}
			else
			{
				guardTick++;
			}

			if (move.CurrentMovementTypes != MovementType.None)
				moveSwitch.SetFlag(true);
			else
				moveSwitch.SetFlag(false);
		}
	}
}
