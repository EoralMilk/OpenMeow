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

		public void SetCommonParaments(IShader shader, World3DRenderer w3dr, bool sunCamera)
		{
			shader.SetBool("RenderDepthBuffer", sunCamera);
		}
	}

	public class ScreenShaderBindings : IShaderBindings
	{
		public string VertexShaderName { get; }
		public string FragmentShaderName { get; }
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
}
