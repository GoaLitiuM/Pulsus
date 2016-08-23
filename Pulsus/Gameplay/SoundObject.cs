using System;
using System.IO;
using Pulsus.Audio;
using Pulsus.FFmpeg;

namespace Pulsus.Gameplay
{
	public class SoundObject
	{
		public string path { get; private set; }
		public string name { get; private set; }
		public Sound sound { get; private set; }

		public bool loaded { get { return sound != null; } }

		// alternate paths where to look up missing files
		static string[] lookupPaths =
		{
			"",			// current directory
			"..\\",		// previous directory (compatibility fix for bms files in sub-folders)
		};

		static string[] lookupExtensions =
		{
			".wav",
			".ogg",
		};

		public SoundObject(string path, string name = "")
		{
			this.path = path;
			this.name = name;
		}

		public bool Load(string basePath = "")
		{
			basePath = Directory.GetParent(basePath).FullName;
			string filename = path;
			path = Utility.FindRealFile(Path.Combine(basePath, path), lookupPaths, lookupExtensions);
			if (!File.Exists(path))
			{
				Log.Warning("Sound not found: " + filename);
				return false;
			}

			try
			{
				sound = FFmpegHelper.SoundFromFile(path);
				sound.polyphony = 1;
			}
			catch (System.Threading.ThreadAbortException)
			{

			}
			catch (Exception e)
			{
				Log.Error("FFmpeg: " + e.Message);
			}

			if (sound == null)
			{
				Log.Error("Failed to load sound object " + Path.GetFileName(path));
				return false;
			}

			return true;
		}
	}
}