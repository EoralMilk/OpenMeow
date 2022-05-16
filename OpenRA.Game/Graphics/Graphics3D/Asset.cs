using GlmSharp;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace OpenRA.Graphics.Graphics3D
{
	struct Vertex
	{
		public vec3 Position;
		public vec3 Normal;
		public vec2 TexCoords;
		public uint[] BoneId;
		public float[] BoneWeight;
	}

	struct MeshAsset
	{
		public string Name;
		public int CacheID;

		public Vector<Vertex> Vertices;
		public Vector<uint> Indices;
	}

	struct SkeletonAsset
	{
		public string Name;
		public int CacheID;

		public Vector<Transformation> BoneOffset;
		public Vector<int> ParentBone; // 如果是-1说明这个骨骼是根骨骼（没父级）
	}

}
