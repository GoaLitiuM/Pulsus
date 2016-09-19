using System;
using Pulsus.Audio;

namespace Pulsus.Gameplay
{
	public class SoundObject
	{
		public string name { get; }
		public SoundFile soundFile { get; private set; }
		public double sliceStart { get; private set; }
		public double sliceEnd { get; private set; }
		public int polyphony { get; }

		public bool loaded { get { return soundFile != null ? soundFile.data != null : false; } }

		public SoundObject(SoundFile soundFile, int polyphony, string name = "")
		{
			this.soundFile = soundFile;
			this.polyphony = polyphony;
			this.name = name;
		}

		public SoundObject(SoundFile soundFile, int polyphony, double sliceStart, double sliceEnd, string name = "")
		{
			this.soundFile = soundFile;
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

			if (sampleEnd > soundFile.data.data.Length)
				sampleEnd = 0;
			if (sampleStart > soundFile.data.data.Length)
				sampleStart = 0;

			return SoundInstance.CreateSlice(soundFile.data, sampleStart, sampleEnd, volume);
		}
	}
}