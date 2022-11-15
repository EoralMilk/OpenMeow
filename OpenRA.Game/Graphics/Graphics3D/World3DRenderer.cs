using System;
using System.Collections.Generic;
using GlmSharp;
using OpenRA.Primitives;
using OpenRA.Traits;
using TrueSync;

namespace OpenRA.Graphics
{
	public sealed class World3DRenderer : IDisposable
	{
		const bool ShowDebugInfo = false;
		public readonly vec3 WorldUp = new vec3(0, 0, 1);
		public readonly float CameraPitch = 60.0f;
		public vec3 CameraUp { get; private set; }
		public vec3 CameraRight { get; private set; }

		public vec3 InverseCameraFront { get; private set; }
		public vec3 InverseCameraFrontMeterPerWDist { get; private set; }

		public readonly float TanCameraPitch;
		public readonly float CosCameraPitch;
		public readonly float SinCameraPitch;
		public readonly float MaxTerrainHeight;

		public readonly vec3 SunPosOne = new vec3(2.39773f, 1.0f, 3.51021f);
		public readonly float SunCos;
		public readonly float SunSin;

		public float FrameShadowBias;
		public vec3 SunPos;
		public vec3 SunDir;
		public vec3 SunUp;
		public vec3 SunRight;
		public mat4 SunView;
		public mat4 SunProjection;
		public float3 AmbientColor;
		public float AmbientIntencity;
		public float3 SunColor;
		public float3 SunSpecularColor;

		readonly Renderer renderer;

		public readonly mat4 ModelRenderRotationFix;
		public readonly WRot WRotRotationFix;

		public vec3 CameraPos { get; private set; }
		vec3 viewPoint;
		long lastLerpTime;

		public readonly float PixPerMeter;
		public readonly float MeterPerPix;
		public readonly int WDistPerPix;

		public readonly float MeterPerPixHalf;
		public mat4 Projection;
		public mat4 View;

		public World3DRenderer(Renderer renderer, MapGrid mapGrid)
		{
			PixPerMeter = (float)(mapGrid.TileSize.Width / 1.4142135d) / (1024 / World3DCoordinate.WDistPerMeter);
			MeterPerPix = (float)((1024 / World3DCoordinate.WDistPerMeter) / (mapGrid.TileSize.Width / 1.4142135d));
			WDistPerPix = (int)(MeterPerPix * World3DCoordinate.WDistPerMeter);
			MeterPerPixHalf = MeterPerPix / 2.0f;

			TanCameraPitch = (float)Math.Tan(glm.Radians(CameraPitch));
			CosCameraPitch = (float)Math.Cos(glm.Radians(CameraPitch));
			SinCameraPitch = (float)Math.Sin(glm.Radians(CameraPitch));
			CameraUp = glm.Normalized(new vec3(0, -1, TanCameraPitch));
			CameraRight = new vec3(1, 0, 0);
			InverseCameraFront = glm.Normalized(new vec3(0, 1, 1 / TanCameraPitch));
			InverseCameraFrontMeterPerWDist = InverseCameraFront / World3DCoordinate.WDistPerMeter;

			MaxTerrainHeight = mapGrid.MaximumTerrainHeight * MapGrid.MapHeightStep * 2f / World3DCoordinate.WDistPerMeter;

			UpdateSunPos(SunPosOne, vec3.Zero);
			var chordPow = (SunPosOne.x * SunPosOne.x + SunPosOne.y * SunPosOne.y) + (SunPosOne.z * SunPosOne.z);
			var chord = MathF.Sqrt(chordPow);
			SunCos = SunPosOne.z / chord;
			SunSin = (SunPosOne.x * SunPosOne.x + SunPosOne.y * SunPosOne.y) / chord;

			AmbientColor = new float3(0.45f, 0.45f, 0.45f);
			SunColor = new float3(1, 1, 1) - AmbientColor;
			SunSpecularColor = new float3(0.25f, 0.25f, 0.25f);

			ModelRenderRotationFix = mat4.Rotate((float)(Math.PI / 2), new vec3(1, 0, 0));
			WRotRotationFix = new WRot(new WAngle(-256), WAngle.Zero, WAngle.Zero);
			this.renderer = renderer;
		}

		void UpdateSunPos(in vec3 sunRelativePos, in vec3 groundPos)
		{
			SunPos = sunRelativePos + groundPos;
			SunDir = glm.Normalized(-sunRelativePos);
			SunRight = vec3.Cross(SunDir, WorldUp);
			SunUp = vec3.Cross(SunRight, SunDir);
			SunView = mat4.LookAt(SunPos, groundPos, SunUp);
		}

		void UpdateSunProject(float radius)
		{
			var halfView = radius * SunCos;
			var far = radius * SunSin + SunPos.z / SunCos;
			SunProjection = mat4.Ortho(-halfView, halfView, -halfView, halfView, far / 8, far);
		}

		public static float CameraRotTest = 0;
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
				var ortho = (-viewPortSize.X * MeterPerPixHalf, viewPortSize.X * MeterPerPixHalf,
									-viewPortSize.Y * MeterPerPixHalf, viewPortSize.Y * MeterPerPixHalf);

				var heightMeter = ortho.Item4 / SinCameraPitch + (MaxTerrainHeight - (ortho.Item4 / TanCameraPitch * CosCameraPitch));
				var far = heightMeter / CosCameraPitch + TanCameraPitch * ortho.Item4 + 100f;
				Projection = mat4.Ortho(ortho.Item1, ortho.Item2, ortho.Item3, ortho.Item4, far / 8, far);

				viewPoint = viewport.ViewPoint;// new vec3((float)viewport.CenterPosition.X / WPosPerMeter, (float)viewport.CenterPosition.Y / WPosPerMeter, 0);

				CameraPos = new vec3(0, TanCameraPitch * heightMeter, heightMeter);

				CameraPos = quat.FromAxisAngle(CameraRotTest, WorldUp) * CameraPos + new vec3(viewPoint.x, viewPoint.y, 0);

				if (CameraRotTest != 0)
				{
					InverseCameraFront = (CameraPos - viewPoint).Normalized;
					CameraRight = vec3.Cross(WorldUp, InverseCameraFront).Normalized;
					CameraUp = vec3.Cross(InverseCameraFront, CameraRight).Normalized;
					InverseCameraFrontMeterPerWDist = InverseCameraFront / World3DCoordinate.WDistPerMeter;
				}
				else
				{
					CameraUp = glm.Normalized(new vec3(0, -1, TanCameraPitch));
					CameraRight = new vec3(1, 0, 0);
					InverseCameraFront = glm.Normalized(new vec3(0, 1, 1 / TanCameraPitch));
					InverseCameraFrontMeterPerWDist = InverseCameraFront / World3DCoordinate.WDistPerMeter;
				}

				View = mat4.LookAt(CameraPos, viewPoint, CameraUp);

				// light params
				AmbientColor = wr.TerrainLighting.GetGlobalAmbient();
				SunColor = wr.TerrainLighting.GetGlobalDirectLight();
				AmbientIntencity = wr.TerrainLighting.GetGlobalAmbientIntencity();
				FrameShadowBias = 1.0f / heightMeter;

				var sunRelativePos = heightMeter * 1.2f * SunPosOne;
				UpdateSunPos(sunRelativePos, viewPoint);
				UpdateSunProject(MathF.Sqrt(ortho.Item1 * ortho.Item1 * 6));

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
		}

		public vec3 Get3DRenderPositionFromFloat3(float3 pos)
		{
			return new vec3(-pos.X / World3DCoordinate.WDistPerMeter,
										pos.Y / World3DCoordinate.WDistPerMeter,
										pos.Z / World3DCoordinate.WDistPerMeter);
		}

		public vec3 Get3DRenderPositionFromWPos(WPos pos)
		{
			return new vec3(-(float)pos.X / World3DCoordinate.WDistPerMeter,
										(float)pos.Y / World3DCoordinate.WDistPerMeter,
										(float)pos.Z / World3DCoordinate.WDistPerMeter);
		}

		public vec3 Get3DRenderVecFromWVec(WVec vec)
		{
			return new vec3(-(float)vec.X / World3DCoordinate.WDistPerMeter,
										(float)vec.Y / World3DCoordinate.WDistPerMeter,
										(float)vec.Z / World3DCoordinate.WDistPerMeter);
		}

		public quat Get3DRenderRotationFromWRot(in WRot rot)
		{
			return rot.ToRenderQuat();
		}

		public void AddMeshInstancesToDraw(float zOffset, IEnumerable<MeshInstance> meshes, float scale,
			in float3 tint, in float alpha, in Color remap)
		{
			var scaleMat = mat4.Scale(scale);
			var viewOffset = Game.Renderer.World3DRenderer.InverseCameraFrontMeterPerWDist * zOffset;
			foreach (var m in meshes)
			{
				if (m.DrawId() == -2)
					continue;

				if (m.UseMatrix)
				{
					if (m.Matrix == null)
						continue;

					var t = m.Matrix();

					float[] data = new float[23] { (float)t.m00, (float)t.m01, (float)t.m02, (float)t.m03,
																(float)t.m10, (float)t.m11, (float)t.m12, (float)t.m13,
																(float)t.m20, (float)t.m21, (float)t.m22, (float)t.m23,
																(float)t.m30, (float)t.m31, (float)t.m32, (float)t.m33,
																tint.X, tint.Y, tint.Z, alpha,
																remap.R, remap.G, remap.B,
					};

					var mat = m.Material.GetParams();
					int[] dataint = new int[5] { m.DrawId(), mat[0], mat[1], mat[2], mat[3] };

					m.OrderedMesh.AddInstanceData(data, 23, dataint, dataint.Length);
				}
				else
				{
					// Convert screen offset to world offset
					var offsetVec = Get3DRenderPositionFromWPos(m.PoistionFunc());
					offsetVec += viewOffset;
					var offsetTransform = mat4.Translate(offsetVec);

					var rotMat = new mat4(Get3DRenderRotationFromWRot(m.RotationFunc()));

					var t = offsetTransform * (scaleMat * rotMat);

					float[] data = new float[23] {t[0], t[1], t[2], t[3],
															t[4], t[5], t[6], t[7],
															t[8], t[9], t[10], t[11],
															t[12], t[13], t[14], t[15],
															tint.X, tint.Y, tint.Z, alpha,
															remap.R, remap.G, remap.B,
					};

					var mat = m.Material.GetParams();
					int[] dataint = new int[5] { m.DrawId(), mat[0], mat[1], mat[2], mat[3] };

					m.OrderedMesh.AddInstanceData(data, 23, dataint, dataint.Length);
				}
			}
		}

		public void Dispose()
		{
		}
	}
}
