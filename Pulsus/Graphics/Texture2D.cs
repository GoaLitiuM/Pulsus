using System;
using System.Diagnostics;
using SharpBgfx;
using Pulsus.FFmpeg;

namespace Pulsus.Graphics
{
	[DebuggerDisplay("{path}, {width}x{height}")]
	public class Texture2D : IDisposable
	{
		public Texture handle { get; internal set; }
		public int width { get { return handle.Width; } }
		public int height { get { return handle.Height; } }

#if DEBUG
		private string path;
#endif

		public Texture2D(byte[] data, int width, int height, TextureFlags flags = TextureFlags.None, TextureFormat format = TextureFormat.BGRA8)
		{
			handle = Texture.Create2D(width, height, 0, format, flags, MemoryBlock.FromArray(data));
		}

		public Texture2D(int width, int height, TextureFlags flags = TextureFlags.None, TextureFormat format = TextureFormat.BGRA8)
		{
			handle = Texture.Create2D(width, height, 0, format, flags);
		}

		public Texture2D(string path, TextureFlags flags = TextureFlags.None, TextureFormat format = TextureFormat.BGRA8)
		{
#if DEBUG
			this.path = System.IO.Path.GetFileNameWithoutExtension(path);
#endif
			int width, height, bytesPerPixel;
			byte[] data = FFmpegHelper.ImageFromFile(path, out width, out height, out bytesPerPixel);
			handle = Texture.Create2D(width, height, 0, format, flags, MemoryBlock.FromArray(data));
		}

		public void SetData(byte[] data)
		{
			handle.Update2D(0, 0, 0, handle.Width, handle.Height, MemoryBlock.FromArray(data), ushort.MaxValue);
		}

		public void SetData(byte[] data, int x, int y, int width, int height)
		{
			handle.Update2D(0, x, y, width, height, MemoryBlock.FromArray(data), ushort.MaxValue);
		}

		public void Dispose()
		{
			handle.Dispose();
		}
	}
}
