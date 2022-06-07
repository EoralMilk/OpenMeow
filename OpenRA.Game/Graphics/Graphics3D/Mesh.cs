using System;
using System.Collections.Generic;
using System.Numerics;

namespace OpenRA.Graphics.Graphics3D
{
	public interface IOrderedMesh
	{
		MeshRenderData RenderData { get; }
		void AddInstanceData(float[] data, int dataCount);
		void Flush();
		void DrawInstances();
		void SetPalette(ITexture pal);
	}

	public struct MeshInstanceData
	{
		// 用于记录Mesh应该读取哪个存储在贴图中的skeleton实例的数据, 负数表示不使用骨架数据
		// 一般来说实际上一类的mesh是否使用skeleton是统一的。
		public float DrawId;

		// 当不使用skeleton数据时，这个meshinstance的坐标位置
		public float[] Transform;

		public MeshInstanceData(float drawid, float[] trans)
		{
			DrawId = drawid;
			Transform = trans;
		}
	}

	public readonly struct MeshRenderData
	{
		public readonly int Start;
		public readonly int Count;
		public readonly IShader Shader;
		public readonly IVertexBuffer VertexBuffer;
		public readonly Dictionary<string, ITexture> Textures;
		public MeshRenderData(int start, int count, IShader shader, IVertexBuffer vertexBuffer, Dictionary<string, ITexture> textures)
		{
			Start = start;
			Count = count;
			Shader = shader;
			VertexBuffer = vertexBuffer;
			Textures = textures;
		}
	}
}
