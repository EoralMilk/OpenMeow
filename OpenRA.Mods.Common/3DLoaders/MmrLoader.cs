using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenRA.FileSystem;
using OpenRA.Graphics;
using OpenRA.Primitives;
using TrueSync;

namespace OpenRA.Mods.Common.Graphics
{
	/// <summary>
	/// Meow Mesh Resource
	/// </summary>
	public sealed class MmrLoader : IMeshLoader
	{

		public MmrLoader()
		{

		}

		public bool TryLoadMesh(IReadOnlyFileSystem fileSystem, string filename, MiniYaml definition, MeshCache cache, OrderedSkeleton skeleton, SkeletonAsset skeletonType, out IOrderedMesh mesh)
		{
			var fields = (filename).Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

			// mesh
			var name = fields[0].Trim();

			if (!fileSystem.Exists(name))
				name += ".mmr";

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
				MmrReader reader;
				using (var s = fileSystem.Open(name))
					reader = new MmrReader(s, skeletonType);

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
			var cullFunc = ReadYamlInfo.LoadField(info, "FaceCullFunc", FaceCullFunc.Back);
			var shaderType = ReadYamlInfo.LoadField(info, "Shader", MeshShaderType.Common);

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

			mesh = new OrderedMesh(name, meshVertex, material, cullFunc, useDQB, skeleton, baseMaterial, shaderType);

			return true;
		}

		public void Dispose()
		{

		}
	}

	class MmrReader
	{
		struct VertInfo
		{
			public float3 pos;
			public Dictionary<int, float> vgs;
		}

		readonly MeshVertex[] vertices;
		readonly uint[] indices;
		readonly TSVector min, max;

		const char EndOfStr = '?';
		public MmrReader(Stream s, SkeletonAsset skeleton)
		{
			if (!s.ReadASCII(8).StartsWith("MeowMesh"))
				throw new InvalidDataException("Invalid mesh header");
			var meshVersion = s.ReadUntil(EndOfStr);
			var meshName = s.ReadUntil(EndOfStr);

			// reserve for unforeseen using
			for (int i = 0; i < 20; i++)
				s.ReadInt32();

			// vertex
			int vertexCount = s.ReadInt32();
			VertInfo[] verts = new VertInfo[vertexCount];
			for (int i = 0; i < vertexCount; i++)
			{
				verts[i].pos = new float3(s.ReadFloat(), s.ReadFloat(), s.ReadFloat());
			}

			// all vert group index name
			int vgroupCount = s.ReadInt32();
			string[] vgroupNames = new string[vgroupCount];
			for (int i = 0; i < vgroupCount; i++)
			{
				vgroupNames[i] = s.ReadUntil(EndOfStr);
			}

			if (vgroupCount > 0)
			{
				// all vert vg weight
				for (int i = 0; i < vertexCount; i++)
				{
					var vertWeightCount = s.ReadInt32();
					Dictionary<int, float> vgIdxWeight = new Dictionary<int, float>();
					for (int vgi = 0; vgi < vertWeightCount; vgi++)
					{
						vgIdxWeight.Add(s.ReadInt32(), s.ReadFloat());
					}

					verts[i].vgs = vgIdxWeight;
				}
			}

			// uv
			int uvCount = s.ReadInt32();
			float2[] uvs = new float2[uvCount];
			for (int i = 0; i < uvCount; i++)
			{
				// flip uv y axis for gl
				uvs[i] = new float2(s.ReadFloat(), 1f - s.ReadFloat());
			}

			// normal
			int nmlCount = s.ReadInt32();
			float3[] nmls = new float3[nmlCount];
			for (int i = 0; i < nmlCount; i++)
			{
				nmls[i] = new float3(s.ReadFloat(), s.ReadFloat(), s.ReadFloat());
			}

			// faces
			List<uint> drawIndicesList = new List<uint>();

			// pos, uv, nml, drawmask
			Dictionary<(int, int, int), uint> vertIdxs = new Dictionary<(int, int, int), uint>();

			int faceCount = s.ReadInt32();
			for (int i = 0; i < faceCount; i++)
			{
				int vcount = s.ReadInt32();

				// uint drawType = s.ReadUInt32();
				uint[] vIdxArray = new uint[vcount];

				for (int vi = 0; vi < vcount; vi++)
				{
					int posIdx = s.ReadInt32();
					int uvIdx = s.ReadInt32();
					int nmIdx = s.ReadInt32();
					if (vertIdxs.ContainsKey((posIdx, uvIdx, nmIdx)))
					{
						vIdxArray[vi] = vertIdxs[(posIdx, uvIdx, nmIdx)];
					}
					else
					{
						vIdxArray[vi] = (uint)(vertIdxs.Count);
						vertIdxs.Add((posIdx, uvIdx, nmIdx), vIdxArray[vi]);
					}
				}

				// triangularization
				for (int di = 1; di < vcount - 1; di++)
				{
					drawIndicesList.Add(vIdxArray[0]);
					drawIndicesList.Add(vIdxArray[di]);
					drawIndicesList.Add(vIdxArray[di + 1]);
				}
			}

			indices = drawIndicesList.ToArray();

			vertices = new MeshVertex[vertIdxs.Count];
			foreach (var kv in vertIdxs)
			{
				float X, Y, Z;
				float U, V;
				float NX, NY, NZ;
				int BoneId1, BoneId2, BoneId3, BoneId4;
				float BoneWeight1, BoneWeight2, BoneWeight3, BoneWeight4;

				float3 pos = verts[kv.Key.Item1].pos;
				float2 uv = uvs[kv.Key.Item2];
				float3 nml = nmls[kv.Key.Item3];

				X = pos.X; Y = pos.Y; Z = pos.Z;
				min.x = TSMath.Min(min.x, X);
				min.y = TSMath.Min(min.y, Y);
				min.z = TSMath.Min(min.z, Z);

				max.x = TSMath.Max(max.x, X);
				max.y = TSMath.Max(max.y, Y);
				max.z = TSMath.Max(max.z, Z);

				U = uv.X; V = uv.Y;
				NX = nml.X; NY = nml.Y; NZ = nml.Z;

				// try to limit the bone weights with in 4
				(int, float)[] boneWeights;
				if (verts[kv.Key.Item1].vgs != null)
				{
					boneWeights = new (int, float)[Math.Max(verts[kv.Key.Item1].vgs.Count, 4)];
					int bwCount = 0;
					foreach (var vgw in verts[kv.Key.Item1].vgs)
					{
						int boneId = skeleton?.GetSkinBoneIdByName(vgroupNames[vgw.Key]) ?? -1;

						// is skin vert group ?
						if (boneId != -1 && vgw.Value > 0)
						{
							boneWeights[bwCount] = (boneId, vgw.Value);
							bwCount++;
						}
					}

					for (int i = 0; i < boneWeights.Length; i++)
					{
						if (i >= bwCount)
							boneWeights[i] = (-1, 0f);
					}

					if (bwCount > 4)
					{
						for (int i = 0; i < boneWeights.Length; i++)
							for (int k = i + 1; k < boneWeights.Length; k++)
							{
								if (boneWeights[k].Item2 > boneWeights[i].Item2)
								{
									var temp = boneWeights[i];
									boneWeights[i] = boneWeights[k];
									boneWeights[k] = temp;
								}
							}
					}

					float allWeight = boneWeights[0].Item2 + boneWeights[1].Item2 + boneWeights[2].Item2 + boneWeights[3].Item2;
					boneWeights[0] = (boneWeights[0].Item1, boneWeights[0].Item2 / allWeight);
					boneWeights[1] = (boneWeights[1].Item1, boneWeights[1].Item2 / allWeight);
					boneWeights[2] = (boneWeights[2].Item1, boneWeights[2].Item2 / allWeight);
					boneWeights[3] = (boneWeights[3].Item1, boneWeights[3].Item2 / allWeight);
				}
				else
				{
					boneWeights = new (int, float)[4];
					for (int i = 0; i < boneWeights.Length; i++)
					{
						boneWeights[i] = (-1, 0f);
					}
				}

				BoneId1 = boneWeights[0].Item1; BoneId2 = boneWeights[1].Item1; BoneId3 = boneWeights[2].Item1; BoneId4 = boneWeights[3].Item1;
				BoneWeight1 = boneWeights[0].Item2; BoneWeight2 = boneWeights[1].Item2; BoneWeight3 = boneWeights[2].Item2; BoneWeight4 = boneWeights[3].Item2;

				vertices[kv.Value] = new MeshVertex(X, Y, Z, NX, NY, NZ, U, V,
					BoneId1, BoneId2, BoneId3, BoneId4,
					BoneWeight1, BoneWeight2, BoneWeight3, BoneWeight4);
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
			IVertexBuffer<MeshVertex> vertexBuffer = Game.Renderer.CreateVertexBuffer<MeshVertex>(vertices.Length);
			vertexBuffer.SetData(vertices, vertices.Length);
			vertexBuffer.SetElementData(indices, indices.Length);
			MeshVertexData renderData = new MeshVertexData(0, indices.Length, vertexBuffer, CalculateBoundingBox(Game.Renderer.World3DRenderer));
			return renderData;
		}
	}
}
