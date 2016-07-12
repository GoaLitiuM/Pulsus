using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Pulsus
{
	[DebuggerDisplay("{x}, {y}, {z}")]
	[StructLayout(LayoutKind.Sequential)]
	public struct Float3
	{
		public float x;
		public float y;
		public float z;

		public Float3(float x, float y, float z)
		{
			this.x = x;
			this.y = y;
			this.z = z;
		}

		public static implicit operator Float3(Int2 i2)
		{
			return new Float3(i2.x, i2.y, 0.0f);
		}
	}
}
