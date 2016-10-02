using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SDL2;

namespace Pulsus
{
	public static partial class Utility
	{
		static PlatformID platform;

		static int ntCurrentResolution = 0;
		static int cpuCount = 0;

		static IntPtr timer = IntPtr.Zero;
		static timeval tv;

		static void Utility_Platform()
		{
			platform = Environment.OSVersion.Platform;
			if (platform == PlatformID.Win32NT)
			{
				// use the lowest possible timer resolution we can get
				if (ntCurrentResolution == 0)
					NtSetTimerResolution(1, true, out ntCurrentResolution);

				timer = CreateWaitableTimer(IntPtr.Zero, true, null);
			}
			else if (platform == PlatformID.Unix || platform == PlatformID.MacOSX)
			{
				tv = new timeval();
			}

			GetProcessorCount();
		}

		/// <summary> Suspends the current thread, time units in 1µs (or 0.001ms) </summary>
		/// <param name="usec"> Minimum sleep time in microseconds. </param>
		/// <remarks> System timer accuracy may affect how long thread will sleep at any given time.
		/// On Windows, minimum timer accuracy is usually 1000 microseconds, but it can be lowered
		/// down to 500 microseconds with undocumented NtSetTimerResolution function.</remarks>
		public static void USleep(ulong usec)
		{
			if (platform == PlatformID.Win32NT)
			{
				// negative values represents relative time
				long period = -(10 * (long)usec);

				SetWaitableTimer(timer, ref period, 0, IntPtr.Zero, IntPtr.Zero, false);
				WaitForSingleObject(timer, 0xFFFFFFFF);
			}
			else if (platform == PlatformID.Unix || platform == PlatformID.MacOSX)
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
					elapsed = (((now - last) * 1000000) / (freq));
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

		public static string GetPlatform()
		{
			if (platform == PlatformID.Unix || platform == PlatformID.Unix)
			{
				try
				{
					string osName = "";

					Process process = new Process();
					process.StartInfo.FileName = "uname";
					process.StartInfo.UseShellExecute = false;
					process.StartInfo.RedirectStandardOutput = true;
					process.OutputDataReceived += (sender, e) => osName += e.Data;

					process.Start();
					process.BeginOutputReadLine();
					process.WaitForExit();

					return osName;
				}
				catch
				{
					return platform.ToString();
				}
			}
			else if (platform == PlatformID.Win32NT)
				return "Windows";
			else
				return platform.ToString();
		}

		public static string GetPlatformVersion()
		{
			if (platform == PlatformID.Unix || platform == PlatformID.MacOSX)
			{
				try
				{
					Process process = new Process();
					process.StartInfo.UseShellExecute = false;
					process.StartInfo.RedirectStandardOutput = true;

					if (platform == PlatformID.Unix)
					{
						process.StartInfo.FileName = "lsb_release";
						process.StartInfo.Arguments = "-i -r";
					}
					else if (platform == PlatformID.MacOSX)
						process.StartInfo.FileName = "sw_vers";

					if (process.Start())
					{
						process.WaitForExit();

						string distribution = "";
						string version = "";
						string line;
						while ((line = process.StandardOutput.ReadLine()) != null)
						{
							if (platform == PlatformID.Unix)
							{
								if (line.Contains("Distributor ID:"))
									distribution += line.Replace("Distributor ID:", "").Trim();
								else if (line.Contains("Release:"))
									version += line.Replace("Release:", "").Trim();
							}
							else if (platform == PlatformID.MacOSX)
							{
								if (line.Contains("ProductName:"))
									distribution += line.Replace("ProductName:", "").Trim();
								else if (line.Contains("ProductVersion:"))
									version += line.Replace("ProductVersion:", "").Trim();
							}
						}

						return distribution + " " + version;
					}
				}
				catch
				{
				}

				Console.WriteLine("Failed to determine PlatformVersion for Unix system");
				return Environment.OSVersion.VersionString;
			}
			else if (platform == PlatformID.Win32NT)
			{
				int major = Environment.OSVersion.Version.Major;
				int minor = Environment.OSVersion.Version.Minor;
				int build = Environment.OSVersion.Version.Build;

				if (major == 6 && minor == 0)
					return "Vista";
				else if (major == 6 && minor == 1)
					return "7";
				else if (major == 6 && minor == 2)
					return "8";
				else if (major == 6 && minor == 3)
					return "8.1";
				else if (major == 10)
					return String.Format("{0} (Build {1})", major.ToString(), build.ToString());
				else
					return Environment.OSVersion.VersionString;
			}
			else
				return Environment.OSVersion.VersionString;
		}

		public static void ShowConsole()
		{
			if (platform == PlatformID.Win32NT)
			{
				const int SW_SHOW = 5;
				ShowWindow(GetConsoleWindow(), SW_SHOW);
			}
		}

		public static void HideConsole()
		{
			if (platform == PlatformID.Win32NT)
			{
				const int SW_HIDE = 0;
				ShowWindow(GetConsoleWindow(), SW_HIDE);
			}
		}

		public static int GetProcessorThreadCount()
		{
			return Environment.ProcessorCount;
		}

		/// <summary> Returns number of physical CPUs or CPU cores in the system. </summary>
		public static int GetProcessorCount()
		{
			if (cpuCount == 0)
			{
				if (platform == PlatformID.Win32NT)
				{
					// size of SYSTEM_LOGICAL_PROCESSOR_INFORMATION structure
					int structureSize = (IntPtr.Size == 4) ? 24 : 32;

					IntPtr buffer = IntPtr.Zero;
					int lengthBytes = 0;

					GetLogicalProcessorInformation(buffer, ref lengthBytes);

					buffer = Marshal.AllocHGlobal(lengthBytes);

					if (GetLogicalProcessorInformation(buffer, ref lengthBytes))
					{
						unsafe
						{
							IntPtr offset = buffer;
							void* bufferEnd = new IntPtr(buffer.ToInt64() + lengthBytes).ToPointer();
							while (offset.ToPointer() < bufferEnd)
							{
								int relationship = *(int*)(offset + IntPtr.Size).ToPointer();
								if (relationship == 0) // RelationProcessorCore
									cpuCount++;

								offset += structureSize;
							}
						}
					}

					Marshal.FreeHGlobal(buffer);
				}
				else if (platform == PlatformID.Unix)
				{
					using (var stream = new System.IO.StreamReader("/proc/cpuinfo"))
					{
						string line;
						while ((line = stream.ReadLine()) != null)
						{
							if (line.StartsWithFast("cpu cores"))
							{
								line = line.Substring(line.IndexOf(':') + 1).Trim();

								int parsedCores = 0;
								if (int.TryParse(line, out parsedCores))
								{
									if (parsedCores > cpuCount)
										cpuCount = parsedCores;
								}
							}
						}
					}
				}
				else if (platform == PlatformID.MacOSX)
				{
					IntPtr value = Marshal.AllocHGlobal(sizeof(int));
					IntPtr length = new IntPtr(sizeof(int));
					if (sysctlbyname("hw.physicalcpu", ref value, ref length,
						IntPtr.Zero, IntPtr.Zero) == 0)
					{
						cpuCount = Marshal.PtrToStructure<int>(value);
					}

					Marshal.FreeHGlobal(value);
				}

				if (cpuCount == 0)
				{
					Log.Error("Failed to retrieve number of physical CPU cores");
					cpuCount = 1;
				}
			}
			return cpuCount;
		}

		// Windows

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

		[DllImport("kernel32.dll")]
		static extern IntPtr GetConsoleWindow();

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

		[DllImport("kernel32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		static extern bool GetLogicalProcessorInformation(IntPtr buffer, ref int returnLength);

		// Unix

		[StructLayout(LayoutKind.Sequential)]
		struct timeval
		{
			public IntPtr tv_sec;
			public IntPtr tv_usec;
		}

		[DllImport("libc", SetLastError = true)]
		static extern int select(int nfds, IntPtr readFds, IntPtr writeFds, IntPtr exceptFds, ref timeval timeout);

		// OS X

		[DllImport("libc", CallingConvention = CallingConvention.Cdecl)]
		static extern int sysctlbyname([MarshalAs(UnmanagedType.LPStr)]string sname, ref IntPtr oldp, ref IntPtr oldlenp, IntPtr newp, IntPtr newlen);
	}
}
