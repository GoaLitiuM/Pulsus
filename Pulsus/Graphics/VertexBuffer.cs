using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpBgfx;

namespace Pulsus.Graphics
{
	public class VertexBuffer : IDisposable
	{
		public SharpBgfx.VertexBuffer handle;

		public VertexBuffer(VertexTextureColor[] vertices)
		{
			handle = new SharpBgfx.VertexBuffer(MemoryBlock.FromArray(vertices), VertexTextureColor.vertexLayout);
		}

		public VertexBuffer(VertexTexture[] vertices)
		{
			handle = new SharpBgfx.VertexBuffer(MemoryBlock.FromArray(vertices), VertexTexture.vertexLayout);
		}

		public VertexBuffer(VertexColor[] vertices)
		{
			handle = new SharpBgfx.VertexBuffer(MemoryBlock.FromArray(vertices), VertexColor.vertexLayout);
		}

		public void Dispose()
		{
			handle.Dispose();
		}
	}
}
