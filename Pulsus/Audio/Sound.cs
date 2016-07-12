using System;
using System.Collections.Generic;
using SDL2;
using FFmpeg.AutoGen;
using System.IO;
using Pulsus.FFmpeg;

namespace Pulsus.Audio
{
	public class Sound
	{
		public static ushort targetFormat;	//SDL2 format
		public static int targetFreq;

		public byte[] data;
		public int sampleCount;
		public int sampleRate;
		public int channels;
		public int polyphony;	// maximum number of playing SoundInstances
		public int instances;	// currently active SoundInstances

		public Sound(byte[] data, int sampleCount, int sampleRate, int channels, int polyphony = 0)
		{
			this.data = data;
			this.sampleCount = sampleCount;
			this.sampleRate = sampleRate;
			this.channels = channels;
			this.polyphony = polyphony;
		}

		public Sound(string path)
		{
			AVSampleFormat targetSampleFormat;
			switch (targetFormat)
			{
				case SDL.AUDIO_S16:
					targetSampleFormat = AVSampleFormat.AV_SAMPLE_FMT_S16;
					break;
				case SDL.AUDIO_F32:
					targetSampleFormat = AVSampleFormat.AV_SAMPLE_FMT_FLT;
					break;
				case SDL.AUDIO_S32:
					targetSampleFormat = AVSampleFormat.AV_SAMPLE_FMT_S32;
					break;
				/*case SDL.AUDIO_U8:
					targetFormat2 = AVSampleFormat.AV_SAMPLE_FMT_U8;
					break;*/
				default:
					throw new ApplicationException("Could not map SDL audio format " + targetFormat.ToString() + " to AVSampleFormat");
			}

			using (FFmpegContext ffContext = FFmpegContext.Read(new FileStream(path, FileMode.Open, FileAccess.Read), path))
			{
				ffContext.SelectStream(AVMediaType.AVMEDIA_TYPE_AUDIO);

				channels = ffContext.GetChannels();
				if (channels > 2)
					throw new ApplicationException("Invalid channel count: " + channels.ToString());

				// setup resamplers and other format converters if needed
				ffContext.ConvertToFormat(targetSampleFormat, targetFreq);

				// read data
				List<byte> bytes = new List<byte>(ffContext.GetTotalSampleCount() + 1024);
				while (ffContext.ReadNextFrame())
					bytes.AddRange(ffContext.GetFrameData());

				data = bytes.ToArray();
				sampleCount = bytes.Count / (ffContext.GetBytesPerSample() * channels);
				sampleRate = ffContext.GetSampleRate();
			}
		}
	}
}
