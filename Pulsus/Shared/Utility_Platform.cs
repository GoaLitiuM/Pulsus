using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SDL2;

namespace Pulsus
{
	public static partial class Utility
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
