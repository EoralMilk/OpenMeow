using System;
using System.Collections.Generic;
using System.IO;
using OpenRA.FileSystem;
using OpenRA.Graphics;
using OpenRA.Primitives;
using TrueSync;

namespace OpenRA.Mods.Common.Graphics
{
	public sealed class MesLoader : IMeshLoader
	{

		public MesLoader()
		{

		}

		public bool TryLoadMesh(IReadOnlyFileSystem fileSystem, string filename, MiniYaml definition, MeshCache cache, OrderedSkeleton skeleton, SkeletonAsset skeletonType, out IOrderedMesh mesh)
		{
			var fields = (filename).Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

			// mesh
			var name = fields[0].Trim();

			if (!fileSystem.Exists(name))
				name += ".mes";

			if (!fileSystem.Exists(name))
			{
				mesh = null;
				return false;
			}

			IMaterial material;

			var info = definition.ToDictionary();
			if (info.ContainsKey("Material"))
			{
				// material file name
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

			IMaterial baseMaterial;
			if (info.ContainsKey("BaseMaterial"))
			{
				// base material file name
				var materialName = info["BaseMaterial"].Value.Trim();
				if (!cache.HasMaterial(materialName))
				{
					var matReader = new MaterialReader(fileSystem, cache, materialName);

					baseMaterial = matReader.CreateMaterial();
					cache.AddOrGetMaterial(materialName, baseMaterial);
				}
				else
				{
					baseMaterial = cache.GetMaterial(materialName);
					if (baseMaterial == null)
					{
						throw new Exception("Can not GetMaterial from MeshCache");
					}
				}
			}
			else
			{
				baseMaterial = null;
			}

			OrderedMeshInfo orderedMeshInfo = new OrderedMeshInfo()
			{
				UseDQB = ReadYamlInfo.LoadField(info, "UseDQBSkin", false),
				ShaderType = ReadYamlInfo.LoadField(info, "Shader", MeshShaderType.Common),
				DefaultMaterial = material,
				BaseMaterial = baseMaterial,
				FaceCullFunc = ReadYamlInfo.LoadField(info, "FaceCullFunc", FaceCullFunc.Back),
				BlendMode = ReadYamlInfo.LoadField(info, "BlendMode", BlendMode.None),
				HasShadow = ReadYamlInfo.LoadField(info, "HasShadow", true),
				DrawType = ReadYamlInfo.LoadField(info, "MeshDrawType", MeshDrawType.Actor),
			};

			mesh = new OrderedMesh(name, meshVertex, skeleton, orderedMeshInfo);

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
		readonly MeshVertex[] vertices;
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

			vertices = new MeshVertex[vertexCount];
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

				vertices[i] = new MeshVertex(X, Y, Z, NX, NY, NZ, U, V,
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
			IVertexBuffer<MeshVertex> vertexBuffer = Game.Renderer.CreateVertexBuffer<MeshVertex>(vertexCount);
			vertexBuffer.SetData(vertices, vertexCount);
			vertexBuffer.SetElementData(indices, indicesCount);
			MeshVertexData renderData = new MeshVertexData(0, indicesCount, vertexBuffer, CalculateBoundingBox(Game.Renderer.World3DRenderer));
			return renderData;
		}
	}
}
