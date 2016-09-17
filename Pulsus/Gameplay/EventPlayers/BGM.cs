using Pulsus.Audio;

namespace Pulsus.Gameplay
{
	public class BGM : EventPlayer
	{
		AudioEngine audioEngine;

		private bool seeking;

		public BGM(Chart chart, AudioEngine audioEngine)
			: base(chart)
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

			SoundObject soundObject = soundEvent.sound;
			if (soundObject != null && soundObject.sound != null)
			{
				if (soundObject.sound.data != null)
				{
					SoundData soundData = soundObject.sound.data;
					SoundInstance instance = soundObject.CreateInstance(audioEngine, (float)chart.volume);
					if (realtime)
						audioEngine.Play(instance, soundObject.polyphony);
					else
						audioEngine.PlayScheduled(soundEvent.timestamp, instance, soundObject.polyphony);
				}
				else
					Log.Warning("Sound file not loaded: " + soundObject.name);
			}
		}
	}
}
