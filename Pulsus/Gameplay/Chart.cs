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

		public long resolution = 240;

		public abstract List<Event> GenerateEvents(bool seekable = false);

		public int players = 0;
		public int playerChannels = 0;
		public int firstPlayerEvent = -1;
		public int playerEventCount = 0;
		public int noteCount = 0;
		public int longNoteCount = 0;
		public int landmineCount = 0;
		public int measureCount = 0;
		public double songLength = 0.0;

		public List<Event> eventList;
		public List<Event> timeEventList = new List<Event>();
		public List<BMSMeasure> measureList = new List<BMSMeasure>();
		public List<Tuple<int, long>> measurePositions = new List<Tuple<int, long>>();

		public Dictionary<int, SoundObject> soundObjects = new Dictionary<int, SoundObject>();
		public Dictionary<int, BGAObject> bgaObjects = new Dictionary<int, BGAObject>();
		public Dictionary<int, double> bpmObjects = new Dictionary<int, double>();
		public Dictionary<int, double> stopObjects = new Dictionary<int, double>();
		public HashSet<int> lnObjects = new HashSet<int>();

		public bool hasTurntable { get { return playerChannels == 6 || playerChannels == 8; } }
		public int playerKeyCount { get { return (playerChannels - (hasTurntable ? 1 : 0)) * players; } }

		public void Dispose()
		{
			foreach (BGAObject bga in bgaObjects.Values)
				bga.Dispose();

			soundObjects.Clear();
			bgaObjects.Clear();
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
	}
}
