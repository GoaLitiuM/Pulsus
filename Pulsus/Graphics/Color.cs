using System;
using System.Diagnostics;

namespace Pulsus.Graphics
{
	[DebuggerDisplay("{ToString()}")]
	public struct Color
	{
		public byte r { get { return (byte)((value & 0xFF000000) >> 24); } }
		public byte g { get { return (byte)((value & 0x00FF0000) >> 16); } }
		public byte b { get { return (byte)((value & 0x0000FF00) >> 8); } }
		public byte alpha { get { return (byte)(value & 0x000000FF); } }

		private uint value;

		public Color(uint rgba)
		{
			value = rgba;
		}

		public Color(int r, int g, int b, int alpha = 255)
		{
			value = ((uint)r << 24) + ((uint)g << 16) + ((uint)b << 8) + ((uint)alpha);
		}

		public Color(Color color, int alpha)
		{
			value = color.value & 0xFFFFFF00;
			value += (byte)alpha;
		}

		public Color(Color color, float alpha)
		{
			value = color.value & 0xFFFFFF00;
			value += (byte)(alpha * 255);
		}

		public Color AsARGB()
		{
			return new Color(
				((value & 0x000000FF) << 24) +
				((value & 0x0000FF00) << 8) +
				((value & 0x00FF0000) >> 8) +
				((value & 0xFF000000) >> 24));
		}

		public Float4 AsFloat4()
		{
			return new Float4(r / 255.0f, g / 255.0f, b / 255.0f, alpha / 255.0f);
		}

		public Color AsARGBPremultiplied()
		{
			if (this == White || this == Transparent)
				return this;

			uint a = alpha;
			float f = a / 255.0f;

			uint r = ((uint)Math.Round(this.r * f));
			uint g = ((uint)Math.Round(this.g * f));
			uint b = ((uint)Math.Round(this.b * f));

			return new Color((a << 24) + (b << 16) + (g << 8) + r);
		}

		public Color(float r, float g, float b, float alpha = 1.0f)
		{
			byte ri = (byte)(r * 255);
			byte gi = (byte)(g * 255);
			byte bi = (byte)(b * 255);
			byte alphai = (byte)(alpha * 255);
			value = ((uint)ri << 24) + ((uint)gi << 16) + ((uint)bi << 8) + (uint)alphai;
		}

		public uint GetRGBA()
		{
			return value;
		}

		public static Color operator *(Color color, float f)
		{
			if (f < 0.0f)
				f = 0.0f;

			uint r = Math.Min((uint)Math.Round(color.r * f), 255);
			uint g = Math.Min((uint)Math.Round(color.g * f), 255);
			uint b = Math.Min((uint)Math.Round(color.b * f), 255);
			uint alpha = Math.Min((uint)Math.Round(color.alpha * f), 255);

			color.value = (r << 24) + (g << 16) + (b << 8) + (alpha);
			return color;
		}

		public static implicit operator Color(uint color)
		{
			return new Color(color);
		}

		public static bool operator ==(Color a, Color b)
		{
			return a.value == b.value;
		}

		public static bool operator !=(Color a, Color b)
		{
			return a.value != b.value;
		}

		public override bool Equals(object other)
		{
			if (other == null)
				return false;
			if (other is Color)
				return this == (Color)other;
			return false;
		}

		public bool Equals(Color other)
		{
			return this == other;
		}

		public override int GetHashCode()
		{
			return value.GetHashCode();
		}

		public static readonly Color White = new Color(uint.MaxValue);
		public static readonly Color Black = new Color(uint.MinValue + 255);
		public static readonly Color Transparent = new Color(uint.MinValue);
		public static readonly Color Gray = new Color(0x808080FF);
		public static readonly Color Red = new Color(0xFF0000FF);
		public static readonly Color Green = new Color(0x00FF00FF);
		public static readonly Color Blue = new Color(0x0000FFFF);
		public static readonly Color LightGreen = new Color(0x90EE90FF);
		public static readonly Color Yellow = new Color(0xFFFF00FF);
		public static readonly Color Orange = new Color(0xFFA500FF);
		public static readonly Color OrangeRed = new Color(0xFF4500FF);
		public static readonly Color Purple = new Color(0x800080FF);
		public static readonly Color LightYellow = new Color(0xFFFFE0FF);
		public static readonly Color Magenta = new Color(0xFF00FFFF);

		public override string ToString()
		{
			return r.ToString() + ", " + g.ToString() + ", " + b.ToString() + ", " + alpha.ToString();
		}
	}
}
