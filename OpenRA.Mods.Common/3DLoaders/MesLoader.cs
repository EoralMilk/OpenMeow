using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenRA.FileSystem;
using OpenRA.Graphics;
using OpenRA.Graphics.Graphics3D;
using OpenRA.Primitives;
using StbImageSharp;

namespace OpenRA.Mods.Common.Graphics
{
	public class MeshShaderBindings : IShaderBindings
	{
		public string VertexShaderName { get; }
		public string FragmentShaderName { get; }
		public int Stride => (17 * sizeof(float));

		public IEnumerable<ShaderVertexAttribute> Attributes { get; } = new[]
		{
			new ShaderVertexAttribute("aVertexPos", 0, 3, 0),
			new ShaderVertexAttribute("aNormal", 1, 3, 3 * sizeof(float)),
			new ShaderVertexAttribute("aTexCoords", 2, 2, 6 * sizeof(float)),
			new ShaderVertexAttribute("aDrawPart", 3, 1, 8 * sizeof(float), AttributeType.UInt32),
			new ShaderVertexAttribute("aBoneId", 4, 4, 9 * sizeof(float), AttributeType.Int32),
			new ShaderVertexAttribute("aBoneWidget", 5, 4, 13 * sizeof(float)),
		};
		public bool Instanced => true;

		public int InstanceStrde => 25 * sizeof(float);

		public IEnumerable<ShaderVertexAttribute> InstanceAttributes { get; } = new[]
		{
			new ShaderVertexAttribute("iModelV1", 6, 4, 0),
			new ShaderVertexAttribute("iModelV2", 7, 4, 4 * sizeof(float)),
			new ShaderVertexAttribute("iModelV3", 8, 4, 8 * sizeof(float)),
			new ShaderVertexAttribute("iModelV4", 9, 4, 12 * sizeof(float)),
			new ShaderVertexAttribute("iTint", 10, 4, 16 * sizeof(float)),
			new ShaderVertexAttribute("iRemap", 11, 3, 20 * sizeof(float)),

			new ShaderVertexAttribute("iDrawId", 12, 1, 23 * sizeof(float), AttributeType.Int32),
			new ShaderVertexAttribute("iDrawMask", 13, 1, 24 * sizeof(float), AttributeType.UInt32),
		};

		public MeshShaderBindings()
		{
			string name = "shader";
			VertexShaderName = name;
			FragmentShaderName = name;
		}

		public void SetCommonParaments(IShader shader, World3DRenderer w3dr, bool sunCamera)
		{
			shader.SetBool("RenderDepthBuffer", sunCamera);
			shader.SetMatrix("rotationFix", w3dr.ModelRotationFix.Values1D);
			if (sunCamera)
			{
				shader.SetMatrix("projection", w3dr.SunProjection.Values1D);
				shader.SetMatrix("view", w3dr.SunView.Values1D);
				shader.SetVec("viewPos", w3dr.SunPos.x, w3dr.SunPos.y, w3dr.SunPos.z);
			}
			else
			{
				shader.SetMatrix("projection", w3dr.Projection.Values1D);
				shader.SetMatrix("view", w3dr.View.Values1D);
				shader.SetVec("viewPos", w3dr.CameraPos.x, w3dr.CameraPos.y, w3dr.CameraPos.z);
			}

			shader.SetVec("dirLight.direction", w3dr.SunDir.x, w3dr.SunDir.y, w3dr.SunDir.z);
			shader.SetVec("dirLight.ambient", w3dr.AmbientColor.X, w3dr.AmbientColor.Y, w3dr.AmbientColor.Z);
			shader.SetVec("dirLight.diffuse", w3dr.SunColor.X, w3dr.SunColor.Y, w3dr.SunColor.Z);
			shader.SetVec("dirLight.specular", w3dr.SunSpecularColor.X, w3dr.SunSpecularColor.Y, w3dr.SunSpecularColor.Z);
		}
	}

	public struct MesVertex
	{
		public readonly float X, Y, Z;
		public readonly float NX, NY, NZ;
		public readonly float U, V;
		public readonly uint RenderMask;
		public readonly int BoneId1, BoneId2, BoneId3, BoneId4;
		public readonly float BoneWeight1, BoneWeight2, BoneWeight3, BoneWeight4;

		public MesVertex(float x, float y, float z, float nx, float ny, float nz, float u, float v, uint renderMask,
			int id1, int id2, int id3, int id4,
			float bw1, float bw2, float bw3, float bw4)
		{
			X = x;
			Y = y;
			Z = z;
			NX = nx;
			NY = ny;
			NZ = nz;
			U = u;
			V = v;
			RenderMask = renderMask;
			BoneId1 = id1;
			BoneId2 = id2;
			BoneId3 = id3;
			BoneId4 = id4;
			BoneWeight1 = bw1;
			BoneWeight2 = bw2;
			BoneWeight3 = bw3;
			BoneWeight4 = bw4;
		}
	};

	public struct MeshInstanceData
	{
		float t0, t1, t2, t3, t4, t5, t6, t7, t8, t9, t10, t11, t12, t13, t14, t15;
		float TintX, TintY, TintZ, TintW;
		float RemapX, RemapY, RemapZ;
		int DrawId;
		uint DrawMask;
		public MeshInstanceData(in float[] data)
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

			DrawId = -1;
			DrawMask = 0xFFFFFFFF;
		}

		public MeshInstanceData(in float[] data, int id, uint mask)
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
			DrawMask = mask;
		}
	}

	class OrderedMesh : IOrderedMesh
	{
		public static readonly int MaxInstanceCount = 1024;

		public OrderedSkeleton Skeleton { get; set; }
		readonly bool useDQB = false;
		readonly MeshVertexData renderData;
		public IMaterial Material;
		readonly string name;
		public string Name => name;

		readonly MeshInstanceData[] instancesToDraw;
		int instanceCount;
		public IVertexBuffer<MeshInstanceData> InstanceArrayBuffer;

		public OrderedMesh(string name, MeshVertexData data, IMaterial material, bool useDQB, OrderedSkeleton skeleton)
		{
			this.name = name;
			this.useDQB = useDQB;
			Skeleton = skeleton;
			renderData = data;
			InstanceArrayBuffer = Game.Renderer.CreateVertexBuffer<MeshInstanceData>(MaxInstanceCount);
			instancesToDraw = new MeshInstanceData[MaxInstanceCount];
			instanceCount = 0;
			Material = material;
		}

		public void AddInstanceData(in float[] data, int dataCount, in int[] dataInt, int dataIntCount)
		{
			if (instanceCount == MaxInstanceCount)
				throw new Exception("Instance Count bigger than MaxInstanceCount");

			if (dataCount != 23 || dataIntCount != 2)
				throw new Exception("AddInstanceData params length unright");

			MeshInstanceData instanceData = new MeshInstanceData(data, dataInt[0], (uint)dataInt[1]);
			instancesToDraw[instanceCount] = instanceData;
			instanceCount++;
		}

		public void Flush()
		{
			instanceCount = 0;
		}

		public void SetPalette(ITexture pal)
		{
			//palette = pal;
		}

		public void DrawInstances()
		{
			if (instanceCount == 0)
				return;

			Game.Renderer.SetFaceCull(Material.FaceCullFunc);
			if (Skeleton != null)
			{
				renderData.Shader.SetBool("useDQB", useDQB);
				renderData.Shader.SetTexture("boneAnimTexture", Skeleton.BoneAnimTexture);
				renderData.Shader.SetMatrix("BindTransformData", Skeleton.SkeletonAsset.BindTransformData, 128);
			}

			Material.SetShader(renderData.Shader);

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

	public sealed class MesLoader : IMeshLoader
	{

		public MesLoader()
		{

		}

		public bool TryLoadMesh(IReadOnlyFileSystem fileSystem, string filename, MiniYaml definition, MeshCache cache, OrderedSkeleton skeleton, SkeletonAsset skeletonType, out IOrderedMesh mesh)
		{
			var fields = (filename).Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

			if (fields.Length <= 1)
			{
				throw new Exception(filename + " Need Material (sequence: MeshName, Material)");
			}

			// material
			var materialName = fields[1].Trim();
			IMaterial material;
			if (!cache.HasMaterial(materialName))
			{
				var matReader = new MaterialReader(fileSystem, materialName);

				material = matReader.CreateMaterial();
				cache.AddOrGetMaterial(materialName, material);
			}
			else
			{
				material = cache.GetMaterial(materialName);
				if (material == null)
				{
					throw new Exception("Can not GetMaterial from MeshCache");
				}
			}

			// mesh
			var name = fields[0].Trim();

			if (!fileSystem.Exists(name))
				name += ".mes";

			if (!fileSystem.Exists(name))
			{
				mesh = null;
				return false;
			}

			// key should be name + skeletonType because the skin info can be modified by skeletonType
			var dictKey = name + (skeletonType != null ? skeletonType.Name : "");

			MeshVertexData meshVertex;

			if (!cache.HasMeshData(dictKey))
			{
				MeshReader reader;
				using (var s = fileSystem.Open(name))
					reader = new MeshReader(s, skeletonType);

				meshVertex = reader.CreateMeshData();
				cache.AddOrGetMeshData(dictKey, meshVertex);
			}
			else
			{
				meshVertex = cache.GetMeshData(dictKey);
				if (meshVertex == null)
				{
					throw new Exception("Can not GetMeshData from MeshCache");
				}
			}

			mesh = new OrderedMesh(name, meshVertex, material, false, skeleton);

			return true;
		}

		public void Dispose()
		{

		}
	}

	class MeshReader
	{
		readonly int vertexCount;
		readonly int indicesCount;
		readonly int boneCount;

		readonly string skeletonType;
		readonly MesVertex[] vertices;
		readonly uint[] indices;

		public MeshReader(Stream s, SkeletonAsset skeleton)
		{
			if (!s.ReadASCII(8).StartsWith("Ora_Mesh"))
				throw new InvalidDataException("Invalid mesh header");

			skeletonType = s.ReadUntil('?');

			Console.WriteLine("skinBoneIndexName using to match skin to skeleton, still in WIP");
			Dictionary<int, string> skinBoneIndexName = new Dictionary<int, string>();

			if (skeletonType != "null_skeleton")
			{
				int skinBoneCount = s.ReadInt32();
				for (int i = 0; i < skinBoneCount; ++i)
				{
					int skinBoneIndex = s.ReadInt32();
					string skinBoneName = s.ReadUntil('?');
					skinBoneIndexName.Add(skinBoneIndex, skinBoneName);
				}
			}

			vertexCount = s.ReadInt32();
			indicesCount = s.ReadInt32();
			boneCount = s.ReadInt32();

			vertices = new MesVertex[vertexCount];
			indices = new uint[indicesCount];

			// vertices
			for (int i = 0; i < vertexCount; i++)
			{
				float X, Y, Z;
				float U, V;
				float NX, NY, NZ;
				uint RenderMask;
				int BoneId1, BoneId2, BoneId3, BoneId4;
				float BoneWeight1, BoneWeight2, BoneWeight3, BoneWeight4;

				X = s.ReadFloat(); Y = s.ReadFloat(); Z = s.ReadFloat();
				U = s.ReadFloat(); V = s.ReadFloat();
				NX = s.ReadFloat(); NY = s.ReadFloat(); NZ = s.ReadFloat();
				RenderMask = s.ReadUInt32();
				BoneId1 = s.ReadInt32(); BoneId2 = s.ReadInt32(); BoneId3 = s.ReadInt32(); BoneId4 = s.ReadInt32();
				BoneWeight1 = s.ReadFloat(); BoneWeight2 = s.ReadFloat(); BoneWeight3 = s.ReadFloat(); BoneWeight4 = s.ReadFloat();

				if (skeleton != null && skeletonType == skeleton.Name)
				{
					BoneId1 = skeleton.GetSkinBoneIdByName(skinBoneIndexName[BoneId1]);
					BoneId2 = skeleton.GetSkinBoneIdByName(skinBoneIndexName[BoneId2]);
					BoneId3 = skeleton.GetSkinBoneIdByName(skinBoneIndexName[BoneId3]);
					BoneId4 = skeleton.GetSkinBoneIdByName(skinBoneIndexName[BoneId4]);
				}

				vertices[i] = new MesVertex(X, Y, Z, NX, NY, NZ, U, V, RenderMask,
					BoneId1, BoneId2, BoneId3, BoneId4,
					BoneWeight1, BoneWeight2, BoneWeight3, BoneWeight4);
			}

			// indices
			for (int i = 0; i < indicesCount; i++)
			{
				indices[i] = (uint)(s.ReadInt32());
			}
		}

		public MeshVertexData CreateMeshData()
		{
			IVertexBuffer<MesVertex> vertexBuffer = Game.Renderer.CreateVertexBuffer<MesVertex>(vertexCount);
			vertexBuffer.SetData(vertices, vertexCount);
			vertexBuffer.SetElementData(indices, indicesCount);
			IShader shader = Game.Renderer.GetOrCreateShader<MeshShaderBindings>("MeshShaderBindings");
			MeshVertexData renderData = new MeshVertexData(0, indicesCount, shader, vertexBuffer);
			return renderData;
		}
	}

	class MaterialReader
	{
		readonly string name;
		readonly float3 diffuseTint;
		readonly float3 specularTint;
		readonly float shininess;
		readonly ITexture diffuseTex;
		readonly ITexture specularTex;
		readonly FaceCullFunc faceCullFunc;
		public MaterialReader(IReadOnlyFileSystem fileSystem, string filename)
		{
			if (!fileSystem.Exists(filename))
				filename += ".mat";

			if (!fileSystem.Exists(filename))
				throw new Exception("Can not find Material");

			List<MiniYamlNode> nodes = MiniYaml.FromStream(fileSystem.Open(filename));

			if (nodes.Count > 1)
			{
				throw new InvalidDataException("Invalid Material Node: Too Many Nodes!");
			}

			var node = nodes[0];
			var info = node.Value.ToDictionary();

			name = node.Key;
			diffuseTint = LoadField(info, "DiffuseTint", float3.Ones);
			specularTint = LoadField(info, "SpecularTint", float3.Ones);
			string diffMapName = LoadField(info, "DiffuseMap", "NO_TEXTURE");
			string specMapName = LoadField(info, "SpecularMap", "NO_TEXTURE");
			shininess = LoadField(info, "Shininess", 0.0f);
			faceCullFunc = LoadField(info, "FaceCull", FaceCullFunc.Front);

			if (diffMapName == "NO_TEXTURE")
			{
				diffuseTex = null;
			}
			else
			{
				// texture
				diffMapName = diffMapName.Trim();
				if (!fileSystem.Exists(diffMapName))
				{
					throw new Exception(filename + " Can not find texture " + diffMapName);
				}

				ImageResult image;
				using (var ss = fileSystem.Open(diffMapName))
				{
					image = ImageResult.FromStream(ss, ColorComponents.RedGreenBlueAlpha);
				}

				diffuseTex = Game.Renderer.CreateTexture();
				diffuseTex.SetData(image.Data, image.Width, image.Height, TextureType.RGBA);
			}

			if (specMapName == "NO_TEXTURE")
			{
				specularTex = null;
			}
			else
			{
				// texture
				specMapName = specMapName.Trim();
				if (!fileSystem.Exists(specMapName))
				{
					throw new Exception(filename + " Can not find texture " + specMapName);
				}

				ImageResult image;
				using (var ss = fileSystem.Open(specMapName))
				{
					image = ImageResult.FromStream(ss, ColorComponents.RedGreenBlueAlpha);
				}

				specularTex = Game.Renderer.CreateTexture();
				specularTex.SetData(image.Data, image.Width, image.Height, TextureType.RGBA);
			}
		}

		public CommonMaterial CreateMaterial()
		{
			return new CommonMaterial(name, diffuseTex != null, diffuseTint, diffuseTex, specularTex != null, specularTint, specularTex, shininess, faceCullFunc);
		}

		T LoadField<T>(Dictionary<string, MiniYaml> d, string key, T fallback)
		{
			if (d.TryGetValue(key, out var value))
				return FieldLoader.GetValue<T>(key, value.Value);

			return fallback;
		}
	}

}
