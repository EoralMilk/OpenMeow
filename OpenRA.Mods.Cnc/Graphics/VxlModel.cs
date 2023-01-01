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
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Cnc.FileFormats;
using OpenRA.Primitives;

namespace OpenRA.Mods.Cnc.Graphics
{
	public class VxlShaderBindings : IShaderBindings
	{
		public string VertexShaderName { get; }
		public string FragmentShaderName { get; }
		public string GeometryShaderName => null;

		public int Stride => 52;

		public IEnumerable<ShaderVertexAttribute> Attributes { get; } = new[]
		{
			new ShaderVertexAttribute("aVertexPosition", 0, 3, 0),
			new ShaderVertexAttribute("aVertexTexCoord", 1, 4, 12),
			new ShaderVertexAttribute("aVertexTexMetadata", 2, 2, 28),
			new ShaderVertexAttribute("aVertexTint", 3, 4, 36) // useless
		};

		public bool Instanced => true;

		public int InstanceStrde => (27 * sizeof(float));

		public IEnumerable<ShaderVertexAttribute> InstanceAttributes { get; } = new[]
		{
			new ShaderVertexAttribute("iModelV1", 4, 4, 0),
			new ShaderVertexAttribute("iModelV2", 5, 4, 4 * sizeof(float)),
			new ShaderVertexAttribute("iModelV3", 6, 4, 8 * sizeof(float)),
			new ShaderVertexAttribute("iModelV4", 7, 4, 12 * sizeof(float)),

			new ShaderVertexAttribute("iPaletteRows", 8, 2, 16 * sizeof(float)),
			new ShaderVertexAttribute("iVplInfo", 9, 2, 18 * sizeof(float)),
			new ShaderVertexAttribute("iVertexTint", 10, 4, 20 * sizeof(float)),
			new ShaderVertexAttribute("iLightModify", 11, 3, 24 * sizeof(float))

		};

		public VxlShaderBindings()
		{
			VertexShaderName = "vxl";
			FragmentShaderName = "vxl";
		}

		public void SetCommonParaments(IShader shader, World3DRenderer w3dr, bool sunCamera)
		{
			shader.SetBool("RenderDepthBuffer", sunCamera);

			if (sunCamera)
			{
				shader.SetMatrix("projection", NumericUtil.MatRenderValues(w3dr.SunProjection));
				shader.SetMatrix("view", NumericUtil.MatRenderValues(w3dr.SunView));
				shader.SetVec("viewPos", w3dr.SunPos.X, w3dr.SunPos.Y, w3dr.SunPos.Z);
			}
			else
			{
				shader.SetMatrix("projection", NumericUtil.MatRenderValues(w3dr.Projection));
				shader.SetMatrix("view", NumericUtil.MatRenderValues(w3dr.View));
				shader.SetVec("viewPos", w3dr.CameraPos.X, w3dr.CameraPos.Y, w3dr.CameraPos.Z);
			}

			shader.SetVec("dirLight.direction", w3dr.SunDir.X, w3dr.SunDir.Y, w3dr.SunDir.Z);
			shader.SetVec("dirLight.ambient", w3dr.AmbientColor.X, w3dr.AmbientColor.Y, w3dr.AmbientColor.Z);
			shader.SetVec("dirLight.diffuse", w3dr.SunColor.X, w3dr.SunColor.Y, w3dr.SunColor.Z);
			shader.SetVec("dirLight.specular", w3dr.SunSpecularColor.X, w3dr.SunSpecularColor.Y, w3dr.SunSpecularColor.Z);
			//Console.WriteLine("SetCommonParaments");
		}
	}

	public struct VxlInstanceData
	{
		float t0, t1, t2, t3, t4, t5, t6, t7, t8, t9, t10, t11, t12, t13, t14, t15;
		float colorPaletteTextureMidIndex;
		float normalsPaletteTextureMidIndex;
		float vplstart, palettecount;
		float tintX, tintY, tintZ, tintW;
		float lightScale, ambientScale, specularScale;
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
			vplstart = data[18];
			palettecount = data[19];
			tintX = data[20];
			tintY = data[21];
			tintZ = data[22];
			tintW = data[23];
			lightScale = data[24];
			ambientScale = data[25];
			specularScale = data[26];
		}
	}

	class OrderedVxlSection : IOrderedMesh
	{
		public static readonly int MaxInstanceCount = 1024;

		readonly string name;
		public string Name => name;
		public IMaterial DefaultMaterial => null;

		public OrderedSkeleton Skeleton { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

		readonly CombinedMeshRenderData renderData;

		ITexture palette;

		VxlInstanceData[] instancesToDraw;
		VxlInstanceData[] twistInstancesToDraw;

		int instanceCount;
		int twistInstanceCount;

		public IVertexBuffer<VxlInstanceData> InstanceArrayBuffer;

		bool alphaBlend;
		public Rectangle BoundingRec { get; }

		public readonly MeshDrawType MeshDrawType;

		public OrderedVxlSection(CombinedMeshRenderData data, string name)
		{
			renderData = data;
			this.name = name;
			InstanceArrayBuffer = Game.Renderer.CreateVertexBuffer<VxlInstanceData>(MaxInstanceCount);
			instancesToDraw = new VxlInstanceData[MaxInstanceCount];
			twistInstancesToDraw = new VxlInstanceData[MaxInstanceCount];
			instanceCount = 0;
			twistInstanceCount = 0;
		}

		public void AddInstanceData(in float[] data, int dataCount, in int[] dataInt, int dataintCount)
		{
			if (instanceCount == MaxInstanceCount)
				throw new Exception("Instance Count bigger than MaxInstanceCount");

			if (dataCount != 27)
				throw new Exception("AddInstanceData params length unright");

			VxlInstanceData instanceData = new VxlInstanceData(data);

			if (data[23] < 1.0f)
				alphaBlend = true;

			instancesToDraw[instanceCount] = instanceData;
			instanceCount++;
		}

		public void AddTwistInstanceData(in float[] data, int dataCount, in int[] dataInt, int dataIntCount)
		{
			if (twistInstanceCount == MaxInstanceCount)
				throw new Exception("Instance Count bigger than MaxInstanceCount");

			if (dataCount != 27)
				throw new Exception("AddInstanceData params length unright");

			VxlInstanceData instanceData = new VxlInstanceData(data);

			twistInstancesToDraw[twistInstanceCount] = instanceData;
			twistInstanceCount++;
		}

		public void Flush()
		{
			instanceCount = 0;
			twistInstanceCount = 0;
			alphaBlend = false;
		}

		public void SetPalette(ITexture pal)
		{
			palette = pal;
		}

		public void DrawInstances(World world, bool shadowBuffser, MeshDrawType drawType)
		{
			if (drawType == MeshDrawType.Twist && twistInstanceCount == 0)
				return;
			else if (drawType != MeshDrawType.Twist && (MeshDrawType != drawType || instanceCount == 0))
				return;

			renderData.Shader.SetBool("IsTwist", drawType == MeshDrawType.Twist);
			renderData.Shader.SetFloat("TwistTime", Game.Renderer.TwistTime);

			renderData.Shader.SetTexture("Palette", palette);

			foreach (var (name, texture) in renderData.Textures)
				renderData.Shader.SetTexture(name, texture);
			renderData.Shader.PrepareRender();

			renderData.VertexBuffer.Bind();
			renderData.Shader.LayoutAttributes();

			if (drawType == MeshDrawType.Twist)
				InstanceArrayBuffer.SetData(twistInstancesToDraw, twistInstanceCount);
			else
				InstanceArrayBuffer.SetData(instancesToDraw, instanceCount);
			InstanceArrayBuffer.Bind();
			renderData.Shader.LayoutInstanceArray();

			if (alphaBlend)
				Game.Renderer.SetBlendMode(BlendMode.Alpha);
			else
				Game.Renderer.SetBlendMode(BlendMode.None);

			// draw instance
			Game.Renderer.RenderInstance(renderData.Start, renderData.Count, drawType == MeshDrawType.Twist ? twistInstanceCount : instanceCount);

			Game.Renderer.SetBlendMode(BlendMode.None);
		}
	}

	struct Limb
	{
		public float Scale;
		public float[] Bounds;
		public byte[] Size;
		public CombinedMeshRenderData RenderData;
	}

	public class VxlModel : IModel
	{
		const float SCALE = 0.0833329975605011f;

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
				var iom = new OrderedVxlSection(l.RenderData, files.Vxl + "_" + i);
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

			// Center and scale, no flip!
			t = OpenRA.Graphics.Util.MatrixMultiply(t, OpenRA.Graphics.Util.TranslationMatrix(l.Bounds[0], l.Bounds[1], l.Bounds[2]));
			// t = OpenRA.Graphics.Util.MatrixMultiply(OpenRA.Graphics.Util.ScaleMatrix(l.Scale, l.Scale, l.Scale), t);
			t = OpenRA.Graphics.Util.MatrixMultiply(OpenRA.Graphics.Util.ScaleMatrix(SCALE, SCALE, SCALE), t);

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
