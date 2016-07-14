using System;
using System.Collections.Generic;

namespace Pulsus.Gameplay
{
	public class EventPlayer : IDisposable
	{
		public double startOffset = 0;   // how early sound scheduling is done in timeline
		public double timeMultiplier = 1.0;
		public bool realtime = true;

		public Song song;
		public Chart chart;
		protected List<Event> eventList;

		public bool playing = false;
		public bool stopping = false;

		public int currentMeasure = 0;
		public double currentTime = 0.0;
		public long pulse = 0;
		public long stopPulse = 0;
		public long startPulse = 0;
		public double stopLeft = 0.0;
		public long resolution = 0;

		protected int lastEventIndex = 0;
		public double bpm = 0.0;
		protected double nextStopTime = 0.0;
		protected double nextBpm = 0.0;

		public double progress { get { return chart != null ? (currentTime / chart.songLength) : 0.0; } }

		public EventPlayer(Song song)
		{
			if (song == null || song.chart == null)
				return;

			this.song = song;
			chart = song.chart;
			eventList = chart.eventList;
			resolution = chart.resolution;

			currentMeasure = 0;

			bpm = chart.bpm;
		}

		public virtual void Dispose()
		{
		}

		public virtual void StartPlayer()
		{
			pulse = startPulse;
			if (pulse != 0)
			{
				currentTime = chart.GetTimeFromPulse(pulse);
			
				for (int i = lastEventIndex; i < eventList.Count; i++)
				{
					if (eventList[i].pulse < pulse)
						continue;

					lastEventIndex = i;
					break;
				}
			}
			currentTime += startOffset;
			playing = true;
		}

		public void StopPlayer()
		{
			StopPlayer(true);
		}

		protected void StopPlayer(bool forced)
		{
			if (!playing)
				return;

			OnPlayerStop(forced);
		}

		public virtual void Update(double deltaTime)
		{
			if (!playing && !stopping)
				return;

			if (stopping)
				OnPlayerStop(false);

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

				long lastPulse = pulse;
				pulse = chart.GetPulseFromTime(currentTime);

				if (timeMultiplier < 0)
					pulse = lastPulse;

				if (stopPulse != 0 && lastPulse == pulse)
					stopLeft -= deltaTime * timeMultiplier;
				else if (lastPulse != pulse)
				{
					if (stopLeft != 0.0)
						stopLeft = 0.0;
					stopPulse = 0;
				}
			}
			else
				pulse = eventList[eventList.Count-1].pulse;
		}

		public virtual void UpdateSong()
		{
			int i = lastEventIndex;
			for (; i < eventList.Count; i++)
			{
				if (pulse >= eventList[i].pulse)
				{
					if (i != lastEventIndex)
					{
						foreach (var pos in chart.measurePositions)
						{
							if (i >= pos.Item2)
								currentMeasure = pos.Item1;
						}
					}

					if (!realtime)
						currentTime = eventList[i].timestamp;

					ProcessEvent(i);
				}
				else
					break;
			}
			lastEventIndex = i;

			if (pulse >= eventList[eventList.Count-1].pulse)
				OnSongEnd();
		}

		protected void ProcessEvent(int index)
		{
			Event currentEvent = eventList[index];

			if (currentEvent is MeasureLengthEvent)
				OnMeasureLength(currentEvent as MeasureLengthEvent);
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
		}

		public virtual void OnPlayerStart()
		{

		}

		public virtual void OnPlayerStop(bool forced)
		{
			playing = false;
		}

		public virtual void OnSongEnd()
		{
			if (song.repeat)
				throw new NotImplementedException("OnSongEnd repeat not implemented");
			else
				StopPlayer(false);
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

		public virtual void OnMeasureLength(MeasureLengthEvent measureLengthEvent)
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
			stopPulse = stopEvent.stopTime;
			stopLeft = (double)stopEvent.stopTime / resolution * 60.0 / bpm;
		}

		public virtual void OnBGA(BGAEvent bgaEvent)
		{

		}
	}
}