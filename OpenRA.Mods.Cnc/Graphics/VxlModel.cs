#region Copyright & License Information
/*
 * Copyright 2007-2022 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Graphics.Graphics3D;
using OpenRA.Mods.Cnc.FileFormats;
using OpenRA.Primitives;

namespace OpenRA.Mods.Cnc.Graphics
{
	public struct VxlInstanceData
	{
		float t0, t1, t2, t3, t4, t5, t6, t7, t8, t9, t10, t11, t12, t13, t14, t15;
		float colorPaletteTextureMidIndex;
		float normalsPaletteTextureMidIndex;

		public VxlInstanceData(float[] mat, float c, float n)
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

			colorPaletteTextureMidIndex = c;
			normalsPaletteTextureMidIndex = n;
		}

		public VxlInstanceData(float t0, float t1, float t2, float t3, float t4, float t5, float t6, float t7, float t8, float t9, float t10, float t11, float t12, float t13, float t14, float t15, float c, float n)
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

			colorPaletteTextureMidIndex = c;
			normalsPaletteTextureMidIndex = n;
		}

		public VxlInstanceData(float[] data)
		{
			this.t0 = data[0];
			this.t1 = data[1];
			this.t2 = data[2];
			this.t3 = data[3];
			this.t4 = data[4];
			this.t5 = data[5];
			this.t6 = data[6];
			this.t7 = data[7];
			this.t8 = data[8];
			this.t9 = data[9];
			this.t10 = data[10];
			this.t11 = data[11];
			this.t12 = data[12];
			this.t13 = data[13];
			this.t14 = data[14];
			this.t15 = data[15];

			colorPaletteTextureMidIndex = data[16];
			normalsPaletteTextureMidIndex = data[17];
		}
	}

	class OrderedVxlSection : IOrderedMesh
	{
		public static readonly int MaxInstanceCount = 1024;

		readonly MeshRenderData renderData;
		public MeshRenderData RenderData => renderData;

		ITexture palette;

		VxlInstanceData[] instancesToDraw;
		int instanceCount;
		public IVertexBuffer<VxlInstanceData> InstanceArrayBuffer;

		public OrderedVxlSection(MeshRenderData data)
		{
			renderData = data;
			InstanceArrayBuffer = Game.Renderer.CreateVertexBuffer<VxlInstanceData>(MaxInstanceCount);
			instancesToDraw = new VxlInstanceData[MaxInstanceCount];
			instanceCount = 0;
		}

		public void AddInstanceData(float[] data, int dataCount)
		{
			if (instanceCount == MaxInstanceCount)
				throw new Exception("Instance Count bigger than MaxInstanceCount");

			if (dataCount != 18)
				throw new Exception("AddInstanceData params length unright");

			VxlInstanceData instanceData = new VxlInstanceData(data);
			instancesToDraw[instanceCount] = instanceData;
			instanceCount++;
		}

		public void Flush()
		{
			instanceCount = 0;
		}

		public void SetPalette(ITexture pal)
		{
			palette = pal;
		}

		public void DrawInstances()
		{
			if (instanceCount == 0)
				return;

			renderData.Shader.SetTexture("Palette", palette);

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

	struct Limb
	{
		public float Scale;
		public float[] Bounds;
		public byte[] Size;
		public MeshRenderData RenderData;
	}

	public class VxlModel : IModel
	{
		readonly Limb[] limbData;
		readonly float[] transforms;
		readonly uint frames;
		readonly uint limbs;
		readonly IOrderedMesh[] orderedSections;
		public NormalType NormalType { get; set; }
		uint IModel.Frames => frames;
		uint IModel.Sections => limbs;

		public VxlModel(VxlLoader loader, VxlReader vxl, HvaReader hva, (string Vxl, string Hva) files)
		{
			if (vxl.LimbCount != hva.LimbCount)
				throw new InvalidOperationException($"{files.Vxl}.vxl and {files.Hva}.hva limb counts don't match.");

			transforms = hva.Transforms;
			frames = hva.FrameCount;
			limbs = hva.LimbCount;

			limbData = new Limb[vxl.LimbCount];
			orderedSections = new IOrderedMesh[vxl.LimbCount];

			for (var i = 0; i < vxl.LimbCount; i++)
			{
				var vl = vxl.Limbs[i];
				var l = default(Limb);
				l.Scale = vl.Scale;
				l.Bounds = (float[])vl.Bounds.Clone();
				l.Size = (byte[])vl.Size.Clone();
				l.RenderData = loader.GenerateRenderData(vxl.Limbs[i]);
				var iom = new OrderedVxlSection(l.RenderData);
				orderedSections[i] = Game.Renderer.UpdateOrderedMeshes(files.Vxl + "_" + i, iom);
				limbData[i] = l;
				NormalType = vl.Type;
			}
		}

		public float[] TransformationMatrix(uint limb, uint frame)
		{
			if (frame >= frames)
				throw new ArgumentOutOfRangeException(nameof(frame), $"Only {frames} frames exist.");
			if (limb >= limbs)
				throw new ArgumentOutOfRangeException(nameof(limb), $"Only {limbs} limbs exist.");

			var l = limbData[limb];
			var t = new float[16];
			Array.Copy(transforms, 16 * (limbs * frame + limb), t, 0, 16);

			// Fix limb position
			t[12] *= l.Scale * (l.Bounds[3] - l.Bounds[0]) / l.Size[0];
			t[13] *= l.Scale * (l.Bounds[4] - l.Bounds[1]) / l.Size[1];
			t[14] *= l.Scale * (l.Bounds[5] - l.Bounds[2]) / l.Size[2];

			// Center, flip and scale
			t = OpenRA.Graphics.Util.MatrixMultiply(t, OpenRA.Graphics.Util.TranslationMatrix(l.Bounds[0], l.Bounds[1], l.Bounds[2]));
			t = OpenRA.Graphics.Util.MatrixMultiply(OpenRA.Graphics.Util.ScaleMatrix(l.Scale, -l.Scale, l.Scale), t);

			return t;
		}

		public IOrderedMesh RenderData(uint limb)
		{
			return orderedSections[limb];
		}

		public float[] Size
		{
			get
			{
				return limbData.Select(a => a.Size.Select(b => a.Scale * b).ToArray())
					.Aggregate((a, b) => new float[]
					{
						Math.Max(a[0], b[0]),
						Math.Max(a[1], b[1]),
						Math.Max(a[2], b[2])
					});
			}
		}

		public float[] Bounds(uint frame)
		{
			var ret = new[]
			{
				float.MaxValue, float.MaxValue, float.MaxValue,
				float.MinValue, float.MinValue, float.MinValue
			};

			for (uint j = 0; j < limbs; j++)
			{
				var l = limbData[j];
				var b = new[]
				{
					0, 0, 0,
					l.Bounds[3] - l.Bounds[0],
					l.Bounds[4] - l.Bounds[1],
					l.Bounds[5] - l.Bounds[2]
				};

				// Calculate limb bounding box
				var bb = OpenRA.Graphics.Util.MatrixAABBMultiply(TransformationMatrix(j, frame), b);
				for (var i = 0; i < 3; i++)
				{
					ret[i] = Math.Min(ret[i], bb[i]);
					ret[i + 3] = Math.Max(ret[i + 3], bb[i + 3]);
				}
			}

			return ret;
		}

		public Rectangle AggregateBounds
		{
			get
			{
				// Corner offsets
				var ix = new uint[] { 0, 0, 0, 0, 3, 3, 3, 3 };
				var iy = new uint[] { 1, 1, 4, 4, 1, 1, 4, 4 };
				var iz = new uint[] { 2, 5, 2, 5, 2, 5, 2, 5 };

				// Calculate the smallest sphere that covers the model limbs
				var rSquared = 0f;
				for (var f = 0U; f < frames; f++)
				{
					var bounds = Bounds(f);
					for (var i = 0; i < 8; i++)
					{
						var x = bounds[ix[i]];
						var y = bounds[iy[i]];
						var z = bounds[iz[i]];
						rSquared = Math.Max(rSquared, x * x + y * y + z * z);
					}
				}

				var r = (int)Math.Sqrt(rSquared) + 1;
				return Rectangle.FromLTRB(-r, -r, r, r);
			}
		}

		public void Dispose()
		{
		}

	}
}
