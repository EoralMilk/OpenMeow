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
using GlmSharp;
using OpenRA.Primitives;

namespace OpenRA.Graphics
{
	public class ScreenRenderer
	{
		readonly Renderer renderer;
		readonly IShader shader;

		readonly ScreenVertex[] vertices;
		readonly IVertexBuffer<ScreenVertex> vBuffer;

		readonly float vertPos = 1.0f;
		float3 screenLight = float3.Ones;
		public Color ScreenTint = Color.White;
		public ScreenRenderer(Renderer renderer, IShader shader)
		{
			this.renderer = renderer;
			this.shader = shader;
			vertices = new ScreenVertex[6];

			float[] quadVertices = {
							// positions			// texCoords
							-vertPos, vertPos,	0.0f, 1.0f,
							-vertPos, -vertPos,	0.0f, 0.0f,
							vertPos, -vertPos,	1.0f, 0.0f,

							-vertPos, vertPos,	0.0f, 1.0f,
							vertPos, -vertPos,	1.0f, 0.0f,
							vertPos, vertPos,		1.0f, 1.0f
			};

			for (int i = 0; i < 6; i++)
			{
				vertices[i] = new ScreenVertex(quadVertices[i * 4], quadVertices[i * 4 + 1], quadVertices[i * 4 + 2], quadVertices[i * 4 + 3]);
			}

			vBuffer = renderer.CreateVertexBuffer<ScreenVertex>(6);
			vBuffer.SetData(vertices, 6);
			SetScreenLight(Color.White);
		}

		public void DrawScreen(Sprite screenSprite, BlendMode blendMode = BlendMode.None)
		{
			shader.SetTexture("screenTexture", screenSprite.Sheet.GetTexture());
			renderer.Context.SetBlendMode(blendMode);
			shader.PrepareRender();
			renderer.DrawBatch(shader, vBuffer, 0, 6, PrimitiveType.TriangleList);
			renderer.Context.SetBlendMode(BlendMode.None);
		}

		public void SetShadowParams(ITexture shadowDepth, ITexture screenDepth, World3DRenderer wr)
		{
			shader.SetTexture("screenDepthTexture", screenDepth);
			shader.SetTexture("sunDepthTexture", shadowDepth);
			shader.SetBool("FrameBufferShadow", true);
			shader.SetBool("FrameBufferPosition", true);

			var sunVP = wr.SunProjection * wr.SunView;
			var invCameraVP = (wr.Projection * wr.View).Inverse;
			shader.SetMatrix("SunVP", sunVP.Values1D);
			shader.SetMatrix("InvCameraVP", invCameraVP.Values1D);
			shader.SetFloat("FrameShadowBias", wr.FrameShadowBias);
			shader.SetFloat("AmbientIntencity", wr.AmbientIntencity);
		}

		public void DrawScreen(ITexture screenTexture, BlendMode blendMode = BlendMode.None)
		{
			//Console.WriteLine("ScreenLight: " + screenLight + " ScreenTint: " + ScreenTint);
			shader.SetTexture("screenTexture", screenTexture);
			renderer.Context.SetBlendMode(blendMode);
			shader.PrepareRender();
			renderer.DrawBatch(shader, vBuffer, 0, 6, PrimitiveType.TriangleList);
			renderer.Context.SetBlendMode(BlendMode.None);
		}

		public void SetScreenLight(Color color)
		{
			ScreenTint = color;
			screenLight = new float3(((float)ScreenTint.R) / 255, ((float)ScreenTint.G) / 255, ((float)ScreenTint.B) / 255);
			shader.SetVec("ScreenLight", screenLight.X, screenLight.Y, screenLight.Z);
		}
	}
}
