﻿using System;
using System.Collections.Generic;
using System.Numerics;
using OpenRA.FileSystem;
using OpenRA.Primitives;
using TrueSync;

namespace OpenRA.Graphics
{

	public class MeshInstance
	{
		public IOrderedMesh OrderedMesh { get; }

		public readonly Func<WPos> PoistionFunc;
		public readonly Func<WRot> RotationFunc;
		public readonly Func<bool> IsVisible;
		public Func<TSMatrix4x4> Matrix;
		public bool UseMatrix = false;
		public readonly string SkeletonBinded = null;

		public int DrawId;
		public IMaterial Material;
		public MeshInstance(IOrderedMesh mesh, Func<WPos> offset, Func<WRot> rotation, Func<bool> isVisible, string skeleton = null)
		{
			OrderedMesh = mesh;
			PoistionFunc = offset;
			RotationFunc = rotation;
			IsVisible = isVisible;
			DrawId = -1;
			SkeletonBinded = skeleton;
			Material = OrderedMesh.DefaultMaterial;
		}

		public MeshInstance(IOrderedMesh mesh, Func<TSMatrix4x4> matrix, Func<bool> isVisible, string skeleton = null)
		{
			OrderedMesh = mesh;
			UseMatrix = true;
			Matrix = matrix;
			IsVisible = isVisible;
			DrawId = -1;
			SkeletonBinded = skeleton;
			Material = OrderedMesh.DefaultMaterial;
		}

		public Rectangle ScreenBounds(WPos wPos, WorldRenderer wr, float scale)
		{
			var spos = wr.Screen3DPxPosition(wPos);

			return Rectangle.FromLTRB(	(int)(spos.X + OrderedMesh.BoundingRec.Left * scale),
															(int)(spos.Y + OrderedMesh.BoundingRec.Top * scale),
															(int)(spos.X + OrderedMesh.BoundingRec.Right * scale),
															(int)(spos.Y + OrderedMesh.BoundingRec.Bottom * scale));
		}
	}

	public interface IOrderedMesh
	{
		string Name { get; }
		IMaterial DefaultMaterial { get; }
		OrderedSkeleton Skeleton { get; set; }
		void AddInstanceData(in float[] data, int dataCount, in int[] dataInt, int dataIntCount);
		void Flush();
		void DrawInstances(World world, bool shadowBuffser);
		void SetPalette(ITexture pal);

		Rectangle BoundingRec { get; }
	}

	public readonly struct CombinedMeshRenderData
	{
		public readonly int Start;
		public readonly int Count;
		public readonly IShader Shader;
		public readonly IVertexBuffer VertexBuffer;
		public readonly Dictionary<string, ITexture> Textures;
		public CombinedMeshRenderData(int start, int count, IShader shader, IVertexBuffer vertexBuffer, Dictionary<string, ITexture> textures)
		{
			Start = start;
			Count = count;
			Shader = shader;
			VertexBuffer = vertexBuffer;
			Textures = textures;
		}
	}

	public interface IMaterial
	{
		void UpdateTextureIndex(MeshCache meshCache);
		int[] GetParams();
		void Dispose();
	}

	public class CombinedMaterial : IMaterial
	{
		public readonly string Name;
		public readonly Sheet DiffuseMap;
		public readonly float3 DiffuseTint;
		public readonly float SpecularTint;
		public int DiffuseMapIndex { get; private set; }
		public readonly Sheet CombinedMap;
		public int CombinedMapIndex { get; private set; }

		public readonly float Shininess;

		readonly int diffuseTint;
		readonly int combineTint;

		int texSize;

		public CombinedMaterial(string name,
			Sheet diffuseMap, float3 diffuseTint,
			Sheet combinedMap, float specularTint,
			float shininess)
		{
			Name = name;
			DiffuseTint = diffuseTint;
			SpecularTint = specularTint;
			DiffuseMap = diffuseMap;
			CombinedMap = combinedMap;
			Shininess = shininess;
			this.diffuseTint =
				((int)(MathF.Min(diffuseTint.X, 1f) * 255) << 16) +
				((int)(MathF.Min(diffuseTint.Y, 1f) * 255) << 8) +
				((int)(MathF.Min(diffuseTint.Z, 1f) * 255));
			this.diffuseTint = this.diffuseTint | (1 << 31);
			combineTint = (0 << 16) + ((int)(MathF.Min(specularTint, 1f) * 255) << 8) + 0;
			combineTint = combineTint | (1 << 31);
			texSize = DiffuseMap != null ? DiffuseMap.Size.Width : CombinedMap != null ? CombinedMap.Size.Width : 0;
		}

		public void UpdateTextureIndex(MeshCache meshCache)
		{
			if (DiffuseMap != null)
				DiffuseMapIndex = meshCache.TexturesIndexBySheet[DiffuseMap];
			if (CombinedMap != null)
				CombinedMapIndex = meshCache.TexturesIndexBySheet[CombinedMap];
		}

		public int[] GetParams()
		{
			return new int[] {
				DiffuseMap != null ? DiffuseMapIndex : diffuseTint,
				CombinedMap != null ? CombinedMapIndex : combineTint,
				(int)(Shininess * 100),
				texSize };
		}

		public void Dispose()
		{
			DiffuseMap?.Dispose();
			CombinedMap?.Dispose();
		}
	}

	// Multiple OrderMesh can use same MeshVertexData
	public class MeshVertexData
	{
		public readonly int Start;
		public readonly int Count;
		public readonly IShader Shader;
		public readonly IVertexBuffer VertexBuffer;
		public readonly Rectangle BoundingRec;
		public MeshVertexData(int start, int count, IShader shader, IVertexBuffer vertexBuffer, Rectangle bound)
		{
			Start = start;
			Count = count;
			Shader = shader;
			VertexBuffer = vertexBuffer;
			BoundingRec = bound;
		}

		public void Dispose()
		{
			VertexBuffer?.Dispose();
		}
	}

	public interface IMeshLoader
	{
		bool TryLoadMesh(IReadOnlyFileSystem fileSystem, string filename, MiniYaml definition, MeshCache cache, OrderedSkeleton skeleton, SkeletonAsset skeletonType, out IOrderedMesh model);
	}

	public interface IMeshSequenceLoader
	{
		MeshCache CacheMeshes(IMeshLoader[] loaders, SkeletonCache skeletonCache, IReadOnlyFileSystem fileSystem, ModData modData, IReadOnlyDictionary<string, MiniYamlNode> modelDefinitions);
	}

	public struct MeshInstanceData
	{
#pragma warning disable IDE1006 // 命名样式
		public float t0, t1, t2, t3, t4, t5, t6, t7, t8, t9, t10, t11, t12, t13, t14, t15;
#pragma warning restore IDE1006 // 命名样式
		public float TintX, TintY, TintZ, TintW;
		public float RemapX, RemapY, RemapZ;
		public int DrawId;
		public int MatX, MatY, MatZ, MatW;
		public MeshInstanceData(in float[] data, int id, int colorIndex, int combinedIndex, int shiness, int texsize)
		{
			t0 = data[0];
			t1 = data[1];
			t2 = data[2];
			t3 = data[3];
			t4 = data[4];
			t5 = data[5];
			t6 = data[6];
			t7 = data[7];
			t8 = data[8];
			t9 = data[9];
			t10 = data[10];
			t11 = data[11];
			t12 = data[12];
			t13 = data[13];
			t14 = data[14];
			t15 = data[15];
			TintX = data[16];
			TintY = data[17];
			TintZ = data[18];
			TintW = data[19];
			RemapX = data[20];
			RemapY = data[21];
			RemapZ = data[22];

			DrawId = id;
			MatX = colorIndex;
			MatY = combinedIndex;
			MatZ = shiness;
			MatW = texsize;
		}
	}

	public class OrderedMesh : IOrderedMesh
	{
		public static readonly int MaxInstanceCount = 4096;
		public OrderedSkeleton Skeleton { get; set; }
		readonly bool useDQB = false;
		readonly MeshVertexData renderData;
		public readonly FaceCullFunc FaceCull;
		readonly IMaterial defaultMaterial;
		public IMaterial DefaultMaterial => defaultMaterial;

		readonly string name;
		public string Name => name;

		readonly MeshInstanceData[] instancesToDraw;
		int instanceCount;
		public IVertexBuffer<MeshInstanceData> InstanceArrayBuffer;
		public Rectangle BoundingRec => renderData.BoundingRec;

		public OrderedMesh(string name, MeshVertexData data, IMaterial defaultMaterial, FaceCullFunc faceCull, bool useDQB, OrderedSkeleton skeleton)
		{
			this.name = name;
			this.useDQB = useDQB;
			Skeleton = skeleton;
			renderData = data;
			InstanceArrayBuffer = Game.Renderer.CreateVertexBuffer<MeshInstanceData>(MaxInstanceCount);
			instancesToDraw = new MeshInstanceData[MaxInstanceCount];
			instanceCount = 0;
			FaceCull = faceCull;
			this.defaultMaterial = defaultMaterial;
		}

		public void AddInstanceData(in float[] data, int dataCount, in int[] dataInt, int dataIntCount)
		{
			if (instanceCount == MaxInstanceCount)
				throw new Exception("Instance Count bigger than MaxInstanceCount");

			if (dataCount != 23 || dataIntCount != 5)
				throw new Exception("AddInstanceData params length unright ");

			MeshInstanceData instanceData = new MeshInstanceData(data, dataInt[0], dataInt[1], dataInt[2], dataInt[3], dataInt[4]);
			instancesToDraw[instanceCount] = instanceData;
			instanceCount++;
		}

		public void Flush()
		{
			instanceCount = 0;
		}

		public void SetPalette(ITexture pal)
		{
		}

		public void DrawInstances(World world, bool shadowBuffser = false)
		{
			if (instanceCount == 0)
				return;
			if (shadowBuffser && FaceCull == FaceCullFunc.Back)
				Game.Renderer.SetFaceCull(FaceCullFunc.Front);
			else
				Game.Renderer.SetFaceCull(FaceCull);
			if (Skeleton != null)
			{
				renderData.Shader.SetBool("useDQB", useDQB);
				renderData.Shader.SetInt("skinBoneCount", Skeleton.SkeletonAsset.SkinBonesIndices.Length);
				renderData.Shader.SetInt("skinBoneTexWidth", SkeletonAsset.AnimTextureWidth);

				renderData.Shader.SetTexture("boneAnimTexture", OrderedSkeleton.BoneAnimTexture);
				renderData.Shader.SetMatrix("BindTransformData", Skeleton.BindTransformData, 128);
			}

			if (world.MeshCache.TextureArray64 != null)
				renderData.Shader.SetTexture("Textures64", world.MeshCache.TextureArray64);
			if (world.MeshCache.TextureArray128 != null)
				renderData.Shader.SetTexture("Textures128", world.MeshCache.TextureArray128);
			if (world.MeshCache.TextureArray256 != null)
				renderData.Shader.SetTexture("Textures256", world.MeshCache.TextureArray256);
			if (world.MeshCache.TextureArray512 != null)
				renderData.Shader.SetTexture("Textures512", world.MeshCache.TextureArray512);
			if (world.MeshCache.TextureArray1024 != null)
				renderData.Shader.SetTexture("Textures1024", world.MeshCache.TextureArray1024);

			renderData.Shader.PrepareRender();

			InstanceArrayBuffer.SetData(instancesToDraw, instanceCount);
			InstanceArrayBuffer.Bind();
			renderData.Shader.LayoutInstanceArray();

			// bind after the Instance Array Buffer because we should use elemented render
			// ebo is in vertexBuffer
			renderData.VertexBuffer.Bind();
			renderData.Shader.LayoutAttributes();

			Game.Renderer.SetBlendMode(BlendMode.Alpha);

			// draw instance, this is elemented
			Game.Renderer.RenderInstance(0, renderData.Count, instanceCount, true);
			Game.Renderer.SetBlendMode(BlendMode.None);
			Game.Renderer.SetFaceCull(FaceCullFunc.None);
		}
	}
}
