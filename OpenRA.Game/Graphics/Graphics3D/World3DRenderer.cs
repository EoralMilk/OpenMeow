using System;
using System.Collections.Generic;
using System.IO;
using GlmSharp;
using OpenRA.Graphics.Graphics3D;
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
		public bool Instanced => true;

		public int InstanceStrde => 16 * sizeof(float);

		public IEnumerable<ShaderVertexAttribute> InstanceAttributes { get; } = new[]
		{
			new ShaderVertexAttribute("iModelV1", 3, 4, 0),
			new ShaderVertexAttribute("iModelV2", 4, 4, 4 * sizeof(float)),
			new ShaderVertexAttribute("iModelV3", 5, 4, 8 * sizeof(float)),
			new ShaderVertexAttribute("iModelV4", 6, 4, 12 * sizeof(float)),
		};

		public MyShaderBindings()
		{
			string name = "myshader";
			VertexShaderName = name;
			FragmentShaderName = name;
		}

		public void SetCommonParaments(IShader shader, World3DRenderer w3dr)
		{
			shader.SetInt("material.diffuse", 0);
			shader.SetFloat("material.shininess", 32.0f);

			shader.SetMatrix("projection", w3dr.Projection.Values1D);
			shader.SetMatrix("view", w3dr.View.Values1D);
			shader.SetVec("viewPos", w3dr.CameraPos.x, w3dr.CameraPos.y, w3dr.CameraPos.z);

			shader.SetVec("dirLight.direction", w3dr.SunDir.x, w3dr.SunDir.y, w3dr.SunDir.z);
			shader.SetVec("dirLight.ambient", w3dr.AmbientColor.x, w3dr.AmbientColor.y, w3dr.AmbientColor.z);
			shader.SetVec("dirLight.diffuse", w3dr.SunColor.x, w3dr.SunColor.y, w3dr.SunColor.z);
			shader.SetVec("dirLight.specular", w3dr.SunSpecularColor.x, w3dr.SunSpecularColor.y, w3dr.SunSpecularColor.z);
		}
	}

	public struct BoxInstanceData
	{
		float t0, t1, t2, t3, t4, t5, t6, t7, t8, t9, t10, t11, t12, t13, t14, t15;
		public BoxInstanceData(float[] mat)
		{
			t0 = mat[0];
			t1 = mat[1];
			t2 = mat[2];
			t3 = mat[3];
			t4 = mat[4];
			t5 = mat[5];
			t6 = mat[6];
			t7 = mat[7];
			t8 = mat[8];
			t9 = mat[9];
			t10 = mat[10];
			t11 = mat[11];
			t12 = mat[12];
			t13 = mat[13];
			t14 = mat[14];
			t15 = mat[15];
		}

		public BoxInstanceData(float t0, float t1, float t2, float t3, float t4, float t5, float t6, float t7, float t8, float t9, float t10, float t11, float t12, float t13, float t14, float t15)
		{
			this.t0 = t0;
			this.t1 = t1;
			this.t2 = t2;
			this.t3 = t3;
			this.t4 = t4;
			this.t5 = t5;
			this.t6 = t6;
			this.t7 = t7;
			this.t8 = t8;
			this.t9 = t9;
			this.t10 = t10;
			this.t11 = t11;
			this.t12 = t12;
			this.t13 = t13;
			this.t14 = t14;
			this.t15 = t15;
		}
	}

	class OrderedTestBox : IOrderedMesh
	{
		public static readonly int MaxInstanceCount = 1024;

		readonly MeshRenderData renderData;
		public MeshRenderData RenderData => renderData;
		public string Name => "box";

		//ITexture palette;

		BoxInstanceData[] instancesToDraw;
		int instanceCount;
		public IVertexBuffer<BoxInstanceData> InstanceArrayBuffer;

		public OrderedTestBox(MeshRenderData data)
		{
			renderData = data;
			InstanceArrayBuffer = Game.Renderer.CreateVertexBuffer<BoxInstanceData>(MaxInstanceCount);
			instancesToDraw = new BoxInstanceData[MaxInstanceCount];
			instanceCount = 0;
		}

		public void AddInstanceData(float[] data, int dataCount)
		{
			if (instanceCount == MaxInstanceCount)
				throw new Exception("Instance Count bigger than MaxInstanceCount");

			if (dataCount != 16)
				throw new Exception("AddInstanceData params length unright");

			BoxInstanceData instanceData = new BoxInstanceData(data);
			instancesToDraw[instanceCount] = instanceData;
			instanceCount++;
			//Console.WriteLine("instanceCount: " + instanceCount);
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

			foreach (var (name, texture) in renderData.Textures)
				renderData.Shader.SetTexture(name, texture);
			renderData.Shader.PrepareRender();

			renderData.VertexBuffer.Bind();
			renderData.Shader.LayoutAttributes();

			InstanceArrayBuffer.SetData(instancesToDraw, instanceCount);
			InstanceArrayBuffer.Bind();
			renderData.Shader.LayoutInstanceArray();

			// draw instance
			Game.Renderer.RenderInstance(renderData.Start, renderData.Count, instanceCount);
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

	public sealed class World3DRenderer : IDisposable
	{
		const bool ShowDebugInfo = false;
		public readonly float CameraPitch = 60.0f;
		public readonly vec3 CameraUp;
		public readonly vec3 InverseCameraFront;
		public readonly vec3 InverseCameraFrontMeterPerWPos;

		public readonly int WPosPerMeter = 256;
		readonly float height = 256 * 200;
		public float HeightOverlay = 50;
		public readonly float TanCameraPitch;
		public readonly float CosCameraPitch;
		public readonly float SinCameraPitch;
		public readonly float WPosPerMeterHeight;
		public readonly float MaxTerrainHeight;

		public readonly vec3 SunDir;
		public readonly vec3 AmbientColor;
		public readonly vec3 SunColor;
		public readonly vec3 SunSpecularColor;

		readonly Renderer renderer;

		public vec3 CameraPos { get; private set; }
		public readonly float MeterPerPix;
		public readonly float MeterPerPixHalf;
		public mat4 Projection;
		public mat4 View;

		// for test
		readonly ImageResult image;
		readonly ImageResult image2;

		public WPos TestPos = WPos.Zero;
		public WRot TestRot = WRot.None;
		OrderedTestBox imo;
		public World3DRenderer(Renderer renderer, MapGrid mapGrid)
		{
			MeterPerPix = (float)((1024 / WPosPerMeter) / (mapGrid.TileSize.Width / 1.4142135d));
			MeterPerPixHalf = MeterPerPix / 2.0f;

			WPosPerMeterHeight = 1773.62f / (1024.0f / WPosPerMeter);
			TanCameraPitch = (float)Math.Tan(glm.Radians(CameraPitch));
			CosCameraPitch = (float)Math.Cos(glm.Radians(CameraPitch));
			SinCameraPitch = (float)Math.Sin(glm.Radians(CameraPitch));
			CameraUp = glm.Normalized(new vec3(0, -1, TanCameraPitch));
			InverseCameraFront = glm.Normalized(new vec3(0, 1, 1/TanCameraPitch));
			InverseCameraFrontMeterPerWPos = InverseCameraFront / WPosPerMeter;

			MaxTerrainHeight = mapGrid.MaximumTerrainHeight * 724 * 1.25f / WPosPerMeterHeight;

			SunDir = glm.Normalized(new vec3(0, 0, 0) - new vec3(-3.2506f, 1.3557f, 4.7588f));
			AmbientColor = new vec3(0.45f, 0.45f, 0.45f);
			SunColor = new vec3(1, 1, 1) - AmbientColor;
			SunSpecularColor = new vec3(0.25f, 0.25f, 0.25f);

			this.renderer = renderer;
			IShader shader = renderer.GetOrCreateShader<MyShaderBindings>("MyShaderBindings");

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
			Vertex3D[] vertexsTestBox = new Vertex3D[vertexCount];

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
			shader.SetVec("spotLight.position", 0, 0, 3);
			shader.SetVec("spotLight.direction", 0, 0, -1);
			shader.SetVec("spotLight.ambient", 0.0f, 0.0f, 0.0f);
			shader.SetVec("spotLight.diffuse", 2.0f, 2.0f, 2.0f);
			shader.SetVec("spotLight.specular", 1.0f, 1.0f, 1.0f);
			shader.SetFloat("spotLight.constant", 1.0f);
			shader.SetFloat("spotLight.linear", 0.09f);
			shader.SetFloat("spotLight.quadratic", 0.032f);
			shader.SetFloat("spotLight.cutOff", glm.Cos(glm.Radians(7.5f)));
			shader.SetFloat("spotLight.outerCutOff", glm.Cos(glm.Radians(10.0f)));

			ITexture diffuseTexBufferTestBox = Game.Renderer.Context.CreateTexture();
			diffuseTexBufferTestBox.SetData(image.Data, image.Width, image.Height, TextureType.RGBA);
			ITexture specularTexBufferTestBox = Game.Renderer.Context.CreateTexture();
			specularTexBufferTestBox.SetData(image2.Data, image2.Width, image2.Height, TextureType.RGBA);

			IVertexBuffer<Vertex3D> vertexBufferTestBox = Game.Renderer.CreateVertexBuffer<Vertex3D>(vertexsTestBox.Length);
			vertexBufferTestBox.SetData(vertexsTestBox, vertexsTestBox.Length);

			Dictionary<string, ITexture> textures = new Dictionary<string, ITexture>();
			textures.Add("material.diffuse", diffuseTexBufferTestBox);
			textures.Add("material.specular", specularTexBufferTestBox);

			MeshRenderData renderData = new MeshRenderData(0, 36, shader, vertexBufferTestBox, textures);
			imo = new OrderedTestBox(renderData);
			renderer.UpdateOrderedMeshes("TestBox", imo);
		}

		public void SetDepthPreview(bool enabled, float contrast, float offset)
		{
			imo.RenderData.Shader.SetBool("EnableDepthPreview", enabled);
			imo.RenderData.Shader.SetVec("DepthPreviewParams", contrast, offset);
		}

		public void PrepareToRender(WorldRenderer wr)
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
				var ortho = (viewPortSize.X * MeterPerPixHalf, -viewPortSize.X * MeterPerPixHalf,
					-viewPortSize.Y * MeterPerPixHalf, viewPortSize.Y * MeterPerPixHalf);

				var heightMeter = ortho.Item4 / SinCameraPitch + (MaxTerrainHeight - (ortho.Item4 / TanCameraPitch * CosCameraPitch));

				var far = heightMeter / CosCameraPitch + TanCameraPitch * ortho.Item4 + 100f;

				Projection = mat4.Ortho(ortho.Item1, ortho.Item2, ortho.Item3, ortho.Item4, 0, far);

				var viewPoint = new vec3((float)viewport.CenterPosition.X / WPosPerMeter, (float)viewport.CenterPosition.Y / WPosPerMeter, 0);
				CameraPos = new vec3((float)viewport.CenterPosition.X / WPosPerMeter, ((float)viewport.CenterPosition.Y) / WPosPerMeter + TanCameraPitch * heightMeter, heightMeter);
				View = mat4.LookAt(CameraPos, viewPoint, CameraUp);

				if (ShowDebugInfo)
				{
					Console.WriteLine("______View and Camera______");
					Console.WriteLine("Ortho: " + ortho.Item1 + ", " + ortho.Item2 + ", " + ortho.Item3 + ", " + ortho.Item4);
					Console.WriteLine("viewport.CenterPosition: " + viewport.CenterPosition);
					Console.WriteLine("viewport.Zoom: " + viewport.Zoom);
					Console.WriteLine("Camera-Position: " + CameraPos.x + ", " + CameraPos.y + ", " + CameraPos.z);
					Console.WriteLine("Camera-ViewPoint: " + viewPoint.x + ", " + viewPoint.y + ", " + viewPoint.z);
					Console.WriteLine("~~~~~~~~~~~~~~~~~~~~~~~~~");
				}
			}

			// render test box
			{
				// draw parent test box
				var parentMat = DrawOneTestBox(TestPos, TestRot, 2);

				// draw child test box
				DrawOneTestBox(parentMat, new vec3(0, -4, 0), new vec3(0, 0, 0));
			}

			return;
		}

		public vec3 Get3DPositionFromWPos(WPos pos)
		{
			return new vec3((float)pos.X / WPosPerMeter, (float)pos.Y / WPosPerMeter, (float)pos.Z / WPosPerMeterHeight);
		}

		public vec3 Get3DRotationFromWRot(WRot rot)
		{
			return -(new vec3(rot.Pitch.Angle / 512.0f * (float)Math.PI, rot.Roll.Angle / 512.0f * (float)Math.PI, rot.Yaw.Angle / 512.0f * (float)Math.PI));
		}

		mat4 DrawOneTestBox(WPos wpos, WRot wrot, float scale = 1.0f)
		{
			var position = Get3DPositionFromWPos(wpos);
			var rotation = Get3DRotationFromWRot(wrot);

			var m = mat4.Translate(position) * new mat4(new quat(rotation)) * mat4.Scale(scale);
			//shader.SetMatrix("model", m.Values1D);
			//renderer.DrawBatch(shader, vertexBufferTestBox, 0, 36, PrimitiveType.TriangleList);
			imo.AddInstanceData(m.Values1D, 16);

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
			imo.AddInstanceData(m.Values1D, 16);
			return m;
		}

		mat4 DrawOneTestBox(mat4 parent, vec3 pos, vec3 rot, float scale = 1.0f)
		{
			var m = parent * mat4.Translate(pos) * new mat4(new quat(rot)) * mat4.Scale(scale);
			imo.AddInstanceData(m.Values1D, 16);
			return m;
		}

		public void Dispose()
		{
		}
	}
}
