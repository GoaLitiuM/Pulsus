using Pulsus.Audio;
using System.IO;
using System;

namespace Pulsus.Gameplay
{
	public class SoundObject
	{
		public string name { get; }
		public SoundFile sound { get; private set; }
		public double sliceStart { get; private set; }
		public double sliceEnd { get; private set; }
		public int polyphony { get; }

		public bool loaded { get { return sound.data != null; } }

		public SoundObject(SoundFile soundFile, int polyphony, string name = "")
		{
			sound = soundFile;
			this.polyphony = polyphony;
			this.name = name;
		}

		public SoundObject(SoundFile soundFile, int polyphony, double sliceStart, double sliceEnd, string name = "")
		{
			sound = soundFile;
			this.polyphony = polyphony;
			this.sliceStart = sliceStart;
			this.sliceEnd = sliceEnd;
			this.name = name;
		}

		public SoundInstance CreateInstance(AudioEngine audio, float volume = 1.0f)
		{
			uint sampleStart = (uint)Math.Round(sliceStart * audio.audioSpec.freq);
			uint sampleEnd = (uint)Math.Round(sliceEnd * audio.audioSpec.freq);

			sampleStart *= audio.bytesPerSample;
			sampleEnd *= audio.bytesPerSample;

			if (sampleEnd > sound.data.data.Length)
				sampleEnd = 0;
			if (sampleStart > sound.data.data.Length)
				sampleStart = 0;

			return SoundInstance.CreateSlice(sound.data, sampleStart, sampleEnd, volume);
		}

		public bool Load(string basePath = "")
		{
			try
			{
				if (!sound.Load())
				{
					Log.Warning("Sound not found: " + Path.GetFileName(sound.path));
					return false;
				}
			}
			catch (System.Threading.ThreadAbortException)
			{

			}
			catch (Exception e)
			{
				Log.Error("FFmpeg: " + e.Message);
				sound = null;
			}

			if (sound == null)
			{
				Log.Error("Failed to load sound: " + Path.GetFileName(sound.path));
				return false;
			}

			return true;
		}
	}
}