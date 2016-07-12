using Pulsus.Audio;

namespace Pulsus.Gameplay
{
	public class BGM : EventPlayer
	{
		AudioEngine audioEngine;

		public BGM(AudioEngine audioEngine, Song song)
			: base(song)
		{
			this.audioEngine = audioEngine;
		}

		public override void OnBGM(int eventIndex, SoundEvent value)
		{
			if (value.sound == null)
				return;

			if (value.sound.sound != null)
			{
				if (realtime)
					audioEngine.Play(value.sound.sound, (float)chart.volume);
				else
					audioEngine.PlayScheduled(value.timestamp, value.sound.sound, (float)chart.volume);
			}
			else
				Log.Warning("Failed to play sound " + value.sound.name);
		}
	}
}
