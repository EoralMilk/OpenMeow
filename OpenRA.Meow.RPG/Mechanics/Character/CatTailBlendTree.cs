using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Trait3D;
using OpenRA.Primitives;
using OpenRA.Traits;
using TrueSync;
using static OpenRA.Meow.RPG.Mechanics.InfantryBlendTree;

namespace OpenRA.Meow.RPG.Mechanics
{
	public class CatTailBlendTreeInfo : TraitInfo, Requires<WithSkeletonInfo>, Requires<InfantryBlendTreeInfo>
	{
		public readonly string SkeletonToUse = null;

		// basic
		public readonly string Stand = null;
		public readonly string Walk = null;
		public readonly string Guard = null;

		public readonly bool CanProne = true;

		// prone
		public readonly string Prone = null;
		public readonly string Crawl = null;

		public readonly string Die = null;
		public readonly string DieProne = null;

		public readonly string[] IdleActions = null;

		public readonly int BlendTick = 10;

		public override object Create(ActorInitializer init) { return new CatTailBlendTree(init.Self, this); }
	}

	public class CatTailBlendTree : IBlendTreeHandler, ITick, INotifyCreated
	{
		readonly BlendTree blendTree;
		readonly CatTailBlendTreeInfo info;
		readonly WithSkeleton withSkeleton;
		readonly Actor self;
		readonly IFacing myFacing;
		readonly IMove move;

		InfantryBlendTree infantryBlendTree;

		readonly SkeletalAnim stand;
		readonly SkeletalAnim walk;
		readonly SkeletalAnim guard;

		readonly SkeletalAnim prone;
		readonly SkeletalAnim crawl;

		readonly SkeletalAnim die;
		readonly SkeletalAnim dieProne;

		readonly AnimationNode animStand;
		readonly AnimationNode animWalk;
		readonly AnimationNode animGuard;

		readonly AnimationNode animProne;
		readonly AnimationNode animCrawl;

		readonly AnimationNode animDie;

		readonly Switch switchWalk;
		readonly Switch switchGuard;

		readonly Switch switchCrawl;

		readonly Switch switchProne;

		readonly OneShot shotDie;

		public CatTailBlendTree(Actor self, CatTailBlendTreeInfo info)
		{
			this.info = info;
			this.self = self;
			move = self.Trait<IMove>();
			myFacing = self.Trait<IFacing>();

			if (info.SkeletonToUse == null)
				throw new YamlException("CatTailBlendTree must define a SkeletonToUse for get animations");
			withSkeleton = self.TraitsImplementing<WithSkeleton>().Single(w => w.Info.Name == info.SkeletonToUse);

			stand = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.Stand);
			walk = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.Walk);
			guard = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.Guard);
			die = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.Die);

			if (info.CanProne)
			{
				prone = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.Prone);
				crawl = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.Crawl);
				dieProne = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.DieProne);
			}

			var allvalidmask = withSkeleton.OrderedSkeleton.SkeletonAsset.AllValidMask;

			blendTree = new BlendTree();

			animStand = new AnimationNode("Stand", 1, blendTree, allvalidmask, stand);
			animWalk = new AnimationNode("Walk", 1, blendTree, allvalidmask, walk);
			animGuard = new AnimationNode("Guard", 1, blendTree, allvalidmask, guard);

			if (info.CanProne)
			{
				animProne = new AnimationNode("Prone", 1, blendTree, allvalidmask, prone);
				animCrawl = new AnimationNode("Crawl", 1, blendTree, allvalidmask, crawl);
			}

			animDie = new AnimationNode("Die", 1, blendTree, allvalidmask, die);

			// set the lerp tick as 1, need handle it in trait
			switchGuard = new Switch("StandGuard", 1, blendTree, allvalidmask, animStand, animGuard, info.BlendTick);

			switchWalk = new Switch("StandWalk", 1, blendTree, allvalidmask, switchGuard, animWalk, info.BlendTick);

			if (info.CanProne)
			{
				switchCrawl = new Switch("ProneCrawl", 1, blendTree, allvalidmask, animProne, animCrawl, info.BlendTick);

				// set the lerp tick as 1, need handle it in trait
				switchProne = new Switch("ProneState", 1, blendTree, allvalidmask, switchWalk, switchCrawl, info.BlendTick);
				shotDie = new OneShot("Die", 1, blendTree, allvalidmask, switchProne, animDie, OneShot.ShotEndType.Keep, info.BlendTick);
			}
			else
			{
				shotDie = new OneShot("Die", 1, blendTree, allvalidmask, switchWalk, animDie, OneShot.ShotEndType.Keep, info.BlendTick);
			}

			blendTree.InitTree(shotDie);
			withSkeleton.BlendTreeHandler = this;
		}

		public void Created(Actor self)
		{
			infantryBlendTree = self.Trait<InfantryBlendTree>();
		}

		public BlendTreeNodeOutPut GetResult()
		{
			return blendTree.GetOutPut();
		}

		void IBlendTreeHandler.UpdateTick()
		{
			blendTree.UpdateTick();
		}

		WRot IBlendTreeHandler.FacingOverride()
		{
			return myFacing.Orientation;
		}

		bool startDie = false;
		public void Tick(Actor self)
		{
			if (infantryBlendTree.CurrentState == InfantryBlendTree.InfantryState.Die)
			{
				animDie.ChangeAnimation(!info.CanProne || infantryBlendTree.CurrentPose == InfantryBlendTree.PoseState.Stand ? die : dieProne);

				if (!startDie)
					shotDie.StartShot();

				startDie = true;
				return;
			}

			if (infantryBlendTree.CurrentState == InfantryBlendTree.InfantryState.Idle)
			{
				switchGuard.SetFlag(false);
			}
			else
			{
				switchGuard.SetFlag(true);
			}

			if (infantryBlendTree.CurrentPose == InfantryBlendTree.PoseState.Prone && info.CanProne)
			{
				switchProne.SetFlag(true);
			}
			else
				switchProne.SetFlag(false);

			if (move.CurrentMovementTypes.HasMovementType(MovementType.Horizontal))
			{
				switchWalk.SetFlag(true);
				if (info.CanProne)
					switchCrawl.SetFlag(true);
			}
			else
			{
				switchWalk.SetFlag(false);
				if (info.CanProne)
					switchCrawl.SetFlag(false);
			}
		}
	}
}
