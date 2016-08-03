using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
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

		private static string _basePath;
		public static string basePath
		{
			get
			{
				if (_basePath == null)
					_basePath = Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName;

				return _basePath;
			}
		}

		public static Eto.Forms.Application etoApplication;
		public static EventWaitHandle etoWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
		private static Thread etoThread;

		private static bool restart = false;

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

			// setup logging file
			Log.SetLogFile(Path.Combine(basePath, name + ".log"));

			Log.Text("{0} {1} ({2})", name, versionDisplay, Environment.Is64BitProcess ? "64-bit" : "32-bit");
			Log.Text("{0} {1} ({2})", platform, platformVersion, Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit");

			// locale independent date and number formatting
			CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
			Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

			// close Eto on exit
			AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

			//SettingsManager.LoadDefaults();
			SettingsManager.LoadPersistent();
			SettingsManager.SavePersistent();	// refresh with new fields
			SettingsManager.ParseArgs(Environment.GetCommandLineArgs());

			Settings settings = SettingsManager.instance;

			// clean up residual files from update
			Updater.CleanUpdateFiles();

			if (settings.checkUpdates)
			{
				UpdateInfo updateInfo = Updater.GetLatestUpdate();
				if (updateInfo.version > version)
				{
					if (!string.IsNullOrEmpty(updateInfo.downloadUrl))
					{
						SDL.SDL_MessageBoxButtonData[] buttons =
						{
							new SDL.SDL_MessageBoxButtonData()
							{
								buttonid = 0,
								flags = SDL.SDL_MessageBoxButtonFlags.SDL_MESSAGEBOX_BUTTON_ESCAPEKEY_DEFAULT,
								text = "No",
							},
							new SDL.SDL_MessageBoxButtonData()
							{
								buttonid = 1,
								flags = SDL.SDL_MessageBoxButtonFlags.SDL_MESSAGEBOX_BUTTON_RETURNKEY_DEFAULT,
								text = "Yes",
							},
						};

						SDL.SDL_MessageBoxData data = new SDL.SDL_MessageBoxData
						{
							flags = SDL.SDL_MessageBoxFlags.SDL_MESSAGEBOX_INFORMATION,
							window = IntPtr.Zero,
							title = name + " Update",
							message =
								"New update available for " + name + "\n\n" +
								"Download and apply update " + Utility.GetVersionString(updateInfo.version) + "?",
							numbuttons = buttons.Length,
							buttons = buttons,
							colorScheme = null
						};

						int result = -1;
						SDL.SDL_ShowMessageBox(ref data, out result);

						if (result == 1)
						{
							string updateFile = Updater.DownloadUpdate(updateInfo);
							Updater.ApplyUpdate(updateFile, updateInfo);
							restart = true;
							return; // exit early
						}
					}
					else
						Log.Warning("Update found, but no release files are available for this platform: " + platformId);
				}
				else
					Log.Info("No updates found");
			}

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

		private static void OnProcessExit(object sender, EventArgs e)
		{
			EtoClose();

			if (restart)
			{
				// launch a new process of this program after update
				Process process = Process.Start(Assembly.GetExecutingAssembly().Location);
			}
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
