using Pulsus.FFmpeg;
using Pulsus.Graphics;
using System.IO;
using System;

namespace Pulsus.Gameplay
{
	public class BGAObject : IDisposable
	{
		public Texture2D texture { get; private set; }
		public string filename { get; private set; }
		public string name { get; private set; }

		private FFmpegVideo video;

		public bool isVideo { get { return video != null && video.isVideo; } }
		public double frametime { get { return video == null ? 0.0 : video.frametime; } }
		public bool loaded { get { return texture != null; } }

		// alternate paths where to look up missing files
		static string[] lookupPaths =
		{
			"",			// current directory
			"bg/",
			"../",		// previous directory (compatibility fix for charts in sub-folders)
		};

		static string[] lookupExtensions =
		{
			// image formats
			".bmp",
			".png",
			".jpg",
			".tga",

			// video formats
			".mpg",
			".avi",
			".mp4",
			".flv",
			".mp4",
			".mkv",
			".wmv",
			".ogv",
			".webm",
			".mov",
			".swf",
			".3gp",
			".asf",
			".m4v",
		};

		public BGAObject(string filename, string name)
		{
			this.filename = filename;
			this.name = name;
		}

		public void Dispose()
		{
			if (texture != null)
				texture.Dispose();
			if (video != null)
				video.Dispose();
		}

		public bool Load(string basePath = "")
		{
			string fullPath = Utility.FindRealFile(Path.Combine(basePath, filename), lookupPaths, lookupExtensions);
			if (!File.Exists(fullPath))
			{
				Log.Warning("BGA file not found: " + filename);
				return false;
			}

			video = new FFmpegVideo();
			try
			{
				video.Load(fullPath);
				texture = new Texture2D(video.width, video.height);
				video.OnNextFrame += texture.SetData;
			}
			catch when (Path.GetExtension(filename).ToLower() == ".lua")
			{
				// scripted background are not supported (yet?)
				Log.Error("Failed to load BGA '" + filename + "', scripted BGA not supported");
				return false;
			}
			catch (ApplicationException e)
			{
				Log.Error("Failed to load BGA '" + filename + "': " + e.Message);

				if (video != null)
					video.Dispose();
				video = null;
				
				return false;
			}

			if (isVideo)
			{
				// preload first frame of the video
				video.ReadNextFrame();
			}
			else
			{
				// fully load image files
				video.ReadFrames();
				video.Dispose();
				video = null;
			}

			return true;
		}

		public void Start()
		{
			if (isVideo)
				video.Start();
		}

		public void Update(double deltaTime)
		{
			if (isVideo)
				video.Update(deltaTime);
		}
	}
}