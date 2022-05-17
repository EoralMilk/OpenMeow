using System;
using System.IO;
using GlmSharp;
using StbImageSharp;

namespace OpenRA.Graphics
{
	public struct Vertex3D
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
		IVertexBuffer<Vertex3D> vertexBuffer;
		Vertex3D[] vertex3Ds;

		public IVertexBuffer<Vertex3D> CreateVertexBuffer(int length)
		{
			return Game.Renderer.CreateVertexBuffer<Vertex3D>(length);
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
			vertex3Ds = new Vertex3D[vertexCount];

			for (int i = 0; i < vertexCount; i++)
			{
				vec3 pos = vec3.Zero;
				vec3 normal = vec3.Zero;
				vec2 uv = vec2.Zero;
				for (int k = 0; k < 8; k++)
				{
					if (k < 3)
					{
						pos[k] = vv[i * 8 + k];
					}
					else if (k < 6)
					{
						normal[k - 3] = vv[i * 8 + k];
					}
					else
					{
						uv[k - 6] = vv[i * 8 + k];
					}
				}

				Console.WriteLine("i = " + i + "vertexCount = " + vertexCount);
				vertex3Ds[i] = new Vertex3D(pos, normal, uv); ;
			}

			Console.WriteLine("vertex3Ds 完成");

			// ...

		}

		public void DrawTest()
		{
			//vertexBuffer?.Dispose();

			//vertexBuffer = Game.Renderer.CreateVertexBuffer<Vertex3D>(vertex3Ds.Length);
			//vertexBuffer.SetData(vertex3Ds, vertex3Ds.Length);
			return;
		}

		public void Dispose()
		{
			vertexBuffer?.Dispose();
		}
	}
}
