using System;
using System.Collections.Generic;
using System.Numerics;
using OpenRA.FileSystem;
using OpenRA.Primitives;

namespace OpenRA.Graphics
{
	public class MeshInstance
	{
		public IOrderedMesh OrderedMesh { get; }

		public Func<WPos> PoistionFunc;
		public Func<WRot> RotationFunc;
		public Func<bool> IsVisible;
		public Func<Matrix4x4> Matrix;
		public bool UseMatrix = false;
		public string SkeletonBinded = null;

		/// <summary>
		/// -2 means this instance should not be draw, -1 might means this instance has no skeleton binded
		/// </summary>
		public Func<int> DrawId;
		public IMaterial Material;
		public Func<Color> GetRemap;
		public Func<float> GetAlpha;
		public Func<float3> GetTint;

		public MeshInstance(IOrderedMesh mesh, Func<WPos> offset, Func<WRot> rotation, Func<bool> isVisible, string skeleton = null)
		{
			OrderedMesh = mesh;
			PoistionFunc = offset;
			RotationFunc = rotation;
			IsVisible = isVisible;
			DrawId = () => -1;
			GetRemap = null;
			GetAlpha = null;
			GetTint = null;
			SkeletonBinded = skeleton;
			Material = OrderedMesh.DefaultMaterial;
		}

		public MeshInstance(IOrderedMesh mesh, Func<Matrix4x4> matrix, Func<bool> isVisible, string skeleton = null)
		{
			OrderedMesh = mesh;
			UseMatrix = true;
			Matrix = matrix;
			IsVisible = isVisible;
			DrawId = () => -1;
			GetRemap = null;
			GetAlpha = null;
			GetTint = null;
			SkeletonBinded = skeleton;
			Material = OrderedMesh.DefaultMaterial;
		}

		public MeshInstance(IOrderedMesh mesh)
		{
			OrderedMesh = mesh;
			UseMatrix = true;
			Matrix = () => Matrix4x4.Identity;
			IsVisible = () => false;
			DrawId = () => -1;
			SkeletonBinded = null;
			GetRemap = null;
			GetAlpha = null;
			GetTint = null;
			Material = OrderedMesh.DefaultMaterial;
		}

		public Rectangle ScreenBounds(WPos wPos, WorldRenderer wr, float scale)
		{
			var spos = wr.Screen3DPxPosition(wPos);

			return Rectangle.FromLTRB((int)(spos.X + OrderedMesh.BoundingRec.Left * scale),
														(int)(spos.Y + OrderedMesh.BoundingRec.Top * scale),
														(int)(spos.X + OrderedMesh.BoundingRec.Right * scale),
														(int)(spos.Y + OrderedMesh.BoundingRec.Bottom * scale));
		}
	}

	public enum MeshDrawType
	{
		Actor,
		Effect,
		Twist,
	}

	public interface ITwistActorMesh
	{
		bool IsTwisting { get; }
	}

	public interface IOrderedMesh
	{
		string Name { get; }
		IMaterial DefaultMaterial { get; }
		OrderedSkeleton Skeleton { get; set; }
		void AddInstanceData(in float[] data, int dataCount, in int[] dataInt, int dataIntCount);
		void AddTwistInstanceData(in float[] data, int dataCount, in int[] dataInt, int dataIntCount);

		void Flush();
		void DrawInstances(World world, bool shadowBuffser, MeshDrawType drawType);
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
		void SetShaderAsBaseMaterial(IShader shader);
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

		public readonly float Emission;

		readonly int diffuseTint;
		readonly int combineTint;

		readonly int texSize;

		public CombinedMaterial(string name,
			Sheet diffuseMap, float3 diffuseTint,
			Sheet combinedMap, float specularTint,
			float shininess,
			float emission)
		{
			Name = name;
			DiffuseTint = diffuseTint;
			SpecularTint = specularTint;
			DiffuseMap = diffuseMap;
			CombinedMap = combinedMap;
			Shininess = shininess;
			Emission = emission;
			this.diffuseTint =
				((int)(MathF.Min(diffuseTint.X, 1f) * 255) << 16) +
				((int)(MathF.Min(diffuseTint.Y, 1f) * 255) << 8) +
				((int)(MathF.Min(diffuseTint.Z, 1f) * 255));
			this.diffuseTint = this.diffuseTint | (1 << 31);
			combineTint = (0 << 16) + ((int)(MathF.Min(specularTint, 1f) * 255) << 8) + (int)(MathF.Min(Emission, 1f) * 255);
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

		public void SetShaderAsBaseMaterial(IShader shader)
		{
			if (DiffuseMap != null)
			{
				shader.SetBool("BaseColorHasTexture", true);
				shader.SetTexture("BaseColorTexture", DiffuseMap.GetTexture());
			}
			else
			{
				shader.SetBool("BaseColorHasTexture", false);
				shader.SetVec("BaseDiffuseColor", DiffuseTint.X, DiffuseTint.Y, DiffuseTint.Z);
			}

			if (CombinedMap != null)
			{
				shader.SetBool("BaseCombinedHasTexture", true);
				shader.SetTexture("BaseCombinedTexture", CombinedMap.GetTexture());
			}
			else
			{
				shader.SetBool("BaseCombinedHasTexture", false);
				shader.SetFloat("BaseSpecular", SpecularTint);
			}

			shader.SetFloat("BaseShininess", Shininess);
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
		public readonly IVertexBuffer VertexBuffer;
		public readonly Rectangle BoundingRec;
		public MeshVertexData(int start, int count, IVertexBuffer vertexBuffer, Rectangle bound)
		{
			Start = start;
			Count = count;
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

	public enum MeshShaderType
	{
		Common,
		CharacterBody,
		CharacterHair,
		FlatLight,
	}

	public struct OrderedMeshInfo
	{
		public bool UseDQB;
		public MeshShaderType ShaderType;
		public IMaterial DefaultMaterial;
		public IMaterial BaseMaterial;
		public FaceCullFunc FaceCullFunc;
		public BlendMode BlendMode;
		public bool HasShadow;
		public MeshDrawType DrawType;
	}

	public class OrderedMesh : IOrderedMesh
	{
		public static readonly int MaxInstanceCount = 4096;

		readonly MeshVertexData renderData;
		public readonly IShader Shader;

		public OrderedSkeleton Skeleton { get; set; }
		readonly bool useDQB = false;

		public readonly FaceCullFunc FaceCull;
		public readonly BlendMode BlendMode = BlendMode.None;
		public readonly bool HasShadow;
		public readonly MeshDrawType MeshDrawType;

		/// <summary>
		/// base material, it should be use for some special shader
		/// </summary>
		public readonly IMaterial BaseMaterial;

		readonly IMaterial defaultMaterial;
		public IMaterial DefaultMaterial => defaultMaterial;

		readonly string name;
		public string Name => name;

		readonly MeshInstanceData[] instancesToDraw;
		readonly MeshInstanceData[] twistInstancesToDraw;

		int instanceCount;
		int twistInstanceCount;
		public IVertexBuffer<MeshInstanceData> InstanceArrayBuffer;
		public Rectangle BoundingRec => renderData.BoundingRec;

		public OrderedMesh(string name, MeshVertexData data, OrderedSkeleton skeleton, in OrderedMeshInfo orderedMeshInfo)
		{
			this.name = name;
			useDQB = orderedMeshInfo.UseDQB && !Game.Settings.Graphics.ForceLinerBlendSkin;
			Skeleton = skeleton;
			renderData = data;
			InstanceArrayBuffer = Game.Renderer.CreateVertexBuffer<MeshInstanceData>(MaxInstanceCount);
			instancesToDraw = new MeshInstanceData[MaxInstanceCount];
			twistInstancesToDraw = new MeshInstanceData[MaxInstanceCount];
			instanceCount = 0;
			twistInstanceCount = 0;
			FaceCull = orderedMeshInfo.FaceCullFunc;
			BlendMode = orderedMeshInfo.BlendMode;
			defaultMaterial = orderedMeshInfo.DefaultMaterial;
			HasShadow = orderedMeshInfo.HasShadow;
			MeshDrawType = orderedMeshInfo.DrawType;

			BaseMaterial = orderedMeshInfo.BaseMaterial;
			switch (orderedMeshInfo.ShaderType)
			{
				case MeshShaderType.Common:
					Shader = Game.Renderer.GetOrCreateShader<MeshShaderBindings>("MeshShaderBindings");
					break;
				case MeshShaderType.CharacterBody:
					{
						Shader = Game.Renderer.GetOrCreateShader<CharacterBodyMeshShaderBindings>("CharacterBodyMeshShaderBindings");
						if (BaseMaterial == null)
							throw new Exception("CharacterBody Mesh must have base material");
					}
					break;
				case MeshShaderType.CharacterHair:
					Shader = Game.Renderer.GetOrCreateShader<CharacterHairMeshShaderBindings>("CharacterHairMeshShaderBindings");
					break;
				case MeshShaderType.FlatLight:
					Shader = Game.Renderer.GetOrCreateShader<FlatLightShaderBindings>("FlatLightShaderBindings");
					break;
			}
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

		public void AddTwistInstanceData(in float[] data, int dataCount, in int[] dataInt, int dataIntCount)
		{
			if (twistInstanceCount == MaxInstanceCount)
				throw new Exception("Instance Count bigger than MaxInstanceCount");

			if (dataCount != 23 || dataIntCount != 5)
				throw new Exception("AddInstanceData params length unright ");

			MeshInstanceData instanceData = new MeshInstanceData(data, dataInt[0], dataInt[1], dataInt[2], dataInt[3], dataInt[4]);
			twistInstancesToDraw[twistInstanceCount] = instanceData;
			twistInstanceCount++;
		}

		public void Flush()
		{
			instanceCount = 0;
			twistInstanceCount = 0;
		}

		public void SetPalette(ITexture pal)
		{
		}

		public void DrawInstances(World world, bool shadowBuffer, MeshDrawType drawType)
		{
			if (shadowBuffer && !HasShadow)
				return;
			if (drawType == MeshDrawType.Twist && twistInstanceCount == 0)
				return;
			else if (drawType != MeshDrawType.Twist && (MeshDrawType != drawType || instanceCount == 0))
				return;

			if (shadowBuffer && FaceCull == FaceCullFunc.Back)
				Game.Renderer.SetFaceCull(FaceCullFunc.Front);
			else
				Game.Renderer.SetFaceCull(FaceCull);
			if (Skeleton != null)
			{
				Shader.SetBool("useDQB", useDQB);
				// Shader.SetInt("skinBoneCount", Skeleton.SkeletonAsset.SkinBonesIndices.Length);
				Shader.SetInt("skinBoneTexWidth", SkeletonAsset.AnimTextureWidth);

				Shader.SetTexture("boneAnimTexture", OrderedSkeleton.BoneAnimTexture);
				Shader.SetBool("FixedBindTransform", Skeleton.UseDynamicAdjBonePose);
				Shader.SetInt("BoneTexOffset", Skeleton.UseDynamicAdjBonePose ? 3 : 6);
				if (Skeleton.UseDynamicAdjBonePose)
					Shader.SetMatrix("BindTransformData", Skeleton.BindTransformData, 128);
			}

			Shader.SetBool("IsTwist", drawType == MeshDrawType.Twist);
			Shader.SetFloat("TwistTime", Game.Renderer.TwistTime);
			Shader.SetFloat("TwistMul", Game.Renderer.TwistMul);

			BaseMaterial?.SetShaderAsBaseMaterial(Shader);

			if (world.MeshCache.TextureArray64 != null)
				Shader.SetTexture("Textures64", world.MeshCache.TextureArray64);
			if (world.MeshCache.TextureArray128 != null)
				Shader.SetTexture("Textures128", world.MeshCache.TextureArray128);
			if (world.MeshCache.TextureArray256 != null)
				Shader.SetTexture("Textures256", world.MeshCache.TextureArray256);
			if (world.MeshCache.TextureArray512 != null)
				Shader.SetTexture("Textures512", world.MeshCache.TextureArray512);
			if (world.MeshCache.TextureArray1024 != null)
				Shader.SetTexture("Textures1024", world.MeshCache.TextureArray1024);

			Shader.PrepareRender();

			if (drawType == MeshDrawType.Twist)
				InstanceArrayBuffer.SetData(twistInstancesToDraw, twistInstanceCount);
			else
				InstanceArrayBuffer.SetData(instancesToDraw, instanceCount);
			InstanceArrayBuffer.Bind();
			Shader.LayoutInstanceArray();

			// bind after the Instance Array Buffer because we should use elemented render
			// ebo is in vertexBuffer
			renderData.VertexBuffer.Bind();
			Shader.LayoutAttributes();

			Game.Renderer.SetBlendMode(BlendMode);

			// draw instance, this is elemented
			Game.Renderer.RenderInstance(0, renderData.Count, drawType == MeshDrawType.Twist ? twistInstanceCount : instanceCount, true);
			Game.Renderer.SetBlendMode(BlendMode.None);
			Game.Renderer.SetFaceCull(FaceCullFunc.None);
		}
	}
}
