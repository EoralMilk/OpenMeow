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
		public readonly string UpperMask = null;

		public readonly int Stand2WalkTick = 10;
		public readonly int GuardBlendTick = 80;

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

			if (info.UpperMask == null)
				throw new Exception("Need UpperrMask");

			blendTree = new BlendTree();
			var uppermask = withSkeleton.OrderedSkeleton.SkeletonAsset.GetAnimMask(withSkeleton.Image, info.UpperMask);
			var allvalidmask = withSkeleton.OrderedSkeleton.SkeletonAsset.AllValidMask;
			standAnim = new AnimationNode(info.Stand, 0, blendTree, allvalidmask, stand);
			walkAnim = new AnimationNode(info.Walk, 1, blendTree, allvalidmask, walk);
			guardAnim = new AnimationNode(info.Guard, 2, blendTree, uppermask, guard);
			moveSwitch = new Switch("Stand2Walk", 10, blendTree, allvalidmask, standAnim, walkAnim, info.Stand2WalkTick);
			guardBlend2 = new Blend2("GuardBlend", 11, blendTree, allvalidmask, moveSwitch, guardAnim);
			blendTree.InitTree(guardBlend2);

			withSkeleton.BlendTreeHandler = this;
			guardBlendSpeed = FP.One / info.GuardBlendTick;
		}

		public BlendTreeNodeOutPut GetResult()
		{
			return blendTree.GetOutPut();
		}

		void IBlendTreeHandler.UpdateTick()
		{
			blendTree.UpdateTick();
		}

		FP guardBlendSpeed;
		int guardTick = 0;
		readonly int guardTime = 50;
		public bool PrepareForAttack(in Target target)
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
