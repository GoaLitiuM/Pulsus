using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using Pulsus;
using Pulsus.Gameplay;

public class BMSChart : Chart
{
	public override string artist { get { return GetHeader<string>("ARTIST"); } }
	public override string title { get { return GetHeader<string>("TITLE"); } }
	public override string genre { get { return GetHeader<string>("GENRE"); } }
	public override double bpm { get { return bpmObjects[0]; } }
	public override int rank { get { return GetHeader<int>("RANK"); } }
	public override double rankMultiplier { get { return GetHeader<double>("DEFEXRANK"); } }
	public override double gaugeTotal { get { return GetHeader<double>("TOTAL"); } }
	public override double gaugeMultiplier { get { return 1.0; } }
	public override double volume { get { return GetHeader<double>("VOLWAV") / 100.0; } }

	private Dictionary<string, object> headerObjects = new Dictionary<string, object>()
	{
		{ "ARTIST", "" },
		{ "TITLE", "" },
		{ "BPM", null },		// parsed but ignored here, refers to bpmObjects[0]
		{ "GENRE", "" },
		{ "RANK", 2 },
		{ "DEFEXRANK", 0.0 },	// multiplier applied to #RANK 2
		{ "TOTAL", -1.0 },
		{ "PLAYER", 0 },		// parsed but mostly ignored: 1 SP, 2 Couple Play, 3 DP, 4 2xSP
		{ "PLAYLEVEL", null },	// > 12 = insane scale?
		{ "VOLWAV", 100.0 },
		{ "VOLOGG", null },
		{ "LNTYPE", 1 },        // 1: RDM-notation, non-zero events marks start and end points
								// 2: MGQ-notation, continuous non-zero events marks the duration

		// unsupported, no warnings
		{ "STAGEFILE", "" },
		{ "DIFFICULTY", 1 },
		{ "SUBTITLE", "" },
		{ "SUBARTIST", "" },
		{ "BANNER", "" },
		{ "COMMENT", "" },
		{ "BACKBMP", "" },
	};

	public int eventCount = 0;
	
	public string filePath;

	public T GetHeader<T>(string header)
	{
		object obj = headerObjects[header];
		if (typeof(T) == typeof(int) && obj is string)
		{
			int value = 0;
			if (int.TryParse((string)obj, out value))
				obj = value;
			else
				obj = 0;
		}
		else if (typeof(T) == typeof(double) && obj is string)
		{
			double value = 0;
			if (double.TryParse((string)obj,
				NumberStyles.Float, CultureInfo.InvariantCulture,
				out value))
			{
				obj = value;
			}
			else
				obj = 0.0;
		}
		else if (typeof(T) == typeof(double) && obj is int)
		{
			obj = (double)((int)obj);
		}

		return (T)obj;
	}

	public void SetHeader<T>(string header, T value)
	{
		headerObjects[header] = value;
	}

	public bool HasHeader<T>(string header)
	{
		return HasHeader(header) && headerObjects[header] is T;
	}

	public bool HasHeader(string header)
	{
		return HeaderDefined(header) && headerObjects[header] != null;
	}

	public bool HeaderDefined(string header)
	{
		return headerObjects.ContainsKey(header);
	}

	public bool ContainsMeasure(int measureIndex)
	{
		return GetMeasure(measureIndex) != null;
	}

	public BMSMeasure GetMeasure(int measureIndex)
	{
		foreach (BMSMeasure measure in measureList)
			if (measure.index == measureIndex)
				return measure;

		return null;
	}

	public override List<Event> GenerateEvents(bool seekable = false)
	{
		List<Event> eventList = new List<Event>(eventCount);

		int longNoteType = GetHeader<int>("LNTYPE");
		Dictionary<int, NoteEvent> lastLNEvent = new Dictionary<int, NoteEvent>();
		Dictionary<int, NoteEvent> startLNEvent = new Dictionary<int, NoteEvent>();
		Dictionary<int, NoteEvent> lastPlayerEvent = new Dictionary<int, NoteEvent>();

		int pulse = 0;
		double currentBpm = bpmObjects[0];

		double measureLength = 1.0;

		if (longNoteType == 2)
		{
			int measureIndex = 0;
			if (measureList.Count > 0)
				measureIndex = measureList[measureList.Count-1].index + 1;

			BMSMeasure measure = new BMSMeasure(measureIndex);
			measureList.Add(measure);
		}

		foreach (BMSMeasure measure in measureList)
		{
			List<Event> measureEvents = new List<Event>();

			if (lastLNEvent.Count > 0 && longNoteType == 2)
			{
				List<int> keys = lastLNEvent.Keys.ToList();
				foreach (var lnChannel in keys)
				{
					bool breakNote = true;
					for (int i = 0; i < measure.channelList.Count; i++)
					{
						if (measure.channelList[i].index != lnChannel)
							continue;
						else if (measure.channelList[i].values.Count > 0 &&
							measure.channelList[i].values[0] != 0)
						{
							breakNote = false;
						}
					}

					if (breakNote)
					{
						// LN section ended at the last note in previous measure,
						// and does not continue in next measure, so restore the
						// last skipped event, and end the long note there.

						NoteEvent lastEvent = lastLNEvent[lnChannel];
						NoteEvent startEvent = startLNEvent[lnChannel];
						startEvent.length = pulse - startEvent.pulse;

						eventList.Add(new LongNoteEndEvent(pulse, lastEvent.sound, lastEvent.lane, 0, startEvent));

						lastLNEvent.Remove(lnChannel);
						startLNEvent.Remove(lnChannel);
					}
				}
			}

			// reset measure length back to 1.0
			if (measureLength != 1.0 && measure.measureLength == 1.0)
			{
				MeasureLengthEvent measureEvent = new MeasureLengthEvent(pulse, measure.measureLength);
				eventList.Add(measureEvent);
				timeEventList.Add(measureEvent);
			}
			measureLength = measure.measureLength;

			// mark measure position
			measurePositions.Add(new Tuple<int, int, int>(measure.index, eventList.Count, pulse));
			MeasureMarkerEvent measureMarker = new MeasureMarkerEvent(pulse);
			eventList.Add(measureMarker);


			int lcmBeats = measure.channelList.Count > 0 ? 1 : 0;
			for (int i = 0; i < measure.channelList.Count; i++)
				lcmBeats = Utility.lcm(measure.channelList[i].values.Count, lcmBeats);

			int stoppedValue = 0;
			double nextBpm = 0.0;
	
			Dictionary<int, double> bpmExValues = new Dictionary<int, double>();
			foreach (BMSChannel channel in measure.channelList)
			{
				int channelPulse = pulse;
				int beatPulse = (resolution * 4) / channel.values.Count;
				int lane = BMSChannel.GetLaneIndex(channel.index, playerChannels, players);
				bool isLongChannel = BMSChannel.IsLong(channel.index);

				for (int i = 0; i < channel.values.Count; i++, channelPulse += beatPulse)
				{
					int value = channel.values[i];
					if (value == 0 && !isLongChannel)
						continue;

					Event bmsEvent = null;
					if (BMSChannel.IsSound(channel.index))
					{
						SoundObject sound = null;
						soundObjects.TryGetValue(value, out sound);

						if (channel.index == (int)BMSChannel.Type.BGM)
							bmsEvent = new SoundEvent(channelPulse, sound);
						else if (BMSChannel.IsInvisible(channel.index))
							bmsEvent = new KeySoundChangeEvent(channelPulse, sound, lane);
						else if (BMSChannel.IsLandmine(channel.index))
						{
							// landmine does damage based on the object value itself,
							// and plays the hit sound from WAV00.

							int damage = value;
							soundObjects.TryGetValue(0, out sound);
							bmsEvent = new LandmineEvent(channelPulse, sound, lane, 0, damage);
						}
						else
						{
							NoteEvent noteEvent = new NoteEvent(channelPulse, sound, lane, 0);
							bool isLnObj = lnObjects.Contains(value);
							if (isLongChannel || isLnObj)
							{
								if (longNoteType == 2)
								{
									// ignore section filler events with #LNTYPE 2
									// skip all the events except the first one, and restore the last
									// skipped event at the end.

									if (lastLNEvent.ContainsKey(channel.index))
									{
										if (value != 0)
										{
											lastLNEvent[channel.index] = noteEvent;
											continue;
										}
										else
										{
											// long note breaks here,
											// replace current event with last skipped LN event.

											NoteEvent lastEvent = lastLNEvent[channel.index];
											NoteEvent startEvent = startLNEvent[channel.index];
											startEvent.length = channelPulse - startEvent.pulse;

											lastLNEvent.Remove(channel.index);
											startLNEvent.Remove(channel.index);

											noteEvent = new LongNoteEndEvent(channelPulse, sound, lane, 0, startEvent);
										}
									}
									else if (value != 0)
									{
										lastLNEvent.Add(channel.index, noteEvent);
										startLNEvent.Add(channel.index, noteEvent);
									}
								}
								else if (longNoteType == 1 && value != 0)
								{
									NoteEvent lastEvent = null;
									
									if (isLnObj)
										lastEvent = lastPlayerEvent[lane];
									else
										lastLNEvent.TryGetValue(channel.index, out lastEvent);

									if (lastEvent != null)
									{
										NoteEvent startEvent = lastEvent;
										startEvent.length = channelPulse - startEvent.pulse;

										lastLNEvent.Remove(channel.index);

										SoundObject releaseSound = null;
										if (startEvent.sound != sound && sound != null)
										{
											// play sound on release
											releaseSound = sound;
										}

										noteEvent = new LongNoteEndEvent(channelPulse, releaseSound, lane, 0, startEvent);
									}
									else if (isLongChannel)
										lastLNEvent.Add(channel.index, noteEvent);
								}
							}

							lastPlayerEvent[lane] = noteEvent;
							bmsEvent = noteEvent;
						}
					}
					else if (channel.index == (int)BMSChannel.Type.MeasureLength)
						bmsEvent = new MeasureLengthEvent(channelPulse, measureLengthObjects[value]);
					else if (channel.index == (int)BMSChannel.Type.BPM)
					{
						bmsEvent = new BPMEvent(channelPulse, value);
						if (value != 0)
							nextBpm = value;
					}
					else if (channel.index == (int)BMSChannel.Type.BPMExtended)
					{
						double bpmValue = 0.0;
						if (value != 0 && bpmObjects.TryGetValue(value, out bpmValue))
						{
							bmsEvent = new BPMEvent(channelPulse, bpmValue);
							if (bpmValue != 0.0)
							{
								nextBpm = bpmValue;
								bpmExValues.Add(channelPulse, bpmValue);
							}
						}
					}
					else if (channel.index == (int)BMSChannel.Type.Stop)
					{
						int stopValue = 0;
						stopObjects.TryGetValue(value, out stopValue);
						bmsEvent = new StopEvent(channelPulse, (int)(stopValue / 192.0 * resolution * 4));

						stoppedValue = stopValue;
					}
					else if (channel.index == (int)BMSChannel.Type.BGA ||
						channel.index == (int)BMSChannel.Type.BGALayer ||
						channel.index == (int)BMSChannel.Type.BGAPoor)
					{
						BGAObject bga = null;
						bgaObjects.TryGetValue(value, out bga);

						BGAEvent.BGAType type = BGAEvent.BGAType.BGA;
						if (channel.index == (int)BMSChannel.Type.BGALayer)
							type = BGAEvent.BGAType.Layer1;
						else if (channel.index == (int)BMSChannel.Type.BGAPoor)
							type = BGAEvent.BGAType.Poor;

						bmsEvent = new BGAEvent(channelPulse, bga, type);
					}
					else
						Log.Warning("Unsupported BMS channel: " + channel.index.ToString("X2"));
					
					if (value == 0 || bmsEvent == null)
						continue;

					measureEvents.Add(bmsEvent);
				}
			}

			pulse += resolution * 4;

			measureEvents.Sort(new Comparison<Event>((e1, e2) =>
			{
				return e1.pulse > e2.pulse ? 1 : (e1.pulse < e2.pulse ? -1 : 
					e1 is StopEvent ? 1 : (e2 is StopEvent ? -1 : 
					e1 is BPMEvent ? 1 : (e2 is BPMEvent ? -1 : 0)));
			}));

			foreach (var eventIter in measureEvents)
			{
				Event bmsEvent = eventIter;

				if (bmsEvent is NoteEvent)
				{
					if (firstPlayerEvent == -1)
						firstPlayerEvent = eventList.Count;

					if (!(bmsEvent is KeySoundChangeEvent))
					{
						NoteEvent noteEvent = bmsEvent as NoteEvent;
						int lane = noteEvent.lane;

						playerEventCount++;

						if (bmsEvent is LandmineEvent)
							landmineCount++;
						else
						{
							noteCount++;
							if (noteEvent.isLongNote)
								longNoteCount++;
						}
					}
				}
				else if (bmsEvent is BPMEvent)
				{
					// on overlap, prefer extended BPM (xxx08) changes over basic (xxx03) values
					double otherBpm;
					if (bpmExValues.TryGetValue(bmsEvent.pulse, out otherBpm))
					{
						if (otherBpm != (bmsEvent as BPMEvent).bpm)
							continue;
					}

					timeEventList.Add(bmsEvent);
				}
				else if (bmsEvent is StopEvent || bmsEvent is MeasureLengthEvent)
					timeEventList.Add(bmsEvent);

				eventList.Add(bmsEvent);
			}
		}

		// generate event timestamps
		foreach (Event ev in eventList)
			ev.timestamp = GetEventTimestamp(ev);
		
		if (eventList.Count > 0)
			songLength = eventList[eventList.Count-1].timestamp;

		return eventList;
	}
}
