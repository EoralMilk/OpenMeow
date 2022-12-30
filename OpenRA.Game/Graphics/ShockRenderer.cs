using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using OpenRA.Effects;
using OpenRA.Primitives;

namespace OpenRA.Graphics
{
	public class ShockWaveEffect : IEffect
	{
		public readonly int Life;
		public readonly WPos Pos;
		public readonly float Width;
		public readonly float Size;

		public float GetProgress()
		{
			return 1f - (float)tick / (float)Life;
		}

		int tick;

		public ShockWaveEffect(WPos pos, int tick, float size,float width)
		{
			Pos = pos;
			Life = tick;
			this.tick = Life;
			Size = size;
			Width = width;
		}

		IEnumerable<IRenderable> IEffect.Render(WorldRenderer r)
		{
			Game.Renderer.ShockRenderer.ShockWaves.Add(this);
			yield break;
		}

		public void Draw()
		{
			if (tick >= 0)
				Game.Renderer.ShockRenderer.DrawShockWave(Pos, GetProgress(), Width, Size);
		}

		void IEffect.Tick(World world)
		{
			tick--;
			if (tick < 0)
				world.AddFrameEndTask(w => { w.Remove(this); });
		}
	}

	public readonly struct AddtionalVertex
	{
		public readonly float X, Y, Z;
		public readonly float U, V;
		public readonly float R, W;

		public AddtionalVertex(float x, float y, float z, float u, float v, float r, float w)
		{
			X = x; Y = y; Z = z;
			U = u; V = v;
			R = r;W = w;
		}

		public AddtionalVertex(float3 xyz, float u, float v, float r, float w)
			: this(xyz.X, xyz.Y, xyz.Z, u, v, r, w) { }
	}

	public class ShockRenderer
	{
		IVertexBuffer<AddtionalVertex> VBO;

		readonly Renderer renderer;
		public readonly IShader Shader;
		readonly AddtionalVertex[] vertices;
		int nv = 0;

		public readonly List<ShockWaveEffect> ShockWaves = new List<ShockWaveEffect>();

		public ShockRenderer(Renderer renderer)
		{
			this.renderer = renderer;
			Shader = renderer.GetOrCreateShader<AdditionalShaderBindings>("Additional");
			vertices = new AddtionalVertex[renderer.TempBufferSize];
			VBO = renderer.CreateVertexBuffer<AddtionalVertex>(vertices.Length);
		}

		public static Vector3 CreatelBoard(AddtionalVertex[] vertices,
			in WPos inPos, in Vector3 leftDir, in Vector3 upDir, float radius, float width, float size, int nv)
		{
			var position = Game.Renderer.World3DRenderer.Get3DRenderPositionFromWPos(inPos);

			var leftTop = position + size * leftDir + size * upDir;
			var rightBottom = position - size * leftDir - size * upDir;
			var rightTop = position - size * leftDir + size * upDir;
			var leftBottom = position + size * leftDir - size * upDir;

			vertices[nv] = new AddtionalVertex(new float3(leftTop), 1, 1, radius, width);
			vertices[nv + 1] = new AddtionalVertex(new float3(rightTop), 0, 1, radius, width);
			vertices[nv + 2] = new AddtionalVertex(new float3(rightBottom), 0, 0, radius, width);

			vertices[nv + 3] = new AddtionalVertex(new float3(rightBottom), 0, 0, radius, width);
			vertices[nv + 4] = new AddtionalVertex(new float3(leftBottom), 1, 0, radius, width);
			vertices[nv + 5] = new AddtionalVertex(new float3(leftTop), 1, 1, radius, width);

			return position;
		}

		public void Render()
		{
			foreach (var shock in ShockWaves)
			{
				shock.Draw();
			}

			ShockWaves.Clear();

			if (nv > 0)
			{
				Flush();
			}
		}

		void Flush()
		{
			Shader.SetMatrix("projection", NumericUtil.MatRenderValues(renderer.World3DRenderer.Projection));
			Shader.SetMatrix("view", NumericUtil.MatRenderValues(renderer.World3DRenderer.View));

			VBO.SetData(vertices, nv);
			renderer.Context.SetBlendMode(BlendMode.Additive);
			Shader.PrepareRender();
			Game.Renderer.DrawBatch(Shader, VBO, 0, nv, PrimitiveType.TriangleList);
			renderer.Context.SetBlendMode(BlendMode.Alpha);

			nv = 0;
		}

		public void DrawShockWave(in WPos pos, float progress, float width, float size)
		{
			if (nv + 6 >= vertices.Length)
			{
				Flush();
			}

			//  renderer.World3DRenderer.CameraUp
			CreatelBoard(vertices, pos, new Vector3(-1, 0, 0), new Vector3(0, -1, 0), progress, width, size, nv);
			nv += 6;
		}
	}
}
