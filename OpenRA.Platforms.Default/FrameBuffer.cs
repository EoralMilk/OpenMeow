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
using System.Diagnostics;
using System.IO;
using OpenRA.Primitives;

namespace OpenRA.Platforms.Default
{
	sealed class DepthTexture : ITexture, ITextureInternal
	{
		public uint id;
		Size size;
		bool disposed;

		public DepthTexture(Size size)
		{
			OpenGL.glGenTextures(1, out id);
			OpenGL.CheckGLError();

			OpenGL.glBindTexture(OpenGL.GL_TEXTURE_2D, id);
			OpenGL.CheckGLError();

			// Milk: GLES might not support FloatData ? or others? I Don't understand.
			OpenGL.glTexImage2D(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_DEPTH_COMPONENT, size.Width, size.Height,
				0, OpenGL.GL_DEPTH_COMPONENT, OpenGL.GL_UNSIGNED_INT, IntPtr.Zero);
			OpenGL.CheckGLError();

			OpenGL.glTexParameteri(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MAG_FILTER, OpenGL.GL_NEAREST);
			OpenGL.CheckGLError();
			OpenGL.glTexParameteri(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MIN_FILTER, OpenGL.GL_NEAREST);
			OpenGL.CheckGLError();

			OpenGL.glTexParameterf(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_S, OpenGL.GL_CLAMP_TO_EDGE);
			OpenGL.CheckGLError();
			OpenGL.glTexParameterf(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_T, OpenGL.GL_CLAMP_TO_EDGE);
			OpenGL.CheckGLError();

			OpenGL.glTexParameteri(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_BASE_LEVEL, 0);
			OpenGL.CheckGLError();
			OpenGL.glTexParameteri(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MAX_LEVEL, 0);
			OpenGL.CheckGLError();

			this.size = size;
		}

		Size ITexture.Size => size;

		TextureScaleFilter ITexture.ScaleFilter { get => TextureScaleFilter.Nearest; set => throw new NotImplementedException(); }
		TextureWrap ITexture.WrapType { get => TextureWrap.ClampToEdge; set => throw new NotImplementedException(); }
		uint ITextureInternal.ID => id;

		byte[] ITexture.GetData()
		{
			throw new NotImplementedException();
		}

		void ITexture.SetData(byte[] colors, int width, int height, TextureType type)
		{
			throw new NotImplementedException();
		}

		void ITextureInternal.SetEmpty(int width, int height)
		{
			throw new NotImplementedException();
		}

		void ITexture.SetFloatData(float[] data, int width, int height, TextureType type)
		{
			throw new NotImplementedException();
		}

		public void Dispose()
		{
			if (disposed)
				return;
			disposed = true;
			OpenGL.glDeleteTextures(1, ref id);
		}
	}

	sealed class FrameBuffer : ThreadAffine, IFrameBuffer
	{
		readonly ITexture texture;
		readonly Texture texture1;

		readonly DepthTexture depthTexture;
		readonly Size size;
		readonly Color clearColor;
		uint framebuffer, depth;
		bool disposed;
		bool scissored;
		readonly bool onlyDepth;

		public FrameBuffer(Size size, ITextureInternal texture, Color clearColor, bool onlyDepth = false, uint renderTargets = 1)
		{
			this.onlyDepth = onlyDepth;
			this.size = size;
			this.clearColor = clearColor;
			if (!Exts.IsPowerOf2(size.Width) || !Exts.IsPowerOf2(size.Height))
				throw new InvalidDataException($"Frame buffer size ({size.Width}x{size.Height}) must be a power of two");

			OpenGL.glGenFramebuffers(1, out framebuffer);
			OpenGL.CheckGLError();
			OpenGL.glBindFramebuffer(OpenGL.GL_FRAMEBUFFER, framebuffer);
			OpenGL.CheckGLError();

			depthTexture = new DepthTexture(size);
			depth = depthTexture.id;

			OpenGL.glFramebufferTexture2D(OpenGL.GL_FRAMEBUFFER, OpenGL.GL_DEPTH_ATTACHMENT, OpenGL.GL_TEXTURE_2D, depth, 0);
			OpenGL.CheckGLError();

			if (onlyDepth)
			{
				OpenGL.glReadBuffer(OpenGL.GL_NONE);
			}
			else
			{
				// Color
				this.texture = texture;
				texture.SetEmpty(size.Width, size.Height);
				OpenGL.glFramebufferTexture2D(OpenGL.GL_FRAMEBUFFER, OpenGL.GL_COLOR_ATTACHMENT0, OpenGL.GL_TEXTURE_2D, texture.ID, 0);
				OpenGL.CheckGLError();
			}

			if (renderTargets > 1)
			{
				texture1 = new Texture(CreateInternalTexture());

				OpenGL.glFramebufferTexture2D(OpenGL.GL_FRAMEBUFFER, OpenGL.GL_COLOR_ATTACHMENT1, OpenGL.GL_TEXTURE_2D, texture1.ID, 0);
				OpenGL.CheckGLError();
			}

			// - 告诉OpenGL我们将要使用(帧缓冲的)哪种颜色附件来进行渲染
			uint[] attachments = new uint[2] { OpenGL.GL_COLOR_ATTACHMENT0, OpenGL.GL_COLOR_ATTACHMENT1 };
			unsafe
			{
				fixed (uint* ptr = &attachments[0])
				{
					OpenGL.glDrawBuffers(attachments.Length, new IntPtr(ptr));
					OpenGL.CheckGLError();
				}
			}

			// Test for completeness
			var status = OpenGL.glCheckFramebufferStatus(OpenGL.GL_FRAMEBUFFER);
			if (status != OpenGL.GL_FRAMEBUFFER_COMPLETE)
			{
				var error = $"Error creating framebuffer: {status}\n{new StackTrace()}";
				OpenGL.WriteGraphicsLog(error);
				throw new InvalidOperationException("OpenGL Error: See graphics.log for details.");
			}

			// Restore default buffer
			OpenGL.glBindFramebuffer(OpenGL.GL_FRAMEBUFFER, 0);
			OpenGL.CheckGLError();
		}

		uint CreateInternalTexture()
		{
			uint id;
			OpenGL.glGenTextures(1, out id);
			OpenGL.CheckGLError();
			OpenGL.glBindTexture(OpenGL.GL_TEXTURE_2D, id);
			OpenGL.CheckGLError();
			OpenGL.glTexImage2D(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_RGBA, size.Width, size.Height,
				0, OpenGL.GL_RGBA, OpenGL.GL_UNSIGNED_BYTE, IntPtr.Zero);
			OpenGL.CheckGLError();
			OpenGL.glTexParameteri(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MAG_FILTER, OpenGL.GL_NEAREST);
			OpenGL.CheckGLError();
			OpenGL.glTexParameteri(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MIN_FILTER, OpenGL.GL_NEAREST);
			OpenGL.CheckGLError();
			OpenGL.glTexParameterf(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_S, OpenGL.GL_CLAMP_TO_EDGE);
			OpenGL.CheckGLError();
			OpenGL.glTexParameterf(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_T, OpenGL.GL_CLAMP_TO_EDGE);
			OpenGL.CheckGLError();
			return id;
		}

		static int[] ViewportRectangle()
		{
			var v = new int[4];
			OpenGL.glGetIntegerv(OpenGL.GL_VIEWPORT, out v[0]);
			OpenGL.CheckGLError();
			return v;
		}

		int[] cv = new int[4];
		public void Bind()
		{
			VerifyThreadAffinity();

			// Cache viewport rect to restore when unbinding
			cv = ViewportRectangle();

			OpenGL.glFlush();
			OpenGL.CheckGLError();
			OpenGL.glBindFramebuffer(OpenGL.GL_FRAMEBUFFER, framebuffer);
			OpenGL.CheckGLError();
			OpenGL.glViewport(0, 0, size.Width, size.Height);
			OpenGL.CheckGLError();
			OpenGL.glClearColor(clearColor.R, clearColor.G, clearColor.B, clearColor.A);
			OpenGL.CheckGLError();
			OpenGL.glClear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT);
			OpenGL.CheckGLError();
		}

		public void BindNotFlush()
		{
			VerifyThreadAffinity();

			// Cache viewport rect to restore when unbinding
			cv = ViewportRectangle();
			OpenGL.glBindFramebuffer(OpenGL.GL_FRAMEBUFFER, framebuffer);
			OpenGL.CheckGLError();
			OpenGL.glViewport(0, 0, size.Width, size.Height);
			OpenGL.CheckGLError();
		}

		public void SetViewport()
		{
			OpenGL.glViewport(0, 0, size.Width, size.Height);
			OpenGL.CheckGLError();
		}

		public void SetViewportBack()
		{
			OpenGL.glViewport(cv[0], cv[1], cv[2], cv[3]);
			OpenGL.CheckGLError();
		}

		public void Unbind()
		{
			if (scissored)
				throw new InvalidOperationException("Attempting to unbind FrameBuffer with an active scissor region.");

			VerifyThreadAffinity();
			OpenGL.glFlush();
			OpenGL.CheckGLError();
			OpenGL.glBindFramebuffer(OpenGL.GL_FRAMEBUFFER, 0);
			OpenGL.CheckGLError();
			OpenGL.glViewport(cv[0], cv[1], cv[2], cv[3]);
			OpenGL.CheckGLError();
		}

		public void UnbindNotFlush()
		{
			if (scissored)
				throw new InvalidOperationException("Attempting to unbind FrameBuffer with an active scissor region.");

			VerifyThreadAffinity();
			OpenGL.glBindFramebuffer(OpenGL.GL_FRAMEBUFFER, 0);
			OpenGL.CheckGLError();
			OpenGL.glViewport(cv[0], cv[1], cv[2], cv[3]);
			OpenGL.CheckGLError();
		}

		public void EnableScissor(Rectangle rect)
		{
			VerifyThreadAffinity();

			OpenGL.glScissor(rect.X, rect.Y, Math.Max(rect.Width, 0), Math.Max(rect.Height, 0));
			OpenGL.CheckGLError();
			OpenGL.glEnable(OpenGL.GL_SCISSOR_TEST);
			OpenGL.CheckGLError();
			scissored = true;
		}

		public void DisableScissor()
		{
			VerifyThreadAffinity();
			OpenGL.glDisable(OpenGL.GL_SCISSOR_TEST);
			OpenGL.CheckGLError();
			scissored = false;
		}

		public ITexture Texture
		{
			get
			{
				VerifyThreadAffinity();
				if (onlyDepth)
					throw new Exception("This buffer is Only use for DepthRender, can't get texture");

				return texture;
			}
		}

		public ITexture Texture1
		{
			get
			{
				VerifyThreadAffinity();

				return texture1;
			}
		}

		public ITexture DepthTexture
		{
			get
			{
				VerifyThreadAffinity();
				return depthTexture;
			}
		}

		public void Dispose()
		{
			if (disposed)
				return;
			disposed = true;
			texture?.Dispose();
			depthTexture?.Dispose();

			OpenGL.glDeleteFramebuffers(1, ref framebuffer);
			OpenGL.CheckGLError();
			OpenGL.glDeleteTextures(1,ref depth);
			OpenGL.CheckGLError();

			//OpenGL.glDeleteRenderbuffers(1, ref depth);
			//OpenGL.CheckGLError();
		}
	}
}
