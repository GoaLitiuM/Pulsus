using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;
using SDL2;

namespace Pulsus.FFmpeg
{
	public static class FFmpegHelper
	{
		private static string _ffmpegPath;
		public static string ffmpegPath
		{
			get
			{
				if (_ffmpegPath == null)
					_ffmpegPath = Path.Combine(Program.basePath, "ffmpeg", Environment.Is64BitProcess ? "x64" : "x86");

				return _ffmpegPath;
			}
		}

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate int ReadStreamDelegate(IntPtr opaque, IntPtr buf, int buf_size);

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate Int64 SeekStreamDelegate(IntPtr opaque, Int64 offset, int whence);

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate void LogDelegate(IntPtr ptr, int i, string str, IntPtr valist);

		// prevents garbage collector from collecting delegates
		static List<Delegate> delegateRefs = new List<Delegate>();

		private static LogDelegate logCallback = Log;
		private static IntPtr logPtr = IntPtr.Zero;
		public static string logLastLine = "";

		static void Log(IntPtr avcl, int level, string fmt, IntPtr vl)
		{
			logLastLine = fmt.Trim();

			if (level > AV_LOG_WARNING)
				return;

			System.Diagnostics.Debug.WriteLine("ffmpeg: " + logLastLine);
		}

		public static void Init()
		{
			// specify additional search path where ffmpeg binaries should be loaded from
			string envVariable = Environment.OSVersion.Platform == PlatformID.Win32NT ? "PATH" : "LD_LIBRARY_PATH";
			string oldValue = Environment.GetEnvironmentVariable(envVariable);
			Environment.SetEnvironmentVariable(envVariable, ffmpegPath + Path.PathSeparator + oldValue);

			try
			{
				ffmpeg.av_register_all();
				ffmpeg.avcodec_register_all();

				// early load
				ffmpeg.swscale_version();
				ffmpeg.swresample_version();

				// forward FFmpeg log messages
				unsafe
				{
					logPtr = Marshal.GetFunctionPointerForDelegate(logCallback);
					GC.KeepAlive(logCallback);
					FFmpegHelper.av_log_set_callback(logPtr);
				}
			}
			catch (DllNotFoundException e)
			{
				string description = "Failed to initialize FFmpeg.";
				if (Environment.OSVersion.Platform != PlatformID.Win32NT)
					description += " Please install FFmpeg (3.0.2), or install the static FFmpeg binaries to \"" + ffmpegPath + "\"";

				throw new DllNotFoundException(description, e);
			}
		}

		public static byte[] ImageFromFile(string path, out int width, out int height, out int bytesPerPixel)
		{
			FFmpegVideo video = new FFmpegVideo();
			width = 0;
			height = 0;
			bytesPerPixel = 0;

			video.Load(path);

			List<byte> bytes = new List<byte>();
			video.OnNextFrame += (data) => bytes.AddRange(data);

			if (video.isVideo)
				video.ReadNextFrame();
			else
				video.ReadFrames();

			width = video.width;
			height = video.height;
			bytesPerPixel = 4;

			video.Dispose();

			return bytes.ToArray();
		}

		public static byte[] SoundFromFile(string path, out int sampleRate, out int channels, out ushort sampleFormatSDL)
		{
			using (FFmpegContext ffContext = FFmpegContext.Read(path))
			{
				ffContext.SelectStream(AVMediaType.AVMEDIA_TYPE_AUDIO);

				sampleRate = ffContext.audioSampleRate;
				channels = ffContext.audioChannels;

				AVSampleFormat sampleFormat = ffContext.audioSampleFormat;
				switch (sampleFormat)
				{
					case AVSampleFormat.AV_SAMPLE_FMT_S16:
						sampleFormatSDL = SDL.AUDIO_S16;
						break;
					case AVSampleFormat.AV_SAMPLE_FMT_FLT:
						sampleFormatSDL = SDL.AUDIO_F32;
						break;
					case AVSampleFormat.AV_SAMPLE_FMT_S32:
						sampleFormatSDL = SDL.AUDIO_S32;
						break;
					default:
						throw new ApplicationException("Could not map AVSampleFormat to SDL audio format: " + sampleFormat.ToString());
				}

				List<byte> bytes = new List<byte>(ffContext.audioBytesTotal);
				while (ffContext.ReadNextFrame())
					bytes.AddRange(ffContext.GetFrameData());

				return bytes.ToArray();
			}
		}

		public static byte[] SoundFromFileResample(string path, int sampleRate, int channels, ushort sampleFormatSDL)
		{
			AVSampleFormat targetFormat2;
			switch (sampleFormatSDL)
			{
				case SDL.AUDIO_S16:
					targetFormat2 = AVSampleFormat.AV_SAMPLE_FMT_S16;
					break;
				case SDL.AUDIO_F32:
					targetFormat2 = AVSampleFormat.AV_SAMPLE_FMT_FLT;
					break;
				case SDL.AUDIO_S32:
					targetFormat2 = AVSampleFormat.AV_SAMPLE_FMT_S32;
					break;
				default:
					throw new ApplicationException("Could not map SDL audio format to AVSampleFormat: " + sampleFormatSDL.ToString());
			}

			using (FFmpegContext ffContext = FFmpegContext.Read(new FileStream(path, FileMode.Open, FileAccess.Read)))
			{
				ffContext.SelectStream(AVMediaType.AVMEDIA_TYPE_AUDIO);

				// setup resamplers and other format converters if needed
				ffContext.ConvertToFormat(targetFormat2, sampleRate, channels);

				// read data
				int allocated = ffContext.audioBytesTotal;
				List<byte> bytes = new List<byte>(allocated);
				while (ffContext.ReadNextFrame())
					bytes.AddRange(ffContext.GetFrameData());

				int overshoot = allocated - bytes.Count;

				return bytes.ToArray();
			}
		}

		// compression level [0-9]
		public static void SaveImagePNG(string path, byte[] data, int width, int height, int compression = 1)
		{
			using (FFmpegContext ffContext = FFmpegContext.Write(path))
			{
				ffContext.SetOutputFormat(AVCodecID.AV_CODEC_ID_PNG, width, height, AVPixelFormat.AV_PIX_FMT_BGRA, compression);
				ffContext.WriteHeader();
				ffContext.WriteFrame(data);
			}
		}

		public static void SaveSound(string path, byte[] data, int sampleCount, int targetSampleRate)
		{
			using (FFmpegContext ffContext = FFmpegContext.Write(path))
			{
				ffContext.SetOutputFormat(AVCodecID.AV_CODEC_ID_NONE, targetSampleRate, sampleCount, AVSampleFormat.AV_SAMPLE_FMT_S16);
				ffContext.WriteHeader();
				ffContext.WriteFrame(data);
			}
		}

		public const int AV_LOG_QUIET = -8;
		public const int AV_LOG_PANIC = 0;
		public const int AV_LOG_FATAL = 8;
		public const int AV_LOG_ERROR = 16;
		public const int AV_LOG_WARNING = 24;
		public const int AV_LOG_INFO = 32;
		public const int AV_LOG_VERBOSE = 40;
		public const int AV_LOG_DEBUG = 48;
		public const int AV_LOG_TRACE = 56;
		public const int AVERROR_EOF = -541478725;  // FFERRTAG( 'E','O','F',' ')

		// corrected signatures from FFmpeg.AutoGen

		private const string libavutil = "avutil-55";
		private const string libavformat = "avformat-57";

		[DllImport(libavutil, EntryPoint = "av_log_set_callback", CallingConvention = CallingConvention.Cdecl)]
		public static extern void av_log_set_callback(IntPtr @callback);

		[DllImport(libavformat, EntryPoint = "avio_alloc_context", CallingConvention = CallingConvention.Cdecl)]
		public static extern unsafe AVIOContext* avio_alloc_context(sbyte* buffer, int buffer_size, int write_flag, void* opaque, IntPtr read_packet, IntPtr write_packet, IntPtr seek);
	}
}
