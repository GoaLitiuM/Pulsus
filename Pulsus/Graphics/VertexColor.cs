using System.Runtime.InteropServices;
using SharpBgfx;

namespace Pulsus.Graphics
{
	[StructLayout(LayoutKind.Sequential)]
	public struct VertexColor
	{
		public float x;
		public float y;
		public float z;
		public Color color;

		public VertexColor(float x, float y, float z, Color color)
		{
			this.x = x;
			this.y = y;
			this.z = z;
			this.color = color.AsARGB();
		}

		public static readonly VertexLayout vertexLayout = new VertexLayout()
				.Begin()
				.Add(VertexAttributeUsage.Position, 3, VertexAttributeType.Float)
				.Add(VertexAttributeUsage.Color0, 4, VertexAttributeType.UInt8, true)
				.End();
	}
}
