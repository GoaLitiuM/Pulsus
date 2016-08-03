using System.Text;
using System.IO;
using Newtonsoft.Json;
using Pulsus.Gameplay;

namespace Pulsus
{
	public static class SettingsManager
	{
		const string defaultPath = "settings.json";

		public static Settings instance { get { return _temporary == null ? persistent : _temporary; } }

		private static Settings persistent = null;
		private static Settings _temporary = null; // commandline override

		private static Settings temporary
		{
			get
			{
				if (_temporary == null)
					LoadTemporary(persistent);

				return _temporary;
			}
		}

		private static JsonSerializerSettings serializerSettings;

		static SettingsManager()
		{
			serializerSettings = new JsonSerializerSettings()
			{
				Formatting = Formatting.Indented,
				ObjectCreationHandling = ObjectCreationHandling.Replace,
			};
		}

		public static Settings Load(string path)
		{
			if (!File.Exists(path))
				return null;

			return JsonConvert.DeserializeObject<Settings>(File.ReadAllText(path, Encoding.UTF8), serializerSettings);
		}

		public static Settings Clone(Settings settings)
		{
			// HACK: creates a copy of the settings through serialization and deserialization
			return JsonConvert.DeserializeObject<Settings>(JsonConvert.SerializeObject(settings, serializerSettings), serializerSettings);
		}

		public static void LoadDefaults()
		{
			LoadPersistent(new Settings());
		}

		// loads settings from file and sets it as persistent
		public static void LoadPersistent()
		{
			Settings settings = Load(defaultPath);
			if (settings == null)
			{
				Log.Info("Settings file (" + defaultPath + ") not found, loading defaults");
				
				settings = new Settings();
				LoadPersistent(settings);
				SavePersistent();
			}

			LoadPersistent(settings);
		}

		public static void LoadPersistent(Settings settings)
		{
			persistent = settings;
			ClearTemporary();
		}

		public static void LoadTemporary(Settings settings)
		{
			_temporary = Clone(settings);
		}

		public static void SavePersistent()
		{
			string json = JsonConvert.SerializeObject(persistent, serializerSettings);
			File.WriteAllText(defaultPath, json, Encoding.UTF8);
		}

		public static void ClearTemporary()
		{
			_temporary = null;
		}

		private static bool ParseArg(string key, string value)
		{
			if (key.StartsWith("-"))
			{
				key = key.ToLower();

				switch (key)
				{
					case "--settings":
					case "--config":
						temporary.showSettings = true;
						break;
					case "--autoplay":
					case "-a":
						temporary.gameplay.assistMode = AssistMode.Autoplay;
						break;
					default:
						break;
				}
				if (!string.IsNullOrEmpty(value))
				{
					switch (key)
					{
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
				if (i+1 < args.Length && !args[i+1].StartsWith("-"))
					value = args[i+1];

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
