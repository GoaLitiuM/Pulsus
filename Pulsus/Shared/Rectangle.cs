using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Pulsus
{
	[DebuggerDisplay("{x}, {y}, {width}x{height}")]
	[StructLayout(LayoutKind.Sequential)]
	public struct Rectangle
	{
		public Int2 position { get { return new Int2(x, y); } }
		public Int2 size { get { return new Int2(width, height); } }

		public int x;
		public int y;
		public int width;
		public int height;

		public Rectangle(int x, int y, int width, int height)
		{
			this.x = x;
			this.y = y;
			this.width = width;
			this.height = height;
		}

		public Rectangle(Int2 position, Int2 size)
		{
			x = position.x;
			y = position.y;
			width = size.x;
			height = size.y;
		}
	}
}
