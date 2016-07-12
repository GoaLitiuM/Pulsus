using System;
using System.IO;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;
using System.Threading;

namespace Pulsus.FFmpeg
{
	public class FFmpegVideo : IDisposable
	{
		Thread loadThread;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate void NextVideoFrameDelegate(byte[] data);

		public bool isVideo { get { return length > 0.0; } }

		FFmpegContext ffContext;

		string path;
		public int width;
		public int height;
		public double frametime;
		public double length;
		public double currentTime;

		object frameLock = new object();
		public int presentedFrames = 0;
		public int decodedFrames = 0;

		double nextFramePts = 0.0;

		public NextVideoFrameDelegate nextFrame;

		public FFmpegVideo()
		{
			loadThread = new Thread(new ThreadStart(LoadThread));
			loadThread.Name = "FFmpegVideoThread";
			loadThread.IsBackground = true;
		}

		public void Dispose()
		{
			if (loadThread.IsAlive)
				loadThread.Abort();

			if (ffContext != null)
				ffContext.Dispose();
			ffContext = null;
		}

		public void Update(double deltaTime)
		{
			currentTime += deltaTime;

			if (currentTime >= nextFramePts && presentedFrames < decodedFrames)
				NextFrame();
		}

		public void ReadFrames()
		{
			while (ffContext.ReadNextFrame())
			{
				lock (frameLock)
				{
					nextFrame(ffContext.GetFrameData());
					presentedFrames++;
				}
			}
		}

		private void NextFrame()
		{
			if (ffContext == null)
				return;

			if (Monitor.TryEnter(frameLock))
			{
				try
				{
					nextFrame(ffContext.GetFrameData());
					presentedFrames++;
				}
				finally
				{
					Monitor.Exit(frameLock);
				}
			}
		}

		public bool Load(string file)
		{
			path = file;
			Load(new FileStream(file, FileMode.Open, FileAccess.Read));

			return true;
		}

		public void Start()
		{
			if (loadThread.IsAlive)
			{
				lock (frameLock)
				{
					ffContext.Dispose();
					ffContext = null;

					currentTime = 0.0;
					presentedFrames = 0;
					decodedFrames = 0;
					nextFramePts = 0.0;

					Load(path);
				}
			}
			else if (ffContext != null)
				loadThread.Start();
		}

		private void LoadThread()
		{
			while (true)
			{
				lock (frameLock)
				{
					// decoder is one frame ahead of presentation
					if (decodedFrames <= presentedFrames)
					{
						if (ffContext.ReadNextFrame())
						{
							decodedFrames++;
							nextFramePts = ffContext.framePts;
						}
					}
				}

				Thread.Sleep(1);
			}
		}

		public unsafe void Load(Stream stream)
		{
			if (!stream.CanRead)
				throw new ApplicationException("Unable to read stream");

			ffContext = FFmpegContext.Read(stream, path);
			ffContext.FindStreamInfo();

			ffContext.SelectStream(AVMediaType.AVMEDIA_TYPE_VIDEO);
	
			width = ffContext.GetWidth();
			height = ffContext.GetHeight();
			frametime = ffContext.GetFrametime();
			length = ffContext.GetLength();

			if (width * height <= 0)
				throw new ApplicationException("Invalid video size: " + width.ToString() + "x" + height.ToString());

			// setup resamplers and other format converters if needed
			ffContext.ConvertToFormat(AVPixelFormat.AV_PIX_FMT_BGRA);
		}
	}
}
