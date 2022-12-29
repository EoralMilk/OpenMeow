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
using OpenRA.Primitives;

namespace OpenRA.Graphics
{
	public class CombinedShaderBindings : IShaderBindings
	{
		protected string vertexShaderName;
		protected string fragmentShaderName;

		public string VertexShaderName => vertexShaderName;
		public string FragmentShaderName => fragmentShaderName;

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
			vertexShaderName = name;
			fragmentShaderName = name;
		}

		public void SetCommonParaments(IShader shader, World3DRenderer w3dr, bool sunCamera)
		{
			shader.SetBool("RenderDepthBuffer", sunCamera);
		}
	}

	public class ScreenShaderBindings : IShaderBindings
	{
		protected string vertexShaderName;
		protected string fragmentShaderName;

		public string VertexShaderName => vertexShaderName;
		public string FragmentShaderName => fragmentShaderName;
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
			vertexShaderName = name;
			fragmentShaderName = name;
		}

		public void SetCommonParaments(IShader shader, World3DRenderer w3dr, bool sunCamera)
		{
		}
	}

	public class UITextureArrayShaderBindings : IShaderBindings
	{
		protected string vertexShaderName;
		protected string fragmentShaderName;

		public string VertexShaderName => vertexShaderName;
		public string FragmentShaderName => fragmentShaderName;
		public string GeometryShaderName => null;

		public int Stride => 7 * sizeof(float);

		public IEnumerable<ShaderVertexAttribute> Attributes { get; } = new[]
		{
			new ShaderVertexAttribute("aPosition", 0, 2, 0),
			new ShaderVertexAttribute("aTexCoords", 1, 2, 2 * sizeof(float)),
			new ShaderVertexAttribute("aTags", 2, 3, 4 * sizeof(float), AttributeType.Int32),
		};

		public bool Instanced => false;

		public int InstanceStrde => throw new System.NotImplementedException();

		public IEnumerable<ShaderVertexAttribute> InstanceAttributes => throw new System.NotImplementedException();

		public UITextureArrayShaderBindings()
		{
			var name = "ui_texturearray";
			vertexShaderName = name;
			fragmentShaderName = name;
		}

		public void SetCommonParaments(IShader shader, World3DRenderer w3dr, bool sunCamera)
		{
		}
	}

	public class MapShaderBindings : IShaderBindings
	{
		protected string vertexShaderName;
		protected string fragmentShaderName;

		public string VertexShaderName => vertexShaderName;
		public string FragmentShaderName => fragmentShaderName;

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
			vertexShaderName = name;
			fragmentShaderName = name;
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
		}
	}

	public class TerrainMaskShaderBindings : IShaderBindings
	{
		protected string vertexShaderName;
		protected string fragmentShaderName;

		public string VertexShaderName => vertexShaderName;
		public string FragmentShaderName => fragmentShaderName;
		public string GeometryShaderName => null;

		public int Stride => 7 * sizeof(float);

		public IEnumerable<ShaderVertexAttribute> Attributes { get; } = new[]
		{
			new ShaderVertexAttribute("aPosition", 0, 2, 0),
			new ShaderVertexAttribute("aTexCoords", 1, 2, 2 * sizeof(float)),

			new ShaderVertexAttribute("aBrushType", 2, 3, 4 * sizeof(float), AttributeType.Int32),
		};

		public bool Instanced => false;

		public int InstanceStrde => throw new System.NotImplementedException();

		public IEnumerable<ShaderVertexAttribute> InstanceAttributes => throw new System.NotImplementedException();

		public TerrainMaskShaderBindings()
		{
			var name = "terrain-mask";
			vertexShaderName = name;
			fragmentShaderName = name;
		}

		public void SetCommonParaments(IShader shader, World3DRenderer w3dr, bool sunCamera)
		{
		}
	}

	public class TerrainBlendingShaderBindings : IShaderBindings
	{
		protected string vertexShaderName;
		protected string fragmentShaderName;

		public string VertexShaderName => vertexShaderName;
		public string FragmentShaderName => fragmentShaderName;

		public string GeometryShaderName => null;
		public int Stride => (27 * sizeof(float));

		public IEnumerable<ShaderVertexAttribute> Attributes { get; } = new[]
		{
			new ShaderVertexAttribute("aMapUV", 0, 2, 0),
			new ShaderVertexAttribute("aVertexUV", 1, 2, 2 * sizeof(float)),
			new ShaderVertexAttribute("aVertexMaskUV", 2, 2, 4 * sizeof(float)),
			new ShaderVertexAttribute("aVertexTint", 3, 4, 6 * sizeof(float)),

			new ShaderVertexAttribute("aVertexTangent", 4, 3, 10 * sizeof(float)),
			new ShaderVertexAttribute("aVertexBitangent", 5, 3, 13 * sizeof(float)),
			new ShaderVertexAttribute("aVertexNormal", 6, 3, 16 * sizeof(float)),

			new ShaderVertexAttribute("aTileType1234", 7, 4, 19 * sizeof(float), AttributeType.Int32),
			new ShaderVertexAttribute("aTileType5678", 8, 4, 23 * sizeof(float), AttributeType.Int32),
		};

		public bool Instanced => false;

		public int InstanceStrde => throw new System.NotImplementedException();

		public IEnumerable<ShaderVertexAttribute> InstanceAttributes => throw new System.NotImplementedException();

		public TerrainBlendingShaderBindings()
		{
			var name = "terrain-blend";
			vertexShaderName = name;
			fragmentShaderName = name;
		}

		public void SetCommonParaments(IShader shader, World3DRenderer w3dr, bool sunCamera)
		{
		}
	}

	public class TerrainFinalShaderBindings : IShaderBindings
	{
		protected string vertexShaderName;
		protected string fragmentShaderName;

		public string VertexShaderName => vertexShaderName;
		public string FragmentShaderName => fragmentShaderName;

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
			vertexShaderName = name;
			fragmentShaderName = name;
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
		}
	}

	public class MeshShaderBindings : IShaderBindings
	{
		protected string vertexShaderName;
		protected string fragmentShaderName;

		public string VertexShaderName => vertexShaderName;
		public string FragmentShaderName => fragmentShaderName;
		public string GeometryShaderName => null;

		public int Stride => 16 * sizeof(float);

		public IEnumerable<ShaderVertexAttribute> Attributes { get; } = new[]
		{
			new ShaderVertexAttribute("aVertexPos", 0, 3, 0),
			new ShaderVertexAttribute("aNormal", 1, 3, 3 * sizeof(float)),
			new ShaderVertexAttribute("aTexCoords", 2, 2, 6 * sizeof(float)),
			new ShaderVertexAttribute("aBoneId", 3, 4, 8 * sizeof(float), AttributeType.Int32),
			new ShaderVertexAttribute("aBoneWeights", 4, 4, 12 * sizeof(float)),
		};
		public bool Instanced => true;

		public int InstanceStrde => 28 * sizeof(float);

		public IEnumerable<ShaderVertexAttribute> InstanceAttributes { get; } = new[]
		{
			new ShaderVertexAttribute("iModelV1", 5, 4, 0),
			new ShaderVertexAttribute("iModelV2", 6, 4, 4 * sizeof(float)),
			new ShaderVertexAttribute("iModelV3", 7, 4, 8 * sizeof(float)),
			new ShaderVertexAttribute("iModelV4", 8, 4, 12 * sizeof(float)),
			new ShaderVertexAttribute("iTint", 9, 4, 16 * sizeof(float)),
			new ShaderVertexAttribute("iRemap", 10, 3, 20 * sizeof(float)),
			new ShaderVertexAttribute("iDrawId", 11, 1, 23 * sizeof(float), AttributeType.Int32),
			new ShaderVertexAttribute("iMaterial", 12, 4, 24 * sizeof(float), AttributeType.Int32),
		};

		public MeshShaderBindings()
		{
			string name = "3d_common";
			vertexShaderName = name;
			fragmentShaderName = name;
		}

		public void SetCommonParaments(IShader shader, World3DRenderer w3dr, bool sunCamera)
		{
			shader.SetBool("RenderDepthBuffer", sunCamera);
			shader.SetMatrix("rotationFix", NumericUtil.MatRenderValues(w3dr.ModelRenderRotationFix));
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
		}
	}

	public class CharacterBodyMeshShaderBindings : MeshShaderBindings
	{
		public CharacterBodyMeshShaderBindings()
		{
			vertexShaderName = "3d_common";
			fragmentShaderName = "3d_characterbody";
		}
	}

	public class CharacterHairMeshShaderBindings : MeshShaderBindings
	{
		public CharacterHairMeshShaderBindings()
		{
			vertexShaderName = "3d_common";
			fragmentShaderName = "3d_characterhair";
		}
	}

	public class FlatLightShaderBindings : MeshShaderBindings
	{
		public FlatLightShaderBindings()
		{
			vertexShaderName = "3d_common";
			fragmentShaderName = "3d_flatlight";
		}
	}
}
