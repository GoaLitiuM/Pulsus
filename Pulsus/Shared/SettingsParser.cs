using System;
using Pulsus.Gameplay;

namespace Pulsus
{
	public static class SettingsParser
	{
		public static Settings Parse(Settings settings, string[] args)
		{
			return new Parser(settings).Parse(args);
		}

		private class Parser
		{
			public Parser(Settings settings)
			{
				baseSettings = settings;
			}

			private Settings baseSettings = null;

			private Settings _settings = null; // values overridden by commandline options
			private Settings settings
			{
				get
				{
					// on-demand cloning of persistent settings
					if (_settings == null)
						_settings = SettingsManager.Clone(baseSettings);

					return _settings;
				}
			}

			private bool resampleQualityOverridden = false;

			public Settings Parse(string[] args)
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
				return _settings;
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
					Tuple.Create("--resample-quality [low|medium|high|highest]",
																	"Changes resampling quality of audio samples"),
					Tuple.Create("--dump-timestamps OUTPUT",        "Dumps all generated note event timestamps of CHARTFILE"),
				};

				foreach (var option in options)
				{
					if (option.Item1.Length < 35)
						Console.WriteLine("  {0,-35}{1}", option.Item1, option.Item2);
					else
						Console.WriteLine("  {0,-35}\n  {2,-35}{1}", option.Item1, option.Item2, "");
				}

				Console.Write("\n");
				Environment.Exit(0);
			}

			public bool ParseArg(string key, string value)
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
							settings.debug = true;
							break;
						case "--settings":
						case "--config":
							settings.showSettings = true;
							break;
						case "--autoplay":
						case "-a":
						case "--preview":
						case "-p":
							settings.gameplay.assistMode = AssistMode.Autoplay;
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
								if (!int.TryParse(value, out settings.startMeasure))
									Log.Error("Invalid value for measure");
								break;
							case "--skin":
								settings.skin = value;
								break;
							case "--render":
								settings.outputMode = OutputMode.Render;
								settings.audio.driver = Audio.AudioDriver.File;
								settings.audio.bufferLength = 4096;
								settings.audio.volume = 100;
								settings.outputPath = value;

								if (!resampleQualityOverridden)
									settings.audio.resampleQuality = ResampleQuality.Highest;
								break;
							case "--resample-quality":
								{
									ResampleQuality resampleQuality;
									if (Enum.TryParse<ResampleQuality>(value, true, out resampleQuality))
									{
										settings.audio.resampleQuality = resampleQuality;
										resampleQualityOverridden = true;
									}
									else
										Log.Warning("Unknown resample quality value: " + value);
								}
								break;
							case "--dump-timestamps":
								settings.outputMode = OutputMode.DumpTimestamps;
								settings.outputPath = value;
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
					settings.playPath = key;

				return false;
			}
		}
	}
}
