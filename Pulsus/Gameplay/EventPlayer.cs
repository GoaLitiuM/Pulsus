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
		public int pulse = 0;
		public int stopPulse = 0;
		public int startPulse = 0;
		public double stopLeft = 0.0;
		public int resolution = 0;

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

				int lastPulse = pulse;
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
				OnMeasureLength(index, currentEvent as MeasureLengthEvent);
			else if (currentEvent is BPMEvent)
				OnBPM(index, currentEvent as BPMEvent);
			else if (currentEvent is SoundEvent)
			{
				SoundEvent soundEvent = currentEvent as SoundEvent;
				NoteEvent noteEvent = soundEvent as NoteEvent;
				KeySoundChangeEvent keySoundEvent = soundEvent as KeySoundChangeEvent;

				OnSoundObject(index, soundEvent);

				if (keySoundEvent != null)
					OnPlayerKeyChange(index, keySoundEvent);
				else if (noteEvent != null)
				{
					LandmineEvent landmineEvent = noteEvent as LandmineEvent;
					if (landmineEvent != null)
						OnLandmine(index, noteEvent);
					else if (noteEvent.isLongNote)
						OnPlayerKeyLong(index, noteEvent);
					else
						OnPlayerKey(index, noteEvent);
				}
				else
					OnBGM(index, soundEvent);
			}		
			else if (currentEvent is StopEvent)
				OnStop(index, currentEvent as StopEvent);
			else if (currentEvent is BGAEvent)
			{
				BGAEvent bgaEvent = currentEvent as BGAEvent;

				OnImageObject(index, bgaEvent);
				OnBGA(index, bgaEvent);
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

		public virtual void OnSoundObject(int eventIndex, SoundEvent value)
		{
			if (!playing)
				return;
		}

		public virtual void OnImageObject(int eventIndex, BGAEvent value)
		{
			if (!playing)
				return;
		}

		public virtual void OnMeasureLength(int eventIndex, MeasureLengthEvent value)
		{

		}

		public virtual void OnBPM(int eventIndex, BPMEvent value)
		{
			nextBpm = value.bpm;
		}

		public virtual void OnBGM(int eventIndex, SoundEvent value)
		{

		}

		public virtual void OnPlayerKey(int eventIndex, NoteEvent value)
		{

		}

		public virtual void OnPlayerKeyLong(int eventIndex, NoteEvent value)
		{
			
		}

		public virtual void OnLandmine(int eventIndex, NoteEvent value)
		{
			
		}

		public virtual void OnPlayerKeyChange(int eventIndex, KeySoundChangeEvent value)
		{

		}

		public virtual void OnStop(int eventIndex, StopEvent value)
		{
			stopPulse = value.stopPulse;
			stopLeft = (double)value.stopPulse / resolution * 60.0 / bpm;
		}

		public virtual void OnStopEnd(int eventIndex, StopEvent value)
		{

		}

		public virtual void OnBGA(int eventIndex, BGAEvent value)
		{

		}
	}
}