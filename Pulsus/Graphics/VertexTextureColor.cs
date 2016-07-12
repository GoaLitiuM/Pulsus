using System.Runtime.InteropServices;
using SharpBgfx;

namespace Pulsus.Graphics
{
	[StructLayout(LayoutKind.Sequential)]
	public struct VertexTextureColor
	{
		public float x;
		public float y;
		public float z;
		public Color color;
		public float u;
		public float v;

		public VertexTextureColor(float x, float y, float z, Color color, float u, float v)
		{
			this.x = x;
			this.y = y;
			this.z = z;
			this.color = color.AsARGB();
			this.u = u;
			this.v = v;
		}

		public static readonly VertexLayout vertexLayout = new VertexLayout()
				.Begin()
				.Add(VertexAttributeUsage.Position, 3, VertexAttributeType.Float)
				.Add(VertexAttributeUsage.Color0, 4, VertexAttributeType.UInt8, true)
				.Add(VertexAttributeUsage.TexCoord0, 2, VertexAttributeType.Float)
				.End();
	}
}
