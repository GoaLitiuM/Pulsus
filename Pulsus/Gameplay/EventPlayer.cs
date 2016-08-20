using System;
using System.Collections.Generic;

namespace Pulsus.Gameplay
{
	public class EventPlayer : IDisposable
	{
		public double timeMultiplier = 1.0;
		public bool realtime = true;

		public Chart chart;
		protected List<Event> eventList;

		public bool playing = false;
		public double startTime = 0.0;

		public int currentMeasure = 0;
		public double currentTime = 0.0;
		public long pulse = 0;

		protected int lastEventIndex = 0;
		public double bpm = 0.0;
		protected double nextStopTime = 0.0;
		protected double nextBpm = 0.0;

		protected int lastTimeEventIndex = 0;
		double lastTimeEventTime = 0.0;
		long lastTimeEventPulse = 0;
		double lastTimeEventBpm;
		double lastTimeEventMeter;

		public double progress { get { return chart != null ? (currentTime / chart.songLength) : 0.0; } }

		public EventPlayer(Song song)
		{
			if (song == null || song.chart == null)
				throw new ArgumentNullException("Invalid chart");

			chart = song.chart;
			eventList = chart.eventList;

			bpm = chart.bpm;
			lastTimeEventBpm = bpm;
			lastTimeEventMeter = 1.0;
		}

		public virtual void Dispose()
		{
		}

		public virtual void StartPlayer()
		{
			currentTime = startTime;
			AdvanceTime(0.0);
			UpdateSong();

			playing = true;
		}

		public void StopPlayer()
		{
			if (!playing)
				return;

			OnPlayerStop();
		}

		public virtual void Update(double deltaTime)
		{
			if (!playing)
				return;

			// apply bpm changes starting from next beat
			if (nextBpm != 0.0)
			{
				bpm = nextBpm;
				nextBpm = 0.0;
			}

			AdvanceTime(deltaTime);

			if (playing && eventList != null)
				UpdateSong();
		}

		public virtual void AdvanceTime(double deltaTime)
		{
			if (realtime)
			{
				if (bpm < 0 && timeMultiplier > 0)
					timeMultiplier = -timeMultiplier;

				currentTime += deltaTime * timeMultiplier;

				// calculate current pulse from time events
				for (; lastTimeEventIndex<chart.timeEventList.Count; lastTimeEventIndex++)
				{
					Event timeEvent = chart.timeEventList[lastTimeEventIndex];
					if (timeEvent.timestamp >= currentTime)
						break;

					lastTimeEventTime = timeEvent.timestamp;
					lastTimeEventPulse = timeEvent.pulse;

					if (timeEvent is BPMEvent)
					{
						lastTimeEventBpm = (timeEvent as BPMEvent).bpm;
						if (lastTimeEventBpm < 0.0)
							lastTimeEventBpm = -lastTimeEventBpm;
					}
					else if (timeEvent is StopEvent)
					{
						double stopPulses = (timeEvent as StopEvent).stopTime;
						double stopTime = stopPulses / chart.resolution * 60.0 / lastTimeEventBpm;
						lastTimeEventTime = Math.Min(lastTimeEventTime + stopTime, currentTime);
					}
					else if (timeEvent is MeterEvent)
						lastTimeEventMeter = (timeEvent as MeterEvent).meter;
				}
				double remainderTime = currentTime - lastTimeEventTime;
				double elapsedPulses = (remainderTime * (lastTimeEventBpm / lastTimeEventMeter) / 60.0 * chart.resolution);
				
				long lastPulse = pulse;
				pulse = lastTimeEventPulse + (long)elapsedPulses;

				if (timeMultiplier < 0)
					pulse = lastPulse;
			}
			else
				pulse = eventList[eventList.Count - 1].pulse;
		}

		public virtual void Seek(double time)
		{
			if (time < currentTime)
			{
				// reset all cached values when seeking backwards
				lastEventIndex = 0;
				lastTimeEventIndex = 0;
				lastTimeEventTime = 0.0;
				lastTimeEventPulse = 0;
				lastTimeEventBpm = chart.bpm;
				lastTimeEventMeter = 1.0;
			}

			currentTime = time;
			pulse = chart.GetPulseFromTime(time);

			if (!playing)
				startTime = currentTime;
		}

		public virtual void SeekEnd()
		{
			Event lastEvent = eventList[eventList.Count - 1];
			pulse = lastEvent.pulse + 1;
			currentTime = chart.GetTimeFromPulse(pulse);

			if (!playing)
				startTime = currentTime;
		}

		public virtual void UpdateSong()
		{
			int i = lastEventIndex;
			for (; i < eventList.Count; i++)
			{
				if (pulse >= eventList[i].pulse)
				{
					if (!realtime)
						currentTime = eventList[i].timestamp;

					ProcessEvent(i);
				}
				else
					break;
			}
			lastEventIndex = i;

			if (pulse >= eventList[eventList.Count - 1].pulse)
				OnSongEnd();
		}

		protected void ProcessEvent(int index)
		{
			Event currentEvent = eventList[index];

			if (currentEvent is MeterEvent)
				OnMeter(currentEvent as MeterEvent);
			else if (currentEvent is BPMEvent)
				OnBPM(currentEvent as BPMEvent);
			else if (currentEvent is SoundEvent)
			{
				SoundEvent soundEvent = currentEvent as SoundEvent;
				NoteEvent noteEvent = soundEvent as NoteEvent;
				KeySoundChangeEvent keySoundEvent = soundEvent as KeySoundChangeEvent;

				OnSoundObject(soundEvent);

				if (keySoundEvent != null)
					OnPlayerKeyChange(keySoundEvent);
				else if (noteEvent != null)
				{
					LandmineEvent landmineEvent = noteEvent as LandmineEvent;
					LongNoteEvent longNoteEvent = noteEvent as LongNoteEvent;
					LongNoteEndEvent longNoteEndEvent = noteEvent as LongNoteEndEvent;

					if (landmineEvent != null)
						OnLandmine(noteEvent);
					else if (longNoteEndEvent != null)
						OnPlayerKeyLongEnd(longNoteEndEvent);
					else if (longNoteEvent != null)
						OnPlayerKeyLong(longNoteEvent);
					else
						OnPlayerKey(noteEvent);
				}
				else
					OnBGM(soundEvent);
			}
			else if (currentEvent is StopEvent)
				OnStop(currentEvent as StopEvent);
			else if (currentEvent is BGAEvent)
			{
				BGAEvent bgaEvent = currentEvent as BGAEvent;

				OnBGAObject(bgaEvent);
				OnBGA(bgaEvent);
			}
			else if (currentEvent is MeasureMarkerEvent)
				OnMeasureChange(currentEvent as MeasureMarkerEvent);
		}

		public virtual void OnPlayerStart()
		{

		}

		public virtual void OnPlayerStop()
		{
			playing = false;
		}

		public virtual void OnSongEnd()
		{
			StopPlayer();
		}

		public virtual void OnSoundObject(SoundEvent soundEvent)
		{
			if (!playing)
				return;
		}

		public virtual void OnBGAObject(BGAEvent bgaEvent)
		{
			if (!playing)
				return;
		}

		public virtual void OnMeter(MeterEvent meterEvent)
		{
		}

		public virtual void OnBPM(BPMEvent bpmEvent)
		{
			nextBpm = bpmEvent.bpm;
		}

		public virtual void OnBGM(SoundEvent bgmEvent)
		{

		}

		public virtual void OnPlayerKey(NoteEvent noteEvent)
		{

		}

		public virtual void OnPlayerKeyLong(LongNoteEvent noteEvent)
		{

		}

		public virtual void OnPlayerKeyLongEnd(LongNoteEndEvent noteEndEvent)
		{

		}

		public virtual void OnLandmine(NoteEvent noteEvent)
		{

		}

		public virtual void OnPlayerKeyChange(KeySoundChangeEvent keySoundChangeEvent)
		{

		}

		public virtual void OnStop(StopEvent stopEvent)
		{

		}

		public virtual void OnBGA(BGAEvent bgaEvent)
		{

		}

		private void OnMeasureChange(MeasureMarkerEvent measureMarkerEvent)
		{
			currentMeasure++;
		}
	}
}