using System;
using System.Collections.Generic;
using System.IO;
using OpenRA.FileSystem;
using TrueSync;

namespace OpenRA.Graphics
{
	/// <summary>
	/// 预先bake的骨骼动画使用的是常规boneid
	/// </summary>
	public class PreBakedSkeletalAnim
	{
		public readonly FrameBaked[] Frames;

		public PreBakedSkeletalAnim(int size)
		{
			Frames = new FrameBaked[size];
		}
	}

	public class FrameBaked
	{
		public TSMatrix4x4[] Trans;

		public FrameBaked(int size)
		{
			Trans = new TSMatrix4x4[size];
		}
	}

	public class SkeletalAnim
	{
		public readonly string Name;
		public readonly string Sequence;
		public int Length
		{
			get
			{
				return Frames.Length;
			}
		}

		public readonly Frame[] Frames;

		public SkeletalAnim(ModifiedBoneRestPose mb, SkeletonAsset assetBind, string name, string sequence = null)
		{
			Name = name;
			Sequence = sequence;
			Frames = new Frame[2];
			Frame frame1 = new Frame(assetBind.BoneNameAnimIndex.Count);
			Frame frame2 = new Frame(assetBind.BoneNameAnimIndex.Count);
			for (int i = 0; i < frame1.Length; i++)
			{
				frame1[i] = new Transformation(assetBind.Bones[assetBind.AnimBonesIndices[i]].RestPose);
				frame2[i] = new Transformation(assetBind.Bones[assetBind.AnimBonesIndices[i]].RestPose);
			}

			frame1[mb.AnimId] = new Transformation(mb.FirstPose);
			frame2[mb.AnimId] = new Transformation(mb.LastPose);
			Frames[0] = frame1;
			Frames[1] = frame2;
		}

		public SkeletalAnim(IReadOnlyFileSystem fileSystem, string sequence, string filename, SkeletonAsset assetBind)
		{
			Sequence = sequence;

			var name = filename;

			bool oldType = false;

			if (!fileSystem.Exists(name))
			{
				name = filename + ".ska";
				oldType = false;
			}

			if (!fileSystem.Exists(name))
			{
				name = filename + ".anim";
				oldType = true;
			}

			if (!fileSystem.Exists(name))
			{
				throw new Exception("SkeletalAnim:FromFile: can't find file " + name);
			}

			SkeletalAnimReader reader;
			using (var s = fileSystem.Open(name))
			{
				reader = new SkeletalAnimReader(s, assetBind, oldType);
			}

			Frames = new Frame[reader.Frames.Length];
			reader.Frames.CopyTo(Frames, 0);
			Name = reader.animName;
		}
	}

	public class Frame
	{
		Transformation[] transformations;

		public bool[] HasTransformation { get; private set; }
		public int Length => transformations.Length;

		public Transformation this[int index]
		{
			get
			{
				return transformations[index];
			}
			set
			{
				transformations[index] = value;
				HasTransformation[index] = true;
			}
		}

		public bool SetNoneTransform(int index)
		{
			if (index >= Length)
				return false;
			HasTransformation[index] = false;
			transformations[index] = Transformation.Identity;
			return true;
		}

		public Frame(int size)
		{
			transformations = new Transformation[size];
			HasTransformation = new bool[size];
			for (int i = 0; i < size; i++)
			{
				HasTransformation[i] = false;
				transformations[i] = Transformation.Identity;
			}
		}
	}

	class SkeletalAnimReader
	{
		Dictionary<int, string> boneIdtoNames = new Dictionary<int, string>();
		public Frame[] Frames;
		public string animName;

		public SkeletalAnimReader(Stream s, SkeletonAsset skeleton, bool oldType)
		{
			string header = s.ReadASCII(8);

			if (header != "ORA_ANIM")
				throw new Exception("SkeletalAnimReader: read file which has error header");

			animName = s.ReadUntil('?');

			uint dictSize = s.ReadUInt32();

			for (int i = 0; i < dictSize; i++)
			{
				int id = s.ReadInt32();
				string name = s.ReadUntil('?');
				boneIdtoNames.Add(id, name);
			}

			uint length = s.ReadUInt32();
			Frames = new Frame[length];
			bool support = true;
			for (int i = 0; i < length; i++)
			{
				uint bones = s.ReadUInt32();
				Frames[i] = new Frame((int)skeleton.BoneNameAnimIndex.Count);
				for (int j = 0; j < bones; j++)
				{
					var id = j;
					if (!oldType)
						id = s.ReadInt32();

					var scale = ReadVec3(s);
					var rotation = ReadQuat(s);
					rotation.Normalize();
					var translation = ReadVec3(s);

					if (!boneIdtoNames.ContainsKey(id))
					{
						continue;
					}

					if (skeleton != null && skeleton.BoneNameAnimIndex.ContainsKey(boneIdtoNames[id]))
					{
						Frames[i][skeleton.BoneNameAnimIndex[boneIdtoNames[id]]] = new Transformation(scale, rotation, translation);
					}
					else
					{
						continue;
						Console.WriteLine("No Match Bone: " + boneIdtoNames[id] + " in skeleton: " + skeleton.Name);
					}
				}
			}

			if (!support)
				Console.WriteLine(animName + " not full support");
		}

		TSVector ReadVec3(Stream s)
		{
			float x, y, z;
			x = s.ReadFloat(); y = s.ReadFloat(); z = s.ReadFloat();
			return new TSVector(FP.FromFloat(x), FP.FromFloat(y), FP.FromFloat(z));
		}

		TSQuaternion ReadQuat(Stream s)
		{
			float x, y, z, w;
			x = s.ReadFloat(); y = s.ReadFloat(); z = s.ReadFloat(); w = s.ReadFloat();
			return new TSQuaternion(FP.FromFloat(x), FP.FromFloat(y), FP.FromFloat(z), FP.FromFloat(w));
		}
	}
}
