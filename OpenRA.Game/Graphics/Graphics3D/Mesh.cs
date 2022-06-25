using System;
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
		public readonly Func<TSMatrix4x4> Matrix;
		public readonly bool UseMatrix = false;
		public readonly string SkeletonBinded = null;

		public int DrawId;
		public int DrawMask;
		public MeshInstance(IOrderedMesh mesh, Func<WPos> offset, Func<WRot> rotation, Func<bool> isVisible, string skeleton = null)
		{
			OrderedMesh = mesh;
			PoistionFunc = offset;
			RotationFunc = rotation;
			IsVisible = isVisible;
			DrawId = -1;
			DrawMask = -1;
			SkeletonBinded = skeleton;
		}

		public MeshInstance(IOrderedMesh mesh, Func<TSMatrix4x4> matrix, Func<bool> isVisible, string skeleton = null)
		{
			OrderedMesh = mesh;
			UseMatrix = true;
			Matrix = matrix;
			IsVisible = isVisible;
			DrawId = -1;
			DrawMask = -1;
			SkeletonBinded = skeleton;
		}

		public Rectangle ScreenBounds(WPos wPos, WorldRenderer wr, float scale)
		{
			var minX = float.MaxValue;
			var minY = float.MaxValue;
			var maxX = float.MinValue;
			var maxY = float.MinValue;

			return Rectangle.FromLTRB((int)minX, (int)minY, (int)maxX, (int)maxY);
		}
	}

	public interface IOrderedMesh
	{
		string Name { get; }
		OrderedSkeleton Skeleton { get; set; }
		void AddInstanceData(in float[] data, int dataCount, in int[] dataInt, int dataIntCount);
		void Flush();
		void DrawInstances();
		void SetPalette(ITexture pal);
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
		FaceCullFunc FaceCullFunc { get; }
		void SetShader(IShader shader, in string matname);
	}

	public class CommonMaterial : IMaterial
	{
		public readonly string Name;
		public readonly bool HasDiffuseMap;
		public readonly float3 DiffuseTint;
		public readonly ITexture DiffuseMap;
		public readonly bool HasSpecularMap;
		public readonly float3 SpecularTint;
		public readonly ITexture SpecularMap;
		public readonly float Shininess;
		readonly FaceCullFunc faceCullFunc;
		public FaceCullFunc FaceCullFunc => faceCullFunc;
		public CommonMaterial(string name, bool hasDiffuseMap, float3 diffuseTint, ITexture diffuseMap, bool hasSpecularMap, float3 specularTint, ITexture specularMap, float shininess, FaceCullFunc faceCullFunc)
		{
			Name = name;
			HasDiffuseMap = hasDiffuseMap;
			DiffuseTint = diffuseTint;
			DiffuseMap = diffuseMap;
			HasSpecularMap = hasSpecularMap;
			SpecularTint = specularTint;
			SpecularMap = specularMap;
			Shininess = shininess;
			this.faceCullFunc = faceCullFunc;
		}

		public virtual void SetShader(IShader shader, in string matname)
		{
			// diffuse
			shader.SetBool(matname + ".hasDiffuseMap", HasDiffuseMap);
			shader.SetVec(matname + ".diffuseTint", DiffuseTint.X, DiffuseTint.Y, DiffuseTint.Z);
			if (HasDiffuseMap)
				shader.SetTexture(matname + ".diffuse", DiffuseMap);

			// specular
			shader.SetBool(matname + ".hasSpecularMap", HasSpecularMap);
			shader.SetVec(matname + ".specularTint", SpecularTint.X, SpecularTint.Y, SpecularTint.Z);
			if (HasSpecularMap)
				shader.SetTexture(matname + ".specular", SpecularMap);
			shader.SetFloat(matname + ".shininess", Shininess);
		}
	}

	// Multiple OrderMesh can use same MeshVertexData
	public class MeshVertexData
	{
		public readonly int Start;
		public readonly int Count;
		public readonly IShader Shader;
		public readonly IVertexBuffer VertexBuffer;
		public MeshVertexData(int start, int count, IShader shader, IVertexBuffer vertexBuffer)
		{
			Start = start;
			Count = count;
			Shader = shader;
			VertexBuffer = vertexBuffer;
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

}
