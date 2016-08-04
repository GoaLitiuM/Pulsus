using System;
using System.Collections.Generic;
using System.Reflection;
using SharpBgfx;

namespace Pulsus.Graphics
{
	public class FrameBuffer : IDisposable
	{
		public static readonly SharpBgfx.FrameBuffer Invalid;// = SharpBgfx.FrameBuffer.Invalid;

		public SharpBgfx.FrameBuffer handle { get; internal set; }

		static FrameBuffer()
		{
			// BUG: SharpBgfx does not define invalid handle correctly (should be ushort.MaxValue, not 0)

			object invalid = Invalid;

			invalid.GetType().GetField("handle",
				BindingFlags.NonPublic | BindingFlags.Instance).SetValue(invalid, ushort.MaxValue);

			Invalid = (SharpBgfx.FrameBuffer)invalid;
		}

		public FrameBuffer(int width, int height, TextureFlags flags = TextureFlags.None, TextureFormat format = TextureFormat.BGRA8)
		{
			handle = new SharpBgfx.FrameBuffer(width, height, format, flags);
		}

		public FrameBuffer(Texture2D texture, TextureFlags flags = TextureFlags.None, TextureFormat format = TextureFormat.BGRA8)
		{
			Attachment[] attachments = { new Attachment() { Texture = texture.handle, Mip = 0, Layer = 0 } };
			handle = new SharpBgfx.FrameBuffer(attachments);
		}

		public FrameBuffer(Texture2D[] textures, TextureFlags flags = TextureFlags.None, TextureFormat format = TextureFormat.BGRA8)
		{
			List<Attachment> attachments = new List<Attachment>(textures.Length);
			foreach (Texture2D texture in textures)
				attachments.Add(new Attachment() { Texture = texture.handle, Mip = 0, Layer = 0 });

			handle = new SharpBgfx.FrameBuffer(attachments.ToArray());
		}

		public void Dispose()
		{
			handle.Dispose();
		}
	}
}
