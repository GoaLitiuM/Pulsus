using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Pulsus
{
	[DebuggerDisplay("{x}, {y}")]
	[StructLayout(LayoutKind.Sequential)]
	public struct Int2
	{
		public int x;
		public int y;

		public Int2(int x, int y)
		{
			this.x = x;
			this.y = y;
		}

		public Int2(float x, float y)
		{
			this.x = (int)Math.Round(x);
			this.y = (int)Math.Round(y);
		}

		public static implicit operator Int2(Float3 f3)
		{
			return new Int2((int)Math.Round(f3.x), (int)Math.Round(f3.y));
		}

		public static Int2 operator +(Int2 a, Int2 b)
		{
			return new Int2(a.x + b.x, a.y + b.y);
		}

		public static Int2 operator -(Int2 a, Int2 b)
		{
			return new Int2(a.x - b.x, a.y - b.y);
		}

		public static bool operator ==(Int2 a, Int2 b)
		{
			return a.x == b.x && a.y == b.y;
		}

		public static bool operator !=(Int2 a, Int2 b)
		{
			return a.x != b.x || a.y != b.y;
		}

		public override bool Equals(object obj)
		{
			if (obj == null)
				return false;
			if (obj is Int2)
				return this == (Int2)obj;
			return false;
		}

		public bool Equals(Int2 obj)
		{
			return this == obj;
		}

		public override int GetHashCode()
		{
			return x ^ y;
		}
	}
}