using Pulsus.Audio;
using System.Collections.Generic;
using System.IO;
using System;

namespace Pulsus.Gameplay
{
	public class BMSONChart : Chart
	{
		public override string artist { get { return bmson.info.artist; } }
		public override string title { get { return bmson.info.title; } }
		public override string genre { get { return bmson.info.genre; } }
		public override double bpm { get { return bmson.info.init_bpm; } }
		public override int rank { get { return rankLegacy; } }
		public override double rankMultiplier { get { return rankMultiplierReal; } }
		public override double gaugeTotal { get { return 0.0; } }
		public override double gaugeMultiplier { get { return bmson.info.total / 100.0; } }
		public override double volume { get { return 1.0; } }
		public override int playLevel { get { return (int)bmson.info.level; } }
		public override string previewFile { get { return bmson.info.preview_music; } }
		public override long resolution { get { return (long)bmson.info.resolution; } }

		public override int players { get; internal set; }
		public override int playerChannels { get; internal set; }
		public override bool hasTurntable { get; internal set; }
		public override int playerEventCount { get; internal set; }
		public override int noteCount { get; internal set; }
		public override int longNoteCount { get; internal set; }
		public override int landmineCount { get; internal set; }
		public override int measureCount { get; internal set; }
		public override double songLength { get; internal set; }

		private BMSONHeader bmson;
		private string basePath;

		private int rankLegacy = 2;
		private double rankMultiplierReal = 1.0;

		public BMSONChart(string basePath, BMSONHeader bmson)
		{
			this.basePath = basePath;
			this.bmson = bmson;
		}

		public override List<Event> GenerateEvents(bool seekable = false)
		{
			if (!(this.bmson is BMSON))
				throw new ApplicationException("Can not generate events from partial BMSON object");

			BMSON bmson = (BMSON)this.bmson;
			eventList = new List<Event>();

			// some charts use judge_rank the same way as #RANK in BMS charts
			// which is wrong, as the judge_rank is supposed to be a multiplier in percents.
			if (bmson.info.judge_rank <= 4)
				rankLegacy = (int)bmson.info.judge_rank;
			else
				rankMultiplierReal = bmson.info.judge_rank / 100.0;

			// collect all time related events into one collection
			timeEventList = new List<Event>(bmson.bpm_events.Length + bmson.stop_events.Length);

			foreach (BMSON.BMSONBpmEvent bpmEvent in bmson.bpm_events)
				timeEventList.Add(new BPMEvent((long)bpmEvent.y, bpmEvent.bpm));
			
			foreach (BMSON.BMSONStopEvent stopEvent in bmson.stop_events)
				timeEventList.Add(new StopEvent((long)stopEvent.y, (long)stopEvent.duration));

			timeEventList.Sort(new Comparison<Event>((e1, e2) =>
			{
				return e1.pulse > e2.pulse ? 1 : (e1.pulse < e2.pulse ? -1 :
					e1 is StopEvent ? 1 : (e2 is StopEvent ? -1 :
					e1 is BPMEvent ? 1 : (e2 is BPMEvent ? -1 : 0)));
			}));

			eventList.AddRange(timeEventList);

			// measure markers
			foreach (BMSON.BarLine line in bmson.lines)
			{
				long pulse = (long)line.y;
				measurePositions.Add(new Tuple<int, long>(measureCount++, pulse));
				eventList.Add(new MeasureMarkerEvent(pulse));
			}

			// parse mode hints

			players = 1;
			string modeHint = bmson.info.mode_hint;
			switch (modeHint)
			{
				case "beat-5k":
					hasTurntable = true;
					playerChannels = 6;
					break;
				case "beat-7k":
					hasTurntable = true;
					playerChannels = 8;
					break;
				case "beat-10k":
					hasTurntable = true;
					players = 2;
					playerChannels = 6;		
					break;
				case "beat-14k":
					hasTurntable = true;
					players = 2;
					playerChannels = 8;
					break;
				
				case "popn-5k":
					playerChannels = 5;
					break;
				case "popn-9k":
					playerChannels = 9;
					break;
								
				default:
					int keys = 0;
					if (modeHint.StartsWithFastIgnoreCase("generic-") && int.TryParse(
						modeHint.Split(new string[] { "generic-", "keys", }, StringSplitOptions.None)[1], out keys)) 
					{
						// generic-nkeys
						throw new ApplicationException("Unsupported BMSON mode_hint generic-nkeys");
					}
					else
						throw new ApplicationException("Unsupported BMSON mode_hint: " + modeHint);
			}

			foreach (BMSON.SoundChannel channel in bmson.sound_channels)
			{
				double lastTimeEventTime = 0.0;
				double lastBpm = bpm;
				double lastMeter = 1.0;
				long lastPulse = 0;
				int lastTimeEventIndex = 0;

				SoundFile soundFile = new SoundFile(Path.Combine(basePath, channel.name));
				List<double> sliceTimestamps = new List<double>();
				int[] noteSlice = new int[channel.notes.Length];
				SoundObject[] slices;

				// TODO: sort notes?

				// generate timestamps for sound slices

				long lastSlicePulse = -1;
				for (int i=0; i<channel.notes.Length; i++)
				{
					BMSON.Note note = channel.notes[i];
					long notePulse = (long)note.y;
					if (notePulse != lastSlicePulse)
					{
						for (; lastTimeEventIndex < timeEventList.Count; lastTimeEventIndex++)
						{
							Event timeEvent = timeEventList[lastTimeEventIndex];
							if (timeEvent.pulse >= notePulse)
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

						double timestamp = lastTimeEventTime + (double)(notePulse - lastPulse) / resolution * 60.0 / (lastBpm / lastMeter);
						sliceTimestamps.Add(timestamp);
					}

					lastSlicePulse = notePulse;
					noteSlice[i] = sliceTimestamps.Count-1;
				}

				// create SoundObjects of sound slices

				slices = new SoundObject[sliceTimestamps.Count];
				double offset = 0.0;
				for (int i=0; i<sliceTimestamps.Count; i++)
				{
					double sliceStart = sliceTimestamps[i];
					double sliceEnd = i+1 < sliceTimestamps.Count ? sliceTimestamps[i+1] : 0.0;

					double length = sliceEnd - sliceStart;

					if (!channel.notes[i].c)
						offset = sliceStart;
					sliceStart -= offset;

					if (length <= 0.0)
						length = 0.0;

					slices[i] = new SoundObject(soundFile, 1, sliceStart, sliceStart + length, channel.name);
				}
					
				// generate note events

				for (int i=0; i<channel.notes.Length; i++)
				{
					BMSON.Note note = channel.notes[i];
					SoundObject soundObject = slices[noteSlice[i]];

					int lane = (int)note.x;
					long pulse = (long)note.y;
					long length = (long)note.l;

					if (lane == 0)
					{
						SoundEvent soundEvent = new SoundEvent(pulse, soundObject);
						eventList.Add(soundEvent);
					}
					else
					{
						playerEventCount++;
						noteCount++;

						if (hasTurntable && lane == 8)
							lane = 0;
						else if (hasTurntable && lane == 16)
							lane = playerChannels;
						else if (lane > 8)
							lane--;

						if (length == 0)
						{
							NoteEvent noteEvent = new NoteEvent(pulse, soundObject, lane);
							eventList.Add(noteEvent);
						}
						else
						{
							longNoteCount++;
							LongNoteEndEvent lnEndEvent = new LongNoteEndEvent(pulse + length, soundObject, lane, null);
							LongNoteEvent lnStartEvent = new LongNoteEvent(pulse, soundObject, lane, lnEndEvent);
							lnEndEvent.startNote = lnStartEvent;

							eventList.Add(lnStartEvent);
							eventList.Add(lnEndEvent);
						}
					}
				}
			}

			if (bmson.bga.bga_header != null)
			{
				// populate BGA objects
				Dictionary<uint, BGAObject> bgaObjects = new Dictionary<uint, BGAObject>(bmson.bga.bga_header.Length);
				foreach (BMSON.BGAHeader bgaHeader in bmson.bga.bga_header)
					bgaObjects[bgaHeader.id] = new BGAObject(bgaHeader.name, bgaHeader.name);

				// generate BGA events

				foreach (BMSON.BGAEvent bga in bmson.bga.bga_events)
				{
					long pulse = bga.y;
					BGAObject bgaObject = null;

					if (bgaObjects.TryGetValue(bga.id, out bgaObject))
						eventList.Add(new BGAEvent(pulse, bgaObject, BGAEvent.BGAType.BGA));
				}

				foreach (BMSON.BGAEvent bga in bmson.bga.layer_events)
				{
					long pulse = bga.y;
					BGAObject bgaObject = null;

					if (bgaObjects.TryGetValue(bga.id, out bgaObject))
						eventList.Add(new BGAEvent(pulse, bgaObject, BGAEvent.BGAType.Layer));
				}

				foreach (BMSON.BGAEvent bga in bmson.bga.poor_events)
				{
					long pulse = bga.y;
					BGAObject bgaObject = null;

					if (bgaObjects.TryGetValue(bga.id, out bgaObject))
						eventList.Add(new BGAEvent(pulse, bgaObject, BGAEvent.BGAType.Poor));
				}
			}

			eventList.Sort(new Comparison<Event>((e1, e2) =>
			{
				return e1.pulse > e2.pulse ? 1 : (e1.pulse < e2.pulse ? -1 :
					e1 is StopEvent ? 1 : (e2 is StopEvent ? -1 :
					e1 is BPMEvent ? 1 : (e2 is BPMEvent ? -1 : 0)));
			}));

			GenerateTimestamps();

			if (eventList.Count > 0)
				songLength = eventList[eventList.Count - 1].timestamp;

			return eventList;
		}
	}
}
