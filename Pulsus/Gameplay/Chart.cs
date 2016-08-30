using System;
using System.Collections.Generic;

namespace Pulsus.Gameplay
{
	public abstract class Chart : IDisposable
	{
		public abstract string artist { get; }
		public abstract string title { get; }
		public abstract string genre { get; }
		public abstract double bpm { get; }
		public abstract int rank { get; }
		public abstract double rankMultiplier { get; }
		public abstract double gaugeTotal { get; }
		public abstract double gaugeMultiplier { get; }
		public abstract double volume { get; }
		public abstract int playLevel { get; }
		public abstract string previewFile { get; }
		public abstract long resolution { get; }

		public abstract int players { get; internal set; }
		public abstract int playerChannels { get; internal set; }
		public abstract bool hasTurntable { get; internal set; }	
		public abstract int playerEventCount { get; internal set; }
		public abstract int noteCount { get; internal set; }
		public abstract int longNoteCount { get; internal set; }
		public abstract int landmineCount { get; internal set; }
		public abstract int measureCount { get; internal set; }
		public abstract double songLength { get; internal set; }

		public abstract List<Event> GenerateEvents(bool seekable = false);

		public int playerKeyCount { get { return (playerChannels - (hasTurntable ? 1 : 0)) * players; } }

		public List<Event> eventList;
		public List<Event> timeEventList = new List<Event>();
		public List<BMSMeasure> measureList = new List<BMSMeasure>();
		public List<Tuple<int, long>> measurePositions = new List<Tuple<int, long>>();

		public virtual void Dispose()
		{
		}

		public double GetTimeFromPulse(long pulse)
		{
			double timestamp = 0.0;
			double currentBpm = bpm;
			double currentMeter = 1.0;
			long lastPulse = 0;
			foreach (Event timeEvent in timeEventList)
			{
				if (timeEvent.pulse >= pulse)
					break;

				timestamp = timeEvent.timestamp;
				lastPulse = timeEvent.pulse;

				if (timeEvent is BPMEvent)
				{
					currentBpm = (timeEvent as BPMEvent).bpm;
					if (currentBpm < 0.0)
						currentBpm = -currentBpm;
				}
				else if (timeEvent is StopEvent)
				{
					/*double stopPulses = (timeEvent as StopEvent).stopPulse;
					double stopTime = stopPulses / resolution * 60.0 / currentBpm;
					timestamp += stopTime;*/
				}
				else if (timeEvent is MeterEvent)
					currentMeter = (timeEvent as MeterEvent).meter;
			}

			timestamp += (double)(pulse-lastPulse) / resolution * 60.0 / (currentBpm / currentMeter);
			return timestamp;
		}

		public long GetPulseFromTime(double time)
		{
			double timestamp = 0.0;
			double currentBpm = bpm;
			double currentMeter = 1.0;
			long lastPulse = 0;
			foreach (Event timeEvent in timeEventList)
			{
				if (timeEvent.timestamp >= time)
					break;

				timestamp = timeEvent.timestamp;
				lastPulse = timeEvent.pulse;

				if (timeEvent is BPMEvent)
				{
					currentBpm = (timeEvent as BPMEvent).bpm;
					if (currentBpm < 0.0)
						currentBpm = -currentBpm;
				}
				else if (timeEvent is StopEvent)
				{
					double stopPulses = (timeEvent as StopEvent).stopTime;
					double stopTime = stopPulses / resolution * 60.0 / currentBpm;
					timestamp = Math.Min(timestamp + stopTime, time);
				}
				else if (timeEvent is MeterEvent)
					currentMeter = (timeEvent as MeterEvent).meter;
			}
			double remaining = time - timestamp;
			double increment = (remaining * (currentBpm / currentMeter) / 60.0 * resolution);
			lastPulse += (long)increment;
			return lastPulse;
		}

		protected void GenerateTimestamps()
		{
			double lastTimeEventTime = 0.0;
			double lastBpm = bpm;
			double lastMeter = 1.0;
			long lastPulse = 0;
			int lastTimeEventIndex = 0;

			for (int i = 0; i < eventList.Count; i++)
			{
				Event bmsEvent = eventList[i];

				// generate event timestamps

				for (; lastTimeEventIndex < timeEventList.Count; lastTimeEventIndex++)
				{
					Event timeEvent = timeEventList[lastTimeEventIndex];
					if (timeEvent.pulse >= bmsEvent.pulse)
						break;

					double increment = (double)(timeEvent.pulse - lastPulse) / resolution * 60.0 / (lastBpm / lastMeter);

					lastTimeEventTime += increment;
					lastPulse = timeEvent.pulse;

					if (timeEvent is BPMEvent)
					{
						lastBpm = (timeEvent as BPMEvent).bpm;
						if (lastBpm < 0.0)
							lastBpm = -lastBpm;
					}
					else if (timeEvent is StopEvent)
					{
						double stopPulses = (timeEvent as StopEvent).stopTime;
						double stopTime = stopPulses / resolution * 60.0 / lastBpm;
						lastTimeEventTime += stopTime;
					}
					else if (timeEvent is MeterEvent)
						lastMeter = (timeEvent as MeterEvent).meter;
				}

				bmsEvent.timestamp = lastTimeEventTime + (double)(bmsEvent.pulse - lastPulse) / resolution * 60.0 / (lastBpm / lastMeter);

				// sanity check for long notes
				LongNoteEvent longNoteEvent = bmsEvent as LongNoteEvent;
				if (longNoteEvent != null)
				{
					if (longNoteEvent.endNote == null)
					{
						Log.Warning("Longnote is missing end point");

						// turn longnote into regular note
						longNoteCount--;
						eventList[i] = new NoteEvent(longNoteEvent.pulse, longNoteEvent.sound, longNoteEvent.lane);
						eventList[i].timestamp = bmsEvent.timestamp;
					}
					else if (longNoteEvent.length <= 0)
						Log.Warning("Invalid longnote length");
				}
			}
		}
	}
}
