using System;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;

namespace Pulsus.FFmpeg
{
	public class FFmpegException : Exception
	{
		public FFmpegException(int errorCode)
			: base(GetError(errorCode))
		{
		}

		private static string GetError(int errorCode)
		{
			IntPtr strPtr = Marshal.AllocHGlobal(256);
			string errorMsg;

			if (ffmpeg.av_strerror(errorCode, strPtr, 256) == 0)
				errorMsg = Marshal.PtrToStringAnsi(strPtr);
			else
				errorMsg = "Unknown error code: " + errorCode.ToString();

			Marshal.FreeHGlobal(strPtr);
			return errorMsg;
		}
	}
}
