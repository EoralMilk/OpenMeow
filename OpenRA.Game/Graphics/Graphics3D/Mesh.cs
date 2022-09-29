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
		OrderedSkeleton Skeleton { get; set; }
		void AddInstanceData(in float[] data, int dataCount, in int[] dataInt, int dataIntCount);
		void Flush();
		void DrawInstances(bool shadowBuffser);
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
		FaceCullFunc FaceCullFunc { get; }
		void SetShader(IShader shader, in string matname);
		void Dispose();
	}

	public class BlinnPhongMaterial : IMaterial
	{
		public readonly string Name;
		public readonly bool HasDiffuseMap;
		public readonly float3 DiffuseTint;
		public readonly Sheet DiffuseMap;
		public readonly bool HasSpecularMap;
		public readonly float3 SpecularTint;
		public readonly Sheet SpecularMap;
		public readonly float Shininess;
		public readonly bool Blinn;
		readonly FaceCullFunc faceCullFunc;
		public FaceCullFunc FaceCullFunc => faceCullFunc;
		public BlinnPhongMaterial(string name,
			bool hasDiffuseMap,
			float3 diffuseTint, Sheet diffuseMap,
			bool hasSpecularMap,
			float3 specularTint, Sheet specularMap,
			float shininess, FaceCullFunc faceCullFunc, bool blinn)
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
			Blinn = blinn;
		}

		public virtual void SetShader(IShader shader, in string matname)
		{
			shader.SetBool(matname + ".blinn", Blinn);

			// diffuse
			shader.SetBool(matname + ".hasDiffuseMap", HasDiffuseMap);
			shader.SetVec(matname + ".diffuseTint", DiffuseTint.X, DiffuseTint.Y, DiffuseTint.Z);
			if (HasDiffuseMap)
				shader.SetTexture(matname + ".diffuse", DiffuseMap.GetTexture());

			// specular
			shader.SetBool(matname + ".hasSpecularMap", HasSpecularMap);
			shader.SetVec(matname + ".specularTint", SpecularTint.X, SpecularTint.Y, SpecularTint.Z);
			if (HasSpecularMap)
				shader.SetTexture(matname + ".specular", SpecularMap.GetTexture());
			shader.SetFloat(matname + ".shininess", Shininess);
		}

		public void Dispose()
		{
			DiffuseMap?.Dispose();
			SpecularMap?.Dispose();
		}
	}

	public class PBRMaterial : IMaterial
	{
		public readonly string Name;
		public readonly bool HasAlbedoMap;
		public readonly float3 AlbedoTint;
		public readonly Sheet AlbedoMap;
		public readonly bool HasRoughnessMap;
		public readonly float Roughness;
		public readonly Sheet RoughnessMap;
		public readonly bool HasMetallicMap;
		public readonly float Metallic;
		public readonly Sheet MetallicMap;
		public readonly bool HasAOMap;
		public readonly float AO;
		public readonly Sheet AOMap;
		readonly FaceCullFunc faceCullFunc;
		public FaceCullFunc FaceCullFunc => faceCullFunc;

		public void Dispose()
		{
			AlbedoMap?.Dispose();
			RoughnessMap?.Dispose();
			MetallicMap?.Dispose();
			AOMap?.Dispose();
		}

		public PBRMaterial(string name, bool hasAlbedoMap, float3 albedoTint, Sheet albedoMap,
			bool hasRoughnessMap, float roughness, Sheet roughnessMap,
			bool hasMetallicMap, float metallic, Sheet metallicMap,
			bool hasAOMap, float ao, Sheet aoMap,
			FaceCullFunc faceCullFunc)
		{
			Name = name;
			HasAlbedoMap = hasAlbedoMap;
			AlbedoTint = albedoTint;
			AlbedoMap = albedoMap;
			HasRoughnessMap = hasRoughnessMap;
			Roughness = roughness;
			RoughnessMap = roughnessMap;
			HasMetallicMap = hasMetallicMap;
			Metallic = metallic;
			MetallicMap = metallicMap;
			HasAOMap = hasAOMap;
			AO = ao;
			AOMap = aoMap;
			this.faceCullFunc = faceCullFunc;
		}

		public virtual void SetShader(IShader shader, in string matname)
		{
			// albedo
			shader.SetBool(matname + ".hasAlbedoMap", HasAlbedoMap);
			shader.SetVec(matname + ".albedoTint", AlbedoTint.X, AlbedoTint.Y, AlbedoTint.Z);
			if (HasAlbedoMap)
				shader.SetTexture(matname + ".albedoMap", AlbedoMap.GetTexture());

			// roughness
			shader.SetBool(matname + ".hasRoughnessMap", HasRoughnessMap);
			shader.SetFloat(matname + ".roughness", Roughness);
			if (HasRoughnessMap)
				shader.SetTexture(matname + ".roughnessMap", RoughnessMap.GetTexture());

			// matallic
			shader.SetBool(matname + ".hasMetallicMap", HasMetallicMap);
			shader.SetFloat(matname + ".metallic", Metallic);
			if (HasMetallicMap)
				shader.SetTexture(matname + ".metallicMap", MetallicMap.GetTexture());

			// ao
			shader.SetBool(matname + ".hasAOMap", HasAOMap);
			shader.SetFloat(matname + ".ao", AO);
			if (HasAOMap)
				shader.SetTexture(matname + ".aoMap", AOMap.GetTexture());
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

}
