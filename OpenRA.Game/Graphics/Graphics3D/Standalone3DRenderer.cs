using System;
using System.IO;
using GlmSharp;
using StbImageSharp;

namespace OpenRA.Graphics
{
	struct Vertex3D
	{
		public vec3 Position;
		public vec3 Normal;
		public vec2 TexCoords;

		// public uint[] BoneId;
		// public float[] BoneWeight;
		public Vertex3D(vec3 p, vec3 n, vec2 uv)
		{
			Position = p;
			Normal = n;
			TexCoords = uv;
		}

		public static Vertex3D Default = new Vertex3D(vec3.Zero, vec3.Zero, vec2.Zero);
	}

	public class Standalone3DRenderer : IDisposable
	{
		//IVertexBuffer<Vertex3D> vertexBuffer;

		public IVertexBuffer<Vertex> CreateVertexBuffer(int length)
		{
			return Game.Renderer.Context.CreateVertexBuffer(length);
		}

		public Standalone3DRenderer()
		{
			ImageResult image;
			using (var stream = File.OpenRead("./texture/container.jpg"))
			{
				image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
			}

			float[] vv =
				{
				-0.5f, -0.5f, -0.5f,  0.0f,  0.0f, -1.0f,  0.0f, 0.0f,
				0.5f, -0.5f, -0.5f,  0.0f,  0.0f, -1.0f,  1.0f, 0.0f,
				0.5f,  0.5f, -0.5f,  0.0f,  0.0f, -1.0f,  1.0f, 1.0f,
				0.5f,  0.5f, -0.5f,  0.0f,  0.0f, -1.0f,  1.0f, 1.0f,
				-0.5f,  0.5f, -0.5f,  0.0f,  0.0f, -1.0f,  0.0f, 1.0f,
				-0.5f, -0.5f, -0.5f,  0.0f,  0.0f, -1.0f,  0.0f, 0.0f,

				-0.5f, -0.5f,  0.5f,  0.0f,  0.0f, 1.0f,   0.0f, 0.0f,
				0.5f, -0.5f,  0.5f,  0.0f,  0.0f, 1.0f,   1.0f, 0.0f,
				0.5f,  0.5f,  0.5f,  0.0f,  0.0f, 1.0f,   1.0f, 1.0f,
				0.5f,  0.5f,  0.5f,  0.0f,  0.0f, 1.0f,   1.0f, 1.0f,
				-0.5f,  0.5f,  0.5f,  0.0f,  0.0f, 1.0f,   0.0f, 1.0f,
				-0.5f, -0.5f,  0.5f,  0.0f,  0.0f, 1.0f,   0.0f, 0.0f,

				-0.5f,  0.5f,  0.5f, -1.0f,  0.0f,  0.0f,  1.0f, 0.0f,
				-0.5f,  0.5f, -0.5f, -1.0f,  0.0f,  0.0f,  1.0f, 1.0f,
				-0.5f, -0.5f, -0.5f, -1.0f,  0.0f,  0.0f,  0.0f, 1.0f,
				-0.5f, -0.5f, -0.5f, -1.0f,  0.0f,  0.0f,  0.0f, 1.0f,
				-0.5f, -0.5f,  0.5f, -1.0f,  0.0f,  0.0f,  0.0f, 0.0f,
				-0.5f,  0.5f,  0.5f, -1.0f,  0.0f,  0.0f,  1.0f, 0.0f,

				0.5f,  0.5f,  0.5f,  1.0f,  0.0f,  0.0f,  1.0f, 0.0f,
				0.5f,  0.5f, -0.5f,  1.0f,  0.0f,  0.0f,  1.0f, 1.0f,
				0.5f, -0.5f, -0.5f,  1.0f,  0.0f,  0.0f,  0.0f, 1.0f,
				0.5f, -0.5f, -0.5f,  1.0f,  0.0f,  0.0f,  0.0f, 1.0f,
				0.5f, -0.5f,  0.5f,  1.0f,  0.0f,  0.0f,  0.0f, 0.0f,
				0.5f,  0.5f,  0.5f,  1.0f,  0.0f,  0.0f,  1.0f, 0.0f,

				-0.5f, -0.5f, -0.5f,  0.0f, -1.0f,  0.0f,  0.0f, 1.0f,
				0.5f, -0.5f, -0.5f,  0.0f, -1.0f,  0.0f,  1.0f, 1.0f,
				0.5f, -0.5f,  0.5f,  0.0f, -1.0f,  0.0f,  1.0f, 0.0f,
				0.5f, -0.5f,  0.5f,  0.0f, -1.0f,  0.0f,  1.0f, 0.0f,
				-0.5f, -0.5f,  0.5f,  0.0f, -1.0f,  0.0f,  0.0f, 0.0f,
				-0.5f, -0.5f, -0.5f,  0.0f, -1.0f,  0.0f,  0.0f, 1.0f,

				-0.5f,  0.5f, -0.5f,  0.0f,  1.0f,  0.0f,  0.0f, 1.0f,
				0.5f,  0.5f, -0.5f,  0.0f,  1.0f,  0.0f,  1.0f, 1.0f,
				0.5f,  0.5f,  0.5f,  0.0f,  1.0f,  0.0f,  1.0f, 0.0f,
				0.5f,  0.5f,  0.5f,  0.0f,  1.0f,  0.0f,  1.0f, 0.0f,
				-0.5f,  0.5f,  0.5f,  0.0f,  1.0f,  0.0f,  0.0f, 0.0f,
				-0.5f,  0.5f, -0.5f,  0.0f,  1.0f,  0.0f,  0.0f, 1.0f
				};
			int vertexCount = vv.Length / 8;
			Vertex3D[] vertex3Ds = new Vertex3D[vertexCount];

			for (int i = 0; i < vertexCount; i++)
			{
				Vertex3D tempVertex = Vertex3D.Default;
				for (int k = 0; k < 8; k++)
				{
					if (k < 3)
					{
						tempVertex.Position[k] = vv[i * 8 + k];
					}
					else if (k < 6)
					{
						tempVertex.Normal[k - 3] = vv[i * 8 + k];
					}
					else
					{
						tempVertex.TexCoords[k - 6] = vv[i * 8 + k];
					}
				}

				vertex3Ds[i] = tempVertex;
			}

		// ...

		}

		public void DrawTest()
		{
			return;
		}

		public void Dispose()
		{
			throw new NotImplementedException();
		}
	}
}
