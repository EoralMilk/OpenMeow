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
using GlmSharp;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Primitives;
using OpenRA.Traits;
using TrueSync;

namespace OpenRA.Mods.Common.Traits.Trait3D
{
	public interface IBlendTreeHandler
	{
		BlendTreeNodeOutPut GetResult();
		void UpdateTick();

		WRot FacingOverride();
	}

	public class WithSkeletonInfo : ConditionalTraitInfo, Requires<RenderMeshesInfo>
	{
		public readonly string SkeletonDefine = null;
		public readonly string Name = "body";
		public readonly bool OnlyUpdateForDraw = false;
		public readonly bool AxisConvert = true;
		public override object Create(ActorInitializer init) { return new WithSkeleton(init, this); }
	}

	public class WithSkeleton : ConditionalTrait<WithSkeletonInfo>, IWithSkeleton
	{
		public readonly OrderedSkeleton OrderedSkeleton;
		public readonly SkeletonInstance Skeleton;
		public readonly string Name;
		public readonly string Image;
		readonly Dictionary<int, IBonePoseModifier> bonePoseModifiers = new Dictionary<int, IBonePoseModifier>();

		public void AddBonePoseModifier(int id, IBonePoseModifier ik)
		{
			bonePoseModifiers.Add(id, ik);
			Skeleton.AddInverseKinematic(id, ik);
		}

		readonly RenderMeshes rm;
		public bool Draw;

		public FP Scale = 1;
		readonly Actor self;
		readonly IFacing myFacing;
		public readonly bool OnlyUpdateForDraw;

		public IBlendTreeHandler BlendTreeHandler;

		/// <summary>
		/// Affects the root migration of the skeleton
		/// </summary>
		public WVec SkeletonOffset = WVec.Zero;

		public WithSkeleton(ActorInitializer init, WithSkeletonInfo info)
			: base(info)
		{
			self = init.Self;
			Name = info.Name;
			myFacing = self.Trait<IFacing>();

			OnlyUpdateForDraw = info.OnlyUpdateForDraw;
			rm = self.Trait<RenderMeshes>();
			Scale = rm.Info.Scale;
			Image = info.SkeletonDefine == null ? rm.Image : info.SkeletonDefine;
			OrderedSkeleton = self.World.SkeletonCache.GetOrderedSkeleton(Image);
			if (OrderedSkeleton == null)
				throw new Exception("orderedSkeleton is null");

			Skeleton = OrderedSkeleton.CreateInstance();
		}

		protected override void Created(Actor self)
		{
			UpdateSkeletonTick();

			// init the current pose by rest pose and offset
			UpdateWholeSkeleton(false);
			RenderUpdateWholeSkeleton(false);
		}

		void UpdateWholeSkeleton(bool callbyParent)
		{
			if (!callbyParent && parent != null)
				return;

			UpdateSkeletonInner();
		}

		void UpdateSkeletonInner()
		{
			Skeleton.UpdateAll();

			foreach (var child in children)
				child.RenderUpdateWholeSkeleton(true);
		}

		WPos lastSelfPos;
		WRot lastSelfRot;
		FP lastScale;

		public void SkeletonTick()
		{
			if (BlendTreeHandler != null)
				BlendTreeHandler.UpdateTick();

			lastSelfPos = self.CenterPosition + SkeletonOffset;
			if (BlendTreeHandler != null)
				lastSelfRot = BlendTreeHandler.FacingOverride();
			else
				lastSelfRot = myFacing.Orientation;
			lastScale = Scale;

			foreach (var kv in bonePoseModifiers)
				kv.Value.UpdateTarget();
		}

		public int GetDrawId()
		{
			if (Skeleton.CanDraw())
				return Skeleton.InstanceID == -1 ? -2 : Skeleton.AnimTexoffset / 4;
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

		public void SetBoneRenderUpdate(int id, bool update)
		{
			Skeleton.Bones[id].NeedUpdateWhenRender = update;
		}

		public WPos GetWPosFromBoneId(int id)
		{
			CallForUpdate(id);
			return Skeleton.BoneWPos(id);
		}

		public WRot GetWRotFromBoneId(int id)
		{
			CallForUpdate(id);
			return Skeleton.BoneWRot(id);
		}

		public TSMatrix4x4 GetMatrixFromBoneId(int id)
		{
			CallForUpdate(id);
			return Skeleton.BoneOffsetMat(id);
		}

		public mat4 GetRenderMatrixFromBoneId(int id)
		{
			return Skeleton.BoneRenderOffsetMat(id);
		}

		public TSQuaternion GetQuatFromBoneId(int id)
		{
			CallForUpdate(id);
			return Transformation.MatRotation(Skeleton.BoneOffsetMat(id));
		}

		IWithSkeleton parent = null;
		int parentBoneId = -1;
		FP scaleAsChild = 1;
		readonly HashSet<IWithSkeleton> children = new HashSet<IWithSkeleton>();

		public bool SetParent(IWithSkeleton parent, int boneId, float scaleOverride = 0.0f)
		{
			if (HasChild(parent))
				return false;
			if (boneId == -1)
				throw new Exception("Can't set parnet on a bone which id is -1");

			ReleaseFromParent();

			this.parent = parent;
			parentBoneId = boneId;
			scaleAsChild = scaleOverride == 0.0f ? Scale : scaleOverride;
			parent.AddChild(this);
			return true;
		}

		public void AddChild(IWithSkeleton child)
		{
			children.Add(child);
		}

		public void RemoveChild(IWithSkeleton child)
		{
			children.Remove(child);
		}

		public void ReleaseFromParent()
		{
			if (parent == null)
				return;

			parent.RemoveChild(this);
			parent = null;
			parentBoneId = -1;
			scaleAsChild = 1;
		}

		public bool HasChild(IWithSkeleton skeleton)
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

		public void CallForUpdate(int boneid)
		{
			if (OnlyUpdateForDraw)
				throw new Exception("This WithSkeleton " + Name + " is OnlyUpdateForDraw, Can't CallForUpdate by logic");

			if (parent != null)
			{
				parent.CallForUpdate(parentBoneId);
			}

			if (BlendTreeHandler != null)
			{
				Skeleton.UpdateBone(boneid, BlendTreeHandler.GetResult());
			}
			else
				Skeleton.UpdateBone(boneid);
		}

		public void FlushLogicPose()
		{
			Skeleton.FlushLogicOffset();
		}

		public void FlushRenderPose()
		{
			Skeleton.FlushRenderOffset();
		}

		public void UpdateSkeletonTick()
		{
			SkeletonTick();
			UpdateSkeletonRoot();
		}

		void UpdateSkeletonRoot()
		{
			if (parent == null)
			{
				if (Info.AxisConvert)
					Skeleton.SetOffset(lastSelfPos, lastSelfRot, lastScale);
				else
					Skeleton.SetOffsetNoConvert(lastSelfPos, lastSelfRot, lastScale);
			}
			else
				Skeleton.SetOffset(Transformation.MatWithNewScale(parent.GetMatrixFromBoneId(parentBoneId), scaleAsChild));
		}

		public void RenderUpdateWholeSkeleton(bool callbyParent)
		{
			if (!callbyParent && parent != null)
				return;

			RenderUpdateSkeletonInner();
		}

		/// <summary>
		/// notice that! the ik and some other trait which can update skeleton need to use the last tick anim params
		/// </summary>
		void RenderUpdateSkeletonInner()
		{
			if (OnlyUpdateForDraw && Draw)
			{
				RenderUpdateDirectly();
			}
			else
			{
				RenderUpdateDirectly();
			}

			foreach (var child in children)
				child.RenderUpdateWholeSkeleton(true);
		}

		void RenderUpdateDirectly()
		{
			if (BlendTreeHandler != null)
			{
				Skeleton.UpdateRenderOffset(BlendTreeHandler.GetResult());
			}
			else
				Skeleton.UpdateRenderOffset();
		}

		public void UpdateDrawInfo(bool callbyParent)
		{
			if (!callbyParent && parent != null)
				return;

			UpdateDrawInfoInner(Draw);
		}

		void UpdateDrawInfoInner(bool callbyParent)
		{
			OrderedSkeleton.AddInstance(Skeleton);
			Skeleton.ProcessManagerData();

			foreach (var child in children)
				child.UpdateDrawInfo(true);
		}
	}
}
