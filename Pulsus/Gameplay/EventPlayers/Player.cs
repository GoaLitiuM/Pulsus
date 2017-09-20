using System;
using Pulsus.Audio;
using System.Collections.Generic;

namespace Pulsus.Gameplay
{
	public class Player : EventPlayer
	{
		public bool autoplay;

		AudioEngine audioEngine;
		BMSJudge judge;
		Skin skin;

		private bool seeking;

		public Player(Chart chart, AudioEngine audioEngine, BMSJudge judge, Skin skin)
			: base(chart)
		{
			this.audioEngine = audioEngine;
			this.judge = judge;
			this.skin = skin;
		}

		public override void OnPlayerStart()
		{
			seeking = true;
			if (pulse > 0)
				UpdateSong();
			seeking = false;
		}

		public override void OnPlayerKey(NoteEvent noteEvent)
		{
			if (!autoplay)
				return;

			PressKey(noteEvent.lane, noteEvent.sounds, noteEvent);
			ReleaseKey(noteEvent.lane);
		}

		public override void OnPlayerKeyLong(LongNoteEvent noteEvent)
		{
			if (!autoplay)
				return;

			PressKey(noteEvent.lane, noteEvent.sounds, noteEvent);
		}

		public override void OnPlayerKeyLongEnd(LongNoteEndEvent noteEndEvent)
		{
			if (!autoplay)
				return;

			ReleaseKey(noteEndEvent.lane, noteEndEvent.sounds);
		}

		public void PlayerPressKey(int lane)
		{
			PressKey(lane, null);
		}

		public void PlayerReleaseKey(int lane)
		{
			ReleaseKey(lane, null);
		}

		private void PressKey(int lane, List<SoundObject> values, NoteEvent pressNote = null)
		{
			if (seeking && realtime)
				return;

			if (skin != null)
				skin.OnKeyPress(lane);

			if (judge != null)
				judge.OnKeyPress(lane, pressNote);

			List<SoundObject> sounds = values;

			if (sounds == null)
			{
				sounds = new List<SoundObject>(1);

				int closest = -1;
				double closestDiff = double.MaxValue;
				for (int i = lastEventIndex; i < eventList.Count; i++)
				{
					double eventTimestamp = eventList[i].timestamp;
					double difference = eventTimestamp - currentTime;
					if (difference >= 2.0)
						break;

					NoteEvent note = eventList[i] as NoteEvent;
					if (note == null || note.lane != lane)
						continue;

					if (difference >= closestDiff)
						break;

					closest = i;
					closestDiff = Math.Abs(eventTimestamp - currentTime);
				}

				for (int i = lastEventIndex - 1; i >= 0; i--)
				{
					double eventTimestamp = eventList[i].timestamp;
					double difference = currentTime - eventTimestamp;
					if (difference >= 2.0)
						break;

					NoteEvent note = eventList[i] as NoteEvent;
					if (note == null || note.lane != lane)
						continue;

					if (difference >= closestDiff)
						continue;

					closest = i;
					closestDiff = Math.Abs(currentTime - eventTimestamp);
				}

				if (closest != -1)
				{
					NoteEvent noteEvent = eventList[closest] as NoteEvent;
					sounds.AddRange(noteEvent.sounds);
				}
			}

			foreach (SoundObject soundObject in sounds)
			{
				if (soundObject.soundFile == null)
					continue;

				if (soundObject.soundFile.data == null)
				{
					Log.Warning("Sound file not loaded: " + soundObject.name);
					continue;
				}
				
				SoundData soundData = soundObject.soundFile.data;
				SoundInstance instance = soundObject.CreateInstance(audioEngine, (float)chart.volume);
				if (realtime)
					audioEngine.Play(instance, soundObject.polyphony);
				else
					audioEngine.PlayScheduled(currentTime, instance, soundObject.polyphony);		
			}
		}

		private void ReleaseKey(int lane, List<SoundObject> values = null)
		{
			if (seeking && realtime)
				return;

			if (skin != null)
				skin.OnKeyRelease(lane);

			if (judge != null)
				judge.OnKeyRelease(lane);
		}
	}
}
