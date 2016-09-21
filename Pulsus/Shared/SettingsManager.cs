using Jil;
using Pulsus.Gameplay;
using System.IO;
using System.Reflection;
using System.Text;
using System;

namespace Pulsus
{
	public static class SettingsManager
	{
		private static readonly string defaultPath = Path.Combine(Program.basePath, "settings.json");

		// returns most up to date settings 
		public static Settings instance { get { return _temporary ?? persistent; } }

		private static Settings persistent = null;
		private static Settings _temporary = null; // values overridden by commandline options
		private static Settings temporary
		{
			get
			{
				// on-demand cloning of persistent settings
				if (_temporary == null)
					LoadTemporary(persistent);

				return _temporary;
			}
		}

		public static Settings Clone(Settings settings)
		{
			Settings cloned = new Settings();
			foreach (FieldInfo field in
				typeof(Settings).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
			{
				field.SetValue(cloned, field.GetValue(settings));
			}

			return cloned;
		}

		// loads settings from file and sets it as persistent
		public static void Load()
		{
			Settings settings = LoadFromFile(defaultPath);
			if (settings != null)
				Apply(settings);
			else
			{
				Log.Info("Settings file (" + defaultPath + ") not found, loading defaults");

				LoadDefaults();
				Save();
			}
		}

		public static Settings LoadFromFile(string path)
		{
			if (!File.Exists(path))
				return null;

			Settings settings = JSON.Deserialize<Settings>(File.ReadAllText(path, Encoding.UTF8));
			Process(settings);

			return settings;
		}

		public static void LoadDefaults()
		{
			Settings settings = new Settings();
			Process(settings);
			Apply(settings);
		}

		public static void LoadTemporary(Settings settings)
		{
			_temporary = Clone(settings);
		}

		public static void ClearTemporary()
		{
			_temporary = null;
		}

		public static void Process(Settings settings)
		{
			if (settings == null)
				return;

			// ensure all the directory paths ends with directory separator character
			for (int i = 0; i < settings.songPaths.Count; i++)
			{
				if (string.IsNullOrWhiteSpace(settings.songPaths[i]))
				{
					settings.songPaths.RemoveAt(i--);
					continue;
				}

				char lastChar = settings.songPaths[i][settings.songPaths[i].Length-1];
				if (lastChar != Path.DirectorySeparatorChar && lastChar != Path.AltDirectorySeparatorChar)
					settings.songPaths[i] += Path.DirectorySeparatorChar;
			}
		}

		public static void Apply(Settings settings)
		{
			persistent = settings;
			ClearTemporary();
		}

		public static void Save()
		{
			string json = JSON.Serialize<Settings>(persistent, Options.PrettyPrint);
			File.WriteAllText(defaultPath, json, Encoding.UTF8);
		}

		private static void PrintHelp()
		{
			Console.WriteLine(
				"\nUsage: " + Environment.GetCommandLineArgs()[0] + " [OPTIONS] CHARTFILE\n" +
				"\nOptions: "
				);

			var options = new Tuple<string, string>[]
			{
				Tuple.Create("-h, --help",                      "Prints this"),
				Tuple.Create("--settings, --config",            "Opens configuration window"),
				Tuple.Create("--skin SKINNAME",                 "Skin override"),
				Tuple.Create("--debug",                         "Shows console window for debugging"),
				Tuple.Create("", ""),
				Tuple.Create("-p, --preview, -a, --autoplay",   "Enables chart preview mode (autoplay)"),
				Tuple.Create("-m VALUE, --measure VALUE",       "Starts the chart from measure number VALUE [0-999]"),
				Tuple.Create("", ""),
				Tuple.Create("--render OUTPUT.wav",             "Renders all audio of CHARTFILE to file"),
				Tuple.Create("--dump-timestamps OUTPUT",        "Dumps all generated note event timestamps of CHARTFILE"),
			};

			foreach (var option in options)
			{
				Console.WriteLine("  {0,-35}{1}", option.Item1, option.Item2);
			}

			Console.Write("\n");
			Environment.Exit(0);
		}

		private static bool ParseArg(string key, string value)
		{
			if (key.StartsWith("-"))
			{
				key = key.ToLower();

				switch (key)
				{
					case "--help":
					case "-help":
					case "-h":
						PrintHelp();
						break;
					case "--debug":
						temporary.debug = true;
						break;
					case "--settings":
					case "--config":
						temporary.showSettings = true;
						break;
					case "--autoplay":
					case "-a":
					case "--preview":
					case "-p":
						temporary.gameplay.assistMode = AssistMode.Autoplay;
						break;
					default:
						break;
				}
				if (!string.IsNullOrEmpty(value))
				{
					switch (key)
					{
						case "--measure":
						case "-m":
							if (!int.TryParse(value, out temporary.startMeasure))
								Log.Error("Invalid value for measure");
							break;
						case "--skin":
							temporary.skin = value;
							break;
						case "--render":
							temporary.outputMode = OutputMode.Render;
							temporary.audio.driver = Audio.AudioDriver.File;
							temporary.audio.bufferLength = 4096;
							temporary.audio.volume = 100;
							temporary.outputPath = value;
							break;
						case "--dump-timestamps":
							temporary.outputMode = OutputMode.DumpTimestamps;
							temporary.outputPath = value;
							break;
						default:
							return false;
					}

					// value was read from next argument, skip it
					return true;
				}
			}
			else if (key == "/?")
				PrintHelp();
			else
				temporary.playPath = key;

			return false;
		}

		public static void ParseArgs(string[] args)
		{
			for (int i = 1; i < args.Length; i++)
			{
				string key = args[i];
				string value = null;
				if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
					value = args[i + 1];

				if (ParseArg(key, value))
					i++;
			}

			ParseArg("-", null);

			if (_temporary != null)
			{
				// settings state was overridden
			}
		}
	}
}
