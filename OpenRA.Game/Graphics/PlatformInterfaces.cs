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
using System.Collections.Generic;
using OpenRA.Graphics;
using OpenRA.Primitives;

namespace OpenRA
{
	public enum GLProfile
	{
		Automatic,
		ANGLE,
		Modern,
		Embedded,
		Legacy
	}

	public interface IPlatform
	{
		IPlatformWindow CreateWindow(Size size, WindowMode windowMode, float scaleModifier, int batchSize, int videoDisplay, GLProfile profile, bool enableLegacyGL);
		ISoundEngine CreateSound(string device);
		IFont CreateFont(byte[] data);
	}

	public interface IHardwareCursor : IDisposable { }

	public enum BlendMode : byte
	{
		None,
		Alpha,
		Additive,
		Subtractive,
		Multiply,
		Multiplicative,
		DoubleMultiplicative,
		LowAdditive,
		Screen,
		Translucent
	}

	public interface IPlatformWindow : IDisposable
	{
		IGraphicsContext Context { get; }

		Size NativeWindowSize { get; }
		Size EffectiveWindowSize { get; }
		float NativeWindowScale { get; }
		float EffectiveWindowScale { get; }
		Size SurfaceSize { get; }
		int DisplayCount { get; }
		int CurrentDisplay { get; }
		bool HasInputFocus { get; }
		bool IsSuspended { get; }

		event Action<float, float, float, float> OnWindowScaleChanged;

		void PumpInput(IInputHandler inputHandler);
		string GetClipboardText();
		bool SetClipboardText(string text);

		void GrabWindowMouseFocus();
		void ReleaseWindowMouseFocus();

		IHardwareCursor CreateHardwareCursor(string name, Size size, byte[] data, int2 hotspot, bool pixelDouble);
		void SetHardwareCursor(IHardwareCursor cursor);
		void SetWindowTitle(string title);
		void SetRelativeMouseMode(bool mode);
		void SetScaleModifier(float scale);

		GLProfile GLProfile { get; }

		GLProfile[] SupportedGLProfiles { get; }
	}

	public interface IGraphicsContext : IDisposable
	{
		IVertexBuffer<T> CreateVertexBuffer<T>(int size) where T : struct;
		ITexture CreateTexture();
		IFrameBuffer CreateFrameBuffer(Size s);
		IFrameBuffer CreateFrameBuffer(Size s, Color clearColor);
		IShader CreateShader<T>() where T : IShaderBindings;
		IShader CreateUnsharedShader<T>() where T : IShaderBindings;
		void EnableScissor(int x, int y, int width, int height);
		void DisableScissor();
		void Present();
		void DrawPrimitives(PrimitiveType pt, int firstVertex, int numVertices);
		void Clear();
		void EnableDepthBuffer();
		void DisableDepthBuffer();
		void ClearDepthBuffer();
		void SetBlendMode(BlendMode mode);
		void SetVSyncEnabled(bool enabled);
		string GLVersion { get; }
	}

	public interface IVertexBuffer : IDisposable
	{
		void Bind();
	}

	public interface IVertexBuffer<T> : IVertexBuffer
		where T : struct
	{
		void SetData(T[] vertices, int length);
		void SetData(T[] vertices, int offset, int start, int length);
	}

	public interface IShaderBindings
	{
		string VertexShaderName { get; }
		string FragmentShaderName { get; }
		int Stride { get; }
		IEnumerable<ShaderVertexAttribute> Attributes { get; }
		void SetRenderData(IShader shader, ModelRenderData renderData);
	}

	public interface IShader
	{
		void SetRenderData(ModelRenderData renderData);
		void SetBool(string name, bool value);
		void SetInt(string name, int value);
		void SetFloat(string name, float value);
		void SetVec(string name, float x);
		void SetVec(string name, float x, float y);
		void SetVec(string name, float x, float y, float z);
		void SetVec(string name, float[] vec, int length);
		void SetTexture(string param, ITexture texture);
		void SetMatrix(string param, float[] mtx);
		void PrepareRender();
		void LayoutAttributes();
	}

	public enum TextureScaleFilter { Nearest, Linear }

	public interface ITexture : IDisposable
	{
		void SetData(byte[] colors, int width, int height, TextureType type = TextureType.BGRA);
		void SetFloatData(float[] data, int width, int height, TextureType type = TextureType.RGBA);
		byte[] GetData();
		Size Size { get; }
		TextureScaleFilter ScaleFilter { get; set; }
	}

	public enum TextureType
	{
		BGRA,
		RGBA,
		RGB,
		Gray,
	}

	public interface IFrameBuffer : IDisposable
	{
		void Bind();
		void Unbind();
		void EnableScissor(Rectangle rect);
		void DisableScissor();
		ITexture Texture { get; }
	}

	public enum PrimitiveType
	{
		PointList,
		LineList,
		TriangleList,
	}

	public readonly struct Range<T>
	{
		public readonly T Start, End;
		public Range(T start, T end) { Start = start; End = end; }
	}

	public enum WindowMode
	{
		Windowed,
		Fullscreen,
		PseudoFullscreen,
	}

	public interface IFont : IDisposable
	{
		FontGlyph CreateGlyph(char c, int size, float deviceScale);
	}

	public struct FontGlyph
	{
		public int2 Offset;
		public Size Size;
		public float Advance;
		public byte[] Data;
	}
}
