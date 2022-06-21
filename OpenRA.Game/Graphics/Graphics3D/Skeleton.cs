using System;
using System.Collections.Generic;
using System.IO;
using GlmSharp;
using OpenRA.FileSystem;
using TrueSync;

namespace OpenRA.Graphics
{
	public struct BoneAsset
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

	public struct BoneInstance
	{
		public readonly int Id;
		public readonly int AnimId;

		public readonly int ParentId;
		public readonly TSMatrix4x4 BaseRestPoseInv;

		public bool ModifiedRest;
		public TSMatrix4x4 RestPose;
		public TSMatrix4x4 CurrentPose;

		public BoneInstance(in BoneAsset asset)
		{
			Id = asset.Id;
			AnimId = asset.AnimId;
			ParentId = asset.ParentId;
			RestPose = asset.RestPose;
			BaseRestPoseInv = asset.RestPoseInv;
			ModifiedRest = false;
			CurrentPose = RestPose;
		}

		public void UpdateOffset(in TSMatrix4x4 parent)
		{
			CurrentPose = parent * RestPose;
		}

		public void UpdateOffset(in TSMatrix4x4 parent, in TSMatrix4x4 anim)
		{
			if (ModifiedRest)
				CurrentPose = parent * (anim * BaseRestPoseInv) * RestPose;
			else
				CurrentPose = parent * anim;
		}
	}

	public struct ModifiedBoneRestPose
	{
		public readonly string Name;
		public readonly int Id;
		public readonly int AnimId;
		public readonly TSMatrix4x4 RestPose;

		public ModifiedBoneRestPose(in BoneAsset asset, TSMatrix4x4 modifiedPose)
		{
			Name = asset.Name;
			Id = asset.Id;
			AnimId = asset.AnimId;
			RestPose = modifiedPose;
		}
	}

	public class SkeletonInstance
	{
		public readonly BoneInstance[] Bones;
		bool hasUpdated = false;
		bool hasUpdatedLast = false;
		public readonly TSMatrix4x4[] LastSkeletonPose;

		readonly SkeletonAsset asset;
		readonly OrderedSkeleton skeleton;

		readonly int boneSize;
		readonly bool[] updateFlags;

		TSMatrix4x4 offset;
		public int DrawID = -1;

		public void SetOffset(WPos wPos, WRot wRot, float scale)
		{
			var scaleMat = TSMatrix4x4.Scale(FP.FromFloat(scale));
			var offsetVec = Game.Renderer.World3DRenderer.Get3DPositionFromWPos(wPos);
			var offsetTransform = TSMatrix4x4.Translate(offsetVec);
			var rotMat = TSMatrix4x4.Rotate(Game.Renderer.World3DRenderer.Get3DRotationFromWRot(wRot));
			offset = offsetTransform * (scaleMat * rotMat);
		}

		public SkeletonInstance(in BoneAsset[] boneAssets, in SkeletonAsset asset, in OrderedSkeleton skeleton)
		{
			this.asset = asset;
			this.skeleton = skeleton;
			boneSize = boneAssets.Length;
			updateFlags = new bool[boneSize];
			Bones = new BoneInstance[boneSize];
			for (int i = 0; i < boneSize; i++)
			{
				Bones[i] = new BoneInstance(boneAssets[i]);
			}

			LastSkeletonPose = new TSMatrix4x4[boneSize];
		}

		void UpdateInner(int id, in Frame animFrame)
		{
			if (updateFlags[id] == true)
				return;

			if (Bones[id].ParentId == -1)
			{
				if (animFrame.Length > Bones[id].AnimId)
					Bones[id].UpdateOffset(offset, animFrame.Trans[Bones[id].AnimId].Matrix);
				else
					Bones[id].UpdateOffset(offset);
				updateFlags[id] = true;
			}
			else
			{
				UpdateInner(Bones[id].ParentId, animFrame);
				if (animFrame.Length > Bones[id].AnimId)
					Bones[id].UpdateOffset(Bones[Bones[id].ParentId].CurrentPose, animFrame.Trans[Bones[id].AnimId].Matrix);
				else
					Bones[id].UpdateOffset(Bones[Bones[id].ParentId].CurrentPose);
				updateFlags[id] = true;
			}
		}

		public void UpdateOffset(in Frame animFrame)
		{
			for (int i = 0; i < boneSize; i++)
			{
				updateFlags[i] = false;
			}

			for (int i = 0; i < boneSize; i++)
			{
				UpdateInner(i, animFrame);
			}

			hasUpdated = true;
		}

		public void UpdateLastPose()
		{
			if (hasUpdated)
				for (int i = 0; i < boneSize; i++)
				{
					LastSkeletonPose[i] = Bones[i].CurrentPose;
				}
			else
				return;

			hasUpdatedLast = true;
		}

		public void ProcessManagerData()
		{
			if (DrawID == -1 || !hasUpdatedLast)
				return;
			if (DrawID >= SkeletonAsset.AnimTextureHeight)
				throw new Exception("ProcessManagerData: Skeleton Instance drawId out of range: might be too many skeleton to draw!");

			int dataWidth = SkeletonAsset.AnimTextureWidth * 4;
			int i = 0;
			int y = DrawID * dataWidth;
			for (int x = 0; x < dataWidth; x += 16)
			{
				var c0 = LastSkeletonPose[asset.SkinBonesIndices[i]].Column0;
				var c1 = LastSkeletonPose[asset.SkinBonesIndices[i]].Column1;
				var c2 = LastSkeletonPose[asset.SkinBonesIndices[i]].Column2;
				var c3 = LastSkeletonPose[asset.SkinBonesIndices[i]].Column3;
				skeleton.AnimTransformData[y + x + 0] = (float)c0.x;
				skeleton.AnimTransformData[y + x + 1] = (float)c0.y;
				skeleton.AnimTransformData[y + x + 2] = (float)c0.z;
				skeleton.AnimTransformData[y + x + 3] = (float)c0.w;
				skeleton.AnimTransformData[y + x + 4] = (float)c1.x;
				skeleton.AnimTransformData[y + x + 5] = (float)c1.y;
				skeleton.AnimTransformData[y + x + 6] = (float)c1.z;
				skeleton.AnimTransformData[y + x + 7] = (float)c1.w;
				skeleton.AnimTransformData[y + x + 8] = (float)c2.x;
				skeleton.AnimTransformData[y + x + 9] = (float)c2.y;
				skeleton.AnimTransformData[y + x + 10] = (float)c2.z;
				skeleton.AnimTransformData[y + x + 11] = (float)c2.w;
				skeleton.AnimTransformData[y + x + 12] = (float)c3.x;
				skeleton.AnimTransformData[y + x + 13] = (float)c3.y;
				skeleton.AnimTransformData[y + x + 14] = (float)c3.z;
				skeleton.AnimTransformData[y + x + 15] = (float)c3.w;
				i++;
				if (i >= asset.SkinBonesIndices.Length)
				{
					break;
				}
			}
		}

		public bool CanGetPose()
		{
			return hasUpdatedLast && hasUpdated;
		}
	}

	public class OrderedSkeleton
	{
		public readonly string Name;
		readonly List<ModifiedBoneRestPose> modifiedBoneRestPoses = new List<ModifiedBoneRestPose>();

		/// <summary>
		/// update each tick, by actor skeleton trait.
		/// </summary>
		readonly List<SkeletonInstance> skeletonInstances = new List<SkeletonInstance>();
		public float[] AnimTransformData = new float[SkeletonAsset.AnimTextureWidth * SkeletonAsset.AnimTextureHeight * 4];
		public ITexture BoneAnimTexture;
		int instanceCount = 0;

		public readonly SkeletonAsset SkeletonAsset;
		public Dictionary<string, PreBakedSkeletalAnim> PreBakedAnimations = new Dictionary<string, PreBakedSkeletalAnim>();

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
						var restPose = ReadYamlInfo.LoadFixPointMat4(boneInfo, "RestPose");
						modifiedBoneRestPoses.Add(new ModifiedBoneRestPose(asset.BonesDict[boneDefine.Key], restPose));
					}
					else
					{
						throw new InvalidDataException("Skeleton " + asset.Name + " has no bone: " + boneDefine.Key);
					}
				}
			}

			SkeletonInstance preBakeInstance = new SkeletonInstance(asset.Bones, asset, this);

			foreach (var newPose in modifiedBoneRestPoses)
			{
				preBakeInstance.Bones[newPose.Id].RestPose = newPose.RestPose;
			}

			foreach (var animkv in asset.Animations)
			{
				var animName = animkv.Key;
				PreBakedSkeletalAnim bakedAnim = new PreBakedSkeletalAnim(animkv.Value.Frames.Length);

				for (int f = 0; f < animkv.Value.Frames.Length; f++)
				{
					preBakeInstance.UpdateOffset(animkv.Value.Frames[f]);
					bakedAnim.Frames[f] = new FrameBaked(preBakeInstance.Bones.Length);
					for (int i = 0; i < preBakeInstance.Bones.Length; i++)
					{
						bakedAnim.Frames[f].Trans[i] = preBakeInstance.Bones[i].CurrentPose;
					}
				}

				PreBakedAnimations.Add(animName, bakedAnim);

				BoneAnimTexture = Game.Renderer.CreateInfoTexture(new Primitives.Size(SkeletonAsset.AnimTextureWidth, SkeletonAsset.AnimTextureHeight));
			}
		}

		public void UpdateAnimTextureData()
		{
			if (instanceCount == 0 || skeletonInstances.Count == 0)
				return;
			instanceCount = 0;
			skeletonInstances.Clear();
			BoneAnimTexture.SetFloatData(AnimTransformData, SkeletonAsset.AnimTextureWidth, SkeletonAsset.AnimTextureHeight, TextureType.RGBA);
		}

		public SkeletonInstance CreateInstance()
		{
			SkeletonInstance skeletonInstance = new SkeletonInstance(SkeletonAsset.Bones, SkeletonAsset, this);

			foreach (var newPose in modifiedBoneRestPoses)
			{
				skeletonInstance.Bones[newPose.Id].RestPose = newPose.RestPose;
			}

			return skeletonInstance;
		}

		public void AddInstance(in SkeletonInstance skeletonInstance)
		{
			skeletonInstances.Add(skeletonInstance);
			skeletonInstance.DrawID = instanceCount;
			instanceCount++;
		}
	}

	/// <summary>
	/// using for create SkeletonInstance
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
		public float[] BindTransformData = new float[MaxSkinBones * 16];
		/// <summary>
		/// Key is Bone's Name, Value is Bone's AnimId
		/// </summary>
		public readonly Dictionary<string, int> BoneNameAnimIndex = new Dictionary<string, int>();

		/// <summary>
		/// the array order by skin bone's skinId, value is bone's common id
		/// </summary>
		public readonly int[] SkinBonesIndices;

		public Dictionary<string, SkeletalAnim> Animations = new Dictionary<string, SkeletalAnim>();

		// unit sequence to anims
		public Dictionary<string, Dictionary<string, SkeletalAnim>> AnimationsRef = new Dictionary<string, Dictionary<string, SkeletalAnim>>();

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
				if (bone.AnimId != -1)
					BoneNameAnimIndex.Add(bone.Name, bone.AnimId);
			}

			List<BoneAsset> skinBones = new List<BoneAsset>();
			foreach (var bone in Bones)
			{
				if (bone.SkinId != -1)
				{
					skinBones.Add(bone);
					var y = bone.SkinId * 16;
					BindTransformData[y + 0] = bone.BindPose.Column0[0];
					BindTransformData[y + 1] = bone.BindPose.Column0[1];
					BindTransformData[y + 2] = bone.BindPose.Column0[2];
					BindTransformData[y + 3] = bone.BindPose.Column0[3];
					BindTransformData[y + 4] = bone.BindPose.Column1[0];
					BindTransformData[y + 5] = bone.BindPose.Column1[1];
					BindTransformData[y + 6] = bone.BindPose.Column1[2];
					BindTransformData[y + 7] = bone.BindPose.Column1[3];
					BindTransformData[y + 8] = bone.BindPose.Column2[0];
					BindTransformData[y + 9] = bone.BindPose.Column2[1];
					BindTransformData[y + 10] = bone.BindPose.Column2[2];
					BindTransformData[y + 11] = bone.BindPose.Column2[3];
					BindTransformData[y + 12] = bone.BindPose.Column3[0];
					BindTransformData[y + 13] = bone.BindPose.Column3[1];
					BindTransformData[y + 14] = bone.BindPose.Column3[2];
					BindTransformData[y + 15] = bone.BindPose.Column3[3];
				}
			}

			SkinBonesIndices = new int[skinBones.Count];

			foreach (var bone in skinBones)
			{
				SkinBonesIndices[bone.SkinId] = bone.Id;
			}
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

		public SkeletalAnim GetSkeletalAnim(in string unit, in string sequence)
		{
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
				Console.WriteLine("BonesDict not ContainsKey " + boneName);
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
				var skinid = ReadYamlInfo.LoadField(info, "SkinID", -1);
				var animId = ReadYamlInfo.LoadField(info, "AnimID", -1);
				var parentName = ReadYamlInfo.LoadField(info, "Parent", "NO_Parent");
				var parentID = ReadYamlInfo.LoadField(info, "ParentID", -1);
				var restPose = ReadYamlInfo.LoadFixPointMat4(info, "RestPose");
				var restPoseInv = ReadYamlInfo.LoadFixPointMat4(info, "RestPoseInv");
				mat4 bindPose = ReadYamlInfo.LoadMat4(info, "BindPose");
				BoneAsset bone = new BoneAsset(name, id, skinid, animId, parentName, parentID, restPose, restPoseInv, bindPose);
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

	public class ReadYamlInfo
	{
		public static T LoadField<T>(Dictionary<string, MiniYaml> d, string key, T fallback)
		{
			if (d.TryGetValue(key, out var value))
				return FieldLoader.GetValue<T>(key, value.Value);

			return fallback;
		}

		public static mat4 LoadMat4(Dictionary<string, MiniYaml> d, string key)
		{
			float3 s = LoadField(d, key + "Scale", float3.Ones);
			float4 r = LoadField(d, key + "Rotation", float4.Identity);
			float3 t = LoadField(d, key + "Translation", float3.Zero);
			mat4 tm = mat4.Translate(new vec3(t.X, t.Y, t.Z));
			mat4 rm = new mat4(new quat(r.X,r.Y,r.Z,r.W));
			mat4 sm = mat4.Scale(new vec3(s.X, s.Y, s.Z));
			return tm * (sm * rm);
		}

		public static TSMatrix4x4 LoadFixPointMat4(Dictionary<string, MiniYaml> d, string key)
		{
			float3 s = LoadField(d, key + "Scale", float3.Ones);
			float4 r = LoadField(d, key + "Rotation", float4.Identity);
			float3 t = LoadField(d, key + "Translation", float3.Zero);
			var tm = TSMatrix4x4.Translate((FP)t.X, (FP)t.Y, (FP)t.Z);
			var rm = TSMatrix4x4.Rotate(new TSQuaternion((FP)r.X, (FP)r.Y, (FP)r.Z, (FP)r.W));
			var sm = TSMatrix4x4.Scale((FP)s.X, (FP)s.Y, (FP)s.Z);
			return tm * (sm * rm);
		}
	}

}
