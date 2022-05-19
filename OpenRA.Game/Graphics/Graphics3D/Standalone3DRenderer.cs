using System;
using System.Collections.Generic;
using System.IO;
using GlmSharp;
using StbImageSharp;

namespace OpenRA.Graphics
{
	public class MyShaderBindings : IShaderBindings
	{
		public string VertexShaderName { get; }
		public string FragmentShaderName { get; }
		public int Stride => (8 * sizeof(float));

		public IEnumerable<ShaderVertexAttribute> Attributes { get; } = new[]
		{
			new ShaderVertexAttribute("aPos", 0, 3, 0),
			new ShaderVertexAttribute("aNormal", 1, 3, 3 * sizeof(float)),
			new ShaderVertexAttribute("aTexCoords", 2, 2, 6 * sizeof(float)),
		};

		public MyShaderBindings()
		{
			string name = "myshader";
			VertexShaderName = name;
			FragmentShaderName = name;
		}

		public void SetRenderData(IShader shader, ModelRenderData renderData)
		{
			foreach (var (name, texture) in renderData.Textures)
				shader.SetTexture(name, texture);
		}
	}

	public struct Vertex3D
	{
		public readonly float X, Y, Z;
		public readonly float NX, NY, NZ;
		public readonly float U, V;

		// public uint[] BoneId;
		// public float[] BoneWeight;
		public Vertex3D(vec3 p, vec3 n, vec2 uv)
		{
			X = p.x;
			Y = p.y;
			Z = p.z;
			NX = n.x;
			NY = n.y;
			NZ = n.z;
			U = uv.x;
			V = uv.y;
		}

		public static Vertex3D Default = new Vertex3D(vec3.Zero, vec3.Zero, vec2.Zero);
	}

	public class Standalone3DRenderer : IDisposable
	{
		public vec3 WorldUp;
		public vec3 CameraHorizontalFront;
		public int WPosPerMeter = 256;
		public vec3 CameraPos { get; private set; }
		readonly float meterPerPix;
		readonly float meterPerPixHalf;
		mat4 projection;
		mat4 view;
		mat4 camera;
		float hight = 102400;

		IVertexBuffer<Vertex3D> vertexBuffer;
		Vertex3D[] vertex3Ds;
		readonly Renderer renderer;
		readonly IShader shader;
		ITexture diffuseTexBuffer;
		ITexture specularTexBuffer;

		mat4 model = mat4.Identity;
		public WPos TestPos = WPos.Zero;

		ImageResult image, image2;
		uint tick = 0;
		public IVertexBuffer<Vertex3D> CreateVertexBuffer(int length)
		{
			return Game.Renderer.CreateVertexBuffer<Vertex3D>(length);
		}

		public static mat4 LookAt(vec3 eye, vec3 center, vec3 up)
		{
			var f = (center - eye).Normalized;
			var s = vec3.Cross(f, up).Normalized;
			var u = vec3.Cross(s, f);
			var m = mat4.Identity;
			m.m00 = s.x;
			m.m10 = s.y;
			m.m20 = s.z;
			m.m01 = u.x;
			m.m11 = u.y;
			m.m21 = u.z;
			m.m02 = -f.x;
			m.m12 = -f.y;
			m.m22 = -f.z;
			m.m30 = -vec3.Dot(s, eye);
			m.m31 = -vec3.Dot(u, eye);
			m.m32 = vec3.Dot(f, eye);
			return m;
		}

		public Standalone3DRenderer(Renderer renderer)
		{
			var tilleWidthPix = Game.ModData.Manifest.Get<MapGrid>().TileSize.Width;
			meterPerPix = (float)((1024 / WPosPerMeter) / (tilleWidthPix / 1.4142135d));
			meterPerPixHalf = meterPerPix / 2.0f;

			this.renderer = renderer;
			this.shader = renderer.Context.CreateUnsharedShader<MyShaderBindings>();

			using (var stream = File.OpenRead("./texture/container.png"))
			{
				image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
			}

			using (var stream = File.OpenRead("./texture/container2_specular.png"))
			{
				image2 = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
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

				//Console.WriteLine("i = " + i + "vertexCount = " + vertexCount);
				vertex3Ds[i] = new Vertex3D(pos, normal, uv); ;
			}

			shader.SetInt("material.diffuse", 0);
			shader.SetFloat("material.shininess", 32.0f);

			// 平行光
			shader.SetVec("dirLight.direction", -0.2f, -1.0f, -0.3f);
			shader.SetVec("dirLight.ambient", 0.05f, 0.05f, 0.05f);
			shader.SetVec("dirLight.diffuse", 0.35f, 0.35f, 0.35f);
			shader.SetVec("dirLight.specular", 0.5f, 0.5f, 0.5f);

			vec3[] pointLightPositions = {
				new vec3(0.7f,  0.2f,  2.0f),
				new vec3(2.3f, -3.3f, -4.0f),
				new vec3(-4.0f,  2.0f, -12.0f),
				new vec3(0.0f,  0.0f, -3.0f)
			};

			// point light 1
			shader.SetVec("pointLights[0].position", pointLightPositions[0][0], pointLightPositions[0][1], pointLightPositions[0][2]);
			shader.SetVec("pointLights[0].ambient", 0.05f, 0.05f, 0.05f);
			shader.SetVec("pointLights[0].diffuse", 0.8f, 0.8f, 0.8f);
			shader.SetVec("pointLights[0].specular", 1.0f, 1.0f, 1.0f);
			shader.SetFloat("pointLights[0].constant", 1.0f);
			shader.SetFloat("pointLights[0].linear", 0.09f);
			shader.SetFloat("pointLights[0].quadratic", 0.032f);
			// point light 2
			shader.SetVec("pointLights[1].position", pointLightPositions[1][0], pointLightPositions[1][1], pointLightPositions[1][2]);
			shader.SetVec("pointLights[1].ambient", 0.05f, 0.05f, 0.05f);
			shader.SetVec("pointLights[1].diffuse", 0.8f, 0.8f, 0.8f);
			shader.SetVec("pointLights[1].specular", 1.0f, 1.0f, 1.0f);
			shader.SetFloat("pointLights[1].constant", 1.0f);
			shader.SetFloat("pointLights[1].linear", 0.09f);
			shader.SetFloat("pointLights[1].quadratic", 0.032f);
			// point light 3
			shader.SetVec("pointLights[2].position", pointLightPositions[2][0], pointLightPositions[2][1], pointLightPositions[2][2]);
			shader.SetVec("pointLights[2].ambient", 0.05f, 0.05f, 0.05f);
			shader.SetVec("pointLights[2].diffuse", 0.8f, 0.8f, 0.8f);
			shader.SetVec("pointLights[2].specular", 1.0f, 1.0f, 1.0f);
			shader.SetFloat("pointLights[2].constant", 1.0f);
			shader.SetFloat("pointLights[2].linear", 0.09f);
			shader.SetFloat("pointLights[2].quadratic", 0.032f);
			// point light 4
			shader.SetVec("pointLights[3].position", pointLightPositions[3][0], pointLightPositions[3][1], pointLightPositions[3][2]);
			shader.SetVec("pointLights[3].ambient", 0.05f, 0.05f, 0.05f);
			shader.SetVec("pointLights[3].diffuse", 0.8f, 0.8f, 0.8f);
			shader.SetVec("pointLights[3].specular", 1.0f, 1.0f, 1.0f);
			shader.SetFloat("pointLights[3].constant", 1.0f);
			shader.SetFloat("pointLights[3].linear", 0.09f);
			shader.SetFloat("pointLights[3].quadratic", 0.032f);

			// spotLight
			shader.SetVec("spotLight.position", 0,0,3);
			shader.SetVec("spotLight.direction", 0,0,-1);
			shader.SetVec("spotLight.ambient", 0.0f, 0.0f, 0.0f);
			shader.SetVec("spotLight.diffuse", 2.0f, 2.0f, 2.0f);
			shader.SetVec("spotLight.specular", 1.0f, 1.0f, 1.0f);
			shader.SetFloat("spotLight.constant", 1.0f);
			shader.SetFloat("spotLight.linear", 0.09f);
			shader.SetFloat("spotLight.quadratic", 0.032f);
			shader.SetFloat("spotLight.cutOff", glm.Cos(glm.Radians(7.5f)));
			shader.SetFloat("spotLight.outerCutOff", glm.Cos(glm.Radians(10.0f)));
			// ...

			WorldUp = new vec3(0, 0, 1);
			CameraHorizontalFront = new vec3(0, -1, 0);
			projection = mat4.Identity;
			view = mat4.Identity;
			camera = mat4.Rotate(glm.Radians(30.0f), new vec3(-1, 0, 0)) * mat4.LookAt(vec3.Zero, CameraHorizontalFront, WorldUp);

		}

		public void DrawTest(Viewport viewport)
		{
			projection = mat4.Ortho(-viewport.ViewportSize.X * meterPerPixHalf, viewport.ViewportSize.X * meterPerPixHalf,
				-viewport.ViewportSize.Y * meterPerPixHalf, viewport.ViewportSize.Y * meterPerPixHalf);

			CameraPos = new vec3(viewport.CenterPosition.X / WPosPerMeter, (viewport.CenterPosition.Y + 1.7320508075f * hight) / WPosPerMeter, hight / WPosPerMeter);
			view = mat4.Translate(CameraPos) * camera;

			diffuseTexBuffer = Game.Renderer.Context.CreateTexture();
			diffuseTexBuffer.SetData(image.Data, image.Width, image.Height, TextureType.RGBA);
			specularTexBuffer = Game.Renderer.Context.CreateTexture();
			specularTexBuffer.SetData(image2.Data, image2.Width, image2.Height, TextureType.RGBA);

			shader.SetTexture("material.diffuse", diffuseTexBuffer);
			shader.SetTexture("material.specular", specularTexBuffer);
			shader.SetMatrix("projection", projection.Values1D);
			shader.SetMatrix("view", view.Values1D);
			shader.SetVec("viewPos", CameraPos.x, CameraPos.y, CameraPos.z);
			model = mat4.Translate(TestPos.X / WPosPerMeter, TestPos.Y / WPosPerMeter, TestPos.Z / WPosPerMeter) * mat4.Rotate(glm.Radians((float)tick++), new vec3(0.5f, 1.0f, 0));
			shader.SetMatrix("model", model.Values1D);

			if (vertexBuffer == null)
			{
				vertexBuffer = Game.Renderer.CreateVertexBuffer<Vertex3D>(vertex3Ds.Length);
				vertexBuffer.SetData(vertex3Ds, vertex3Ds.Length);
			}

			shader.PrepareRender();

			renderer.Context.EnableDepthBuffer();

			renderer.DrawBatch(shader, vertexBuffer, 0, 36, PrimitiveType.TriangleList);
			return;
		}

		public void Dispose()
		{
			vertexBuffer?.Dispose();
		}
	}
}
