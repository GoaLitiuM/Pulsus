using System;
using System.Collections.Generic;
using System.Linq;
using Pulsus.Audio;

namespace Pulsus.Gameplay
{
	public class Player : EventPlayer
	{
		AudioEngine audioEngine;
		BMSJudge judge;
		Skin skin;

		public bool autoplay;
		Dictionary<int, int> channelActive = new Dictionary<int, int>(18);

		public Player(AudioEngine audioEngine, Song song, BMSJudge judge, Skin skin)
			: base(song)
		{
			this.audioEngine = audioEngine;
			this.judge = judge;
			this.skin = skin;
		}
	
		public override void UpdateSong()
		{
			base.UpdateSong();

			if (skin != null)
			{
				List<int> keys = channelActive.Keys.ToList();
				foreach (int channel in keys)
				{
					if (channelActive[channel] >= pulse)
					{
						if (song.chart.hasTurntable &&
							(channel == (int)BMSChannel.KeyBMS.P1Scratch || channel == (int)BMSChannel.KeyBMS.P2Scratch))
						{
							// only triggers key press effects once for turntable input
						}
						else
							skin.OnKeyPress(channel);
					}
					else if (channelActive[channel] != -1)
					{
						ReleaseKey(channel, null);
						channelActive[channel] = -1;
					}
				}
			}
		}

		public override void OnPlayerKey(int eventIndex, NoteEvent value)
		{
			if (!autoplay)
				return;
			
			PressKey(value.lane, value.sound, 0);
		}

		public override void OnPlayerKeyLong(int eventIndex, NoteEvent value)
		{
			if (!autoplay)
				return;

			PressKey(value.lane, value.sound, value.length);
		}

		public void PlayerPressKey(int lane)
		{
			PressKey(lane, null, int.MaxValue);
		}

		public void PlayerReleaseKey(int lane)
		{
			ReleaseKey(lane, null);
		}
		
		private void PressKey(int lane, SoundObject value, int length)
		{
			if (skin != null)
				skin.OnKeyPress(lane);

			if (judge != null)
				judge.OnKeyPress(lane);

			if (length > 0)
				channelActive[lane] = pulse + length;

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
			if (judge != null)
				judge.OnKeyRelease(lane);
		}
	}
}
