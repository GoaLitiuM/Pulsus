using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using FFmpeg.AutoGen;

namespace Pulsus.FFmpeg
{
	public class FFmpegVideo : IDisposable
	{
		public int width { get; private set; }
		public int height { get; private set; }
		public double frametime { get; private set; }
		public double length { get; private set; }
		public double currentTime { get; private set; }
		public int presentedFrames { get; private set; }
		public int decodedFrames { get; private set; }

		public OnNextFrameDelegate OnNextFrame;

		private byte[] bytes = null;
		public byte[] imageBytes { get { return bytes; } }

		public bool isVideo { get { return length > 0.0; } }

		private string path;
		private double nextFramePts;
		private FFmpegContext ffContext;
		private Thread loadThread;
		private AutoResetEvent nextFrameEvent;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate void OnNextFrameDelegate(byte[] data);

		public void Dispose()
		{
			if (ffContext != null)
				ffContext.Dispose();
			ffContext = null;

			if (loadThread != null)
			{
				nextFrameEvent.Set();
				if (loadThread.IsAlive)
					loadThread.Join();
			}
		}

		public void Load(string path)
		{
			this.path = path;

			Load(new FileStream(path, FileMode.Open, FileAccess.Read));

			if (isVideo)
			{
				loadThread = new Thread(new ThreadStart(LoadThread));
				loadThread.Name = "FFmpegVideoThread";
				loadThread.IsBackground = true;

				nextFrameEvent = new AutoResetEvent(false);
				loadThread.Start();
			}
		}

		private void Load(Stream stream)
		{
			if (!stream.CanRead)
				throw new ApplicationException("Unable to read stream");

			ffContext = FFmpegContext.Read(stream);
			ffContext.FindStreamInfo();

			ffContext.SelectStream(AVMediaType.AVMEDIA_TYPE_VIDEO);

			width = ffContext.imageWidth;
			height = ffContext.imageHeight;
			frametime = ffContext.videoFrametime;
			length = ffContext.videoDuration;

			if (width * height <= 0)
				throw new ApplicationException("Invalid video size: " + width.ToString() + "x" + height.ToString());

			// setup resamplers and other format converters if needed
			ffContext.ConvertToFormat(AVPixelFormat.AV_PIX_FMT_BGRA);

			bytes = new byte[width * height * 4];
		}

		public void Update(double deltaTime)
		{
			if (ffContext == null)
				return;

			currentTime += deltaTime;

			if (currentTime >= nextFramePts && presentedFrames < decodedFrames)
			{
				ffContext.GetFrameData(ref bytes, 0);
				if (OnNextFrame != null)
					OnNextFrame(bytes);

				presentedFrames++;
				nextFrameEvent.Set();
			}
		}

		public byte[] ReadFrames()
		{
			if (!ffContext.ReadFrame())
				return null;

			ffContext.GetFrameData(ref bytes, 0);	
			presentedFrames++;
			return bytes;
		}

		public void Start()
		{
			if (presentedFrames == 0)
			{
				nextFrameEvent.Set();
				return;
			}

			// TODO: seek back to first frame

			ffContext.Dispose();
			ffContext = null;

			currentTime = 0.0;
			presentedFrames = 0;
			decodedFrames = 0;
			nextFramePts = 0.0;

			Load(path);
			nextFrameEvent.Set();
		}

		private void LoadThread()
		{
			nextFrameEvent.WaitOne();
			while (ffContext != null)
			{
				ReadNextFrame();
				nextFrameEvent.WaitOne();
			}
		}

		public bool ReadNextFrame()
		{
			// decoder is one frame ahead of presentation
			if (decodedFrames <= presentedFrames)
			{
				if (ffContext.ReadFrame())
				{
					decodedFrames++;
					nextFramePts = ffContext.framePts;
				}
				else
					return false;
			}

			return true;
		}
	}
}
