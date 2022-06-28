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
using TrueSync;

namespace OpenRA.Mods.Common.Traits.Trait3D
{
	public class WithSkeletonInfo : ConditionalTraitInfo, Requires<RenderMeshesInfo>
	{
		public readonly string SkeletonDefine = null;
		public readonly string Name = "body";
		public readonly string Stand = null;
		public readonly string Walk = null;
		public readonly int Stand2WalkTick = 10;
		public override object Create(ActorInitializer init) { return new WithSkeleton(init.Self, this); }
	}

	public class WithSkeleton : ConditionalTrait<WithSkeletonInfo>, ITick, IWithSkeleton
	{
		public readonly OrderedSkeleton OrderedSkeleton;
		public readonly SkeletonInstance Skeleton;
		public readonly string Name;
		public readonly string Image;
		readonly List<IBonePoseModifier> bonePoseModifiers = new List<IBonePoseModifier>();

		public void AddBonePoseModifier(int id, IBonePoseModifier ik)
		{
			bonePoseModifiers.Add(ik);
			Skeleton.AddInverseKinematic(id, ik);
		}

		public void CheckIKUpdate()
		{
			foreach (var ik in bonePoseModifiers)
			{
				if (ik.IKState != InverseKinematicState.Keeping)
				{
					CallForUpdate();
					break;
				}
			}
		}

		readonly BlendTree blendTree;
		World3DRenderer w3dr;

		readonly RenderMeshes rm;
		public bool Draw;
		int tick = 0;
		public int Drawtick = 0;
		int lastDrawtick = 0;
		bool created = false;
		int toUpdate = 0;
		public int ToUpdateTick { get => toUpdate; }
		/// <summary>
		/// WIP
		/// </summary>
		public bool ToUpdateSkeleton
		{
			get
			{
				return Draw || (toUpdate > 0);
			}
		}

		public readonly float Scale = 1;
		readonly Actor self;
		readonly IFacing myFacing;
		readonly BodyOrientation body;
		readonly IMove move;

		// nodes
		readonly Switch switchNode;
		readonly AnimationNode animNode1;
		readonly AnimationNode animNode2;

		// temp test
		readonly bool hasAnim = true;
		readonly SkeletalAnim stand;
		readonly SkeletalAnim walk;
		public WithSkeleton(Actor self, WithSkeletonInfo info)
			: base(info)
		{
			Name = info.Name;
			body = self.Trait<BodyOrientation>();
			move = self.Trait<IMove>();
			myFacing = self.Trait<IFacing>();
			this.self = self;

			rm = self.Trait<RenderMeshes>();
			Scale = rm.Info.Scale;
			Image = info.SkeletonDefine == null ? rm.Image : info.SkeletonDefine;
			OrderedSkeleton = self.World.SkeletonCache.GetOrderedSkeleton(Image);
			if (OrderedSkeleton == null)
				throw new Exception("orderedSkeleton is null");

			Skeleton = OrderedSkeleton.CreateInstance();

			if (info.Stand == null || info.Walk == null)
				hasAnim = false;
			else
			{
				stand = OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(Image, info.Stand);
				walk = OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(Image, info.Walk);

				if (OrderedSkeleton.SkeletonAsset.Animations.Count == 0)
				{
					throw new Exception("unit " + Image + " has no animation");
				}

				blendTree = new BlendTree();
				var animMask = new AnimMask("all", OrderedSkeleton.SkeletonAsset.BoneNameAnimIndex.Count);
				animNode1 = new AnimationNode(info.Stand, 0, blendTree, animMask, stand);
				animNode2 = new AnimationNode(info.Walk, 1, blendTree, animMask, walk);
				switchNode = new Switch("Stand2Walk", 2, blendTree, animMask, animNode1, animNode2, info.Stand2WalkTick);
				blendTree.InitTree(switchNode);
			}
		}

		protected override void Created(Actor self)
		{
			w3dr = Game.Renderer.World3DRenderer;
			created = true;
			SkeletonTick();
		}

		void ITick.Tick(Actor self)
		{
			tick++;
			Drawtick++;
		}

		WPos lastSelfPos;
		WRot lastSelfRot;
		float lastScale;

		public void SkeletonTick()
		{
			if (!created)
				return;

			if (hasAnim)
			{
				if (move.CurrentMovementTypes != MovementType.None)
					switchNode.SetFlag(true);
				else
					switchNode.SetFlag(false);

				blendTree.UpdateTick();
			}

			lastSelfPos = self.CenterPosition;
			lastSelfRot = myFacing.Orientation;
			lastScale = Scale;
		}

		public int GetDrawId()
		{
			if (Skeleton.CanGetPose() && Drawtick > 2)
				return Skeleton.DrawID == -1 ? -2 : Skeleton.DrawID;
			else
				return -2;
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

		public bool HasUpdated { get; private set; }
		WithSkeleton parent = null;
		int parentBoneId = -1;
		FP scaleAsChild = 1;
		readonly List<WithSkeleton> children = new List<WithSkeleton>();

		public bool SetParent(WithSkeleton parent, int boneId, float scaleOverride = 0.0f)
		{
			if (HasChild(parent))
				return false;
			if (boneId == -1)
				throw new Exception("Can't set parnet on a bone which id is -1");

			ReleaseFromParent();

			this.parent = parent;
			parentBoneId = boneId;
			scaleAsChild = scaleOverride == 0.0f ? Scale : scaleOverride;
			parent.children.Add(this);
			return true;
		}

		public void ReleaseFromParent()
		{
			if (parent == null)
				return;

			parent.children.Remove(this);
			parent = null;
			parentBoneId = -1;
			scaleAsChild = 1;
		}

		public bool HasChild(WithSkeleton skeleton)
		{
			if (children.Count == 0)
				return false;

			foreach (var ws in children)
			{
				if (ws == skeleton)
					return true;
				else
					ws.HasChild(parent);
			}

			return false;
		}

		public void CallForUpdate(int tickForUpdate = 2)
		{
			toUpdate = tickForUpdate;

			if (!created)
				return;

			if (parent != null)
			{
				parent.CallForUpdate();
			}
		}

		public void UpdateOffset(in SkeletonInstance skeleton)
		{
			skeleton.SetOffset(self.CenterPosition, myFacing.Orientation, Scale);
		}

		public void UpdateSkeleton()
		{
			HasUpdated = false;

			SkeletonTick();
			CheckIKUpdate();

			if (parent != null || !created)
				return;

			UpdateSkeletonInner(ToUpdateSkeleton);
		}

		/// <summary>
		/// notice that! the ik and some other trait which can update skeleton need to use the last tick anim params
		/// </summary>
		void UpdateSkeletonInner(bool callbyParent)
		{
			if (callbyParent)
			{
				if (parent == null)
					Skeleton.SetOffset(lastSelfPos, lastSelfRot, lastScale);
				else
					Skeleton.SetOffset(Transformation.MatWithNewScale(parent.Skeleton.BoneOffsetMat(parentBoneId), scaleAsChild));

				if (hasAnim)
				{
					Skeleton.UpdateOffset(blendTree.GetOutPut().OutPutFrame);
				}
				else
					Skeleton.UpdateOffset(new Frame(0));

				// TODO: this is using for multiple thread in future
				Skeleton.UpdateLastPose();
				HasUpdated = true;
				if (toUpdate > 0)
					toUpdate--;
			}

			foreach (var child in children)
				child.UpdateSkeletonInner(callbyParent);
		}

		public void UpdateDrawInfo()
		{
			if (parent != null || !created)
				return;

			UpdateDrawInfoInner(Draw);
		}

		void UpdateDrawInfoInner(bool callbyParent)
		{
			if (lastDrawtick == Drawtick)
			{
				foreach (var child in children)
					child.UpdateDrawInfoInner(callbyParent);
				return;
			}

			lastDrawtick = Drawtick;

			if (Draw || callbyParent)
			{
				// update my skeletonInstance drawId
				OrderedSkeleton.AddInstance(Skeleton);
				Skeleton.ProcessManagerData();
			}
			else
			{
				Skeleton.DrawID = -1;
			}

			foreach (var child in children)
				child.UpdateDrawInfoInner(callbyParent);
		}
	}
}
