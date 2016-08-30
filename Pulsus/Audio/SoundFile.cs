using System.IO;
using Pulsus.FFmpeg;

namespace Pulsus.Audio
{
	public class SoundFile
	{
		public SoundData data { get; private set; }
		public string path { get; private set; }

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
			".m4a",
		};

		public SoundFile(string path)
		{
			this.path = path;
		}

		public bool Load()
		{
			if (data != null)
				return true;

			path = Utility.FindRealFile(path, lookupPaths, lookupExtensions);
			if (!File.Exists(path))
				return false;

			data = FFmpegHelper.SoundFromFile(path);
			return true;
		}
	}
}
