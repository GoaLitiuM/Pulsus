using System;

namespace Pulsus.Audio
{
	public class SoundInstance
	{
		public Sound sound { get; private set; }
		public uint length { get { return (uint)sound.data.Length; } }
		public uint position;
		public float volume { get; private set; }

		public uint startPosition { get; private set; }
		public uint endPosition { get; private set; }

		public bool loop { get; private set; }
		public bool paused;
		public bool remove;

		// starting and stopping offsets in next sound buffer
		public uint offsetStart;
		public uint offsetStop;

		public SoundInstance(Sound sound, float volume = 1.0f)
		{
			this.sound = sound;
			this.volume = Math.Max(0.0f, volume);

			endPosition = length;
		}

		public SoundInstance(Sound sound, uint startSample, uint endSample, float volume = 1.0f)
		{
			this.sound = sound;
			this.volume = Math.Max(0.0f, volume);

			startPosition = startSample;
			endPosition = endSample;
		}
	}
}
