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

		public string GeometryShaderName => null;
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

		public void SetCommonParaments(IShader shader, World3DRenderer w3dr, bool sunCamera)
		{
			shader.SetBool("RenderDepthBuffer", sunCamera);
		}
	}

	public class ScreenShaderBindings : IShaderBindings
	{
		public string VertexShaderName { get; }
		public string FragmentShaderName { get; }
		public string GeometryShaderName => null;

		public int Stride => 4 * sizeof(float);

		public IEnumerable<ShaderVertexAttribute> Attributes { get; } = new[]
		{
			new ShaderVertexAttribute("aPosition", 0, 2, 0),
			new ShaderVertexAttribute("aTexCoords", 1, 2, 2 * sizeof(float)),
		};

		public bool Instanced => false;

		public int InstanceStrde => throw new System.NotImplementedException();

		public IEnumerable<ShaderVertexAttribute> InstanceAttributes => throw new System.NotImplementedException();

		public ScreenShaderBindings()
		{
			var name = "screen";
			VertexShaderName = name;
			FragmentShaderName = name;
		}

		public void SetCommonParaments(IShader shader, World3DRenderer w3dr, bool sunCamera)
		{
		}
	}

	public class MapShaderBindings : IShaderBindings
	{
		public string VertexShaderName { get; }
		public string FragmentShaderName { get; }

		public string GeometryShaderName => null;
		public int Stride => (25 * sizeof(float));

		public IEnumerable<ShaderVertexAttribute> Attributes { get; } = new[]
		{
			new ShaderVertexAttribute("aVertexPosition", 0, 3, 0),
			new ShaderVertexAttribute("aVertexTexCoord", 1, 4, 3 * sizeof(float)),
			new ShaderVertexAttribute("aVertexTexMetadata", 2, 2, 7 * sizeof(float)),
			new ShaderVertexAttribute("aVertexTint", 3, 4, 9 * sizeof(float)),

			new ShaderVertexAttribute("aVertexTangent", 4, 3, 13 * sizeof(float)),
			new ShaderVertexAttribute("aVertexBitangent", 5, 3, 16 * sizeof(float)),
			new ShaderVertexAttribute("aVertexNormal", 6, 3, 19 * sizeof(float)),

			new ShaderVertexAttribute("aTileTexCoord", 7, 2, 22 * sizeof(float)),
			new ShaderVertexAttribute("aDrawType", 8, 1, 24 * sizeof(float), AttributeType.UInt32),

		};

		public bool Instanced => false;

		public int InstanceStrde => throw new System.NotImplementedException();

		public IEnumerable<ShaderVertexAttribute> InstanceAttributes => throw new System.NotImplementedException();

		public MapShaderBindings()
		{
			var name = "map";
			VertexShaderName = name;
			FragmentShaderName = name;
		}

		public void SetCommonParaments(IShader shader, World3DRenderer w3dr, bool sunCamera)
		{
			shader.SetBool("RenderDepthBuffer", sunCamera);

			if (sunCamera)
			{
				shader.SetMatrix("projection", w3dr.SunProjection.Values1D);
				shader.SetMatrix("view", w3dr.SunView.Values1D);
				shader.SetVec("viewPos", w3dr.SunPos.x, w3dr.SunPos.y, w3dr.SunPos.z);
			}
			else
			{
				shader.SetMatrix("projection", w3dr.Projection.Values1D);
				shader.SetMatrix("view", w3dr.View.Values1D);
				shader.SetVec("viewPos", w3dr.CameraPos.x, w3dr.CameraPos.y, w3dr.CameraPos.z);
			}

			shader.SetVec("dirLight.direction", w3dr.SunDir.x, w3dr.SunDir.y, w3dr.SunDir.z);
			shader.SetVec("dirLight.ambient", w3dr.AmbientColor.X, w3dr.AmbientColor.Y, w3dr.AmbientColor.Z);
			shader.SetVec("dirLight.diffuse", w3dr.SunColor.X, w3dr.SunColor.Y, w3dr.SunColor.Z);
			shader.SetVec("dirLight.specular", w3dr.SunSpecularColor.X, w3dr.SunSpecularColor.Y, w3dr.SunSpecularColor.Z);
		}
	}

	public class TerrainMaskShaderBindings : IShaderBindings
	{
		public string VertexShaderName { get; }
		public string FragmentShaderName { get; }
		public string GeometryShaderName => null;

		public int Stride => 4 * sizeof(float);

		public IEnumerable<ShaderVertexAttribute> Attributes { get; } = new[]
		{
			new ShaderVertexAttribute("aPosition", 0, 2, 0),
			new ShaderVertexAttribute("aTexCoords", 1, 2, 2 * sizeof(float)),
		};

		public bool Instanced => false;

		public int InstanceStrde => throw new System.NotImplementedException();

		public IEnumerable<ShaderVertexAttribute> InstanceAttributes => throw new System.NotImplementedException();

		public TerrainMaskShaderBindings()
		{
			var name = "terrain-mask";
			VertexShaderName = name;
			FragmentShaderName = name;
		}

		public void SetCommonParaments(IShader shader, World3DRenderer w3dr, bool sunCamera)
		{
		}
	}

	public class TerrainBlendingShaderBindings : IShaderBindings
	{
		public string VertexShaderName { get; }
		public string FragmentShaderName { get; }

		public string GeometryShaderName => null;
		public int Stride => (18 * sizeof(float));

		public IEnumerable<ShaderVertexAttribute> Attributes { get; } = new[]
		{
			new ShaderVertexAttribute("aVertexUV", 0, 2, 0),
			new ShaderVertexAttribute("aVertexMaskUV", 1, 2, 2 * sizeof(float)),
			new ShaderVertexAttribute("aVertexTint", 2, 4, 4 * sizeof(float)),

			new ShaderVertexAttribute("aVertexTangent", 3, 3, 8 * sizeof(float)),
			new ShaderVertexAttribute("aVertexBitangent", 4, 3, 11 * sizeof(float)),
			new ShaderVertexAttribute("aVertexNormal", 5, 3, 14 * sizeof(float)),

			new ShaderVertexAttribute("aDrawType", 6, 1, 17 * sizeof(float), AttributeType.UInt32),

		};

		public bool Instanced => false;

		public int InstanceStrde => throw new System.NotImplementedException();

		public IEnumerable<ShaderVertexAttribute> InstanceAttributes => throw new System.NotImplementedException();

		public TerrainBlendingShaderBindings()
		{
			var name = "terrain-blend";
			VertexShaderName = name;
			FragmentShaderName = name;
		}

		public void SetCommonParaments(IShader shader, World3DRenderer w3dr, bool sunCamera)
		{
		}
	}

	public class TerrainFinalShaderBindings : IShaderBindings
	{
		public string VertexShaderName { get; }
		public string FragmentShaderName { get; }

		public string GeometryShaderName => null;
		public int Stride => (5 * sizeof(float));

		public IEnumerable<ShaderVertexAttribute> Attributes { get; } = new[]
		{
			new ShaderVertexAttribute("aVertexPosition", 0, 3, 0),
			new ShaderVertexAttribute("aVertexTexCoord", 1, 2, 3 * sizeof(float)),
		};

		public bool Instanced => false;

		public int InstanceStrde => throw new System.NotImplementedException();

		public IEnumerable<ShaderVertexAttribute> InstanceAttributes => throw new System.NotImplementedException();

		public TerrainFinalShaderBindings()
		{
			var name = "terrain-final";
			VertexShaderName = name;
			FragmentShaderName = name;
		}

		public void SetCommonParaments(IShader shader, World3DRenderer w3dr, bool sunCamera)
		{
			shader.SetBool("RenderDepthBuffer", sunCamera);

			if (sunCamera)
			{
				shader.SetMatrix("projection", w3dr.SunProjection.Values1D);
				shader.SetMatrix("view", w3dr.SunView.Values1D);
				shader.SetVec("viewPos", w3dr.SunPos.x, w3dr.SunPos.y, w3dr.SunPos.z);
			}
			else
			{
				shader.SetMatrix("projection", w3dr.Projection.Values1D);
				shader.SetMatrix("view", w3dr.View.Values1D);
				shader.SetVec("viewPos", w3dr.CameraPos.x, w3dr.CameraPos.y, w3dr.CameraPos.z);
			}

			shader.SetVec("dirLight.direction", w3dr.SunDir.x, w3dr.SunDir.y, w3dr.SunDir.z);
			shader.SetVec("dirLight.ambient", w3dr.AmbientColor.X, w3dr.AmbientColor.Y, w3dr.AmbientColor.Z);
			shader.SetVec("dirLight.diffuse", w3dr.SunColor.X, w3dr.SunColor.Y, w3dr.SunColor.Z);
			shader.SetVec("dirLight.specular", w3dr.SunSpecularColor.X, w3dr.SunSpecularColor.Y, w3dr.SunSpecularColor.Z);
		}
	}

}
