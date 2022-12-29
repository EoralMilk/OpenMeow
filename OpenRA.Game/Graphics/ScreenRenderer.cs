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
using System.Numerics;
using System.Collections.Generic;
using OpenRA.Primitives;

namespace OpenRA.Graphics
{
	public class ScreenRenderer
	{
		readonly Renderer renderer;
		readonly IShader shader;

		readonly ScreenVertex[] vertices;
		readonly ScreenVertex[] verticesUI;

		readonly IVertexBuffer<ScreenVertex> vBuffer;
		readonly IVertexBuffer<ScreenVertex> vBufferUI;


		readonly float vertPos = 1.0f;
		float3 screenLight = float3.Ones;
		public Color ScreenTint = Color.White;
		public ScreenRenderer(Renderer renderer, IShader shader)
		{
			this.renderer = renderer;
			this.shader = shader;
			vertices = new ScreenVertex[6];
			verticesUI = new ScreenVertex[6];


			float[] quadVertices = {
							// positions			// texCoords
							-vertPos, vertPos,	0.0f, 1.0f,
							-vertPos, -vertPos,	0.0f, 0.0f,
							vertPos, -vertPos,	1.0f, 0.0f,

							-vertPos, vertPos,	0.0f, 1.0f,
							vertPos, -vertPos,	1.0f, 0.0f,
							vertPos, vertPos,		1.0f, 1.0f
			};

			float[] quadVerticesInvUV = {
							// positions			// texCoords
							-vertPos, vertPos,  0.0f, 0.0f,
							-vertPos, -vertPos, 0.0f, 1.0f,
							vertPos, -vertPos,  1.0f, 1.0f,

							-vertPos, vertPos,  0.0f, 0.0f,
							vertPos, -vertPos,  1.0f, 1.0f,
							vertPos, vertPos,   1.0f, 0.0f
			};

			for (int i = 0; i < 6; i++)
			{
				vertices[i] = new ScreenVertex(quadVertices[i * 4], quadVertices[i * 4 + 1], quadVertices[i * 4 + 2], quadVertices[i * 4 + 3]);
			}

			for (int i = 0; i < 6; i++)
			{
				verticesUI[i] = new ScreenVertex(quadVerticesInvUV[i * 4], quadVerticesInvUV[i * 4 + 1], quadVerticesInvUV[i * 4 + 2], quadVerticesInvUV[i * 4 + 3]);
			}

			vBuffer = renderer.CreateVertexBuffer<ScreenVertex>(6);
			vBuffer.SetData(vertices, 6);
			vBufferUI = renderer.CreateVertexBuffer<ScreenVertex>(6);
			vBufferUI.SetData(verticesUI, 6);
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
			Matrix4x4.Invert(wr.Projection * wr.View, out var invCameraVP);
			shader.SetMatrix("SunVP", NumericUtil.MatRenderValues(sunVP));
			shader.SetMatrix("InvCameraVP", NumericUtil.MatRenderValues(invCameraVP));
			shader.SetFloat("FrameShadowBias", wr.FrameShadowBias);
			shader.SetFloat("AmbientIntencity", wr.AmbientIntencity);
		}

		public void DrawScreen(ITexture screenTexture, BlendMode blendMode = BlendMode.None)
		{
			//Console.WriteLine("ScreenLight: " + screenLight + " ScreenTint: " + ScreenTint);
			shader.SetBool("DrawUI", false);
			shader.SetTexture("screenTexture", screenTexture);
			renderer.Context.SetBlendMode(blendMode);
			shader.PrepareRender();
			renderer.DrawBatch(shader, vBuffer, 0, 6, PrimitiveType.TriangleList);
			renderer.Context.SetBlendMode(BlendMode.None);
		}

		public void DrawUIScreen(ITexture screenTexture, float2 pos, float2 scale, BlendMode blendMode = BlendMode.None)
		{
			//Console.WriteLine("ScreenLight: " + screenLight + " ScreenTint: " + ScreenTint);
			shader.SetTexture("screenTexture", screenTexture);
			shader.SetBool("DrawUI", true);
			shader.SetVec("UIPos", pos.X, pos.Y);
			shader.SetVec("UIScale", scale.X,scale.Y);

			renderer.Context.SetBlendMode(blendMode);
			shader.PrepareRender();
			renderer.DrawBatch(shader, vBufferUI, 0, 6, PrimitiveType.TriangleList);
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
