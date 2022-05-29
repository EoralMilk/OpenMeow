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

	public sealed class Standalone3DRenderer : IDisposable
	{
		const bool ShowDebugInfo = false;
		public readonly float CameraPitch = 60.0f;
		public readonly vec3 CameraUp;
		public readonly int WPosPerMeter = 256;
		readonly float height = 256 * 200;
		public readonly float TanCameraPitch;
		public readonly float CosCameraPitch;
		public readonly float SinCameraPitch;
		public readonly float WPosPerMeterHeight;

		readonly Renderer renderer;
		readonly IShader shader;

		public vec3 CameraPos { get; private set; }
		bool init = false;
		public readonly float meterPerPix;
		public readonly float meterPerPixHalf;
		mat4 projection;
		mat4 view;

		// for test
		readonly ImageResult image;
		readonly ImageResult image2;
		readonly ITexture diffuseTexBufferTestBox;
		readonly ITexture specularTexBufferTestBox;
		readonly IVertexBuffer<Vertex3D> vertexBufferTestBox;
		readonly Vertex3D[] vertexsTestBox;
		public WPos TestPos = WPos.Zero;
		public WRot TestRot = WRot.None;

		public Standalone3DRenderer(Renderer renderer, int tilleWidthPix)
		{
			meterPerPix = (float)((1024 / WPosPerMeter) / (tilleWidthPix / 1.4142135d));
			meterPerPixHalf = meterPerPix / 2.0f;

			WPosPerMeterHeight = 1773.62f / (1024.0f / WPosPerMeter);
			TanCameraPitch = (float)Math.Tan(glm.Radians(CameraPitch));
			CosCameraPitch = (float)Math.Cos(glm.Radians(CameraPitch));
			SinCameraPitch = (float)Math.Sin(glm.Radians(CameraPitch));
			CameraUp = glm.Normalized(new vec3(0, -1, TanCameraPitch));

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
			vertexsTestBox = new Vertex3D[vertexCount];

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

				vertexsTestBox[i] = new Vertex3D(pos, normal, uv); ;
			}

			shader.SetInt("material.diffuse", 0);
			shader.SetFloat("material.shininess", 32.0f);

			// 平行光
			shader.SetVec("dirLight.direction", 0.5f, -0.1f, -0.3f);
			shader.SetVec("dirLight.ambient", 0.45f, 0.45f, 0.45f);
			shader.SetVec("dirLight.diffuse", 0.95f, 0.95f, 0.95f);
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

			diffuseTexBufferTestBox = Game.Renderer.Context.CreateTexture();
			diffuseTexBufferTestBox.SetData(image.Data, image.Width, image.Height, TextureType.RGBA);
			specularTexBufferTestBox = Game.Renderer.Context.CreateTexture();
			specularTexBufferTestBox.SetData(image2.Data, image2.Width, image2.Height, TextureType.RGBA);
			shader.SetTexture("material.diffuse", diffuseTexBufferTestBox);
			shader.SetTexture("material.specular", specularTexBufferTestBox);
			if (vertexBufferTestBox == null)
			{
				vertexBufferTestBox = Game.Renderer.CreateVertexBuffer<Vertex3D>(vertexsTestBox.Length);
				vertexBufferTestBox.SetData(vertexsTestBox, vertexsTestBox.Length);
			}
		}

		public void SetDepthPreview(bool enabled, float contrast, float offset)
		{
			shader.SetBool("EnableDepthPreview", enabled);
			shader.SetVec("DepthPreviewParams", contrast, offset);
		}

		public void DrawTest(WorldRenderer wr)
		{
			// projection and view
			{
				if (ShowDebugInfo)
				{
					Console.WriteLine("______Resolutions______");
					Console.WriteLine("Resolution: " + Game.Renderer.Resolution);
					Console.WriteLine("NativeResolution: " + Game.Renderer.NativeResolution);
					Console.WriteLine("NativeResolution: " + Game.Renderer.Window);
					Console.WriteLine("NativeResolution: " + Game.Renderer.WindowScale);
					Console.WriteLine("------------------------------------");
				}

				Viewport viewport = wr.Viewport;
				var viewPortSize = (1f / viewport.Zoom * new float2(Game.Renderer.NativeResolution));
				var ortho = (viewPortSize.X * meterPerPixHalf, -viewPortSize.X * meterPerPixHalf,
					-viewPortSize.Y * meterPerPixHalf, viewPortSize.Y * meterPerPixHalf);

				projection = mat4.Ortho(ortho.Item1, ortho.Item2, ortho.Item3, ortho.Item4, 0.1f, 480);
				//var vv = mat4.Rotate(glm.Radians(-30.0f), new vec3(-1,0,0)) * mat4.LookAt(vec3.Zero, new vec3(0, -1, 0), new vec3(0, 0, 1));

				var viewPoint = new vec3((float)viewport.CenterPosition.X / WPosPerMeter, (float)viewport.CenterPosition.Y / WPosPerMeter, 0);
				CameraPos = new vec3((float)viewport.CenterPosition.X / WPosPerMeter, ((float)viewport.CenterPosition.Y + TanCameraPitch * height) / WPosPerMeter, (float)height / WPosPerMeter);
				view = mat4.LookAt(CameraPos, viewPoint, CameraUp);
				//view = mat4.Translate(CameraPos) * vv;

				shader.SetMatrix("projection", projection.Values1D);
				shader.SetMatrix("view", view.Values1D);
				shader.SetVec("viewPos", CameraPos.x, CameraPos.y, CameraPos.z);

				if (ShowDebugInfo)
				{
					Console.WriteLine("______View and Camera______");
					Console.WriteLine("Ortho: " + ortho.Item1 + ", " + ortho.Item2 + ", " + ortho.Item3 + ", " + ortho.Item4);
					Console.WriteLine("viewport.CenterPosition: " + viewport.CenterPosition);
					Console.WriteLine("Camera-Position: " + CameraPos.x + ", " + CameraPos.y + ", " + CameraPos.z);
					Console.WriteLine("Camera-ViewPoint: " + viewPoint.x + ", " + viewPoint.y + ", " + viewPoint.z);
					Console.WriteLine("~~~~~~~~~~~~~~~~~~~~~~~~~");
				}
			}

			shader.PrepareRender();
			renderer.Context.EnableDepthTest();

			// draw parent test box
			var parentMat = DrawOneTestBox(TestPos, TestRot, 2);

			// draw child test box
			DrawOneTestBox(parentMat, new vec3(0, -4, 0), new vec3(0, 0, 0));

			Game.Renderer.Context.ClearDepthBuffer();
			Game.Renderer.Context.DisableDepthBuffer();
			return;
		}

		mat4 DrawOneTestBox(WPos wpos, WRot wrot, float scale = 1.0f)
		{
			var position = new vec3((float)wpos.X / WPosPerMeter, (float)wpos.Y / WPosPerMeter, (float)wpos.Z / WPosPerMeterHeight);
			var rotation = -(new vec3(wrot.Pitch.Angle / 512.0f * (float)Math.PI, wrot.Roll.Angle / 512.0f * (float)Math.PI, wrot.Yaw.Angle / 512.0f * (float)Math.PI));

			var m = mat4.Translate(position) * new mat4(new quat(rotation)) * mat4.Scale(scale);
			shader.SetMatrix("model", m.Values1D);
			renderer.DrawBatch(shader, vertexBufferTestBox, 0, 36, PrimitiveType.TriangleList);

			if (ShowDebugInfo)
			{
				Console.WriteLine("______Draw Test______");
				Console.WriteLine("position: " + position);
				Console.WriteLine("rotation: " + rotation);
				Console.WriteLine("scale: " + scale);
				Console.WriteLine("~~~~~~~~~~~~~~~~~~~");
			}

			return m;
		}

		mat4 DrawOneTestBox(mat4 parent, WPos wpos, WRot wrot, float scale = 1.0f)
		{
			var position = new vec3((float)wpos.X / WPosPerMeter, (float)wpos.Y / WPosPerMeter, (float)wpos.Z / WPosPerMeterHeight);
			var rotation = -(new vec3(wrot.Pitch.Angle / 512.0f * (float)Math.PI, wrot.Roll.Angle / 512.0f * (float)Math.PI, wrot.Yaw.Angle / 512.0f * (float)Math.PI));

			var m = parent * mat4.Translate(position) * new mat4(new quat(rotation)) * mat4.Scale(scale);
			shader.SetMatrix("model", m.Values1D);
			renderer.DrawBatch(shader, vertexBufferTestBox, 0, 36, PrimitiveType.TriangleList);
			return m;
		}

		mat4 DrawOneTestBox(mat4 parent, vec3 pos, vec3 rot, float scale = 1.0f)
		{
			var m = parent * mat4.Translate(pos) * new mat4(new quat(rot)) * mat4.Scale(scale);
			shader.SetMatrix("model", m.Values1D);
			renderer.DrawBatch(shader, vertexBufferTestBox, 0, 36, PrimitiveType.TriangleList);
			return m;
		}

		public void Dispose()
		{
			vertexBufferTestBox?.Dispose();
		}
	}
}
