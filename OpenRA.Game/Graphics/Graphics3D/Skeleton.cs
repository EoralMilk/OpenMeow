using System;
using System.Collections.Generic;
using System.IO;
using GlmSharp;
using OpenRA.FileSystem;
using OpenRA.Primitives;
using OpenRA.Traits;
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
		public BoneAsset(string name, int id, int skinId, int animId, string parentName, int parentId, TSMatrix4x4 restPose, TSMatrix4x4 restPoseInv, mat4 bindPose, bool adjust)
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
			IsAdjBone = adjust;
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

		public Dictionary<ModifiedBoneRestPose, FP> modifiers = new Dictionary<ModifiedBoneRestPose, FP>();

		public TSMatrix4x4 RestPose { get; private set; }
		public TSMatrix4x4 CurrentPose;
		public mat4 RenderRestPose { get; private set; }
		public mat4 RenderCurrentPose;
		public mat4 BindPose { get; private set; }
		public bool OverridePose;
		public bool NeedUpdateWhenRender = false;

		public BoneInstance(in BoneAsset asset, SkeletonInstance skeletonInstance)
		{
			Skeleton = skeletonInstance;
			Id = asset.Id;
			AnimId = asset.AnimId;
			ParentId = asset.ParentId;

			RestPose = asset.RestPose;
			RenderRestPose = RestPose.ToMat4();
			if (!Skeleton.OrderedSkeleton.UseDynamicAdjBonePose && Skeleton.SkeletonAsset.Bones[Id].IsAdjBone && Skeleton.SkeletonAsset.Bones[Id].SkinId > -1)
			{
				BindPose = InitApplyRestPose(Id) * asset.BindPose;
			}
			else
			{
				BindPose = asset.BindPose;
			}

			BaseRestPoseInv = asset.RestPoseInv;
			CurrentPose = RestPose;
			RenderCurrentPose = RenderRestPose;
			RenderBaseRestPoseInv = BaseRestPoseInv.ToMat4();

			OverridePose = false;
			ModifiedRest = false;
		}

		public void SetRestPose(TSMatrix4x4 pos)
		{
			RestPose = pos;
			RenderRestPose = RestPose.ToMat4();
			if (!Skeleton.OrderedSkeleton.UseDynamicAdjBonePose && Skeleton.SkeletonAsset.Bones[Id].IsAdjBone && Skeleton.SkeletonAsset.Bones[Id].SkinId > -1)
			{
				BindPose = ApplyRestPose(Id) * Skeleton.SkeletonAsset.Bones[Id].BindPose;
			}

			ModifiedRest = true;
		}

		public TSMatrix4x4 ApplyModifier(ModifiedBoneRestPose modifiedBoneRestPose, FP t)
		{
			if (modifiers.ContainsKey(modifiedBoneRestPose))
			{
				modifiers[modifiedBoneRestPose] = t;
			}
			else
			{
				modifiers.Add(modifiedBoneRestPose, t);
			}

			var mat = Skeleton.SkeletonAsset.Bones[Id].RestPose;
			foreach (var mbkv in modifiers)
			{
				mat = mat * Transformation.LerpMatrix(mbkv.Key.FirstTransform, mbkv.Key.LastTransform, mbkv.Value);
			}

			return mat;
		}

		/// <summary>
		/// use for init
		/// </summary>
		public mat4 InitApplyRestPose(int id)
		{
			if (Skeleton.SkeletonAsset.Bones[Skeleton.SkeletonAsset.Bones[id].ParentId].IsAdjBone)
				return InitApplyRestPose(Skeleton.SkeletonAsset.Bones[id].ParentId) * Skeleton.SkeletonAsset.Bones[id].RestPose.ToMat4();
			else
				return Skeleton.SkeletonAsset.Bones[id].RestPose.ToMat4();
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

	public class SkeletonRestPoseModifier
	{
		public readonly string Name;
		public readonly Dictionary<int, ModifiedBoneRestPose> BoneModifiers = new Dictionary<int, ModifiedBoneRestPose>();

		public SkeletonRestPoseModifier(string name)
		{
			Name = name;
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
		public readonly OrderedSkeleton OrderedSkeleton;

		readonly int boneSize;
		readonly bool[] renderUpdateFlags;
		readonly bool[] updateFlags;

		bool SkipUpdateAdjBonePose => !OrderedSkeleton.UseDynamicAdjBonePose;
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

		public void ApplySkeletonModifier(SkeletonRestPoseModifier m, FP t)
		{
			foreach (var mbkv in m.BoneModifiers)
			{
				Bones[mbkv.Key].SetRestPose(Bones[mbkv.Key].ApplyModifier(mbkv.Value, t));
			}
		}

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
			return World3DCoordinate.GetWRotFromBoneMatrix(Bones[id].CurrentPose);
		}

		public SkeletonInstance(in BoneAsset[] boneAssets, in SkeletonAsset asset, in OrderedSkeleton skeleton)
		{
			this.SkeletonAsset = asset;
			this.OrderedSkeleton = skeleton;
			boneSize = boneAssets.Length;
			renderUpdateFlags = new bool[boneSize];
			updateFlags = new bool[boneSize];

			Bones = new BoneInstance[boneSize];
			for (int i = 0; i < boneSize; i++)
			{
				Bones[i] = new BoneInstance(boneAssets[i], this);
			}

			//foreach (var mb in skeleton.ModifiedBoneRestPoses)
			//{
			//	Bones[mb.Value.Id].ModifiedRest = true;
			//	Bones[mb.Value.Id].SetRestPose(mb.Value.RestPose);
			//}

			FlushRenderOffset();
			FlushLogicOffset();
			AnimTexoffset = -1;
		}

		public bool HasUpdateBone(int id)
		{
			return updateFlags[id];
		}

		public void UpdateBone(int id, IBlendTreeHandler blendTreeHandler)
		{
			if (updateFlags[id])
				return;
			UpdateBoneInner(id, blendTreeHandler);
		}

		public void UpdateBone(int id)
		{
			if (updateFlags[id])
				return;
			UpdateBoneInner(id, null);
		}

		void UpdateBoneInner(int id, IBlendTreeHandler blendTreeHandler)
		{
			if (updateFlags[id] == true)
				return;
			var br = Bones[id].AnimId == -1 ? null : blendTreeHandler?.GetOneAnimTrans(Bones[id].AnimId);
			bool hasAnim = br != null && br.Value.AnimMask[Bones[id].AnimId];

			if (Bones[id].ParentId == -1)
			{
				// animMask length should be same as frame length (&& animMask.Length > Bones[id].AnimId) no need
				if (hasAnim)
					Bones[id].UpdateOffset(Offset, br.Value.Trans.Matrix);
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
				UpdateBoneInner(Bones[id].ParentId, blendTreeHandler);

				// animMask length should be same as frame length
				if (hasAnim)
					Bones[id].UpdateOffset(Bones[Bones[id].ParentId].CurrentPose, br.Value.Trans.Matrix);
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
				if (!Bones[i].NeedUpdateWhenRender && (SkeletonAsset.Bones[i].SkinId < 0 || (SkipUpdateAdjBonePose && SkeletonAsset.Bones[i].IsAdjBone)))
					continue;
				RenderUpdateBoneInner(i, animFrame == null ? EmptyFrame : animFrame, animMask == null ? SkeletonAsset.AllValidMask : animMask);
			}

			hasUpdatedAll = true;
		}

		public void UpdateRenderOffset(in BlendTreeNodeOutPut treeNodeOutPut)
		{
			UpdateRenderOffset(treeNodeOutPut.OutPutFrame, treeNodeOutPut.AnimMask);
		}

		public void TempUpdateRenderSingle(int boneId)
		{
			RenderUpdateBoneInner(boneId, EmptyFrame, SkeletonAsset.AllValidMask);
		}

		public void ProcessManagerData()
		{
			if (InstanceID == -1 || !hasUpdatedAll)
				return;
			int offset = 12;
			if (SkipUpdateAdjBonePose)
				offset = 24;
			int dataWidth = SkeletonAsset.SkinBonesIndices.Length * offset;
			int start = OrderedSkeleton.AnimTransformDataIndex;
			if ((start + dataWidth) >= OrderedSkeleton.AnimTransformData.Length)
				throw new Exception("ProcessManagerData: Skeleton Instance drawId out of range: might be too many skeleton to draw!");
			OrderedSkeleton.AnimTransformDataIndex = start + dataWidth;
			AnimTexoffset = start;
			int i = 0;

			for (int x = 0; x < dataWidth; x += offset)
			{
				int id = SkipUpdateAdjBonePose ? SkeletonAsset.SkinBonesMatchIndices[i] : SkeletonAsset.SkinBonesIndices[i];
				var mat4 = Bones[id].RenderCurrentPose;
				var c0 = mat4.Column0;
				var c1 = mat4.Column1;
				var c2 = mat4.Column2;
				var c3 = mat4.Column3;

				// the last row always be 0,0,0,1
				// so we only need to save 12 float
				// save as row vector
				OrderedSkeleton.AnimTransformData[start + x + 0] = c0.x;
				OrderedSkeleton.AnimTransformData[start + x + 1] = c1.x; // c0.y;
				OrderedSkeleton.AnimTransformData[start + x + 2] = c2.x; // c0.z;
				OrderedSkeleton.AnimTransformData[start + x + 3] = c3.x; // c0.w;
				OrderedSkeleton.AnimTransformData[start + x + 4] = c0.y; // c1.x;
				OrderedSkeleton.AnimTransformData[start + x + 5] = c1.y; // c1.y;
				OrderedSkeleton.AnimTransformData[start + x + 6] = c2.y; // c1.z;
				OrderedSkeleton.AnimTransformData[start + x + 7] = c3.y; // c1.w;
				OrderedSkeleton.AnimTransformData[start + x + 8] = c0.z; // c2.x;
				OrderedSkeleton.AnimTransformData[start + x + 9] = c1.z; // c2.y;
				OrderedSkeleton.AnimTransformData[start + x + 10] = c2.z; // c2.z;
				OrderedSkeleton.AnimTransformData[start + x + 11] = c3.z; // c2.w;

				if (SkipUpdateAdjBonePose)
				{
					mat4 = Bones[SkeletonAsset.SkinBonesIndices[i]].BindPose;
					c0 = mat4.Column0;
					c1 = mat4.Column1;
					c2 = mat4.Column2;
					c3 = mat4.Column3;

					OrderedSkeleton.AnimTransformData[start + x + 12 + 0] = c0.x;
					OrderedSkeleton.AnimTransformData[start + x + 12 + 1] = c1.x; // c0.y;
					OrderedSkeleton.AnimTransformData[start + x + 12 + 2] = c2.x; // c0.z;
					OrderedSkeleton.AnimTransformData[start + x + 12 + 3] = c3.x; // c0.w;
					OrderedSkeleton.AnimTransformData[start + x + 12 + 4] = c0.y; // c1.x;
					OrderedSkeleton.AnimTransformData[start + x + 12 + 5] = c1.y; // c1.y;
					OrderedSkeleton.AnimTransformData[start + x + 12 + 6] = c2.y; // c1.z;
					OrderedSkeleton.AnimTransformData[start + x + 12 + 7] = c3.y; // c1.w;
					OrderedSkeleton.AnimTransformData[start + x + 12 + 8] = c0.z; // c2.x;
					OrderedSkeleton.AnimTransformData[start + x + 12 + 9] = c1.z; // c2.y;
					OrderedSkeleton.AnimTransformData[start + x + 12 + 10] = c2.z; // c2.z;
					OrderedSkeleton.AnimTransformData[start + x + 12 + 11] = c3.z; // c2.w;
				}

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
		public readonly Dictionary<string, SkeletonRestPoseModifier> SkeletonRestPoseModifiers = new Dictionary<string, SkeletonRestPoseModifier>();
		public readonly Dictionary<int, List<SkeletonRestPoseModifier>> BoneModifiedBy = new Dictionary<int, List<SkeletonRestPoseModifier>>();
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
				//var bonesInfo = info["BoneRests"].ToDictionary();
				//foreach (var boneDefine in bonesInfo)
				//{
				//	if (asset.BonesDict.ContainsKey(boneDefine.Key))
				//	{
				//		var boneInfo = boneDefine.Value.ToDictionary();
				//		var firstIsRest = ReadYamlInfo.LoadField(boneInfo, "FirstPoseAsRestPose", false);
				//		var restPose = asset.BonesDict[boneDefine.Key].RestPose * ReadYamlInfo.LoadTransformation(boneInfo, "RestPoseModify").Matrix;
				//		var lastPose = asset.BonesDict[boneDefine.Key].RestPose * ReadYamlInfo.LoadTransformation(boneInfo, "LastPose").Matrix;
				//		var firstPose = asset.BonesDict[boneDefine.Key].RestPose * ReadYamlInfo.LoadTransformation(boneInfo, "FirstPose").Matrix;
				//		var mb = new ModifiedBoneRestPose(asset.BonesDict[boneDefine.Key], firstIsRest ? firstPose : restPose, firstPose, lastPose);
				//		ModifiedBoneRestPoses.Add(asset.BonesDict[boneDefine.Key].Id, mb);
				//	}
				//	else
				//	{
				//		throw new InvalidDataException("Skeleton " + asset.Name + " has no bone: " + boneDefine.Key);
				//	}
				//}
			}

			if (info.ContainsKey("BoneModifiers"))
			{
				var bonesInfo = info["BoneModifiers"].ToDictionary();
				foreach (var kv in bonesInfo)
				{
					SkeletonRestPoseModifier modifier = new SkeletonRestPoseModifier(kv.Key);

					var modifierInfo = kv.Value.ToDictionary();

					foreach (var boneM in modifierInfo)
					{
						if (asset.BonesDict.ContainsKey(boneM.Key))
						{
							if (BoneModifiedBy.ContainsKey(asset.BonesDict[boneM.Key].Id))
							{
								if (BoneModifiedBy[asset.BonesDict[boneM.Key].Id] == null)
									BoneModifiedBy[asset.BonesDict[boneM.Key].Id] = new List<SkeletonRestPoseModifier>();
								BoneModifiedBy[asset.BonesDict[boneM.Key].Id].Add(modifier);
							}
							else
							{
								BoneModifiedBy.Add(asset.BonesDict[boneM.Key].Id, new List<SkeletonRestPoseModifier>());
								BoneModifiedBy[asset.BonesDict[boneM.Key].Id].Add(modifier);
							}

							var boneInfo = boneM.Value.ToDictionary();
							var firstIsRest = ReadYamlInfo.LoadField(boneInfo, "FirstPoseAsRestPose", false);
							var restPose = ReadYamlInfo.LoadTransformation(boneInfo, "RestPoseModify").Matrix;
							var lastPose = ReadYamlInfo.LoadTransformation(boneInfo, "LastPose").Matrix;
							var firstPose = ReadYamlInfo.LoadTransformation(boneInfo, "FirstPose").Matrix;
							var mb = new ModifiedBoneRestPose(asset.BonesDict[boneM.Key], firstIsRest ? firstPose : restPose, firstPose, lastPose);
							modifier.BoneModifiers.Add(asset.BonesDict[boneM.Key].Id, mb);
						}
						else
						{
							throw new InvalidDataException("Skeleton " + asset.Name + " has no bone: " + boneM.Key);
						}
					}

					SkeletonRestPoseModifiers.Add(modifier.Name, modifier);
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
				// if (SkeletonAsset.Bones[id].IsAdjBone && !UseDynamicAdjBonePose)
				// {
				//	bindPose = ApplyRestPose(SkeletonAsset.Bones[id].Id) * SkeletonAsset.Bones[id].BindPose;
				// }

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

			AllValidMask = new AnimMask("all", BoneNameAnimIndex.Count);
		}

		/// <summary>
		/// we should find adjust bone's AdjParent,
		/// the parent bone will be the RenderPose provider and the adjust bone's restpose and restpose changing
		/// will store in the bindpose. gpu can handle it
		/// </summary>
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

				// Attention, the Adj bone is a type of special bone which won't apply any animation, and won't uppdate logic offset;
				// the "BindPose" of adj bone whilc contains restPose relative to the AdjParent bone and self bindPose
				// cpu pak the bindPose and AdjParentPose to gpu, gpu while calculate the final bindOffset when process skin
				var adjust = ReadYamlInfo.LoadField(info, "Adjust", false);

				var parentName = ReadYamlInfo.LoadField(info, "Parent", "NO_Parent");
				var parentID = ReadYamlInfo.LoadField(info, "ParentID", -1);
				var restPose = ReadYamlInfo.LoadTransformation(info, "RestPose").Matrix;
				var restPoseInv = ReadYamlInfo.LoadTransformation(info, "RestPoseInv").Matrix;
				mat4 bindPose = ReadYamlInfo.LoadMat4(info, "BindPose");
				BoneAsset bone = new BoneAsset(name, id, (skin ? 1 : -1), (anim ? 1 : -1), parentName, parentID, restPose, restPoseInv, bindPose, adjust);
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
