using System;
using System.IO;
using System.Runtime.InteropServices;
using SDL2;

namespace Pulsus
{
	public static class Utility
	{
		static PlatformID? osVersion;
		static int ntCurrentResolution = 0;
		static IntPtr timer = IntPtr.Zero;
		static timeval tv;

		/// <summary> Suspends the current thread, time units in 1µs (or 0.001ms) </summary>
		/// <param name="usec"> Minimum sleep time in microseconds. </param>
		/// <remarks> System timer accuracy may affect how long thread will sleep at any given time.
		/// On Windows, minimum timer accuracy is usually 500 or 1000 microseconds.</remarks>
		public static void USleep(ulong usec)
		{
			if (osVersion == null)
			{
				osVersion = Environment.OSVersion.Platform;
				if (osVersion == PlatformID.Win32NT)
				{
					// use the lowest possible timer resolution we can get
					if (ntCurrentResolution == 0)
						NtSetTimerResolution(1, true, out ntCurrentResolution);

					timer = CreateWaitableTimer(IntPtr.Zero, true, null);
				}
				else if (osVersion == PlatformID.Unix || osVersion == PlatformID.MacOSX)
				{
					tv = new timeval();
				}
			}

			if (osVersion == PlatformID.Win32NT)
			{
				// negative values represents relative time
				long period = -(10 * (long)usec);

				SetWaitableTimer(timer, ref period, 0, IntPtr.Zero, IntPtr.Zero, false);
				WaitForSingleObject(timer, 0xFFFFFFFF);
			}
			else if (osVersion == PlatformID.Unix || osVersion == PlatformID.MacOSX)
			{
				// adapted from SDL_Delay

				const int EINTR = 4;
	
				ulong freq = SDL.SDL_GetPerformanceFrequency();
				ulong last = SDL.SDL_GetPerformanceCounter();
				ulong now, elapsed;
				int was_error = 0;

				int errno = 0;
				do
				{
					now = SDL.SDL_GetPerformanceCounter();
					elapsed = (((now - last)*1000000) / (freq));
					last = now;

					if (elapsed >= usec)
						break;

					usec -= elapsed;
					
					tv.tv_sec = new IntPtr((int)(usec / 1000000));
					tv.tv_usec = new IntPtr((int)(usec % 1000000));

					was_error = select(0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, ref tv);
					errno = Marshal.GetLastWin32Error();
				} while (was_error != 0 && (errno == EINTR));
			}
			else
				SDL.SDL_Delay((uint)(usec * 1000));
		}

		public static bool FastStartsWith(this string str, string str2)
		{
			int len = str2.Length < str.Length ? str2.Length : str.Length;
			for (int i = 0; i < len; ++i)
				if (str[i] != str2[i] && str[i] != char.ToLower(str2[i]))
					return false;

			return len > 0;
		}

		private static readonly char[] base36Table = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();

		public static string ToBase36(int index)
		{
			if (index >= 1296)
				throw new OverflowException();

			char[] chrs = new char[2] { '0', '0' };
			if (index == 0)
				return new string(chrs);

			int i = 1;
			while (index != 0)
			{
				chrs[i--] = base36Table[index % 36];
				index /= 36;
			}

			return new string(chrs);
		}

		public static int FromBase36(string str)
		{
			if (string.IsNullOrEmpty(str) || str.Length != 2)
				throw new ArgumentException("Invalid index (string length != 2)");

			str = str.ToUpper();

			int i1 = -1;
			int i2 = -1;
			for (int i = 0; i < base36Table.Length; ++i)
			{
				if (str[0] == base36Table[i])
					i1 = i;
				if (str[1] == base36Table[i])
					i2 = i;
			}

			if (i1 == -1 || i2 == -1)
				throw new ArgumentException("'" + str + "' is not a valid index");

			return i1 * 36 + i2;
		}

		public static int lcm(int a, int b)
		{
			checked
			{
				return (a / gcf(a, b)) * b;
			}
		}

		public static int gcf(int a, int b)
		{
			while (b != 0)
			{
				int temp = b;
				b = a % b;
				a = temp;
			}
			return a;
		}

		public static long lcm(long a, long b)
		{
			checked
			{
				return (a / gcf(a, b)) * b;
			}
		}

		public static long gcf(long a, long b)
		{
			while (b != 0)
			{
				long temp = b;
				b = a % b;
				a = temp;
			}
			return a;
		}

		// returns value between min and max (exclusive)
		public static int Range(this Random random, int min, int max)
		{
			if (random == null)
				random = new Random();

			return random.Next(min, max);
		}

		// returns value between min and max (inclusive)
		public static double Range(this Random random, double min, double max)
		{
			if (random == null)
				random = new Random();

			// NextDouble upper is exclusive
			return ((random.NextDouble() / 0.99999999999999978) * (max-min)) + min;
		}

		// finds a file that matches the filename from alternative locations and with different file extensions
		public static string FindRealFile(string path, string[] lookupPaths, string[] lookupExtensions)
		{
			string pathParent = Directory.GetParent(path).FullName;
			string filename = Path.GetFileName(path);
			string newFilepath = path;
			foreach (string lookup in lookupPaths)
			{
				newFilepath = Path.Combine(Path.Combine(pathParent, lookup), filename);
				bool exists = File.Exists(newFilepath);
				if (!exists)
				{
					string oldExt = Path.GetExtension(newFilepath).ToLower();

					foreach (string ext in lookupExtensions)
					{
						if (ext == oldExt)
							continue;

						newFilepath = Path.ChangeExtension(newFilepath, ext);
						exists = File.Exists(newFilepath);
						if (exists)
							break;
					}
				}

				if (exists)
					break;
				else
					newFilepath = null;
			}

			if (newFilepath == null)
				return path;

			return newFilepath;
		}


		[DllImport("ntdll.dll")]
		static extern int NtSetTimerResolution(int DesiredResolution, bool SetResolution, out int CurrentResolution);
		
		[DllImport("kernel32.dll", CharSet = CharSet.Auto)]
		static extern IntPtr CreateWaitableTimer(IntPtr lpTimerAttributes, bool bManualReset, string lpTimerName);

		[DllImport("kernel32.dll")]
		static extern int SetWaitableTimer(IntPtr hTimer, ref long pDueTime, int lPeriod, IntPtr pfnCompletionRoutine, IntPtr lpArgToCompletionRoutine, bool fResume);

		[DllImport("kernel32.dll", SetLastError = true)]
		static extern UInt32 WaitForSingleObject(IntPtr Handle, uint Wait);

		[DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Auto)]
		[return: MarshalAs(UnmanagedType.Bool)]
		static extern bool CloseHandle(IntPtr hObject);

		[StructLayout(LayoutKind.Sequential)]
		struct timeval
		{
			public IntPtr tv_sec;
			public IntPtr tv_usec;
		}

		[DllImport("libc", SetLastError = true)]
		static extern int select(int nfds, IntPtr readFds, IntPtr writeFds, IntPtr exceptFds, ref timeval timeout);
	}
}
