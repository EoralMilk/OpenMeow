using System;
using System.Collections.Generic;
using System.IO;
using OpenRA.FileSystem;
using OpenRA.Graphics;
using OpenRA.Primitives;
using StbImageSharp;
using TrueSync;

namespace OpenRA.Mods.Common.Graphics
{
	public class MeshShaderBindings : IShaderBindings
	{
		public string VertexShaderName { get; }
		public string FragmentShaderName { get; }
		public string GeometryShaderName => null;

		public int Stride => (17 * sizeof(float));

		public IEnumerable<ShaderVertexAttribute> Attributes { get; } = new[]
		{
			new ShaderVertexAttribute("aVertexPos", 0, 3, 0),
			new ShaderVertexAttribute("aNormal", 1, 3, 3 * sizeof(float)),
			new ShaderVertexAttribute("aTexCoords", 2, 2, 6 * sizeof(float)),
			new ShaderVertexAttribute("aDrawPart", 3, 1, 8 * sizeof(float), AttributeType.UInt32),
			new ShaderVertexAttribute("aBoneId", 4, 4, 9 * sizeof(float), AttributeType.Int32),
			new ShaderVertexAttribute("aBoneWeights", 5, 4, 13 * sizeof(float)),
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
			shader.SetMatrix("rotationFix", w3dr.ModelRenderRotationFix.Values1D);
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
		public float t0, t1, t2, t3, t4, t5, t6, t7, t8, t9, t10, t11, t12, t13, t14, t15;
		public float TintX, TintY, TintZ, TintW;
		public float RemapX, RemapY, RemapZ;
		public int DrawId;
		public uint DrawMask;
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
		public static readonly int MaxInstanceCount = 4096;
		public OrderedSkeleton Skeleton { get; set; }
		readonly bool useDQB = false;
		readonly MeshVertexData renderData;
		public IMaterial Material;
		readonly string name;
		public readonly bool IsCloth = false;
		public IMaterial BodyMaterial;
		public string Name => name;

		readonly MeshInstanceData[] instancesToDraw;
		int instanceCount;
		public IVertexBuffer<MeshInstanceData> InstanceArrayBuffer;
		public Rectangle BoundingRec => renderData.BoundingRec;

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

		public OrderedMesh(string name, MeshVertexData data, IMaterial material, IMaterial bodyMaterial, bool useDQB, OrderedSkeleton skeleton)
		{
			IsCloth = true;
			this.name = name;
			this.useDQB = useDQB;
			Skeleton = skeleton;
			renderData = data;
			InstanceArrayBuffer = Game.Renderer.CreateVertexBuffer<MeshInstanceData>(MaxInstanceCount);
			instancesToDraw = new MeshInstanceData[MaxInstanceCount];
			instanceCount = 0;
			Material = material;
			BodyMaterial = bodyMaterial;
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
				renderData.Shader.SetInt("skinBoneCount", Skeleton.SkeletonAsset.SkinBonesIndices.Length);
				renderData.Shader.SetInt("skinBoneTexWidth", SkeletonAsset.AnimTextureWidth);

				renderData.Shader.SetTexture("boneAnimTexture", Skeleton.BoneAnimTexture);
				renderData.Shader.SetMatrix("BindTransformData", Skeleton.BindTransformData, 128);
			}

			renderData.Shader.SetBool("isCloth", IsCloth);
			renderData.Shader.SetBool("usePBR", Material is PBRMaterial);
			if (Material is PBRMaterial)
			{
				Material.SetShader(renderData.Shader, "pbrMaterial");
			}
			else
				Material.SetShader(renderData.Shader, "mainMaterial");
			if (IsCloth)
			{
				renderData.Shader.SetBool("usePBRBody", BodyMaterial is PBRMaterial);
				if (BodyMaterial is PBRMaterial)
				{
					BodyMaterial.SetShader(renderData.Shader, "pbrBodyMaterial");
				}
				else
					BodyMaterial.SetShader(renderData.Shader, "bodyMaterial");
			}

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

			IMaterial material;

			var info = definition.ToDictionary();
			if (info.ContainsKey("Material"))
			{
				// material
				var materialName = info["Material"].Value.Trim();
				if (!cache.HasMaterial(materialName))
				{
					var matReader = new MaterialReader(fileSystem, cache, materialName);

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
			}
			else
			{
				throw new Exception("Mesh " + fields[0].Trim() + " has no Material");
			}

			IMaterial bodyMaterial;
			bool isCloth = false;
			if (info.ContainsKey("BodyMaterial"))
			{
				isCloth = true;

				// body material
				var materialName = info["BodyMaterial"].Value.Trim();
				if (!cache.HasMaterial(materialName))
				{
					var matReader = new MaterialReader(fileSystem, cache, materialName);

					bodyMaterial = matReader.CreateMaterial();
					cache.AddOrGetMaterial(materialName, bodyMaterial);
				}
				else
				{
					bodyMaterial = cache.GetMaterial(materialName);
					if (bodyMaterial == null)
					{
						throw new Exception("Can not Get Body Material from MeshCache");
					}
				}
			}
			else
			{
				isCloth = false;
				bodyMaterial = null;
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

			var useDQB = ReadYamlInfo.LoadField(info, "UseDQBSkin", false);

			if (isCloth)
				mesh = new OrderedMesh(name, meshVertex, material, bodyMaterial, useDQB, skeleton: skeleton);
			else
				mesh = new OrderedMesh(name, meshVertex, material, useDQB, skeleton);

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
		readonly TSVector min, max;
		public MeshReader(Stream s, SkeletonAsset skeleton)
		{
			if (!s.ReadASCII(8).StartsWith("Ora_Mesh"))
				throw new InvalidDataException("Invalid mesh header");

			skeletonType = s.ReadUntil('?');

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
				min.x = TSMath.Min(min.x, X);
				min.y = TSMath.Min(min.y, Y);
				min.z = TSMath.Min(min.z, Z);

				max.x = TSMath.Max(max.x, X);
				max.y = TSMath.Max(max.y, Y);
				max.z = TSMath.Max(max.z, Z);

				U = s.ReadFloat(); V = s.ReadFloat();
				NX = s.ReadFloat(); NY = s.ReadFloat(); NZ = s.ReadFloat();
				RenderMask = s.ReadUInt32();
				BoneId1 = s.ReadInt32(); BoneId2 = s.ReadInt32(); BoneId3 = s.ReadInt32(); BoneId4 = s.ReadInt32();
				BoneWeight1 = s.ReadFloat(); BoneWeight2 = s.ReadFloat(); BoneWeight3 = s.ReadFloat(); BoneWeight4 = s.ReadFloat();

				if (skeleton != null && skeletonType == skeleton.Name)
				{
					if (skinBoneIndexName.ContainsKey(BoneId1))
						BoneId1 = skeleton.GetSkinBoneIdByName(skinBoneIndexName[BoneId1]);
					else if (BoneWeight1 == 0.0f)
						BoneId1 = 0;
					else
						throw new Exception("Not valid mesh data");
					if (skinBoneIndexName.ContainsKey(BoneId2))
						BoneId2 = skeleton.GetSkinBoneIdByName(skinBoneIndexName[BoneId2]);
					else if (BoneWeight2 == 0.0f)
						BoneId2 = 0;
					else
						throw new Exception("Not valid mesh data");
					if (skinBoneIndexName.ContainsKey(BoneId3))
						BoneId3 = skeleton.GetSkinBoneIdByName(skinBoneIndexName[BoneId3]);
					else if (BoneWeight3 == 0.0f)
						BoneId3 = 0;
					else
						throw new Exception("Not valid mesh data");
					if (skinBoneIndexName.ContainsKey(BoneId4))
						BoneId4 = skeleton.GetSkinBoneIdByName(skinBoneIndexName[BoneId4]);
					else if (BoneWeight4 == 0.0f)
						BoneId4 = 0;
					else
						throw new Exception("Not valid mesh data");
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

		public Rectangle CalculateBoundingBox(World3DRenderer w3dr)
		{
			if (w3dr == null)
				throw new Exception("CalculateBoundingBox: need World3DRenderer");
			var r = (int)((max - min).magnitude * w3dr.PixPerMeter) + 1;
			return Rectangle.FromLTRB(-r, -r, r, r);
		}

		public MeshVertexData CreateMeshData()
		{
			IVertexBuffer<MesVertex> vertexBuffer = Game.Renderer.CreateVertexBuffer<MesVertex>(vertexCount);
			vertexBuffer.SetData(vertices, vertexCount);
			vertexBuffer.SetElementData(indices, indicesCount);
			IShader shader = Game.Renderer.GetOrCreateShader<MeshShaderBindings>("MeshShaderBindings");
			MeshVertexData renderData = new MeshVertexData(0, indicesCount, shader, vertexBuffer, CalculateBoundingBox(Game.Renderer.World3DRenderer));
			return renderData;
		}
	}

	public enum MaterialType
	{
		BlinnPhong,
		PBR
	}

	class MaterialReader
	{
		readonly string name;
		readonly MaterialType materialType;

		// Blinn-Phong
		readonly float3 diffuseTint;
		readonly float3 specularTint;
		readonly float shininess;
		readonly string diffMapName;
		readonly string specMapName;
		readonly ITexture diffuseTex;
		readonly ITexture specularTex;
		readonly bool blinn;

		// PBR
		readonly float3 albedoTint;
		readonly float roughness;
		readonly float metallic;
		readonly float ao;
		readonly ITexture albedoTex;
		readonly ITexture roughnessTex;
		readonly ITexture matallicTex;
		readonly ITexture aoTex;

		readonly FaceCullFunc faceCullFunc;
		readonly IReadOnlyFileSystem fileSystem;
		readonly MeshCache cache;
		readonly string filename;
		public MaterialReader(IReadOnlyFileSystem fileSystem, MeshCache cache, string filename)
		{
			this.fileSystem = fileSystem;
			this.cache = cache;
			this.filename = filename;
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

			materialType = ReadYamlInfo.LoadField(info, "Type", MaterialType.BlinnPhong);
			if (materialType == MaterialType.BlinnPhong)
			{
				diffuseTint = ReadYamlInfo.LoadField(info, "DiffuseTint", float3.Ones);
				specularTint = ReadYamlInfo.LoadField(info, "SpecularTint", float3.Ones);
				diffMapName = ReadYamlInfo.LoadField(info, "DiffuseMap", "NO_TEXTURE");
				specMapName = ReadYamlInfo.LoadField(info, "SpecularMap", "NO_TEXTURE");
				shininess = ReadYamlInfo.LoadField(info, "Shininess", 0.0f);
				blinn = ReadYamlInfo.LoadField(info, "Blinn", true);

				if (diffMapName == "NO_TEXTURE")
				{
					diffuseTex = null;
				}
				else
				{
					// texture
					diffMapName = diffMapName.Trim();
					PrepareTexture(diffMapName, out diffuseTex);
				}

				if (specMapName == "NO_TEXTURE")
				{
					specularTex = null;
				}
				else
				{
					// texture
					specMapName = specMapName.Trim();
					PrepareTexture(specMapName, out specularTex);
				}
			}
			else if (materialType == MaterialType.PBR)
			{
				albedoTint = ReadYamlInfo.LoadField(info, "AlbedoTint", float3.Ones);
				roughness = ReadYamlInfo.LoadField(info, "Roughness", 1f);
				metallic = ReadYamlInfo.LoadField(info, "Metallic", 1f);
				ao = ReadYamlInfo.LoadField(info, "AO", 1f);
				var albedoTexName = ReadYamlInfo.LoadField(info, "AlbedoMap", "NO_TEXTURE");
				var roughnessTexName = ReadYamlInfo.LoadField(info, "RoughnessMap", "NO_TEXTURE");
				var metallicTexName = ReadYamlInfo.LoadField(info, "MetallicMap", "NO_TEXTURE");
				var aoTexName = ReadYamlInfo.LoadField(info, "AOMap", "NO_TEXTURE");

				if (albedoTexName == "NO_TEXTURE")
				{
					albedoTex = null;
				}
				else
				{
					// texture
					albedoTexName = albedoTexName.Trim();
					PrepareTexture(albedoTexName, out albedoTex);
				}

				if (roughnessTexName == "NO_TEXTURE")
				{
					roughnessTex = null;
				}
				else
				{
					// texture
					roughnessTexName = roughnessTexName.Trim();
					PrepareTexture(roughnessTexName, out roughnessTex);
				}

				if (metallicTexName == "NO_TEXTURE")
				{
					matallicTex = null;
				}
				else
				{
					// texture
					metallicTexName = metallicTexName.Trim();
					PrepareTexture(metallicTexName, out matallicTex);
				}

				if (aoTexName == "NO_TEXTURE")
				{
					aoTex = null;
				}
				else
				{
					// texture
					aoTexName = aoTexName.Trim();
					PrepareTexture(aoTexName, out aoTex);
				}
			}

			faceCullFunc = ReadYamlInfo.LoadField(info, "FaceCull", FaceCullFunc.Back);

		}

		void PrepareTexture(string name, out ITexture texture)
		{
			if (cache.HasTexture(name))
			{
				texture = cache.GetTexture(name);
			}
			else
			{
				if (!fileSystem.Exists(name))
				{
					throw new Exception(filename + " Can not find texture " + name);
				}

				ImageResult image;
				using (var ss = fileSystem.Open(name))
				{
					image = ImageResult.FromStream(ss, ColorComponents.RedGreenBlueAlpha);
				}

				texture = Game.Renderer.CreateTexture();
				texture.SetData(image.Data, image.Width, image.Height, TextureType.RGBA);
				cache.AddOrGetTexture(name, texture);
			}
		}

		public IMaterial CreateMaterial()
		{
			if (materialType == MaterialType.BlinnPhong)
				return new BlinnPhongMaterial(name, diffuseTex != null, diffuseTint, diffuseTex, specularTex != null, specularTint, specularTex, shininess, faceCullFunc, blinn);
			else if (materialType == MaterialType.PBR)
				return new PBRMaterial(name, albedoTex != null, albedoTint, albedoTex, roughnessTex != null, roughness, roughnessTex, matallicTex != null, metallic, matallicTex, aoTex != null, ao, aoTex, faceCullFunc);
			else
				throw new Exception("Not valid Material Type");
		}
	}

}
