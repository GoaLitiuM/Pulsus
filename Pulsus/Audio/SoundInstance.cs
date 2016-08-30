using System;

namespace Pulsus.Audio
{
	public class SoundInstance
	{
		public SoundData sound { get; }
		public float volume { get; }
		public uint startPosition { get; }
		public uint endPosition { get; }

		private SoundInstance(SoundData sound, float volume = 1.0f)
			: this(sound, 0, 0, volume)
		{
			endPosition = (uint)sound.data.Length;
		}

		private SoundInstance(SoundData sound, uint startSample, uint endSample, float volume = 1.0f)
		{
			this.sound = sound;
			this.volume = Math.Max(0.0f, volume);

			startPosition = startSample;

			if (endSample > startSample)
				endPosition = endSample;
			else
				endPosition = (uint)sound.data.Length;
		}

		public static SoundInstance Create(SoundData sound, float volume = 1.0f)
		{
			return new SoundInstance(sound, volume);
		}

		public static SoundInstance CreateSlice(SoundData sound, uint startSample, uint endSample, float volume = 1.0f)
		{
			return new SoundInstance(sound, startSample, endSample, volume);
		}
	}
}
