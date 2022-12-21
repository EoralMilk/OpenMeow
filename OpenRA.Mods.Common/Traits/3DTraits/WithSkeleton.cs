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
using System.Linq;
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
		BlendTreeNodeOutPutOne GetOneAnimTrans(int animId);

		void UpdateTick();

		WRot FacingOverride();
	}

	public class WithSkeletonInfo : ConditionalTraitInfo, Requires<RenderMeshesInfo>
	{
		public readonly string SkeletonDefine = null;
		public readonly string Name = "body";
		public readonly bool OnlyUpdateForDraw = false;
		public readonly bool AxisConvert = true;

		public readonly string ParentSkeleton = null;
		public readonly string AttachingParentBone = null;
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
		public FP GetScale()
		{
			return Scale;
		}

		readonly Actor self;
		readonly IFacing myFacing;
		public readonly bool OnlyUpdateForDraw;

		public IBlendTreeHandler BlendTreeHandler;

		public IWithSkeleton Parent { get; private set; }
		public Func<TSMatrix4x4> GetRootOffset = null;

		int parentBoneId = -1;
		FP scaleAsChild = 1;
		readonly HashSet<IWithSkeleton> children = new HashSet<IWithSkeleton>();

		/// <summary>
		/// Affects the root migration of the skeleton
		/// </summary>
		public WVec SkeletonOffset = WVec.Zero;

		public WithSkeleton(ActorInitializer init, WithSkeletonInfo info)
			: base(info)
		{
			self = init.Self;
			Name = info.Name;
			myFacing = self.TraitOrDefault<IFacing>();

			OnlyUpdateForDraw = info.OnlyUpdateForDraw;
			rm = self.Trait<RenderMeshes>();
			Scale = rm.Info.Scale;
			Image = info.SkeletonDefine == null ? rm.Image : info.SkeletonDefine;
			OrderedSkeleton = self.World.SkeletonCache.GetOrderedSkeleton(Image);
			if (OrderedSkeleton == null)
				throw new Exception("orderedSkeleton is null");

			Skeleton = OrderedSkeleton.CreateInstance();

			if (!string.IsNullOrEmpty(Info.ParentSkeleton))
			{
				var parent = self.TraitsImplementing<WithSkeleton>().Single(w => w.Info.Name == Info.ParentSkeleton);
				if (parent == null)
					throw new Exception(self.Info.Name + " Mesh Attachment Can not find main skeleton " + Info.ParentSkeleton);
				var attachBone = parent.GetBoneId(Info.AttachingParentBone);
				if (attachBone == -1)
					throw new Exception("can't find bone " + Info.AttachingParentBone + " in skeleton: " + Info.ParentSkeleton);

				SetParent(parent, attachBone, FP.Zero);
			}
		}

		protected override void Created(Actor self)
		{
			UpdateSkeletonTick(false);

			// init the current pose by rest pose and offset
			RenderUpdateWholeSkeleton(false);
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
			else if (myFacing != null)
				lastSelfRot = myFacing.Orientation;
			lastScale = Scale;

			foreach (var kv in bonePoseModifiers)
				kv.Value.UpdateTarget();

			updatedRoot = false;
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

		public bool SetParent(IWithSkeleton parent, int boneId, FP scaleOverride)
		{
			if (HasChild(parent))
				return false;
			if (boneId == -1)
				throw new Exception("Can't set parnet on a bone which id is -1");

			ReleaseFromParent();

			this.Parent = parent;
			parentBoneId = boneId;
			scaleAsChild = scaleOverride == FP.Zero ? Scale : scaleOverride;
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
			if (Parent == null)
				return;

			Parent.RemoveChild(this);
			Parent = null;
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
					ws.HasChild(Parent);
			}

			return false;
		}

		public void CallForUpdate(int boneid)
		{
			if (OnlyUpdateForDraw)
				throw new Exception("This WithSkeleton " + Name + " is OnlyUpdateForDraw, Can't CallForUpdate by logic");

			if (Parent != null)
			{
				Parent.CallForUpdate(parentBoneId);
			}

			UpdateSkeletonRoot();

			if (Skeleton.HasUpdateBone(boneid))
				return;

			if (BlendTreeHandler != null && OrderedSkeleton.SkeletonAsset.Bones[boneid].AnimId != -1)
			{
				Skeleton.UpdateBone(boneid, BlendTreeHandler.GetOneAnimTrans(OrderedSkeleton.SkeletonAsset.Bones[boneid].AnimId));
			}
			else
				Skeleton.UpdateBone(boneid);
		}

		public void FlushLogicPose(bool callByParent)
		{
			if (Parent != null && !callByParent)
				return;

			Skeleton.FlushLogicOffset();

			foreach (var child in children)
				child.FlushLogicPose(true);
		}

		public void FlushRenderPose(bool callByParent)
		{
			if (Parent != null && !callByParent)
				return;

			Skeleton.FlushRenderOffset();

			foreach (var child in children)
				child.FlushRenderPose(true);
		}

		public void UpdateSkeletonTick(bool callByParent)
		{
			if (Parent != null && !callByParent)
				return;

			SkeletonTick();

			foreach (var child in children)
				child.UpdateSkeletonTick(true);
		}

		bool updatedRoot = false;

		void UpdateSkeletonRoot()
		{
			if (updatedRoot)
				return;

			if (GetRootOffset != null)
			{
				Skeleton.SetOffset(GetRootOffset());
				return;
			}

			if (Parent == null)
			{
				if (Info.AxisConvert)
					Skeleton.SetOffset(lastSelfPos, lastSelfRot, lastScale);
				else
					Skeleton.SetOffsetNoConvert(lastSelfPos, lastSelfRot, lastScale);
			}
			else
			{
				var mat = Parent.GetMatrixFromBoneId(parentBoneId);
				Skeleton.SetOffset(Transformation.MatWithNewScale(mat, scaleAsChild / Parent.GetScale() * Transformation.MatScale(mat)));
			}

			updatedRoot = true;
		}

		void RenderUpdateSkeletonRoot()
		{
			if (updatedRoot)
				return;

			if (GetRootOffset != null)
			{
				Skeleton.SetOffset(GetRootOffset());
				return;
			}

			if (Parent == null)
			{
				if (Info.AxisConvert)
					Skeleton.SetOffset(lastSelfPos, lastSelfRot, lastScale);
				else
					Skeleton.SetOffsetNoConvert(lastSelfPos, lastSelfRot, lastScale);
			}
			else
			{
				var mat = TSMatrix4x4.FromMat4(Parent.GetRenderMatrixFromBoneId(parentBoneId));
				Skeleton.SetOffset(Transformation.MatWithNewScale(mat, scaleAsChild / Parent.GetScale() * Transformation.MatScale(mat)));
			}
		}

		public void RenderUpdateWholeSkeleton(bool callbyParent)
		{
			if (!callbyParent && Parent != null)
				return;

			RenderUpdateSkeletonRoot();

			RenderUpdateSkeletonInner();
		}

		/// <summary>
		/// notice that! the ik and some other trait which can update skeleton need to use the last tick anim params
		/// </summary>
		void RenderUpdateSkeletonInner()
		{
			RenderUpdateDirectly();

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
			if (!callbyParent && Parent != null)
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
