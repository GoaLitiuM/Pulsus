using Pulsus.Graphics;
using System;
using System.IO;
using Pulsus.FFmpeg;

namespace Pulsus.Gameplay
{
	public class BGAObject : IDisposable
	{
		public Texture2D texture { get; private set; }
		public string path { get; private set; }
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

		public BGAObject(string path, string name)
		{
			this.path = path;
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
			basePath = Directory.GetParent(basePath).FullName;
			path = Utility.FindRealFile(Path.Combine(basePath, path), lookupPaths, lookupExtensions);
			if (!File.Exists(path))
			{
				Log.Warning("BGA file not found: " + path);
				return false;
			}

			video = new FFmpegVideo();
			try
			{
				if (video.Load(path) && video.width * video.height > 0)
					texture = new Texture2D(video.width, video.height);

				if (texture != null)
				{
					video.nextFrame += texture.SetData;
					if (!isVideo)
					{
						video.ReadFrames();
						video.Dispose();
						video = null;
					}
				}
			}
			catch when (Path.GetExtension(path).ToLower() == ".lua")
			{
				// scripted background are not supported (yet?)
			}
			catch (Exception e)
			{
				Log.Error("FFmpeg: " + e.Message);
			}

			if (texture == null)
			{
				if (video != null)
					video.Dispose();
				video = null;

				Log.Error("Failed to load bga object " + Path.GetFileName(path));
				return false;
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