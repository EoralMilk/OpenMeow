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
using System.IO;
using OpenRA.FileFormats;
using OpenRA.Primitives;

namespace OpenRA.Graphics
{
	public sealed class Sheet : IDisposable
	{
		bool dirty;
		bool releaseBufferOnCommit;
		readonly bool releaseData = true;
		ITexture texture;
		byte[] data;

		public readonly TextureWrap WrapType = TextureWrap.ClampToEdge;
		public readonly Size Size;
		public readonly SheetType Type;

		public byte[] GetData()
		{
			CreateBuffer();
			return data;
		}

		public bool Buffered => data != null || texture == null;

		public Sheet(SheetType type, Size size, TextureWrap textureWrap = TextureWrap.ClampToEdge)
		{
			Type = type;
			Size = size;
			WrapType = textureWrap;
		}

		public Sheet(SheetType type, ITexture texture, TextureWrap textureWrap = TextureWrap.ClampToEdge)
		{
			Type = type;
			this.texture = texture;
			Size = texture.Size;
			WrapType = textureWrap;
		}

		public Sheet(SheetType type, Stream stream, TextureWrap textureWrap = TextureWrap.ClampToEdge, bool releaseData = true)
		{
			WrapType = textureWrap;
			var png = new Png(stream);
			Size = new Size(png.Width, png.Height);
			data = new byte[4 * Size.Width * Size.Height];
			Util.FastCopyIntoSprite(new Sprite(this, new Rectangle(0, 0, png.Width, png.Height), TextureChannel.Red), png);

			Type = type;
			this.releaseData = releaseData;
			ReleaseBuffer();
		}

		public ITexture GetTexture()
		{
			if (texture == null)
			{
				texture = Game.Renderer.Context.CreateTexture();
				texture.WrapType = WrapType;
				dirty = true;
			}

			if (data != null && dirty)
			{
				texture.SetData(data, Size.Width, Size.Height);
				dirty = false;
				if (releaseBufferOnCommit && releaseData)
					data = null;
			}

			return texture;
		}

		public Png AsPng()
		{
			if (Type == SheetType.Indexed)
				throw new InvalidOperationException("AsPng() cannot be called on Indexed sheets.");

			return new Png(GetData(), SpriteFrameType.Bgra32, Size.Width, Size.Height);
		}

		public Png AsPng(TextureChannel channel, IPalette pal)
		{
			if (Type != SheetType.Indexed)
				throw new InvalidOperationException("AsPng(TextureChannel, IPalette) can only be called on Indexed sheets.");

			var d = GetData();
			var plane = new byte[Size.Width * Size.Height];
			var dataStride = 4 * Size.Width;
			var channelOffset = (int)channel;

			for (var y = 0; y < Size.Height; y++)
				for (var x = 0; x < Size.Width; x++)
					plane[y * Size.Width + x] = d[y * dataStride + channelOffset + 4 * x];

			var palColors = new Color[Palette.Size];
			for (var i = 0; i < Palette.Size; i++)
				palColors[i] = pal.GetColor(i);

			return new Png(plane, SpriteFrameType.Indexed8, Size.Width, Size.Height, palColors);
		}

		public void CreateBuffer()
		{
			if (data != null)
				return;
			if (texture == null)
				data = new byte[4 * Size.Width * Size.Height];
			else
				data = texture.GetData();
			releaseBufferOnCommit = false;
		}

		public void CommitBufferedData()
		{
			if (!Buffered)
				throw new InvalidOperationException(
					"This sheet is unbuffered. You cannot call CommitBufferedData on an unbuffered sheet. " +
					"If you need to completely replace the texture data you should set data into the texture directly. " +
					"If you need to make only small changes to the texture data consider creating a buffered sheet instead.");

			dirty = true;
		}

		public void ReleaseBuffer()
		{
			if (!Buffered)
				return;
			dirty = true;
			releaseBufferOnCommit = true;

			// Commit data from the buffer to the texture, allowing the buffer to be released and reclaimed by GC.
			if (Game.Renderer != null)
				GetTexture();
		}

		public void Dispose()
		{
			texture?.Dispose();
		}

		public void RefreshTexture()
		{
			//texture?.Dispose();
			texture = null;
			GetTexture();
		}
	}
}
