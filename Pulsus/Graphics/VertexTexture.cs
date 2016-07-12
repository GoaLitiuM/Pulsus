using System.Runtime.InteropServices;
using SharpBgfx;

namespace Pulsus.Graphics
{
	[StructLayout(LayoutKind.Sequential)]
	public struct VertexTexture
	{
		public float x;
		public float y;
		public float z;
		public float u;
		public float v;

		public VertexTexture(float x, float y, float z, float u, float v)
		{
			this.x = x;
			this.y = y;
			this.z = z;
			this.u = u;
			this.v = v;
		}

		public static readonly VertexLayout vertexLayout = new VertexLayout()
				.Begin()
				.Add(VertexAttributeUsage.Position, 3, VertexAttributeType.Float)
				.Add(VertexAttributeUsage.TexCoord0, 2, VertexAttributeType.Float)
				.End();
	}
}
