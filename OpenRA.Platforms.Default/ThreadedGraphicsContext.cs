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
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using OpenRA.Graphics;
using OpenRA.Primitives;

namespace OpenRA.Platforms.Default
{
	/// <summary>
	/// Creates a dedicated thread for the graphics device. An internal message queue is used to perform actions on the
	/// device. This allows calls to be enqueued to be processed asynchronously and thus free up the calling thread.
	/// </summary>
	sealed class ThreadedGraphicsContext : IGraphicsContext
	{
		// PERF: Maintain several object pools to reduce allocations.
		readonly Dictionary<Type, Stack<object>> verticesPool = new Dictionary<Type, Stack<object>>();

		readonly Stack<Message> messagePool = new Stack<Message>();
		readonly Queue<Message> messages = new Queue<Message>();

		public readonly int BatchSize;
		readonly object syncObject = new object();
		readonly Thread renderThread;
		volatile ExceptionDispatchInfo messageException;

		// Delegates that perform actions on the real device.
		Func<object> doClear;
		Action doClearDepthBuffer;
		Action doDisableDepthBuffer;
		Action<object> doEnableDepthBuffer;
		Action<object> doEnableDepthTest;
		Action<object> doEnableDepthWrite;
		Action<object> doEnableCullFace;
		Action doDisableCullFace;
		Action doDisableScissor;
		Action doPresent;

		Func<string> getGLVersion;
		Func<ITexture> getCreateTexture;

		Func<object, IFrameBuffer> getCreateFrameBuffer;
		Func<object, IFrameBuffer> getCreateDepthFrameBuffer;
		Func<object, IShader> getCreateShader;
		Func<object, IShader> getCreateUnsharedShader;
		Func<object, Type, object> getCreateVertexBuffer;
		Func<object, ITexture> getCreateInfoTexture;
		Action<object> doDrawPrimitives;
		Action<object> doDrawInstances;
		Action<object> doEnableScissor;
		Action<object> doSetBlendMode;
		Action<object> doSetVSync;
		Action<object> doSetViewport;

		public ThreadedGraphicsContext(Sdl2GraphicsContext context, int batchSize)
		{
			BatchSize = batchSize;
			renderThread = new Thread(RenderThread)
			{
				Name = "ThreadedGraphicsContext RenderThread",
				IsBackground = true
			};
			lock (syncObject)
			{
				// Start and wait for the rendering thread to have initialized before returning.
				// Otherwise, the delegates may not have been set yet.
				renderThread.Start(context);
				Monitor.Wait(syncObject);
			}
		}

		void RenderThread(object contextObject)
		{
			using (var context = (Sdl2GraphicsContext)contextObject)
			{
				// This lock allows the constructor to block until initialization completes.
				lock (syncObject)
				{
					context.InitializeOpenGL();

					doClear = () => { context.Clear(); return null; };
					doClearDepthBuffer = () => context.ClearDepthBuffer();
					doDisableDepthBuffer = () => context.DisableDepthBuffer();
					doEnableDepthBuffer = type => context.EnableDepthBuffer((DepthFunc)type);
					doEnableDepthTest = type => context.EnableDepthTest((DepthFunc)type);
					doEnableDepthWrite = enable => context.EnableDepthWrite((bool)enable);
					doEnableCullFace = type => context.EnableCullFace((FaceCullFunc)type);
					doDisableCullFace = () => context.DisableCullFace();
					doDisableScissor = () => context.DisableScissor();
					doPresent = () => context.Present();
					getGLVersion = () => context.GLVersion;
					getCreateTexture = () => new ThreadedTexture(this, (ITextureInternal)context.CreateTexture());
					getCreateFrameBuffer =
						tuple =>
						{
							var t = (ValueTuple<Size, Color>)tuple;
							return new ThreadedFrameBuffer(this,
								context.CreateFrameBuffer(t.Item1, (ITextureInternal)CreateTexture(), t.Item2));
						};

					getCreateDepthFrameBuffer =
						tuple =>
						{
							var t = (ValueTuple<Size, Color>)tuple;
							return new ThreadedFrameBuffer(this, context.CreateDepthFrameBuffer(t.Item1));
						};

					getCreateShader = type => new ThreadedShader(this, context.CreateShader((Type)type));
					getCreateInfoTexture = size => context.CreateInfoTexture((Size)size);
					getCreateUnsharedShader = type => new ThreadedShader(this, context.CreateUnsharedShader((Type)type));
					getCreateVertexBuffer = (length, type) =>
					{
						var vertexBuffer = context.GetType().GetMethod(nameof(context.CreateVertexBuffer)).MakeGenericMethod(type).Invoke(context, new[] { length });
						return typeof(ThreadedVertexBuffer<>).MakeGenericType(type).GetConstructors().First().Invoke(new[] { this, vertexBuffer });
					};
					doDrawPrimitives =
						 tuple =>
						 {
							 var t = (ValueTuple<PrimitiveType, int, int>)tuple;
							 context.DrawPrimitives(t.Item1, t.Item2, t.Item3);
						 };
					doDrawInstances =
						tuple =>
						{
							var t = (ValueTuple<PrimitiveType, int, int, int, bool>)tuple;
							context.DrawInstances(t.Item1, t.Item2, t.Item3, t.Item4, t.Item5);
						};
					doEnableScissor =
						tuple =>
						{
							var t = (ValueTuple<int, int, int, int>)tuple;
							context.EnableScissor(t.Item1, t.Item2, t.Item3, t.Item4);
						};
					doSetBlendMode = mode => { context.SetBlendMode((BlendMode)mode); };
					doSetVSync = enabled => { context.SetVSyncEnabled((bool)enabled); };
					doSetViewport = tuple =>
					{
						var t = (ValueTuple<int, int>)tuple;
						context.SetViewport(t.Item1, t.Item2);
					};

					Monitor.Pulse(syncObject);
				}

				// Run a message loop.
				// Only this rendering thread can perform actions on the real device,
				// so other threads must send us a message which we process here.
				Message message;
				while (true)
				{
					lock (messages)
					{
						if (messages.Count == 0)
						{
							if (messageException != null)
								break;

							Monitor.Wait(messages);
						}

						message = messages.Dequeue();
					}

					if (message == null)
						break;

					message.Execute();
				}
			}
		}

		internal T[] GetVertices<T>(int size)
			where T : struct
		{
			lock (verticesPool)
				if (verticesPool.ContainsKey(typeof(T)))
					if (size <= BatchSize && verticesPool[typeof(T)].Count > 0)
						return verticesPool[typeof(T)].Pop() as T[];

			return new T[size < BatchSize ? BatchSize : size];
		}

		internal void ReturnVertices<T>(T[] vertices)
		{
			if (vertices.Length == BatchSize)
				lock (verticesPool)
				{
					if (!verticesPool.ContainsKey(typeof(T)))
						verticesPool.Add(typeof(T), new Stack<object>());
					verticesPool[typeof(T)].Push(vertices);
				}
		}

		class Message
		{
			public Message(ThreadedGraphicsContext device)
			{
				this.device = device;
			}

			readonly AutoResetEvent completed = new AutoResetEvent(false);
			readonly ThreadedGraphicsContext device;
			volatile Action action;
			volatile Action<object> actionWithParam;
			volatile Func<object> func;
			volatile Func<object, object> funcWithParam;
			volatile Func<object, Type, object> funcWithParamAndGeneric;
			volatile object param;
			volatile Type genericType;
			volatile object result;
			volatile ExceptionDispatchInfo edi;

			public void SetAction(Action method)
			{
				action = method;
			}

			public void SetAction(Action<object> method, object state)
			{
				actionWithParam = method;
				param = state;
			}

			public void SetAction(Func<object> method)
			{
				func = method;
			}

			public void SetAction(Func<object, object> method, object state)
			{
				funcWithParam = method;
				param = state;
			}

			public void SetAction(Func<object, Type, object> method, object state, Type type)
			{
				funcWithParamAndGeneric = method;
				param = state;
				genericType = type;
			}

			public void Execute()
			{
				var wasSend = action != null || actionWithParam != null;
				try
				{
					if (action != null)
					{
						action();
						result = null;
						action = null;
					}
					else if (actionWithParam != null)
					{
						actionWithParam(param);
						result = null;
						actionWithParam = null;
						param = null;
					}
					else if (func != null)
					{
						result = func();
						func = null;
					}
					else if (funcWithParamAndGeneric != null)
					{
						result = funcWithParamAndGeneric(param, genericType);
						funcWithParamAndGeneric = null;
						param = null;
						genericType = null;
					}
					else
					{
						result = funcWithParam(param);
						funcWithParam = null;
						param = null;
					}
				}
				catch (Exception ex)
				{
					edi = ExceptionDispatchInfo.Capture(ex);

					if (wasSend)
						device.messageException = edi;

					result = null;
					param = null;
					genericType = null;
					action = null;
					actionWithParam = null;
					func = null;
					funcWithParam = null;
					funcWithParamAndGeneric = null;
				}

				if (wasSend)
				{
					lock (device.messagePool)
						device.messagePool.Push(this);
				}
				else
				{
					completed.Set();
				}
			}

			public object Result()
			{
				completed.WaitOne();

				var localEdi = edi;
				edi = null;
				var localResult = result;
				result = null;

				localEdi?.Throw();
				return localResult;
			}
		}

		Message GetMessage()
		{
			lock (messagePool)
				if (messagePool.Count > 0)
					return messagePool.Pop();

			return new Message(this);
		}

		void QueueMessage(Message message)
		{
			var exception = messageException;
			exception?.Throw();

			lock (messages)
			{
				messages.Enqueue(message);
				if (messages.Count == 1)
					Monitor.Pulse(messages);
			}
		}

		object RunMessage(Message message)
		{
			QueueMessage(message);
			var result = message.Result();
			lock (messagePool)
				messagePool.Push(message);
			return result;
		}

		/// <summary>
		/// Sends a message to the rendering thread.
		/// This method blocks until the message is processed, and returns the result.
		/// </summary>
		public T Send<T>(Func<T> method) where T : class
		{
			if (renderThread == Thread.CurrentThread)
				return method();

			var message = GetMessage();
			message.SetAction(method);
			return (T)RunMessage(message);
		}

		/// <summary>
		/// Sends a message to the rendering thread.
		/// This method blocks until the message is processed, and returns the result.
		/// </summary>
		public object Send(Func<object, Type, object> method, object state, Type type)
		{
			if (renderThread == Thread.CurrentThread)
				return method(state, type);

			var message = GetMessage();
			message.SetAction(method, state, type);
			return RunMessage(message);
		}

		/// <summary>
		/// Sends a message to the rendering thread.
		/// This method blocks until the message is processed, and returns the result.
		/// </summary>
		public T Send<T>(Func<object, T> method, object state) where T : class
		{
			if (renderThread == Thread.CurrentThread)
				return method(state);

			var message = GetMessage();
			message.SetAction(method, state);
			return (T)RunMessage(message);
		}

		/// <summary>
		/// Posts a message to the rendering thread.
		/// This method then returns immediately and does not wait for the message to be processed.
		/// </summary>
		public void Post(Action method)
		{
			if (renderThread == Thread.CurrentThread)
			{
				method();
				return;
			}

			var message = GetMessage();
			message.SetAction(method);
			QueueMessage(message);
		}

		/// <summary>
		/// Posts a message to the rendering thread.
		/// This method then returns immediately and does not wait for the message to be processed.
		/// </summary>
		public void Post(Action<object> method, object state)
		{
			if (renderThread == Thread.CurrentThread)
			{
				method(state);
				return;
			}

			var message = GetMessage();
			message.SetAction(method, state);
			QueueMessage(message);
		}

		public void Dispose()
		{
			// Use a null message to signal the rendering thread to clean up, then wait for it to complete.
			QueueMessage(null);
			renderThread.Join();
		}

		public string GLVersion => Send(getGLVersion);

		public void Clear()
		{
			// We send the clear even though we could just post it.
			// This ensures all previous messages have been processed before we return.
			// This prevents us from queuing up work faster than it can be processed if rendering is behind.
			Send(doClear);
		}

		public void ClearDepthBuffer()
		{
			Post(doClearDepthBuffer);
		}

		public IFrameBuffer CreateDepthFrameBuffer(Size s)
		{
			return Send(getCreateDepthFrameBuffer, (s, Color.FromArgb(0)));
		}

		public IFrameBuffer CreateFrameBuffer(Size s)
		{
			return Send(getCreateFrameBuffer, (s, Color.FromArgb(0)));
		}

		public IFrameBuffer CreateFrameBuffer(Size s, Color clearColor)
		{
			return Send(getCreateFrameBuffer, (s, clearColor));
		}

		public ITexture CreateInfoTexture(Size size)
		{
			return Send(getCreateInfoTexture, size);
		}

		public IShader CreateUnsharedShader<T>()
					where T : IShaderBindings
		{
			return Send(getCreateUnsharedShader, typeof(T));
		}

		public IShader CreateShader<T>()
			where T : IShaderBindings
		{
			return Send(getCreateShader, typeof(T));
		}

		public ITexture CreateTexture()
		{
			return Send(getCreateTexture);
		}

		public IVertexBuffer<T> CreateVertexBuffer<T>(int length)
			where T : struct
		{
			return (IVertexBuffer<T>)Send(getCreateVertexBuffer, length, typeof(T));
		}

		public void DisableDepthBuffer()
		{
			Post(doDisableDepthBuffer);
		}

		public void DisableScissor()
		{
			Post(doDisableScissor);
		}

		public void DrawPrimitives(PrimitiveType type, int firstVertex, int numVertices)
		{
			Post(doDrawPrimitives, (type, firstVertex, numVertices));
		}

		public void DrawInstances(PrimitiveType type, int firstVertex, int numVertices, int count, bool elemented)
		{
			Post(doDrawInstances, (type, firstVertex, numVertices, count, elemented));
		}

		public void EnableDepthBuffer(DepthFunc type)
		{
			Post(doEnableDepthBuffer, type);
		}

		public void EnableDepthTest(DepthFunc type)
		{
			Post(doEnableDepthTest, type);
		}

		public void EnableDepthWrite(bool enable)
		{
			Post(doEnableDepthWrite, enable);
		}

		public void EnableCullFace(FaceCullFunc type)
		{
			Post(doEnableCullFace, type);
		}

		public void DisableCullFace()
		{
			Post(doDisableCullFace);
		}

		public void EnableScissor(int left, int top, int width, int height)
		{
			Post(doEnableScissor, (left, top, width, height));
		}

		public void Present()
		{
			Post(doPresent);
		}

		public void SetBlendMode(BlendMode mode)
		{
			Post(doSetBlendMode, mode);
		}

		public void SetVSyncEnabled(bool enabled)
		{
			Post(doSetVSync, enabled);
		}

		public void SetViewport(int width, int height)
		{
			Post(doSetViewport, (width, height));
		}
	}

	class ThreadedFrameBuffer : IFrameBuffer
	{
		readonly ThreadedGraphicsContext device;
		readonly Func<ITexture> getTexture;
		readonly Func<ITexture> getDepthTexture;
		readonly Action bind;
		readonly Action bindNotFlush;
		readonly Action setViewport;
		readonly Action setViewportBack;
		readonly Action unbind;
		readonly Action unbindnotflush;
		readonly Action dispose;
		readonly Action<object> enableScissor;
		readonly Action disableScissor;

		public ThreadedFrameBuffer(ThreadedGraphicsContext device, IFrameBuffer frameBuffer)
		{
			this.device = device;
			getTexture = () => frameBuffer.Texture;
			getDepthTexture = () => frameBuffer.DepthTexture;
			bind = frameBuffer.Bind;
			bindNotFlush = frameBuffer.BindNotFlush;
			setViewport = frameBuffer.SetViewport;
			setViewportBack = frameBuffer.SetViewportBack;
			unbind = frameBuffer.Unbind;
			unbindnotflush = frameBuffer.UnbindNotFlush;
			dispose = frameBuffer.Dispose;

			enableScissor = rect => frameBuffer.EnableScissor((Rectangle)rect);
			disableScissor = frameBuffer.DisableScissor;
		}

		public ITexture Texture => device.Send(getTexture);
		public ITexture DepthTexture => device.Send(getDepthTexture);

		public void SetViewport()
		{
			device.Post(setViewport);
		}

		public void SetViewportBack()
		{
			device.Post(setViewport);
		}

		public void UnbindNotFlush()
		{
			device.Post(UnbindNotFlush);
		}

		public void Bind()
		{
			device.Post(bind);
		}

		public void BindNotFlush()
		{
			device.Post(bindNotFlush);
		}

		public void Unbind()
		{
			device.Post(unbind);
		}

		public void EnableScissor(Rectangle rect)
		{
			device.Post(enableScissor, rect);
		}

		public void DisableScissor()
		{
			device.Post(disableScissor);
		}

		public void Dispose()
		{
			device.Post(dispose);
		}
	}

	class ThreadedVertexBuffer<T> : IVertexBuffer<T>
		where T : struct
	{
		readonly ThreadedGraphicsContext device;
		readonly Action bind;
		readonly Action<object> setData1;
		readonly Action<object> setData2;
		readonly Func<object, object> setData3;
		readonly Action<object> setEboData;
		readonly Action dispose;
		readonly Func<bool> getHasEbo;

		public ThreadedVertexBuffer(ThreadedGraphicsContext device, IVertexBuffer<T> vertexBuffer)
		{
			this.device = device;
			bind = vertexBuffer.Bind;
			setData1 = tuple => { var t = (ValueTuple<T[], int>)tuple; vertexBuffer.SetData(t.Item1, t.Item2); device.ReturnVertices(t.Item1.Cast<object>().ToArray()); };
			setData2 = tuple => { var t = (ValueTuple<T[], int, int, int>)tuple; vertexBuffer.SetData(t.Item1, t.Item2, t.Item3, t.Item4); device.ReturnVertices(t.Item1.Cast<object>().ToArray()); };
			setData3 = tuple => { setData2(tuple); return null; };
			setEboData = tuple => { var t = (ValueTuple<uint[], int>)tuple; vertexBuffer.SetElementData(t.Item1, t.Item2); device.ReturnVertices(t.Item1.Cast<object>().ToArray()); };
			dispose = vertexBuffer.Dispose;
			getHasEbo = () => vertexBuffer.HasElementBuffer;
		}

		public bool HasElementBuffer => getHasEbo();

		public void Bind()
		{
			device.Post(bind);
		}

		public void SetElementData(uint[] indices, int length)
		{
			var buffer = device.GetVertices<uint>(length);
			Array.Copy(indices, buffer, length);
			device.Post(setEboData, (buffer, length));
		}

		public void SetData(T[] vertices, int length)
		{
			var buffer = device.GetVertices<T>(length);
			Array.Copy(vertices, buffer, length);
			device.Post(setData1, (buffer, length));
		}

		public void SetData(T[] vertices, int offset, int start, int length)
		{
			if (length <= device.BatchSize)
			{
				// If we are able to use a buffer without allocation, post a message to avoid blocking.
				var buffer = device.GetVertices<T>(length);
				Array.Copy(vertices, offset, buffer, 0, length);
				device.Post(setData2, (buffer, 0, start, length));
			}
			else
			{
				// If the length is too large for a buffer, send a message and block to avoid allocations.
				device.Send(setData3, (vertices, offset, start, length));
			}
		}

		public void Dispose()
		{
			device.Post(dispose);
		}
	}

	class ThreadedTexture : ITextureInternal
	{
		readonly ThreadedGraphicsContext device;
		readonly uint id;
		readonly Func<object> getScaleFilter;
		readonly Action<object> setScaleFilter;
		readonly Func<object> getWrapType;
		readonly Action<object> setWrapType;
		readonly Func<object> getSize;
		readonly Action<object> setEmpty;
		readonly Func<byte[]> getData;
		readonly Action<object> setData1;
		readonly Func<object, object> setData2;
		readonly Action<object> setData3;
		readonly Func<object, object> setData4;
		readonly Action dispose;

		public ThreadedTexture(ThreadedGraphicsContext device, ITextureInternal texture)
		{
			this.device = device;
			id = texture.ID;
			getScaleFilter = () => texture.ScaleFilter;
			setScaleFilter = value => texture.ScaleFilter = (TextureScaleFilter)value;
			getWrapType = () => texture.WrapType;
			setWrapType = value => texture.WrapType = (TextureWrap)value;
			getSize = () => texture.Size;
			setEmpty = tuple => { var t = (ValueTuple<int, int>)tuple; texture.SetEmpty(t.Item1, t.Item2); };
			getData = () => texture.GetData();
			setData1 = tuple => { var t = (ValueTuple<byte[], int, int, TextureType>)tuple; texture.SetData(t.Item1, t.Item2, t.Item3, t.Item4); };
			setData2 = tuple => { setData1(tuple); return null; };
			setData3 = tuple => { var t = (ValueTuple<float[], int, int, TextureType>)tuple; texture.SetFloatData(t.Item1, t.Item2, t.Item3, t.Item4); };
			setData4 = tuple => { setData3(tuple); return null; };
			dispose = texture.Dispose;
		}

		public uint ID => id;

		public TextureScaleFilter ScaleFilter
		{
			get => (TextureScaleFilter)device.Send(getScaleFilter);

			set => device.Post(setScaleFilter, value);
		}

		public TextureWrap WrapType
		{
			get => (TextureWrap)device.Send(getWrapType);

			set => device.Post(setWrapType, value);
		}

		public Size Size => (Size)device.Send(getSize);

		public void SetEmpty(int width, int height)
		{
			device.Post(setEmpty, (width, height));
		}

		public byte[] GetData()
		{
			return device.Send(getData);
		}

		public void SetData(byte[] colors, int width, int height, TextureType type = TextureType.BGRA)
		{
			// Objects 85000 bytes or more will be directly allocated in the Large Object Heap (LOH).
			// https://docs.microsoft.com/en-us/dotnet/standard/garbage-collection/large-object-heap
			if (colors.Length < 85000)
			{
				// If we are able to create a small array the GC can collect easily, post a message to avoid blocking.
				var temp = new byte[colors.Length];
				Array.Copy(colors, temp, temp.Length);
				device.Post(setData1, (temp, width, height, type));
			}
			else
			{
				// If the length is large and would result in an array on the Large Object Heap (LOH),
				// send a message and block to avoid LOH allocation as this requires a Gen2 collection.
				device.Send(setData2, (colors, width, height, type));
			}
		}

		public void SetFloatData(float[] data, int width, int height, TextureType type = TextureType.RGBA)
		{
			// Objects 85000 bytes or more will be directly allocated in the Large Object Heap (LOH).
			// https://docs.microsoft.com/en-us/dotnet/standard/garbage-collection/large-object-heap
			if (data.Length < 21250)
			{
				// If we are able to create a small array the GC can collect easily, post a message to avoid blocking.
				var temp = new float[data.Length];
				Array.Copy(data, temp, temp.Length);
				device.Post(setData3, (temp, width, height, type));
			}
			else
			{
				// If the length is large and would result in an array on the Large Object Heap (LOH),
				// send a message and block to avoid LOH allocation as this requires a Gen2 collection.
				device.Send(setData4, (data, width, height, type));
			}
		}

		public void Dispose()
		{
			device.Post(dispose);
		}
	}

	class ThreadedShader : IShader
	{
		readonly ThreadedGraphicsContext device;
		readonly Action prepareRender;
		readonly Action<object> setBool;
		readonly Action<object> setInt;
		readonly Action<object> setFloat;
		readonly Action<object> setMatrix;
		readonly Action<object> setTexture;
		readonly Action<object> setVec1;
		readonly Action<object> setVec2;
		readonly Action<object> setVec3;
		readonly Action<object> setVec4;
		readonly Action<object> setVecArray;

		readonly Action layoutAttributes;
		readonly Action layoutInstanceArray;
		readonly Action<object> setCommonParaments;

		public ThreadedShader(ThreadedGraphicsContext device, IShader shader)
		{
			this.device = device;
			prepareRender = shader.PrepareRender;
			setBool = tuple => { var t = (ValueTuple<string, bool>)tuple; shader.SetBool(t.Item1, t.Item2); };
			setInt = tuple => { var t = (ValueTuple<string, int>)tuple; shader.SetInt(t.Item1, t.Item2); };
			setFloat = tuple => { var t = (ValueTuple<string, float>)tuple; shader.SetFloat(t.Item1, t.Item2); };
			setMatrix = tuple => { var t = (ValueTuple<string, float[], int>)tuple; shader.SetMatrix(t.Item1, t.Item2, t.Item3); };
			setTexture = tuple => { var t = (ValueTuple<string, ITexture>)tuple; shader.SetTexture(t.Item1, t.Item2); };
			setVec1 = tuple => { var t = (ValueTuple<string, float>)tuple; shader.SetVec(t.Item1, t.Item2); };
			setVec2 = tuple => { var t = (ValueTuple<string, float, float>)tuple; shader.SetVec(t.Item1, t.Item2, t.Item3); };
			setVec3 = tuple => { var t = (ValueTuple<string, float, float, float>)tuple; shader.SetVec(t.Item1, t.Item2, t.Item3, t.Item4); };
			setVec4 = tuple => { var t = (ValueTuple<string, float, float, float, float>)tuple; shader.SetVec(t.Item1, t.Item2, t.Item3, t.Item4, t.Item5); };
			setVecArray = tuple => { var t = (ValueTuple<string, float[], int, int>)tuple; shader.SetVecArray(t.Item1, t.Item2, t.Item3, t.Item4); };
			layoutAttributes = () => { shader.LayoutAttributes(); };
			layoutInstanceArray = () => { shader.LayoutInstanceArray(); };
			setCommonParaments = tuple => {var t = (ValueTuple<World3DRenderer, bool>)tuple; shader.SetCommonParaments(t.Item1, t.Item2); };
		}

		public void PrepareRender()
		{
			device.Post(prepareRender);
		}

		public void SetBool(string name, bool value)
		{
			device.Post(setBool, (name, value));
		}

		public void SetInt(string name, int value)
		{
			device.Post(setInt, (name, value));
		}

		public void SetFloat(string name, float value)
		{
			device.Post(setFloat, (name, value));
		}

		public void SetMatrix(string param, float[] mtx, int count = 1)
		{
			device.Post(setMatrix, (param, mtx, count));
		}

		public void SetTexture(string param, ITexture texture)
		{
			device.Post(setTexture, (param, texture));
		}

		public void SetVec(string name, float x)
		{
			device.Post(setVec1, (name, x));
		}

		public void SetVec(string name, float x, float y)
		{
			device.Post(setVec2, (name, x, y));
		}

		public void SetVec(string name, float x, float y, float z)
		{
			device.Post(setVec3, (name, x, y, z));
		}

		public void SetVec(string name, float x, float y, float z, float w)
		{
			device.Post(setVec4, (name, x, y, z, w));
		}

		public void SetVecArray(string name, float[] vec, int vecLength, int count)
		{
			device.Post(setVecArray, (name, vec, vecLength, count));
		}

		public void LayoutAttributes()
		{
			device.Post(layoutAttributes);
		}

		public void LayoutInstanceArray()
		{
			device.Post(layoutInstanceArray);
		}

		public void SetCommonParaments(World3DRenderer w3dr, bool sunCamera)
		{
			device.Post(setCommonParaments, (w3dr, sunCamera));
		}
	}
}
