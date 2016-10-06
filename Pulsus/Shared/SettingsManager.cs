using System.IO;
using System.Reflection;
using System.Text;
using Jil;

namespace Pulsus
{
	public static class SettingsManager
	{
		private static readonly string defaultPath = Path.Combine(Program.basePath, "settings.json");

		// returns most up to date settings 
		public static Settings instance { get { return overridden ?? persistent; } }

		private static Settings persistent = null;
		private static Settings overridden = null; // values overridden by commandline options

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

		public static void ClearOverrides()
		{
			overridden = null;
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

				char lastChar = settings.songPaths[i][settings.songPaths[i].Length - 1];
				if (lastChar != Path.DirectorySeparatorChar && lastChar != Path.AltDirectorySeparatorChar)
					settings.songPaths[i] += Path.DirectorySeparatorChar;
			}
		}

		public static void Apply(Settings settings)
		{
			persistent = settings;
			ClearOverrides();
		}

		public static void Save()
		{
			string json = JSON.Serialize<Settings>(persistent, Options.PrettyPrint);
			File.WriteAllText(defaultPath, json, Encoding.UTF8);
		}

		public static void ParseArgs(string[] args)
		{
			overridden = SettingsParser.Parse(persistent, args);

			if (overridden != null)
			{
				// settings state was overridden
			}
		}
	}
}
