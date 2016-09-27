using System;
using Pulsus.FFmpeg;
using Pulsus.Graphics;

namespace Pulsus.Gameplay
{
	public class BGAObject : IDisposable
	{
		public Texture2D texture { get; private set; }
		public string path { get; private set; }
		public string name { get; private set; }

		public FFmpegVideo video { get; private set; }

		public bool isVideo { get { return video != null; } }
		public double frametime { get { return video.frametime; } }
		public bool loaded { get { return texture != null; } }

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

		public void SetVideo(FFmpegVideo video)
		{
			this.video = video;
			video.OnNextFrame = UpdateTexture;
		}

		public void Start()
		{
			if (video != null)
				video.Start();
		}

		public void Update(double deltaTime)
		{
			if (video != null)
				video.Update(deltaTime);
		}

		private void UpdateTexture(byte[] data)
		{
			if (texture == null)
				texture = new Texture2D(video.width, video.height);

			texture.SetData(data);

			if (!video.isVideo)
				video = null;
		}
	}
}