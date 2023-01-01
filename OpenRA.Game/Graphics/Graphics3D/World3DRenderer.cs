using System;
using System.Collections.Generic;
using System.Numerics;
using OpenRA.Primitives;

namespace OpenRA.Graphics
{
	public sealed class World3DRenderer : IDisposable
	{
		const bool ShowDebugInfo = false;
		public readonly Vector3 WorldUp = new Vector3(0, 0, 1);
		public readonly float CameraPitch = 60.0f;
		public Vector3 CameraUp { get; private set; }
		public Vector3 CameraRight { get; private set; }

		public Vector3 InverseCameraFront { get; private set; }
		public Vector3 InverseCameraFrontMeterPerWDist { get; private set; }

		public readonly float TanCameraPitch;
		public readonly float CosCameraPitch;
		public readonly float SinCameraPitch;
		public readonly float MaxTerrainHeight;

		public readonly Vector3 SunPosOne = new Vector3(2.39773f, 1.0f, 3.51021f);
		public readonly float SunCos;
		public readonly float SunSin;

		public float FrameShadowBias;
		public Vector3 SunPos;
		public Vector3 SunDir;
		public Vector3 SunUp;
		public Vector3 SunRight;
		public Matrix4x4 SunView;
		public Matrix4x4 SunProjection;
		public float3 AmbientColor;
		public float AmbientIntencity;
		public float3 SunColor;
		public float3 SunSpecularColor;

		readonly Renderer renderer;

		public readonly Matrix4x4 ModelRenderRotationFix;
		public readonly WRot WRotRotationFix;

		public Vector3 CameraPos { get; private set; }
		public float2 ViewportSize { get; private set; }
		Vector3 viewPoint;

		public readonly float PixPerMeter;
		public readonly float MeterPerPix;
		public readonly int WDistPerPix;

		public readonly float MeterPerPixHalf;
		public Matrix4x4 Projection;
		public Matrix4x4 View;

		/// <summary>
		/// Why we use this?
		/// It seems that the system Numeric methods are not suitable for GL coordinate systems
		/// </summary>
		public static Matrix4x4 Ortho(float left, float right, float bottom, float top, float zNear, float zFar)
		{
			var m = Matrix4x4.Identity;
			m.M11 = 2 / (right - left);
			m.M22 = 2 / (top - bottom);
			m.M33 = -2 / (zFar - zNear);
			m.M14 = -(right + left) / (right - left);
			m.M24 = -(top + bottom) / (top - bottom);
			m.M34 = -(zFar + zNear) / (zFar - zNear);
			return m;
		}

		/// <summary>
		/// Why we use this?
		/// It seems that the system Numeric methods are not suitable for GL coordinate systems
		/// </summary>
		public static Matrix4x4 LookAt(Vector3 eye, Vector3 center, Vector3 up)
		{
			var f = Vector3.Normalize(center - eye);
			var s = Vector3.Normalize(Vector3.Cross(f, up));
			var u = Vector3.Cross(s, f);
			var m = Matrix4x4.Identity;
			m.M11 = s.X;
			m.M12 = s.Y;
			m.M13 = s.Z;
			m.M21 = u.X;
			m.M22 = u.Y;
			m.M23 = u.Z;
			m.M31 = -f.X;
			m.M32 = -f.Y;
			m.M33 = -f.Z;
			m.M14 = -Vector3.Dot(s, eye);
			m.M24 = -Vector3.Dot(u, eye);
			m.M34 = Vector3.Dot(f, eye);
			return m;
		}

		/// <summary>
		/// Why we use this?
		/// It seems that the system Numeric methods are not suitable for GL coordinate systems
		/// </summary>
		public static Matrix4x4 FromQuat(Quaternion q)
		{
			return new Matrix4x4(1 - 2 * (q.Y * q.Y + q.Z * q.Z),	2 * (q.X * q.Y - q.W * q.Z),		2 * (q.X * q.Z + q.W * q.Y), 0,
												2 * (q.X * q.Y + q.W * q.Z),		1 - 2 * (q.X * q.X + q.Z * q.Z),	2 * (q.Y * q.Z - q.W * q.X), 0,
												2 * (q.X * q.Z - q.W * q.Y),		2 * (q.Y * q.Z + q.W * q.X),		1 - 2 * (q.X * q.X + q.Y * q.Y), 0,
												0, 0, 0, 1);
		}

		/// <summary>
		/// Why we use this?
		/// It seems that the system Numeric methods are not suitable for GL coordinate systems
		/// </summary>
		public static Matrix4x4 FromTranslation(Vector3 vector3)
		{
			var m = Matrix4x4.Identity;
			m.M14 = vector3.X;
			m.M24 = vector3.Y;
			m.M34 = vector3.Z;
			return m;
		}

		public World3DRenderer(Renderer renderer, MapGrid mapGrid)
		{
			PixPerMeter = (float)(mapGrid.TileSize.Width / 1.4142135d) / (1024 / World3DCoordinate.WDistPerMeter);
			MeterPerPix = (float)((1024 / World3DCoordinate.WDistPerMeter) / (mapGrid.TileSize.Width / 1.4142135d));
			WDistPerPix = (int)(MeterPerPix * World3DCoordinate.WDistPerMeter);
			MeterPerPixHalf = MeterPerPix / 2.0f;

			TanCameraPitch = (float)Math.Tan(NumericUtil.ToRadians(CameraPitch));
			CosCameraPitch = (float)Math.Cos(NumericUtil.ToRadians(CameraPitch));
			SinCameraPitch = (float)Math.Sin(NumericUtil.ToRadians(CameraPitch));
			CameraUp = Vector3.Normalize(new Vector3(0, -1, TanCameraPitch));
			CameraRight = new Vector3(1, 0, 0);
			InverseCameraFront = Vector3.Normalize(new Vector3(0, 1, 1 / TanCameraPitch));
			InverseCameraFrontMeterPerWDist = InverseCameraFront / World3DCoordinate.WDistPerMeter;

			MaxTerrainHeight = mapGrid.MaximumTerrainHeight * MapGrid.MapHeightStep * 2f / World3DCoordinate.WDistPerMeter;

			UpdateSunPos(SunPosOne, Vector3.Zero);
			var chordPow = (SunPosOne.X * SunPosOne.X + SunPosOne.Y * SunPosOne.Y) + (SunPosOne.Z * SunPosOne.Z);
			var chord = MathF.Sqrt(chordPow);
			SunCos = SunPosOne.Z / chord;
			SunSin = (SunPosOne.X * SunPosOne.X + SunPosOne.Y * SunPosOne.Y) / chord;

			AmbientColor = new float3(0.45f, 0.45f, 0.45f);
			SunColor = new float3(1, 1, 1) - AmbientColor;
			SunSpecularColor = SunColor * new float3(0.66f, 0.66f, 0.66f);

			ModelRenderRotationFix = Matrix4x4.CreateFromAxisAngle(new Vector3(1, 0, 0), 90f);
			WRotRotationFix = new WRot(new WAngle(-256), WAngle.Zero, WAngle.Zero);
			this.renderer = renderer;
		}

		void UpdateSunPos(in Vector3 sunRelativePos, in Vector3 groundPos)
		{
			SunPos = sunRelativePos + groundPos;
			SunDir = Vector3.Normalize(-sunRelativePos);
			SunRight = Vector3.Cross(SunDir, WorldUp);
			SunUp = Vector3.Cross(SunRight, SunDir);
			SunView = LookAt(SunPos, groundPos, SunUp);
		}

		void UpdateSunProject(float radius)
		{
			var halfView = radius * SunCos;
			var far = radius * SunSin + SunPos.Z / SunCos;
			SunProjection = Ortho(-halfView, halfView, -halfView, halfView, 0, far);
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
				ViewportSize = (1f / viewport.Zoom * new float2(Game.Renderer.NativeResolution));
				var ortho = (-ViewportSize.X * MeterPerPixHalf, ViewportSize.X * MeterPerPixHalf,
									-ViewportSize.Y * MeterPerPixHalf, ViewportSize.Y * MeterPerPixHalf);

				var heightMeter = ortho.Item4 / SinCameraPitch + (MaxTerrainHeight - (ortho.Item4 / TanCameraPitch * CosCameraPitch));
				var far = heightMeter / CosCameraPitch + TanCameraPitch * ortho.Item4 + 100f;
				Projection = Ortho(ortho.Item1, ortho.Item2, ortho.Item3, ortho.Item4, 0, far);

				viewPoint = viewport.ViewPoint; // new vec3((float)viewport.CenterPosition.X / WPosPerMeter, (float)viewport.CenterPosition.Y / WPosPerMeter, 0);

				CameraPos = new Vector3(0, TanCameraPitch * heightMeter, heightMeter) + new Vector3(viewPoint.X, viewPoint.Y, 0);

				CameraUp = Vector3.Normalize(new Vector3(0, -1, TanCameraPitch));
				CameraRight = new Vector3(1, 0, 0);
				InverseCameraFront = Vector3.Normalize(new Vector3(0, 1, 1 / TanCameraPitch));
				InverseCameraFrontMeterPerWDist = InverseCameraFront / World3DCoordinate.WDistPerMeter;

				View = LookAt(CameraPos, viewPoint, CameraUp);

				// light params
				AmbientColor = wr.TerrainLighting.GetGlobalAmbient();
				SunColor = wr.TerrainLighting.GetGlobalDirectLight();
				SunSpecularColor = SunColor * new float3(0.88f, 0.88f, 0.88f);
				AmbientIntencity = wr.TerrainLighting.GetGlobalAmbientIntencity();
				FrameShadowBias = 1.0f / heightMeter;

				var sunRelativePos = heightMeter * 1.2f * SunPosOne;
				UpdateSunPos(sunRelativePos, viewPoint);

				// hack: the sun project calculation should not be this
				UpdateSunProject(MathF.Sqrt(ortho.Item1 * ortho.Item1 * 4f) * viewport.Zoom);

				if (ShowDebugInfo)
				{
					Console.WriteLine("______View and Camera______");
					Console.WriteLine("Ortho: " + ortho.Item1 + ", " + ortho.Item2 + ", " + ortho.Item3 + ", " + ortho.Item4);
					Console.WriteLine("viewport.CenterPosition: " + viewport.CenterPosition);
					Console.WriteLine("viewport.Zoom: " + viewport.Zoom);
					Console.WriteLine("Camera-Position: " + CameraPos.X + ", " + CameraPos.Y + ", " + CameraPos.Z);
					Console.WriteLine("Camera-ViewPoint: " + viewPoint.X + ", " + viewPoint.Y + ", " + viewPoint.Z);
					Console.WriteLine("~~~~~~~~~~~~~~~~~~~~~~~~~");
				}
			}
		}

		public Vector3 Get3DRenderPositionFromFloat3(float3 pos)
		{
			return new Vector3(-pos.X / World3DCoordinate.WDistPerMeter,
										pos.Y / World3DCoordinate.WDistPerMeter,
										pos.Z / World3DCoordinate.WDistPerMeter);
		}

		public Vector3 Get3DRenderPositionFromWPos(WPos pos)
		{
			return new Vector3(-(float)pos.X / World3DCoordinate.WDistPerMeter,
										(float)pos.Y / World3DCoordinate.WDistPerMeter,
										(float)pos.Z / World3DCoordinate.WDistPerMeter);
		}

		public Vector3 Get3DRenderVecFromWVec(WVec vec)
		{
			return new Vector3(-(float)vec.X / World3DCoordinate.WDistPerMeter,
										(float)vec.Y / World3DCoordinate.WDistPerMeter,
										(float)vec.Z / World3DCoordinate.WDistPerMeter);
		}

		public Quaternion Get3DRenderRotationFromWRot(in WRot rot)
		{
			return rot.ToRenderQuat();
		}

		public void AddMeshInstancesToDraw(float zOffset, IEnumerable<MeshInstance> meshes, float scale,
			in float3 tint, in float alpha, in Color remap, bool twist)
		{
			var scaleMat = Matrix4x4.CreateScale(scale);
			var viewOffset = Game.Renderer.World3DRenderer.InverseCameraFrontMeterPerWDist * zOffset;
			foreach (var m in meshes)
			{
				if (m.DrawId() == -2)
					continue;

				var remapcolor = remap;
				var replaceAlpha = alpha;
				var replaceTint = tint;

				if (m.GetRemap != null)
				{
					remapcolor = m.GetRemap();
				}

				if (m.GetAlpha != null)
				{
					replaceAlpha = m.GetAlpha();
				}

				if (m.GetTint != null)
				{
					replaceTint = m.GetTint();
				}

				if (m.UseMatrix)
				{
					if (m.Matrix == null)
						continue;

					var t = m.Matrix();

					float[] data = new float[23] { t.M11, t.M21, t.M31, t.M41,
																t.M12, t.M22, t.M32, t.M42,
																t.M13, t.M23, t.M33, t.M43,
																t.M14, t.M24, t.M34, t.M44,
																replaceTint.X, replaceTint.Y, replaceTint.Z, replaceAlpha,
																remapcolor.R, remapcolor.G, remapcolor.B,
					};

					var mat = m.Material.GetParams();
					int[] dataint = new int[5] { m.DrawId(), mat[0], mat[1], mat[2], mat[3] };

					if (twist)
						m.OrderedMesh.AddTwistInstanceData(data, 23, dataint, dataint.Length);
					else
						m.OrderedMesh.AddInstanceData(data, 23, dataint, dataint.Length);
				}
				else
				{
					// Convert screen offset to world offset
					var offsetVec = Get3DRenderPositionFromWPos(m.PoistionFunc());
					offsetVec += viewOffset;
					var offsetTransform = FromTranslation(offsetVec);

					var rotMat = FromQuat(Get3DRenderRotationFromWRot(m.RotationFunc()));

					var t = offsetTransform * (scaleMat * rotMat);

					float[] data = new float[23]{
						t.M11, t.M21, t.M31, t.M41,
						t.M12, t.M22, t.M32, t.M42,
						t.M13, t.M23, t.M33, t.M43,
						t.M14, t.M24, t.M34, t.M44,
						replaceTint.X, replaceTint.Y, replaceTint.Z, replaceAlpha,
						remapcolor.R, remapcolor.G, remapcolor.B,
					};

					var mat = m.Material.GetParams();
					int[] dataint = new int[5] { m.DrawId(), mat[0], mat[1], mat[2], mat[3] };

					if (twist)
						m.OrderedMesh.AddTwistInstanceData(data, 23, dataint, dataint.Length);
					else
						m.OrderedMesh.AddInstanceData(data, 23, dataint, dataint.Length);
				}
			}
		}

		public void Dispose()
		{
		}
	}
}
