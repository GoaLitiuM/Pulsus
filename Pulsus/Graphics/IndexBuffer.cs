using System;
using SharpBgfx;

namespace Pulsus.Graphics
{
	public class IndexBuffer : IDisposable
	{
		public SharpBgfx.IndexBuffer handle;

		public IndexBuffer(ushort[] indices)
		{
			handle = new SharpBgfx.IndexBuffer(MemoryBlock.FromArray(indices));
		}

		public void Dispose()
		{
			handle.Dispose();
		}
	}
}
