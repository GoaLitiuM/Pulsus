using System;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using SDL2;
using System.Reflection;
using System.Threading;
using Pulsus.FFmpeg;

namespace Pulsus
{
	public static class Program
	{
		public static string name { get; private set; }
		public static Version version { get; private set; }
		public static string versionDisplay { get; private set; }
		public static string versionLongDisplay { get; private set; }
		public static string platform { get; private set; }
		public static string platformVersion { get; private set; }
		public static string platformId { get; private set; }

		public static Eto.Forms.Application etoApplication;
		public static EventWaitHandle etoWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
		private static Thread etoThread;

		[STAThread]
		static void Main()
		{
			if (!Debugger.IsAttached)
			{
				// handle thrown exceptions when debugger is not present
				AppDomain.CurrentDomain.UnhandledException +=
					(sender, e) => OnCaughtException(e.ExceptionObject as Exception);
			}
			
			// store program name and version number
			Assembly assembly = Assembly.GetExecutingAssembly();
			name = assembly.GetName().Name;
			version = assembly.GetName().Version;
			versionDisplay = Utility.GetVersionString(version);
			versionLongDisplay = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
			platform = Utility.GetPlatform();
			platformVersion = Utility.GetPlatformVersion();
			platformId = (Environment.OSVersion.Platform == PlatformID.Win32NT ? "Win" : platform) + (Environment.Is64BitProcess ? "-x64" : "-x86");

			// Fixes weird behaviour when using drag'n'drop over the exe:
			// Working directory changes to the path where the dragged
			// dragged file is located. This corrects it by using
			// the correct working directory path in the first argument.
			
			string[] args = Environment.GetCommandLineArgs();
			string dropFile = args.Length > 1 ? Directory.GetParent(args[args.Length-1]).FullName : "";

			if (Environment.CurrentDirectory == dropFile && args.Length > 1)
				Environment.CurrentDirectory = Directory.GetParent(Path.GetFullPath(args[0])).FullName;

			// setup logging file
			Log.SetLogFile(name + ".log");

			Log.Text("{0} {1} ({2})", name, versionDisplay, Environment.Is64BitProcess ? "64-bit" : "32-bit");
			Log.Text("{0} {1} ({2})", platform, platformVersion, Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit");

			// locale independent date and number formatting
			CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
			Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

			// close Eto on exit
			AppDomain.CurrentDomain.ProcessExit += (sender, e) => EtoClose();

			//SettingsManager.LoadDefaults();
			SettingsManager.LoadPersistent();
			SettingsManager.SavePersistent();	// refresh with new fields
			SettingsManager.ParseArgs(args);

			Settings settings = SettingsManager.instance;
			if (settings.showSettings)
			{
				if (!ShowSettings(false))
					return; // exit early
			}

			FFmpegHelper.Init();

			using (Game game = new Game())
				game.Run();

			SettingsManager.SavePersistent();

		}

		public static void EtoStartup()
		{
			// run Eto UI in separate thread
			etoThread = new Thread(new ThreadStart(() =>
			{
				using (etoApplication = new Eto.Forms.Application())
				{
					etoApplication.Initialized += (s, e) => etoWaitHandle.Set();
					etoApplication.Run();
				}
			}));
			etoThread.SetApartmentState(ApartmentState.STA);
			etoThread.Name = "EtoThread";
			etoThread.IsBackground = true;
			etoThread.Start();
		}

		public static void EtoClose()
		{
			if (etoApplication == null)
				return;

			EtoInvoke(() =>	etoApplication.Quit());
			etoThread.Join();
		}

		public static void EtoInvoke(Action action)
		{
			if (action == null)
				return;

			if (etoApplication == null)
				EtoStartup();
			
			etoWaitHandle.WaitOne();

			if (etoApplication == null)
				throw new ApplicationException("Eto context is invalid, initialization failed or Eto thread ended prematurely.");

			etoApplication.Invoke(() =>	action());
		}

		public static bool ShowSettings(bool inGame = false)
		{
			using (SettingsWindow settingsWindow = new SettingsWindow(false))
				return settingsWindow.Show();
		}

		public static void OnCaughtException(Exception exception, string description = null)
		{
			if (description == null)
				description = exception.GetType().Name;

			Log.Fatal((string.IsNullOrEmpty(description) ? "" : (description + ": ")) + exception.Message + "\n" + exception.StackTrace);

			if (!string.IsNullOrEmpty(description))
				description += "\n\n";

			string exceptionMessage = exception.Message;
			if (exceptionMessage.Length > 80)
			{
				string[] words = exceptionMessage.Split(new char[] { ' '/*, '\n'*/ });
				string line = "";
				exceptionMessage = "";
				for (int i = 0; i < words.Length; i++)
				{
					if (line.Length + words[i].Length > 80)
					{
						exceptionMessage += line + "\n";
						line = "";
					}
					line += words[i] + " ";
				}
				exceptionMessage += line;
			}

			SDL.SDL_ShowSimpleMessageBox(SDL.SDL_MessageBoxFlags.SDL_MESSAGEBOX_ERROR,
					"Pulsus Error", description + exceptionMessage + "\n\nSee " + Log.logPath + " for more details.", IntPtr.Zero);
	
			EtoInvoke(() => etoApplication.Quit());
			Environment.Exit(1);
		}
	}
}
