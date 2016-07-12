using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Pulsus.Audio;
using FFmpeg.AutoGen;
using SDL2;

namespace Pulsus.FFmpeg
{
	public static class FFmpegHelper
	{
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
			string executablePath = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
			string ffmpegPath = Path.Combine("ffmpeg", Environment.Is64BitProcess ? "x64" : "x86");
			string ffmpegPathFull = Path.Combine(executablePath, ffmpegPath);

			switch (Environment.OSVersion.Platform)
			{
				case PlatformID.Win32NT:
					if (!SetDllDirectory(ffmpegPathFull))
						throw new ApplicationException("Failed to call SetDllDirectory, error code " + Marshal.GetLastWin32Error().ToString());
					break;
				case PlatformID.Unix:
				case PlatformID.MacOSX:
					// in cases where the system does not have ffmpeg installed,
					// allow loading ffmpeg binaries from the subfolder instead.
					string currentValue = Environment.GetEnvironmentVariable("LD_LIBRARY_PATH");
					if (string.IsNullOrWhiteSpace(currentValue) == false && currentValue.Contains(ffmpegPath) == false)
					{
						string newValue = currentValue + Path.PathSeparator + ffmpegPath;
						Environment.SetEnvironmentVariable("LD_LIBRARY_PATH", newValue);
					}
					break;
			}

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
			catch (DllNotFoundException exception)
			{
				string msg = "Failed to initialize FFmpeg.";
				if (Environment.OSVersion.Platform != PlatformID.Win32NT)
					msg += " Please install FFmpeg (3.0.2), or install the static FFmpeg binaries to \"" + ffmpegPathFull + "\"";
				Program.OnCaughtException(exception, msg);
			}
		}

		public static byte[] ImageFromFile(string path, out int width, out int height, out int bytesPerPixel)
		{
			FFmpegVideo video = new FFmpegVideo();
			width = 0;
			height = 0;
			bytesPerPixel = 0;

			if (!video.Load(path) || video.width * video.height <= 0)
				return null;
			
			List<byte> bytes = new List<byte>();

			video.nextFrame += (data) => bytes.AddRange(data);
			//video.NextFrame();
			video.ReadFrames();

			width = video.width;
			height = video.height;
			bytesPerPixel = 4;

			video.Dispose();

			return bytes.ToArray();
		}

		public static Sound SoundFromFile(string path)
		{
			return SoundFromFile(path, Sound.targetFreq, Sound.targetFormat);
		}

		public static Sound SoundFromFile(string path, int targetSampleRate, ushort targetFormat)
		{
			AVSampleFormat targetFormat2;
			switch (targetFormat)
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
				/*case SDL.AUDIO_U8:
					targetFormat2 = AVSampleFormat.AV_SAMPLE_FMT_U8;
					break;*/
				default:
					throw new ApplicationException("Could not map SDL audio format to AVSampleFormat: " + targetFormat.ToString());
			}
			using (FFmpegContext ffContext = FFmpegContext.Read(new FileStream(path, FileMode.Open, FileAccess.Read), path))
			{
				ffContext.SelectStream(AVMediaType.AVMEDIA_TYPE_AUDIO);
				int channels = ffContext.GetChannels();

				if (channels != 2 && channels != 1)
					throw new ApplicationException("Invalid channel count: " + channels.ToString());

				// setup resamplers and other format converters if needed
				ffContext.ConvertToFormat(targetFormat2, targetSampleRate, 2);

				// read data
				List<byte> bytes = new List<byte>(ffContext.GetTotalSampleCount() + 1024);
				while (ffContext.ReadNextFrame())
					bytes.AddRange(ffContext.GetFrameData());

				int realCount = bytes.Count / (ffContext.GetBytesPerSample() * channels);
				Sound sound = new Sound(bytes.ToArray(), realCount, ffContext.GetSampleRate(), channels);
				return sound;
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
		public const int AVERROR_EOF = -541478725;	// FFERRTAG( 'E','O','F',' ')

		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool SetDllDirectory(string lpPathName);

		// corrected signatures from FFmpeg.AutoGen

		private const string libavutil = "avutil-55";
		private const string libavformat = "avformat-57";

		[DllImport(libavutil, EntryPoint = "av_log_set_callback", CallingConvention = CallingConvention.Cdecl)]
		public static extern void av_log_set_callback(IntPtr @callback);

		[DllImport(libavformat, EntryPoint = "avio_alloc_context", CallingConvention = CallingConvention.Cdecl)]
		public static extern unsafe AVIOContext* avio_alloc_context(sbyte* buffer, int buffer_size, int write_flag, void* opaque, IntPtr read_packet, IntPtr write_packet, IntPtr seek);
	}
}
