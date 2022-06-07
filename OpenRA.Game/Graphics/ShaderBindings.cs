#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;

namespace OpenRA.Graphics
{
	public class CombinedShaderBindings : IShaderBindings
	{
		public string VertexShaderName { get; }
		public string FragmentShaderName { get; }
		public int Stride => 52;

		public IEnumerable<ShaderVertexAttribute> Attributes { get; } = new[]
		{
			new ShaderVertexAttribute("aVertexPosition", 0, 3, 0),
			new ShaderVertexAttribute("aVertexTexCoord", 1, 4, 12),
			new ShaderVertexAttribute("aVertexTexMetadata", 2, 2, 28),
			new ShaderVertexAttribute("aVertexTint", 3, 4, 36)
		};

		public bool Instanced => false;

		public int InstanceStrde => throw new System.NotImplementedException();

		public IEnumerable<ShaderVertexAttribute> InstanceAttributes => throw new System.NotImplementedException();

		public CombinedShaderBindings()
		{
			var name = "combined";
			VertexShaderName = name;
			FragmentShaderName = name;
		}

		public void SetCommonParaments(IShader shader, World3DRenderer w3dr)
		{
			// Set By SpriteRenderer
		}
	}

	public class ModelShaderBindings : IShaderBindings
	{
		public string VertexShaderName { get; }
		public string FragmentShaderName { get; }
		public int Stride => 52;

		public IEnumerable<ShaderVertexAttribute> Attributes { get; } = new[]
		{
			new ShaderVertexAttribute("aVertexPosition", 0, 3, 0),
			new ShaderVertexAttribute("aVertexTexCoord", 1, 4, 12),
			new ShaderVertexAttribute("aVertexTexMetadata", 2, 2, 28),
			new ShaderVertexAttribute("aVertexTint", 3, 4, 36)
		};

		public bool Instanced => true;

		public int InstanceStrde => (18 * sizeof(float));

		public IEnumerable<ShaderVertexAttribute> InstanceAttributes { get; } = new[]
		{
			new ShaderVertexAttribute("iModelV1", 4, 4, 0),
			new ShaderVertexAttribute("iModelV2", 5, 4, 4 * sizeof(float)),
			new ShaderVertexAttribute("iModelV3", 6, 4, 8 * sizeof(float)),
			new ShaderVertexAttribute("iModelV4", 7, 4, 12 * sizeof(float)),

			new ShaderVertexAttribute("iPaletteRows", 8, 2, 16 * sizeof(float)),
		};

		public ModelShaderBindings()
		{
			VertexShaderName = "combined";
			FragmentShaderName = "combined";
		}

		public void SetCommonParaments(IShader shader, World3DRenderer w3dr)
		{
			shader.SetMatrix("projection", w3dr.Projection.Values1D);
			shader.SetMatrix("view", w3dr.View.Values1D);
			shader.SetVec("viewPos", w3dr.CameraPos.x, w3dr.CameraPos.y, w3dr.CameraPos.z);

			shader.SetVec("dirLight.direction", w3dr.SunDir.x, w3dr.SunDir.y, w3dr.SunDir.z);
			shader.SetVec("dirLight.ambient", w3dr.AmbientColor.x, w3dr.AmbientColor.y, w3dr.AmbientColor.z);
			shader.SetVec("dirLight.diffuse", w3dr.SunColor.x, w3dr.SunColor.y, w3dr.SunColor.z);
			shader.SetVec("dirLight.specular", w3dr.SunSpecularColor.x, w3dr.SunSpecularColor.y, w3dr.SunSpecularColor.z);
			//Console.WriteLine("SetCommonParaments");
		}
	}
}
