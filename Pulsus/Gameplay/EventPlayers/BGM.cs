using Pulsus.Audio;

namespace Pulsus.Gameplay
{
	public class BGM : EventPlayer
	{
		AudioEngine audioEngine;

		private bool seeking;

		public BGM(AudioEngine audioEngine, Song song)
			: base(song)
		{
			this.audioEngine = audioEngine;
		}

		public override void OnPlayerStart()
		{
			// process and ignore past sound events
			seeking = true;
			if (pulse > 0)
				UpdateSong();
			seeking = false;
		}

		public override void OnBGM(SoundEvent soundEvent)
		{
			if (seeking)
				return;

			if (soundEvent.sound == null)
				return;

			if (soundEvent.sound.sound != null)
			{
				if (realtime)
					audioEngine.Play(soundEvent.sound.sound, (float)chart.volume);
				else
					audioEngine.PlayScheduled(soundEvent.timestamp, soundEvent.sound.sound, (float)chart.volume);
			}
			else
				Log.Warning("Failed to play sound " + soundEvent.sound.name);
		}
	}
}
