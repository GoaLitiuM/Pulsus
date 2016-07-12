using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Pulsus.Graphics
{
	[DebuggerDisplay("{ToString()}")]
	[StructLayout(LayoutKind.Sequential)]
	public struct Matrix4
	{
		public static Matrix4 Identity = new Matrix4(new float[16] {1, 0, 0, 0,  0, 1, 0, 0,  0, 0, 1, 0,  0, 0, 0, 1});
		unsafe private fixed float m[16];

		public unsafe float* Pointer()
		{
			fixed (float* f = m) { return f; }
		}

		public float this[int i]
		{
			get { unsafe { fixed (float* f = m) { return f[i]; } } }
			set { unsafe { fixed (float* f = m) { f[i] = value; } } }
		}

		public Matrix4(float[] f16)
		{
			unsafe
			{
				fixed (float* f = m)
				{
					for (int i = 0; i < 16; ++i)
						f[i] = f16[i];
				}
			}
		}

		public static Matrix4 Ortho(float left, float top, float right, float bottom, float near, float far)
		{
			Matrix4 ortho = Matrix4.Identity;
			ortho[0] = 2.0f / (right-left);
			ortho[5] = 2.0f / (bottom-top);
			ortho[10] = 1.0f / (far - near);
			ortho[12] = (left+right) / (left-right);
			ortho[13] = (top+bottom) / (bottom-top);
			ortho[14] = near / (far-near);
	
			return ortho;
		}

		public Matrix4 Multiply(Matrix4 mat)
		{
			return Multiply(this, mat);
		}

		public static Matrix4 Multiply(Matrix4 mat1, Matrix4 mat2)
		{
			Matrix4 result = new Matrix4();
			result[0] = (mat1[0]*mat2[0]) + (mat1[1]*mat2[4]) + (mat1[2]*mat2[8]) + (mat1[3]*mat2[12]);
			result[1] = (mat1[0]*mat2[1]) + (mat1[1]*mat2[5]) + (mat1[2]*mat2[9]) + (mat1[3]*mat2[13]);
			result[2] = (mat1[0]*mat2[2]) + (mat1[1]*mat2[6]) + (mat1[2]*mat2[10]) + (mat1[3]*mat2[14]);
			result[3] = (mat1[0]*mat2[3]) + (mat1[1]*mat2[7]) + (mat1[2]*mat2[11]) + (mat1[3]*mat2[15]);

			result[4] = (mat1[4]*mat2[0]) + (mat1[5]*mat2[4]) + (mat1[6]*mat2[8]) + (mat1[7]*mat2[12]);
			result[5] = (mat1[4]*mat2[1]) + (mat1[5]*mat2[5]) + (mat1[6]*mat2[9]) + (mat1[7]*mat2[13]);
			result[6] = (mat1[4]*mat2[2]) + (mat1[5]*mat2[6]) + (mat1[6]*mat2[10]) + (mat1[7]*mat2[14]);
			result[7] = (mat1[4]*mat2[3]) + (mat1[5]*mat2[7]) + (mat1[6]*mat2[11]) + (mat1[7]*mat2[15]);

			result[8] = (mat1[8]*mat2[0]) + (mat1[9]*mat2[4]) + (mat1[10]*mat2[8]) + (mat1[11]*mat2[12]);
			result[9] = (mat1[8]*mat2[1]) + (mat1[9]*mat2[5]) + (mat1[10]*mat2[9]) + (mat1[11]*mat2[13]);
			result[10] = (mat1[8]*mat2[2]) + (mat1[9]*mat2[6]) + (mat1[10]*mat2[10]) + (mat1[11]*mat2[14]);
			result[11] = (mat1[8]*mat2[3]) + (mat1[9]*mat2[7]) + (mat1[10]*mat2[11]) + (mat1[11]*mat2[15]);

			result[12] = (mat1[12]*mat2[0]) + (mat1[13]*mat2[4]) + (mat1[14]*mat2[8]) + (mat1[15]*mat2[12]);
			result[13] = (mat1[12]*mat2[1]) + (mat1[13]*mat2[5]) + (mat1[14]*mat2[9]) + (mat1[15]*mat2[13]);
			result[14] = (mat1[12]*mat2[2]) + (mat1[13]*mat2[6]) + (mat1[14]*mat2[10]) + (mat1[15]*mat2[14]);
			result[15] = (mat1[12]*mat2[3]) + (mat1[13]*mat2[7]) + (mat1[14]*mat2[11]) + (mat1[15]*mat2[15]);
			return result;
		}

		public static Matrix4 Translate(float x, float y, float z)
		{
			Matrix4 result = Matrix4.Identity;
			result[12] = x;
			result[13] = y;
			result[14] = z;
			return result;
		}

		public static Matrix4 Scale(float x, float y, float z)
		{
			Matrix4 result = Matrix4.Identity;
			result[0] = x;
			result[5] = y;
			result[10] = z;
			return result;
		}

		public static Matrix4 RotateX(float radians)
		{
			Matrix4 result = Matrix4.Identity;
			result[5] = result[10] = (float)Math.Cos(radians);
			result[9] = (float)Math.Sin(radians);
			result[6] = -result[9];
			return result;
		}

		public static Matrix4 RotateY(float radians)
		{
			Matrix4 result = Matrix4.Identity;
			result[0] = result[10] = (float)Math.Cos(radians);
			result[2] = (float)Math.Sin(radians);
			result[8] = -result[2];
			return result;
		}

		public static Matrix4 RotateZ(float radians)
		{
			Matrix4 result = Matrix4.Identity;
			result[0] = result[5] = (float)Math.Cos(radians);
			result[4] = (float)Math.Sin(radians);
			result[1] = -result[4];
			return result;
		}

		public static Matrix4 Rotate(float yaw, float pitch, float roll)
		{
			float halfRoll = roll * 0.5f;
			float sr = (float)Math.Sin(halfRoll);
			float cr = (float)Math.Cos(halfRoll);

			float halfPitch = pitch * 0.5f;
			float sp = (float)Math.Sin(halfPitch);
			float cp = (float)Math.Cos(halfPitch);

			float halfYaw = yaw * 0.5f;
			float sy = (float)Math.Sin(halfYaw);
			float cy = (float)Math.Cos(halfYaw);

			float x = cy * sp * cr + sy * cp * sr;
			float y = sy * cp * cr - cy * sp * sr;
			float z = cy * cp * sr - sy * sp * cr;
			float w = cy * cp * cr + sy * sp * sr;

			float xx = x * x;
			float yy = y * y;
			float zz = z * z;

			float xy = x * y;
			float wz = z * w;
			float xz = z * x;
			float wy = y * w;
			float yz = y * z;
			float wx = x * w;

			Matrix4 result = Matrix4.Identity;
			result[0] = 1.0f - 2.0f * (yy + zz);
			result[1] = 2.0f * (xy + wz);
			result[2] = 2.0f * (xz - wy);
			result[4] = 2.0f * (xy - wz);
			result[5] = 1.0f - 2.0f * (zz + xx);
			result[6] = 2.0f * (yz + wx);
			result[8] = 2.0f * (xz + wy);
			result[9] = 2.0f * (yz - wx);
			result[10] = 1.0f - 2.0f * (yy + xx);
			return result;
		}

		public override string ToString()
		{
			StringBuilder sb = new StringBuilder(128);
			unsafe
			{
				fixed (float* f = m)
				{
					sb.Append("{ " + f[0].ToString("0.000") + " ");
					for (int i = 1; i < 16; ++i)
					{
						if (i % 4 == 0)
							sb.Append(" }{ ");
						float val = f[i];
						sb.Append(val.ToString("0.000") + " ");
						
					}
					sb.Append(" }");
				}
			}
			return sb.ToString();
		}
	}
}
