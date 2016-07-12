using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Pulsus
{
	[DebuggerDisplay("{x}, {y}, {z}, {w}")]
	[StructLayout(LayoutKind.Sequential)]
	public struct Float4
	{
		public float x;
		public float y;
		public float z;
		public float w;

		public Float4(float x, float y, float z, float w)
		{
			this.x = x;
			this.y = y;
			this.z = z;
			this.w = w;
		}

		public static implicit operator Float4(Int2 i2)
		{
			return new Float4(i2.x, i2.y, 0.0f, 0.0f);
		}

		public static implicit operator Float4(Float3 f3)
		{
			return new Float4(f3.x, f3.y, f3.z, 0.0f);
		}
	}
}
