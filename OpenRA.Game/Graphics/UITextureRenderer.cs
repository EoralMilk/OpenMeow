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
using OpenRA.Primitives;

namespace OpenRA.Graphics
{
	public class UITextureRenderer : Renderer.IBatchRenderer
	{
		readonly Renderer renderer;
		public readonly IShader Shader;

		BlendMode currentBlend = BlendMode.Alpha;

		public ITexture Texture { get; private set; }

		/// <summary>
		/// for editor ui brush preview
		/// </summary>
		readonly UITexArrayVertex[] vertices;
		readonly IVertexBuffer<UITexArrayVertex> tempBuffer;
		int nv = 0;

		public UITextureRenderer(Renderer renderer, IShader shader)
		{
			this.renderer = renderer;
			this.Shader = shader;
			vertices = new UITexArrayVertex[renderer.TempBufferSize];
			tempBuffer = renderer.Context.CreateVertexBuffer<UITexArrayVertex>(renderer.TempBufferSize);
		}

		public void Flush(BlendMode blendMode = BlendMode.None)
		{
			if (nv > 0)
			{
				Shader.SetTexture("Textures", Texture);

				renderer.Context.SetBlendMode(blendMode != BlendMode.None ? blendMode : currentBlend);
				Shader.PrepareRender();
				tempBuffer.SetData(vertices, nv);
				renderer.DrawBatch(Shader, tempBuffer, 0, nv, PrimitiveType.TriangleList);
				renderer.Context.SetBlendMode(BlendMode.None);

				nv = 0;
			}
		}

		public void DrawQuadForTexArray(ITexture texture, int index, in int2 pos, float2 size, BlendMode blendMode)
		{
			renderer.CurrentBatchRenderer = this;
			if (blendMode != currentBlend)
				Flush();

			if (texture != Texture)
				Flush();

			Texture = texture;

			if (nv + 6 >= vertices.Length)
				Flush();

			UITexArrayVertex[] quad = new UITexArrayVertex[6];

			float2 tl, tr, bl, br;
			tl = pos;
			tr = new float2(pos.X + size.X, pos.Y);
			bl = new float2(pos.X, pos.Y + size.Y);
			br = new float2(pos.X + size.X, pos.Y + size.Y);

			quad[0] = new UITexArrayVertex(tl.X, tl.Y, 0, 0, index);
			quad[1] = new UITexArrayVertex(bl.X, bl.Y, 0, 1, index);
			quad[2] = new UITexArrayVertex(br.X, br.Y, 1, 1, index);

			quad[3] = new UITexArrayVertex(tr.X, tr.Y, 1, 0, index);
			quad[4] = new UITexArrayVertex(tl.X, tl.Y, 0, 0, index);
			quad[5] = new UITexArrayVertex(br.X, br.Y, 1, 1, index);

			Array.Copy(quad, 0, vertices, nv, quad.Length);

			nv += 6;
		}

		public void SetViewportParams(Size sheetSize, int downscale, float depthMargin, int2 scroll)
		{
			// Calculate the scale (r1) and offset (r2) that convert from OpenRA viewport pixels
			// to OpenGL normalized device coordinates (NDC). OpenGL expects coordinates to vary from [-1, 1],
			// so we rescale viewport pixels to the range [0, 2] using r1 then subtract 1 using r2.
			var width = 2f / (downscale * sheetSize.Width);
			var height = 2f / (downscale * sheetSize.Height);

			var depth = depthMargin != 0f ? 2f / (downscale * (sheetSize.Height + depthMargin)) : 0;

			Shader.SetVec("Scroll", scroll.X, scroll.Y, depthMargin != 0f ? scroll.Y : 0);
			Shader.SetVec("r1", width, height, -depth);
			Shader.SetVec("r2", -1, -1, depthMargin != 0f ? 1 : 0);
		}
	}
}
