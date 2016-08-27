using System;
using Pulsus.Audio;

namespace Pulsus.Gameplay
{
	public class Player : EventPlayer
	{
		public bool autoplay;

		AudioEngine audioEngine;
		BMSJudge judge;
		Skin skin;

		private bool seeking;

		public Player(AudioEngine audioEngine, Song song, BMSJudge judge, Skin skin)
			: base(song)
		{
			this.audioEngine = audioEngine;
			this.judge = judge;
			this.skin = skin;
		}

		public override void StartPlayer()
		{
			if (playing)
				return;

			seeking = true;
			base.StartPlayer();
			seeking = false;
		}

		public override void OnPlayerKey(NoteEvent noteEvent)
		{
			if (!autoplay)
				return;
			
			PressKey(noteEvent.lane, noteEvent.sound, noteEvent);
			ReleaseKey(noteEvent.lane);
		}

		public override void OnPlayerKeyLong(LongNoteEvent noteEvent)
		{
			if (!autoplay)
				return;

			PressKey(noteEvent.lane, noteEvent.sound, noteEvent);
		}

		public override void OnPlayerKeyLongEnd(LongNoteEndEvent noteEndEvent)
		{
			if (!autoplay)
				return;

			ReleaseKey(noteEndEvent.lane, noteEndEvent.sound);
		}

		public void PlayerPressKey(int lane)
		{
			PressKey(lane, null);
		}

		public void PlayerReleaseKey(int lane)
		{
			ReleaseKey(lane, null);
		}
		
		private void PressKey(int lane, SoundObject value, NoteEvent pressNote = null)
		{
			if (seeking)
				return;

			if (skin != null)
				skin.OnKeyPress(lane);

			if (judge != null)
				judge.OnKeyPress(lane, pressNote);

			if (value == null)
			{
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
					value = noteEvent.sound;
				}
			}

			SoundObject keySound = value;
			if (keySound != null && keySound.sound != null)
			{
				if (keySound.sound != null)
				{
					if (realtime)
						audioEngine.Play(keySound.sound, (float)chart.volume);
					else
						audioEngine.PlayScheduled(currentTime, keySound.sound, (float)chart.volume);
				}
				else
					Log.Warning("Failed to play sound: " + keySound.name);
			}
		}

		private void ReleaseKey(int lane, SoundObject value = null)
		{
			if (seeking)
				return;

			if (skin != null)
				skin.OnKeyRelease(lane);

			if (judge != null)
				judge.OnKeyRelease(lane);
		}
	}
}
