using System;
using System.Collections.Generic;
using GlmSharp;
using OpenRA.Primitives;
using TrueSync;

namespace OpenRA.Graphics
{
	public sealed class World3DRenderer : IDisposable
	{
		const bool ShowDebugInfo = false;
		public readonly vec3 WorldUp = new vec3(0, 0, 1);
		public readonly float CameraPitch = 60.0f;
		public readonly vec3 CameraUp;
		public readonly vec3 CameraRight;

		public readonly vec3 InverseCameraFront;
		public readonly vec3 InverseCameraFrontMeterPerWPos;

		public readonly int WPosPerMeter = 256;
		readonly float height = 256 * 200;
		public readonly float TanCameraPitch;
		public readonly float CosCameraPitch;
		public readonly float SinCameraPitch;
		public readonly float WPosPerMeterHeight;
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
		public readonly float PixPerMeter;
		public readonly float MeterPerPix;
		public readonly float MeterPerPixHalf;
		public mat4 Projection;
		public mat4 View;

		public World3DRenderer(Renderer renderer, MapGrid mapGrid)
		{
			PixPerMeter = (float)(mapGrid.TileSize.Width / 1.4142135d) / (1024 / WPosPerMeter);
			MeterPerPix = (float)((1024 / WPosPerMeter) / (mapGrid.TileSize.Width / 1.4142135d));
			MeterPerPixHalf = MeterPerPix / 2.0f;

			WPosPerMeterHeight = WPosPerMeter ;// 1773.62f / (1024.0f / WPosPerMeter);
			TanCameraPitch = (float)Math.Tan(glm.Radians(CameraPitch));
			CosCameraPitch = (float)Math.Cos(glm.Radians(CameraPitch));
			SinCameraPitch = (float)Math.Sin(glm.Radians(CameraPitch));
			CameraUp = glm.Normalized(new vec3(0, -1, TanCameraPitch));
			CameraRight = new vec3(1, 0, 0);
			InverseCameraFront = glm.Normalized(new vec3(0, 1, 1 / TanCameraPitch));
			InverseCameraFrontMeterPerWPos = InverseCameraFront / WPosPerMeter;

			MaxTerrainHeight = mapGrid.MaximumTerrainHeight * MapGrid.MapHeightStep * 2f / WPosPerMeterHeight;

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

				var viewPoint = Get3DRenderPositionFromWPos(viewport.CenterPosition);// new vec3((float)viewport.CenterPosition.X / WPosPerMeter, (float)viewport.CenterPosition.Y / WPosPerMeter, 0);
				CameraPos = new vec3(viewPoint.x, viewPoint.y + TanCameraPitch * heightMeter, heightMeter);
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

			// render test box
			//{
			//	// draw parent test box
			//	var parentMat = DrawOneTestBox(TestPos, TestRot, 2);

			//	// draw child test box
			//	DrawOneTestBox(parentMat, new vec3(0, -4, 0), new vec3(0, 0, 0));
			//}

			return;
		}

		public TSVector Get3DPositionFromWPos(WPos pos)
		{
			return new TSVector(-FP.FromFloat((float)pos.X / WPosPerMeter),
												FP.FromFloat((float)pos.Y / WPosPerMeter),
												FP.FromFloat((float)pos.Z / WPosPerMeterHeight));
		}

		public TSVector Get3DPositionFromWVec(WVec vec)
		{
			return new TSVector(-FP.FromFloat((float)vec.X / WPosPerMeter),
												FP.FromFloat((float)vec.Y / WPosPerMeter),
												FP.FromFloat((float)vec.Z / WPosPerMeterHeight));
		}

		public vec3 Get3DRenderPositionFromWPos(WPos pos)
		{
			return new vec3(-(float)pos.X / WPosPerMeter,
										(float)pos.Y / WPosPerMeter,
										(float)pos.Z / WPosPerMeterHeight);
		}

		public WPos GetWPosFromMatrix(in TSMatrix4x4 matrix)
		{
			return new WPos(-(int)(matrix.M14 * WPosPerMeter),
										(int)(matrix.M24 * WPosPerMeter),
										(int)(matrix.M34 * WPosPerMeterHeight));
		}

		public int rollAdd = 0;
		public int pitchAdd = 256;
		public int yawAdd = 0;

		// x yaw;
		public WRot GetWRotFromMatrix(in TSMatrix4x4 matrix)
		{
			var q = Transformation.MatRotation(in matrix);
			var v = q.eulerAngles;
			return new WRot(
				new WAngle(rollAdd + (int)(v.y * 512 / 180)),
				new WAngle(pitchAdd + (int)(v.x * 512 / 180)),
				new WAngle(yawAdd + (int)(v.z * 512 / 180)));
		}

		public TSQuaternion Get3DRotationFromWRot(in WRot rot)
		{
			return rot.ToQuat();
		}

		public quat Get3DRenderRotationFromWRot(in WRot rot)
		{
			//return -(new vec3(rot.Pitch.Angle / 512.0f * (float)Math.PI,
			//							-rot.Roll.Angle / 512.0f * (float)Math.PI,
			//							rot.Yaw.Angle / 512.0f * (float)Math.PI));
			return rot.ToRenderQuat();
		}

		public void AddInstancesToDraw(float zOffset, IEnumerable<MeshInstance> meshes, float scale,
			in float3 tint, in float alpha, in Color remap)
		{
			var scaleMat = mat4.Scale(scale);
			var viewOffset = Game.Renderer.World3DRenderer.InverseCameraFrontMeterPerWPos * zOffset;
			foreach (var m in meshes)
			{
				if (m.UseMatrix)
				{
					TSMatrix4x4 t = m.Matrix();
					float[] data = new float[23] { (float)t.M11, (float)t.M21, (float)t.M31, (float)t.M41,
																(float)t.M12, (float)t.M22, (float)t.M32, (float)t.M42,
																(float)t.M13, (float)t.M23, (float)t.M33, (float)t.M43,
																(float)t.M14, (float)t.M24, (float)t.M34, (float)t.M44,
																tint.X, tint.Y, tint.Z, alpha,
																remap.R, remap.G, remap.B,
					};
					int[] dataint = new int[2] { m.DrawId, m.DrawMask };

					m.OrderedMesh.AddInstanceData(data, 23, dataint, 2);
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
					int[] dataint = new int[2] { m.DrawId, m.DrawMask };

					m.OrderedMesh.AddInstanceData(data, 23, dataint, 2);
				}
			}
		}

		public void Dispose()
		{
		}
	}
}
