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

		private struct UpdateInfo
		{
			public Version version;
			public string date;
			public string changelog;
			public string downloadUrl;
		}
		
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
			AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

			//SettingsManager.LoadDefaults();
			SettingsManager.LoadPersistent();
			SettingsManager.SavePersistent();	// refresh with new fields
			SettingsManager.ParseArgs(args);

			Settings settings = SettingsManager.instance;

			// clean up residual files from update
			CleanUpdateFiles();

			if (settings.checkUpdates)
			{
				Log.Info("Checking for updates...");
				UpdateInfo updateInfo = GetLatestUpdate();
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
							string updateFile = DownloadUpdate(updateInfo);
							ApplyUpdate(updateFile, updateInfo);
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

		private static UpdateInfo GetLatestUpdate()
		{
			UpdateInfo info = new UpdateInfo();
			info.version = new Version(0, 0, 0);

			string url = "https://api.github.com/repos/goalitium/pulsus/releases/latest";

			System.Net.HttpWebRequest request = (System.Net.HttpWebRequest)System.Net.HttpWebRequest.Create(url);
			request.UserAgent = Program.name;
			request.Timeout = 2000;

			try
			{
				using (var response = request.GetResponse())
				{
					using (StreamReader stream = new StreamReader(response.GetResponseStream()))
					{
						var result = Newtonsoft.Json.Linq.JObject.Parse(stream.ReadToEnd());

						info = new UpdateInfo()
						{
							changelog = result.Value<string>("body"),
							version = Version.Parse(result.Value<string>("tag_name")),
							date = result.Value<string>("published_at"),
						};

						foreach (var asset in result.GetValue("assets").Children())
						{
							string filename = asset.Value<string>("name");
							string[] tokens = filename.Split(new char[] { '_' });

							string fileVersion = tokens[1];
							string filePlatform = tokens[2].Replace(".zip", "");
							if (tokens.Length >= 4)
							{
								// old release format: Program_Version_OS_Arch
								filePlatform += "-" + tokens[3].Replace(".zip", "");
							}

							if (filePlatform.Equals(platformId, StringComparison.OrdinalIgnoreCase))
							{
								info.downloadUrl = asset.Value<string>("browser_download_url");
								break;
							}
						}
					}
				}
			}
			catch (Exception e)
			{
				Log.Warning("Failed to retrieve latest update information: " + e.Message);
			}

			return info;
		}

		private static void CleanUpdateFiles()
		{
			foreach (string file in Directory.EnumerateFiles(basePath, "*.tmp", SearchOption.TopDirectoryOnly))
				File.Delete(file);

			foreach (string file in Directory.EnumerateFiles(FFmpegHelper.ffmpegPath, "*.tmp", SearchOption.TopDirectoryOnly))
				File.Delete(file);
		}

		private static string DownloadUpdate(UpdateInfo updateInfo)
		{
			string filename = Path.GetFileName(updateInfo.downloadUrl);
			string outputDir = Path.Combine(Path.GetTempPath(), Program.name + "Cache");
			string outputFile = Path.Combine(outputDir, filename);

			if (!File.Exists(outputFile))
			{
				Log.Info("Downloading update " + Utility.GetVersionString(updateInfo.version) + "...");
				if (!Directory.Exists(outputDir))
					Directory.CreateDirectory(outputDir);

				System.Net.WebClient webClient = new System.Net.WebClient();
				webClient.DownloadFile(updateInfo.downloadUrl, outputFile);
			}
			else
				Log.Warning("Update files already present at: " + outputFile);

			return outputFile;
		}

		private static void ApplyUpdate(string updateFile, UpdateInfo updateInfo)
		{
			Log.Info("Applying update " + Utility.GetVersionString(updateInfo.version));

			string exePath = Assembly.GetExecutingAssembly().Location;
			try
			{
				using (ZipArchive archive = ZipFile.OpenRead(updateFile))
				{
					foreach (ZipArchiveEntry entry in archive.Entries)
					{
						string entryOutPath = Path.Combine(basePath, entry.FullName);
						if (!entry.FullName.EndsWith("/"))
						{
							try
							{
								// save the old executable so it can be restored in case of failure
								if (entryOutPath.Equals(exePath, StringComparison.Ordinal))
									File.Move(entryOutPath, entryOutPath + ".tmp");

								entry.ExtractToFile(entryOutPath, true);
							}
							catch (IOException)
							{
								// file is in use, rename it and delete it during next launch
								File.Move(entryOutPath, entryOutPath + ".tmp");
								entry.ExtractToFile(entryOutPath, true);
							}
						}
						else
							Directory.CreateDirectory(entryOutPath);
					}
				}
			}
			catch (Exception e)
			{
				// restore old executable back
				if (File.Exists(exePath + ".tmp"))
				{
					if (File.Exists(exePath))
						File.Delete(exePath);

					File.Move(exePath + ".tmp", exePath);
				}

				throw new ApplicationException("Failed to install update: " + e.Message);
			}

			File.Delete(updateFile);
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
