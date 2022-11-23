using System;
using System.Collections.Generic;
using System.IO;
using GlmSharp;
using OpenRA.FileSystem;
using OpenRA.Primitives;
using TrueSync;

namespace OpenRA.Graphics
{
	public class BoneAsset
	{
		public string Name;
		public int Id;
		public int SkinId;
		public int AnimId;
		public string ParentName;
		public int ParentId;
		public TSMatrix4x4 RestPose;
		public TSMatrix4x4 RestPoseInv;
		public mat4 BindPose;
		public bool IsAdjBone;
		public int AdjParentId;
		public BoneAsset(string name, int id, int skinId, int animId, string parentName, int parentId, TSMatrix4x4 restPose, TSMatrix4x4 restPoseInv, mat4 bindPose)
		{
			Name = name;
			Id = id;
			SkinId = skinId;
			AnimId = animId;
			ParentName = parentName;
			ParentId = parentId;
			RestPose = restPose;
			RestPoseInv = restPoseInv;
			BindPose = bindPose;
		}
	}

	public class BoneInstance
	{
		public readonly SkeletonInstance Skeleton;
		public readonly int Id;
		public readonly int AnimId;

		public readonly int ParentId;
		public bool ModifiedRest;

		public readonly TSMatrix4x4 BaseRestPoseInv;
		public readonly mat4 RenderBaseRestPoseInv;

		public TSMatrix4x4 RestPose { get; private set; }
		public TSMatrix4x4 CurrentPose;
		public mat4 RenderRestPose { get; private set; }
		public mat4 RenderCurrentPose;
		public bool OverridePose;
		public bool NeedUpdateWhenRender = false;

		public BoneInstance(in BoneAsset asset, SkeletonInstance skeletonInstance)
		{
			Skeleton = skeletonInstance;
			Id = asset.Id;
			AnimId = asset.AnimId;
			ParentId = asset.ParentId;
			RestPose = asset.RestPose;
			BaseRestPoseInv = asset.RestPoseInv;
			CurrentPose = RestPose;
			RenderRestPose = RestPose.ToMat4();
			RenderCurrentPose = RenderRestPose;
			RenderBaseRestPoseInv = BaseRestPoseInv.ToMat4();
			OverridePose = false;
			ModifiedRest = false;
		}

		public void SetRestPose(TSMatrix4x4 pos)
		{
			RestPose = pos;
			RenderRestPose = RestPose.ToMat4();
			if (!Skeleton.OrdererSkeleton.UseDynamicAdjBonePose && Skeleton.SkeletonAsset.Bones[Id].IsAdjBone)
			{
				mat4 bindPose = ApplyRestPose(Id) * Skeleton.SkeletonAsset.Bones[Id].BindPose;

				Skeleton.OrdererSkeleton.ChangeBoneBindTransform(Id, bindPose);
			}
		}

		public mat4 ApplyRestPose(int id)
		{
			if (Skeleton.SkeletonAsset.Bones[Skeleton.Bones[id].ParentId].IsAdjBone)
				return ApplyRestPose(Skeleton.Bones[id].ParentId) * Skeleton.Bones[id].RenderRestPose;
			else
				return Skeleton.Bones[id].RenderRestPose;
		}

		public void UpdateOffset(in TSMatrix4x4 parent)
		{
			if (OverridePose)
				return;

			CurrentPose = parent * RestPose;
		}

		public void UpdateOffset(in TSMatrix4x4 parent, in TSMatrix4x4 anim)
		{
			if (OverridePose)
				return;

			if (ModifiedRest)
				CurrentPose = parent * (anim * BaseRestPoseInv) * RestPose;
			else
				CurrentPose = parent * anim;
		}

		public void UpdateRenderOffset(in mat4 parent)
		{
			if (OverridePose)
			{
				return;
			}

			RenderCurrentPose = parent * RenderRestPose;
		}

		public void UpdateRenderOffset(in mat4 parent, in mat4 anim)
		{
			if (OverridePose)
			{
				return;
			}

			if (ModifiedRest)
				RenderCurrentPose = parent * (anim * RenderBaseRestPoseInv) * RenderRestPose;
			else
				RenderCurrentPose = parent * anim;
		}
	}

	public class ModifiedBoneRestPose
	{
		public readonly string Name;
		public readonly int Id;
		public readonly int AnimId;
		public readonly TSMatrix4x4 FirstPose;
		public readonly TSMatrix4x4 LastPose;
		public readonly Transformation FirstTransform;
		public readonly Transformation LastTransform;

		public readonly bool OnlyRestPose = false;
		public readonly TSMatrix4x4 RestPose;

		public ModifiedBoneRestPose(in BoneAsset asset, TSMatrix4x4 modifiedPose, TSMatrix4x4 firstPose, TSMatrix4x4 lastPos)
		{
			Name = asset.Name;
			Id = asset.Id;
			AnimId = asset.AnimId;
			RestPose = modifiedPose;
			FirstPose = firstPose;
			FirstTransform = new Transformation(firstPose);
			LastPose = lastPos;
			LastTransform = new Transformation(lastPos);
			if (LastPose == FirstPose)
				OnlyRestPose = true;
		}
	}

	public enum InverseKinematicState
	{
		Keeping,
		Resolving,
	}

	public interface IBonePoseModifier
	{
		InverseKinematicState IKState { get; }
		void InitIK(ref TSMatrix4x4 self);
		void CalculateIK(ref TSMatrix4x4 self, bool rendercal);
		void UpdateTarget();
	}

	public class SkeletonInstance
	{
		public readonly BoneInstance[] Bones;
		bool hasUpdatedAll = false;

		public readonly SkeletonAsset SkeletonAsset;
		public readonly OrderedSkeleton OrdererSkeleton;

		readonly int boneSize;
		readonly bool[] renderUpdateFlags;
		readonly bool[] updateFlags;

		readonly bool skipUpdateAdjBonePose;
		public TSMatrix4x4 Offset { get; private set; }
		public int InstanceID = -1;
		public int AnimTexoffset { get; private set; }

		TSVector offsetVec;
		FP offsetScale;
		TSQuaternion offsetRot;

		TSMatrix4x4 scaleMat;
		TSMatrix4x4 translateMat;
		TSMatrix4x4 rotMat;

		readonly Dictionary<int, IBonePoseModifier> inverseKinematics = new Dictionary<int, IBonePoseModifier>();
		IBonePoseModifier currentIK;
		public static Frame EmptyFrame = new Frame(0);
		public void AddInverseKinematic(int id ,in IBonePoseModifier ik)
		{
			inverseKinematics.Add(id, ik);
			ik.InitIK(ref Bones[id].CurrentPose);
		}

		public void SetOffset(WPos wPos, WRot wRot, FP scale)
		{
			offsetScale = scale;
			scaleMat = TSMatrix4x4.Scale(offsetScale);
			offsetVec = World3DCoordinate.WPosToTSVec3(wPos);
			translateMat = TSMatrix4x4.Translate(offsetVec);
			offsetRot = wRot.ToQuat();
			rotMat = TSMatrix4x4.Rotate(offsetRot);
			Offset = translateMat * (scaleMat * rotMat);
		}

		public void SetOffsetNoConvert(WPos wPos, WRot wRot, FP scale)
		{
			offsetScale = scale;
			scaleMat = TSMatrix4x4.Scale(offsetScale);
			offsetVec = World3DCoordinate.WPosToTSVec3(wPos);
			translateMat = TSMatrix4x4.Translate(offsetVec);
			offsetRot = wRot.ToQuatNoConvert();
			rotMat = TSMatrix4x4.Rotate(offsetRot);
			Offset = translateMat * (scaleMat * rotMat);
		}

		public void SetOffset(TSMatrix4x4 matrix)
		{
			Offset = matrix;
		}

		public TSMatrix4x4 BoneOffsetMat(int id)
		{
			return Bones[id].CurrentPose;
		}

		public mat4 BoneRenderOffsetMat(int id)
		{
			return Bones[id].RenderCurrentPose;
		}

		public void SetBoneOffsetMat(int id, TSMatrix4x4 mat, bool ignoreScale = false)
		{
			if (ignoreScale)
			{
				var scale = Transformation.MatScale(Bones[id].CurrentPose);
				mat = Transformation.MatWithNewScale(mat, scale);
			}

			Bones[id].CurrentPose = mat;
			Bones[id].RenderCurrentPose = Bones[id].CurrentPose.ToMat4();
			Bones[id].OverridePose = true;
		}

		/// <summary>
		/// don't directly use this if you dom't know about how the skeleton update
		/// </summary>
		public WPos BoneWPos(int id)
		{
			return World3DCoordinate.GetWPosFromMatrix(Bones[id].CurrentPose);
		}

		/// <summary>
		/// don't directly use this if you dom't know about how the skeleton update
		/// </summary>
		public WRot BoneWRot(int id)
		{
			return World3DCoordinate.GetWRotFromMatrix(Bones[id].CurrentPose);
		}

		public SkeletonInstance(in BoneAsset[] boneAssets, in SkeletonAsset asset, in OrderedSkeleton skeleton)
		{
			this.SkeletonAsset = asset;
			this.OrdererSkeleton = skeleton;
			skipUpdateAdjBonePose = !skeleton.UseDynamicAdjBonePose;
			boneSize = boneAssets.Length;
			renderUpdateFlags = new bool[boneSize];
			updateFlags = new bool[boneSize];

			Bones = new BoneInstance[boneSize];
			for (int i = 0; i < boneSize; i++)
			{
				Bones[i] = new BoneInstance(boneAssets[i], this);
			}

			foreach (var mb in skeleton.ModifiedBoneRestPoses)
			{
				Bones[mb.Value.Id].ModifiedRest = true;
				Bones[mb.Value.Id].SetRestPose(mb.Value.RestPose);
			}

			FlushRenderOffset();
			FlushLogicOffset();
			AnimTexoffset = -1;
		}

		public void UpdateBone(int id, in BlendTreeNodeOutPut treeNodeOutPut)
		{
			if (updateFlags[id])
				return;
			UpdateBoneInner(id, treeNodeOutPut.OutPutFrame, treeNodeOutPut.AnimMask);
		}

		public void UpdateBone(int id)
		{
			if (updateFlags[id])
				return;
			UpdateBoneInner(id, EmptyFrame, SkeletonAsset.AllValidMask);
		}

		public void UpdateAll()
		{
			// init update the current pose
			for (int i = 0; i < boneSize; i++)
			{
				if (skipUpdateAdjBonePose && SkeletonAsset.Bones[i].IsAdjBone)
					continue;
				UpdateBoneInner(i, EmptyFrame, SkeletonAsset.AllValidMask);
			}
		}

		void UpdateBoneInner(int id, in Frame animFrame, in AnimMask animMask)
		{
			if (updateFlags[id] == true)
				return;

			if (Bones[id].ParentId == -1)
			{
				// animMask length should be same as frame length (&& animMask.Length > Bones[id].AnimId) no need
				if (Bones[id].AnimId != -1 && animFrame.Length > Bones[id].AnimId && animMask[Bones[id].AnimId])
					Bones[id].UpdateOffset(Offset, animFrame[Bones[id].AnimId].Matrix);
				else
					Bones[id].UpdateOffset(Offset);

				if (inverseKinematics.TryGetValue(id, out currentIK))
				{
					currentIK.CalculateIK(ref Bones[id].CurrentPose, false);
				}

				updateFlags[id] = true;
			}
			else
			{
				UpdateBoneInner(Bones[id].ParentId, animFrame, animMask);

				// animMask length should be same as frame length
				if (Bones[id].AnimId != -1 && animFrame.Length > Bones[id].AnimId && animFrame.HasTransformation[Bones[id].AnimId] && animMask[Bones[id].AnimId])
					Bones[id].UpdateOffset(Bones[Bones[id].ParentId].CurrentPose, animFrame[Bones[id].AnimId].Matrix);
				else
					Bones[id].UpdateOffset(Bones[Bones[id].ParentId].CurrentPose);

				if (inverseKinematics.TryGetValue(id, out currentIK))
				{
					currentIK.CalculateIK(ref Bones[id].CurrentPose, false);
				}

				updateFlags[id] = true;
			}
		}

		public void FlushLogicOffset()
		{
			for (int i = 0; i < boneSize; i++)
			{
				updateFlags[i] = false;
				Bones[i].OverridePose = false;
			}
		}

		public void FlushRenderOffset()
		{
			for (int i = 0; i < boneSize; i++)
			{
				renderUpdateFlags[i] = false;
			}

			hasUpdatedAll = false;
		}

		void RenderUpdateBoneInner(int id, in Frame animFrame, in AnimMask animMask)
		{
			if (renderUpdateFlags[id] == true)
				return;

			if (Bones[id].ParentId == -1)
			{
				// animMask length should be same as frame length (&& animMask.Length > Bones[id].AnimId) no need
				if (Bones[id].AnimId != -1 && animFrame.Length > Bones[id].AnimId && animMask[Bones[id].AnimId])
					Bones[id].UpdateRenderOffset(Offset.ToMat4(), animFrame[Bones[id].AnimId].Matrix.ToMat4());
				else
					Bones[id].UpdateRenderOffset(Offset.ToMat4());

				if (inverseKinematics.TryGetValue(id, out currentIK))
				{
					var m = TSMatrix4x4.FromMat4(Bones[id].RenderCurrentPose);
					currentIK.CalculateIK(ref m, true);
					Bones[id].RenderCurrentPose = m.ToMat4();
				}

				renderUpdateFlags[id] = true;
			}
			else
			{
				RenderUpdateBoneInner(Bones[id].ParentId, animFrame, animMask);

				// animMask length should be same as frame length
				if (Bones[id].AnimId != -1 && animFrame.Length > Bones[id].AnimId && animFrame.HasTransformation[Bones[id].AnimId] && animMask[Bones[id].AnimId])
					Bones[id].UpdateRenderOffset(Bones[Bones[id].ParentId].RenderCurrentPose, animFrame[Bones[id].AnimId].Matrix.ToMat4());
				else
					Bones[id].UpdateRenderOffset(Bones[Bones[id].ParentId].RenderCurrentPose);

				if (inverseKinematics.TryGetValue(id, out currentIK))
				{
					var m = TSMatrix4x4.FromMat4(Bones[id].RenderCurrentPose);
					currentIK.CalculateIK(ref m, true);
					Bones[id].RenderCurrentPose = m.ToMat4();
				}

				renderUpdateFlags[id] = true;
			}
		}

		public void UpdateRenderOffset(in Frame animFrame = null, in AnimMask animMask = null)
		{
			for (int i = 0; i < boneSize; i++)
			{
				if (!Bones[i].NeedUpdateWhenRender && (SkeletonAsset.Bones[i].SkinId < 0 || (skipUpdateAdjBonePose && SkeletonAsset.Bones[i].IsAdjBone)))
					continue;
				RenderUpdateBoneInner(i, animFrame == null ? EmptyFrame : animFrame, animMask == null ? SkeletonAsset.AllValidMask : animMask);
			}

			hasUpdatedAll = true;
		}

		public void UpdateRenderOffset(in BlendTreeNodeOutPut treeNodeOutPut)
		{
			UpdateRenderOffset(treeNodeOutPut.OutPutFrame, treeNodeOutPut.AnimMask);
		}

		public void ProcessManagerData()
		{
			if (InstanceID == -1 || !hasUpdatedAll)
				return;
			int dataWidth = SkeletonAsset.SkinBonesIndices.Length * 16;
			int start = OrderedSkeleton.AnimTransformDataIndex;
			if ((start + dataWidth) >= OrderedSkeleton.AnimTransformData.Length)
				throw new Exception("ProcessManagerData: Skeleton Instance drawId out of range: might be too many skeleton to draw!");
			OrderedSkeleton.AnimTransformDataIndex = start + dataWidth;
			AnimTexoffset = start;
			int i = 0;

			for (int x = 0; x < dataWidth; x += 16)
			{
				int id = SkeletonAsset.SkinBonesIndices[i];
				if (skipUpdateAdjBonePose)
					id = SkeletonAsset.SkinBonesMatchIndices[i];
				var mat4 = Bones[id].RenderCurrentPose;
				var c0 = mat4.Column0;
				var c1 = mat4.Column1;
				var c2 = mat4.Column2;
				var c3 = mat4.Column3;
				OrderedSkeleton.AnimTransformData[start + x + 0] = c0.x;
				OrderedSkeleton.AnimTransformData[start + x + 1] = c0.y;
				OrderedSkeleton.AnimTransformData[start + x + 2] = c0.z;
				OrderedSkeleton.AnimTransformData[start + x + 3] = c0.w;
				OrderedSkeleton.AnimTransformData[start + x + 4] = c1.x;
				OrderedSkeleton.AnimTransformData[start + x + 5] = c1.y;
				OrderedSkeleton.AnimTransformData[start + x + 6] = c1.z;
				OrderedSkeleton.AnimTransformData[start + x + 7] = c1.w;
				OrderedSkeleton.AnimTransformData[start + x + 8] = c2.x;
				OrderedSkeleton.AnimTransformData[start + x + 9] = c2.y;
				OrderedSkeleton.AnimTransformData[start + x + 10] = c2.z;
				OrderedSkeleton.AnimTransformData[start + x + 11] = c2.w;
				OrderedSkeleton.AnimTransformData[start + x + 12] = c3.x;
				OrderedSkeleton.AnimTransformData[start + x + 13] = c3.y;
				OrderedSkeleton.AnimTransformData[start + x + 14] = c3.z;
				OrderedSkeleton.AnimTransformData[start + x + 15] = c3.w;
				i++;
				if (i >= SkeletonAsset.SkinBonesIndices.Length)
				{
					break;
				}
			}
		}

		public bool CanDraw()
		{
			return AnimTexoffset >= 0;
		}
	}

	public class OrderedSkeleton
	{
		public readonly string Name;
		public readonly Dictionary<int, ModifiedBoneRestPose> ModifiedBoneRestPoses = new Dictionary<int, ModifiedBoneRestPose>();

		/// <summary>
		/// update each tick, by actor skeleton trait.
		/// </summary>
		readonly List<SkeletonInstance> skeletonInstances = new List<SkeletonInstance>();
		public static float[] AnimTransformData = new float[SkeletonAsset.AnimTextureWidth * SkeletonAsset.AnimTextureHeight * 4];
		public static ITexture BoneAnimTexture;
		public static int AnimTransformDataIndex = 0;
		int instanceCount = 0;

		public readonly SkeletonAsset SkeletonAsset;

		// public Dictionary<string, PreBakedSkeletalAnim> PreBakedAnimations = new Dictionary<string, PreBakedSkeletalAnim>();
		public float[] BindTransformData = new float[SkeletonAsset.MaxSkinBones * 16];
		readonly SkeletonInstance preBakeInstance;
		public readonly bool UseDynamicAdjBonePose = false;

		public OrderedSkeleton(string name, in SkeletonAsset asset, in MiniYaml skeletonDefine)
		{
			Name = name;
			SkeletonAsset = asset;
			// modify the skeleton rest pose, creating the skeleton that has been modified
			// such as a taller skeleton, or a fatter skeleton
			// the info for modify skeleton read from yaml defination
			var info = skeletonDefine.ToDictionary();
			if (info.ContainsKey("Bones"))
			{
				var bonesInfo = info["Bones"].ToDictionary();
				foreach (var boneDefine in bonesInfo)
				{
					if (asset.BonesDict.ContainsKey(boneDefine.Key))
					{
						var boneInfo = boneDefine.Value.ToDictionary();
						var firstIsRest = ReadYamlInfo.LoadField(boneInfo, "FirstPoseAsRestPose", false);
						var restPose = asset.BonesDict[boneDefine.Key].RestPose * ReadYamlInfo.LoadTransformation(boneInfo, "RestPoseModify").Matrix;
						var lastPose = asset.BonesDict[boneDefine.Key].RestPose * ReadYamlInfo.LoadTransformation(boneInfo, "LastPose").Matrix;
						var firstPose = asset.BonesDict[boneDefine.Key].RestPose * ReadYamlInfo.LoadTransformation(boneInfo, "FirstPose").Matrix;
						var mb = new ModifiedBoneRestPose(asset.BonesDict[boneDefine.Key], firstIsRest ? firstPose : restPose, firstPose, lastPose);
						ModifiedBoneRestPoses.Add(asset.BonesDict[boneDefine.Key].Id, mb);
					}
					else
					{
						throw new InvalidDataException("Skeleton " + asset.Name + " has no bone: " + boneDefine.Key);
					}
				}
			}

			UseDynamicAdjBonePose = ReadYamlInfo.LoadField(info, "UseDynamicAdjBonePose", false);
			preBakeInstance = new SkeletonInstance(asset.Bones, asset, this);

			foreach (var newPose in ModifiedBoneRestPoses)
			{
				preBakeInstance.Bones[newPose.Value.Id].SetRestPose(newPose.Value.RestPose);
			}

			//foreach (var animkv in asset.Animations)
			//{
			//	var animName = animkv.Key;
			//	PreBakedSkeletalAnim bakedAnim = new PreBakedSkeletalAnim(animkv.Value.Frames.Length);

			//	for (int f = 0; f < animkv.Value.Frames.Length; f++)
			//	{
			//		preBakeInstance.UpdateOffset(animkv.Value.Frames[f]);
			//		bakedAnim.Frames[f] = new FrameBaked(preBakeInstance.Bones.Length);
			//		for (int i = 0; i < preBakeInstance.Bones.Length; i++)
			//		{
			//			bakedAnim.Frames[f].Trans[i] = preBakeInstance.Bones[i].CurrentPose;
			//		}
			//	}

			//	PreBakedAnimations.Add(animName, bakedAnim);
			//}

			//if (BoneAnimTexture == null)
			//	BoneAnimTexture = Game.Renderer.CreateInfoTexture(new Primitives.Size(SkeletonAsset.AnimTextureWidth, SkeletonAsset.AnimTextureHeight));

			foreach (var bone in SkeletonAsset.Bones)
			{
				UpdateBoneBindTransform(bone.Id);
			}
		}

		void UpdateBoneBindTransform(int id)
		{
			if (SkeletonAsset.Bones[id].SkinId != -1)
			{
				mat4 bindPose = SkeletonAsset.Bones[id].BindPose;

				// notice: Adj bone's bindPose can be (RestPose * BindPose),
				// if the adj Bone is a child of another adj bone, the bindPose should apply the parentBone's restpose, until the parent is not adj bone.
				if (SkeletonAsset.Bones[id].IsAdjBone && !UseDynamicAdjBonePose)
				{
					bindPose = ApplyRestPose(SkeletonAsset.Bones[id].Id) * SkeletonAsset.Bones[id].BindPose;
				}

				ChangeBoneBindTransform(id, bindPose);
			}
		}

		public void ChangeBoneBindTransform(int id, in mat4 bindPose)
		{
			if (SkeletonAsset.Bones[id].SkinId < 0)
				return;

			var y = SkeletonAsset.Bones[id].SkinId * 16;

			BindTransformData[y + 0] = bindPose.Column0[0];
			BindTransformData[y + 1] = bindPose.Column0[1];
			BindTransformData[y + 2] = bindPose.Column0[2];
			BindTransformData[y + 3] = bindPose.Column0[3];
			BindTransformData[y + 4] = bindPose.Column1[0];
			BindTransformData[y + 5] = bindPose.Column1[1];
			BindTransformData[y + 6] = bindPose.Column1[2];
			BindTransformData[y + 7] = bindPose.Column1[3];
			BindTransformData[y + 8] = bindPose.Column2[0];
			BindTransformData[y + 9] = bindPose.Column2[1];
			BindTransformData[y + 10] = bindPose.Column2[2];
			BindTransformData[y + 11] = bindPose.Column2[3];
			BindTransformData[y + 12] = bindPose.Column3[0];
			BindTransformData[y + 13] = bindPose.Column3[1];
			BindTransformData[y + 14] = bindPose.Column3[2];
			BindTransformData[y + 15] = bindPose.Column3[3];
		}

		public mat4 ApplyRestPose(int id)
		{
			if (SkeletonAsset.Bones[SkeletonAsset.Bones[id].ParentId].IsAdjBone)
				return ApplyRestPose(SkeletonAsset.Bones[id].ParentId) * TSMatrix4x4ToMat4(preBakeInstance.Bones[id].RestPose);
			else
				return TSMatrix4x4ToMat4(preBakeInstance.Bones[id].RestPose);
		}

		mat4 TSMatrix4x4ToMat4(TSMatrix4x4 tSMatrix4X4)
		{
			mat4 mat = mat4.Identity;
			var c0 = tSMatrix4X4.Column0;
			var c1 = tSMatrix4X4.Column1;
			var c2 = tSMatrix4X4.Column2;
			var c3 = tSMatrix4X4.Column3;
			mat[0] = (float)c0.x;
			mat[1] = (float)c0.y;
			mat[2] = (float)c0.z;
			mat[3] = (float)c0.w;
			mat[4] = (float)c1.x;
			mat[5] = (float)c1.y;
			mat[6] = (float)c1.z;
			mat[7] = (float)c1.w;
			mat[8] = (float)c2.x;
			mat[9] = (float)c2.y;
			mat[10] = (float)c2.z;
			mat[11] = (float)c2.w;
			mat[12] = (float)c3.x;
			mat[13] = (float)c3.y;
			mat[14] = (float)c3.z;
			mat[15] = (float)c3.w;
			return mat;
		}

		public void UpdateAnimTextureData()
		{
			instanceCount = 0;
			skeletonInstances.Clear();
		}

		public SkeletonInstance CreateInstance()
		{
			SkeletonInstance skeletonInstance = new SkeletonInstance(SkeletonAsset.Bones, SkeletonAsset, this);

			foreach (var newPose in ModifiedBoneRestPoses)
			{
				skeletonInstance.Bones[newPose.Value.Id].SetRestPose(newPose.Value.RestPose);
			}

			return skeletonInstance;
		}

		public void AddInstance(in SkeletonInstance skeletonInstance)
		{
			skeletonInstances.Add(skeletonInstance);
			skeletonInstance.InstanceID = instanceCount;
			instanceCount++;
		}
	}

	/// <summary>
	/// using for create OrderedSkeleton
	/// </summary>
	public class SkeletonAsset
	{
		public const int AnimTextureWidth = 512;
		public const int AnimTextureHeight = 512;
		public const int MaxSkinBones = 128;

		public readonly string Name;
		readonly string rootBone;
		readonly int rootBoneId;
		public readonly BoneAsset[] Bones;
		public readonly Dictionary<string, BoneAsset> BonesDict;
		/// <summary>
		/// Key is Bone's Name, Value is Bone's AnimId
		/// </summary>
		public readonly Dictionary<string, int> BoneNameAnimIndex = new Dictionary<string, int>();
		public readonly Dictionary<string, int> BoneNameSkinIndex = new Dictionary<string, int>();

		/// <summary>
		/// the array order by skin bone's skinId, value is bone's common id
		/// </summary>
		public readonly int[] SkinBonesIndices;

		/// <summary>
		/// the array order by anim bone's animid, value is bone's common id
		/// </summary>
		public readonly int[] AnimBonesIndices;

		/// <summary>
		/// considering the adj bone, the adj bone's "boneOffsetTextureId" should be the AdjParentId
		/// </summary>
		public readonly int[] SkinBonesMatchIndices;

		public Dictionary<string, SkeletalAnim> Animations = new Dictionary<string, SkeletalAnim>();
		public Dictionary<string, AnimMask> Masks = new Dictionary<string, AnimMask>();

		// unit sequence to anims
		public Dictionary<string, Dictionary<string, SkeletalAnim>> AnimationsRef = new Dictionary<string, Dictionary<string, SkeletalAnim>>();

		// unit mask ref
		public Dictionary<string, Dictionary<string, AnimMask>> MasksRef = new Dictionary<string, Dictionary<string, AnimMask>>();

		public readonly AnimMask AllValidMask;
		public SkeletonAsset(IReadOnlyFileSystem fileSystem, string filename)
		{
			Name = filename;
			var name = filename;

			if (!fileSystem.Exists(name))
				name += ".skl";

			if (!fileSystem.Exists(name))
			{
				throw new Exception("SkeletonAsset:FromFile: can't find file " + name);
			}

			SkeletonReader reader = new SkeletonReader(fileSystem, filename);
			rootBone = reader.rootBone;
			BonesDict = new Dictionary<string, BoneAsset>(reader.bonesDict);
			rootBoneId = BonesDict[rootBone].Id;
			Bones = new BoneAsset[BonesDict.Count];
			foreach (var bone in BonesDict)
			{
				// Attention, the Adj bone is a type of special bone which won't apply any animation, and won't uppdate logic offset;
				// the "BindPose" of adj bone whilc contains restPose relative to the AdjParent bone and self bindPose
				// cpu pak the bindPose and AdjParentPose to gpu, gpu while calculate the final bindOffset when process skin
				if (bone.Value.Name.Contains("Adj_"))
				{
					bone.Value.IsAdjBone = true;
				}

				Bones[bone.Value.Id] = bone.Value;
			}

			foreach (var bone in Bones)
			{
				if (bone.IsAdjBone)
				{
					bone.AdjParentId = FindAdjParent(bone.Id);
				}
			}

			// reordered the skeleton anim and skin id
			int animid = 0;
			int skinid = 0;
			for (int i = 0; i < Bones.Length; i++)
			{
				if (Bones[i].AnimId != -1)
				{
					Bones[i].AnimId = animid;
					BonesDict[Bones[i].Name].AnimId = animid;
					BoneNameAnimIndex.Add(Bones[i].Name, Bones[i].AnimId);
					animid++;
				}

				if (Bones[i].SkinId != -1)
				{
					Bones[i].SkinId = skinid;
					BonesDict[Bones[i].Name].SkinId = skinid;
					BoneNameSkinIndex.Add(Bones[i].Name, Bones[i].SkinId);
					skinid++;
				}
			}

			List<BoneAsset> skinBones = new List<BoneAsset>();
			foreach (var bone in Bones)
			{
				if (bone.SkinId != -1)
				{
					skinBones.Add(bone);
					//var y = bone.SkinId * 16;
					//BindTransformData[y + 0] = bone.BindPose.Column0[0];
					//BindTransformData[y + 1] = bone.BindPose.Column0[1];
					//BindTransformData[y + 2] = bone.BindPose.Column0[2];
					//BindTransformData[y + 3] = bone.BindPose.Column0[3];
					//BindTransformData[y + 4] = bone.BindPose.Column1[0];
					//BindTransformData[y + 5] = bone.BindPose.Column1[1];
					//BindTransformData[y + 6] = bone.BindPose.Column1[2];
					//BindTransformData[y + 7] = bone.BindPose.Column1[3];
					//BindTransformData[y + 8] = bone.BindPose.Column2[0];
					//BindTransformData[y + 9] = bone.BindPose.Column2[1];
					//BindTransformData[y + 10] = bone.BindPose.Column2[2];
					//BindTransformData[y + 11] = bone.BindPose.Column2[3];
					//BindTransformData[y + 12] = bone.BindPose.Column3[0];
					//BindTransformData[y + 13] = bone.BindPose.Column3[1];
					//BindTransformData[y + 14] = bone.BindPose.Column3[2];
					//BindTransformData[y + 15] = bone.BindPose.Column3[3];
				}
			}

			SkinBonesIndices = new int[skinBones.Count];
			SkinBonesMatchIndices = new int[skinBones.Count];
			foreach (var bone in skinBones)
			{
				SkinBonesIndices[bone.SkinId] = bone.Id;
				if (bone.IsAdjBone)
				{
					SkinBonesMatchIndices[bone.SkinId] = bone.AdjParentId;
				}
				else
					SkinBonesMatchIndices[bone.SkinId] = bone.Id;
			}

			AnimBonesIndices = new int[BoneNameAnimIndex.Count];
			foreach (var b in Bones)
			{
				if (b.AnimId != -1)
					AnimBonesIndices[b.AnimId] = b.Id;
			}

			AllValidMask = new AnimMask("all", Bones.Length);
		}

		int FindAdjParent(int id)
		{
			if (Bones[id].ParentId != -1 && !Bones[Bones[id].ParentId].IsAdjBone)
				return Bones[id].ParentId;
			else if (Bones[id].ParentId != -1)
			{
				return FindAdjParent(Bones[id].ParentId);
			}
			else
				throw new Exception("Adj bone can't be the root bone");
		}

		public bool TryAddAnimation(in IReadOnlyFileSystem fileSystem, in string unit, in string sequence, in string filename)
		{
			var fields = (filename).Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
			var file = fields[0].Trim();

			if (Animations.ContainsKey(file))
			{
				if (!AnimationsRef.ContainsKey(unit))
				{
					AnimationsRef.Add(unit, new Dictionary<string, SkeletalAnim>());
				}

				AnimationsRef[unit].Add(sequence, Animations[file]);

				return false;
			}
			else
			{
				SkeletalAnim anim = new SkeletalAnim(fileSystem, sequence, file, this);
				Animations.Add(file, anim);

				if (!AnimationsRef.ContainsKey(unit))
				{
					AnimationsRef.Add(unit, new Dictionary<string, SkeletalAnim>());
				}

				AnimationsRef[unit].Add(sequence, Animations[file]);

				return true;
			}
		}

		public bool TryAddMask(in IReadOnlyFileSystem fileSystem, in string unit, in string sequence, in string filename)
		{
			var fields = (filename).Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
			var file = fields[0].Trim();

			if (Masks.ContainsKey(file))
			{
				if (!MasksRef.ContainsKey(unit))
				{
					MasksRef.Add(unit, new Dictionary<string, AnimMask>());
				}

				MasksRef[unit].Add(sequence, Masks[file]);

				return false;
			}
			else
			{
				var maskFile = file;
				if (!fileSystem.Exists(maskFile))
					maskFile += ".skm";

				if (!fileSystem.Exists(maskFile))
					throw new Exception("Can not find AnimMask");

				List<MiniYamlNode> nodes = MiniYaml.FromStream(fileSystem.Open(maskFile));
				bool[] maskValue = new bool[BoneNameAnimIndex.Count];
				for (int i = 0; i < maskValue.Length; i++)
					maskValue[i] = false;

				foreach (var node in nodes)
				{
					var info = node.Value.ToDictionary();
					var name = node.Key;
					bool animUse = ReadYamlInfo.LoadField(info, "Using", false);
					BoneAsset bone;
					if (BonesDict.TryGetValue(name, out bone))
					{
						// if the bone is not a anim bone, skip, which means this boneMask won't be used
						if (bone.AnimId == -1)
							continue;
						maskValue[bone.AnimId] = animUse;
					}
					else
					{
						throw new FileLoadException("The AnimMask: " + maskFile + " has boneMask: " + name + " which can't find in Skeleton: " + Name);
					}
				}

				AnimMask mask = new AnimMask(sequence, maskValue);
				Masks.Add(file, mask);

				if (!MasksRef.ContainsKey(unit))
				{
					MasksRef.Add(unit, new Dictionary<string, AnimMask>());
				}

				MasksRef[unit].Add(sequence, Masks[file]);

				return true;
			}
		}

		public SkeletalAnim GetSkeletalAnim(in string unit, in string sequence)
		{
			if (string.IsNullOrEmpty(unit) || string.IsNullOrEmpty(sequence))
			{
				if (string.IsNullOrEmpty(unit))
					throw new Exception("Unit (Or we call Image) is Null Or Empty");
				else
					throw new Exception("Sequence is Null Or Empty");
			}

			if (!AnimationsRef.ContainsKey(unit))
			{
				throw new Exception("Unit " + unit + " has no any sequence defined");
			}
			else if (!AnimationsRef[unit].ContainsKey(sequence))
			{
				throw new Exception("Unit " + unit + " has no anim for sequence " + sequence);
			}

			return AnimationsRef[unit][sequence];
		}

		public AnimMask GetAnimMask(in string unit, in string sequence)
		{
			if (!MasksRef.ContainsKey(unit))
			{
				throw new Exception("Unit " + unit + " has no any mask defined");
			}
			else if (!MasksRef[unit].ContainsKey(sequence))
			{
				throw new Exception("Unit " + unit + " has no mask for sequence " + sequence);
			}

			return MasksRef[unit][sequence];
		}

		public int GetBoneIdByName(string boneName)
		{
			if (BonesDict.ContainsKey(boneName))
			{
				return BonesDict[boneName].Id;
			}
			else
			{
				return -1;
			}
		}

		public int GetSkinBoneIdByName(string boneName)
		{
			if (BonesDict.ContainsKey(boneName))
			{
				return BonesDict[boneName].SkinId;
			}
			else
			{
				//Console.WriteLine("BonesDict not ContainsKey " + boneName);
				return -1;
			}
		}

		public int GetAnimBoneIdByName(string boneName)
		{
			if (BonesDict.ContainsKey(boneName))
			{
				return BonesDict[boneName].AnimId;
			}
			else
			{
				return -1;
			}
		}
	}

	class SkeletonReader
	{
		public readonly Dictionary<string, BoneAsset> bonesDict = new Dictionary<string, BoneAsset>();
		public string rootBone;

		public SkeletonReader(IReadOnlyFileSystem fileSystem, string filename)
		{
			if (!fileSystem.Exists(filename))
				filename += ".skl";

			if (!fileSystem.Exists(filename))
				throw new Exception("Can not find skeleton");

			List<MiniYamlNode> nodes = MiniYaml.FromStream(fileSystem.Open(filename));

			foreach (var node in nodes)
			{
				var info = node.Value.ToDictionary();
				var name = node.Key;
				var id = ReadYamlInfo.LoadField(info, "ID", -1);
				var skin = ReadYamlInfo.LoadField(info, "Skin", false);
				var anim = ReadYamlInfo.LoadField(info, "Anim", false);

				var parentName = ReadYamlInfo.LoadField(info, "Parent", "NO_Parent");
				var parentID = ReadYamlInfo.LoadField(info, "ParentID", -1);
				var restPose = ReadYamlInfo.LoadTransformation(info, "RestPose").Matrix;
				var restPoseInv = ReadYamlInfo.LoadTransformation(info, "RestPoseInv").Matrix;
				mat4 bindPose = ReadYamlInfo.LoadMat4(info, "BindPose");
				BoneAsset bone = new BoneAsset(name, id, (skin ? 1 : -1), (anim ? 1 : -1), parentName, parentID, restPose, restPoseInv, bindPose);
				bonesDict.Add(bone.Name, bone);
				if (bone.ParentId == -1 && parentName == "NO_Parent")
				{
					if (rootBone != null)
					{
						throw new Exception("SkeletonReader: root bone must be unique!");
					}

					rootBone = bone.Name;
				}
			}
		}
	}
}
