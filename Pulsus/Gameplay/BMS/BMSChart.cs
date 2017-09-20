using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;

namespace Pulsus.Gameplay
{
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
		public override int playLevel { get { return GetHeader<int>("PLAYLEVEL"); } }
		public override string previewFile { get { return GetHeader<string>("PREVIEW"); } }
		public override long resolution { get { return resolution_; } }

		public override int players { get; internal set; }
		public override int playerChannels { get; internal set; }
		public override bool hasTurntable { get; internal set; }
		public override int playerEventCount { get; internal set; }
		public override int noteCount { get; internal set; }
		public override int longNoteCount { get; internal set; }
		public override int landmineCount { get; internal set; }
		public override int measureCount { get; internal set; }
		public override double songLength { get; internal set; }

		private Dictionary<string, object> headerObjects = new Dictionary<string, object>()
		{
			// #HEADER, <default value>
			{ "ARTIST", "" },
			{ "TITLE", "" },
			{ "GENRE", "" },
			{ "RANK", 2 },
			{ "DEFEXRANK", 0.0 },	// multiplier applied to #RANK 2
			{ "TOTAL", -1.0 },
			{ "PLAYLEVEL", 0 },		// > 12 = insane scale?
			{ "VOLWAV", 100.0 },
			{ "VOLOGG", 100.0 },
			{ "LNTYPE", 1 },        // 1: RDM-notation, non-zero events marks start and end points
									// 2: MGQ-notation, continuous non-zero events marks the duration
			{ "PREVIEW", null },	// path to sound used for preview
		};

		public static HashSet<string> unsupportedObjects = new HashSet<string>()
		{
			"PLAYER",		// actual value is determined from channel data (1: SP, 2: Couple Play, 3: DP, 4: 2xSP)
			"BPM",			// parsed but ignored here, actual value is parsed to bpmObjects[0]
			"STAGEFILE",
			"DIFFICULTY",
			"SUBTITLE",
			"SUBARTIST",
			"BANNER",
			"COMMENT",
			"BACKBMP",
			"IF",
			"ENDIF",
			"RANDOM",
			"ENDRANDOM",
		};

		public Dictionary<int, SoundObject> soundObjects = new Dictionary<int, SoundObject>();
		public Dictionary<int, BGAObject> bgaObjects = new Dictionary<int, BGAObject>();
		public Dictionary<int, double> bpmObjects = new Dictionary<int, double>();
		public Dictionary<int, double> stopObjects = new Dictionary<int, double>();
		public HashSet<int> lnObjects = new HashSet<int>();

		internal int eventCount = 0;
		internal long resolution_ = 0;

		public BMSChart(string basePath)
			: base(basePath)
		{
		}

		public override void Dispose()
		{
			base.Dispose();

			foreach (BGAObject bga in bgaObjects.Values)
				bga.Dispose();

			soundObjects.Clear();
			bgaObjects.Clear();
		}

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

		public override void GenerateEvents()
		{
			eventList = new List<Event>(eventCount);

			int longNoteType = GetHeader<int>("LNTYPE");
			Dictionary<int, LongNoteEvent> lastLNEvent = new Dictionary<int, LongNoteEvent>();
			Dictionary<int, LongNoteEvent> startLNEvent = new Dictionary<int, LongNoteEvent>();
			Dictionary<int, NoteEvent> lastPlayerEvent = new Dictionary<int, NoteEvent>();

			long pulse = 0;
			double currentBpm = bpmObjects[0];
			double meter = 1.0;
		
			if (longNoteType == 2)
			{
				// insert empty measure at the end to close longnotes ending at last beat (#LNTYPE 2)

				int measureIndex = 0;
				if (measureList.Count > 0)
					measureIndex = measureList[measureList.Count - 1].index + 1;

				BMSMeasure measure = new BMSMeasure(measureIndex);
				measureList.Add(measure);
			}

			foreach (BMSMeasure measure in measureList)
			{
				List<Event> measureEvents = new List<Event>();

				// check if long notes should be ended at previous measure (#LNTYPE 2)
				if (longNoteType == 2 && lastLNEvent.Count > 0)
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
								break;
							}
						}

						if (breakNote)
						{
							// LN section ended at the last note in previous measure,
							// and does not continue in next measure, so restore the
							// last skipped event, and end the long note there.

							LongNoteEvent lastEvent = lastLNEvent[lnChannel];
							LongNoteEvent startEvent = startLNEvent[lnChannel];
							LongNoteEndEvent endEvent = new LongNoteEndEvent(pulse, lastEvent.sounds, lastEvent.lane, startEvent);
							startEvent.endNote = endEvent;

							eventList.Add(endEvent);

							lastLNEvent.Remove(lnChannel);
							startLNEvent.Remove(lnChannel);
						}
					}
				}

				// reset meter back to 1.0
				if (meter != measure.meter)
				{
					MeterEvent measureEvent = new MeterEvent(pulse, measure.meter);
					eventList.Add(measureEvent);
					timeEventList.Add(measureEvent);
				}
				meter = measure.meter;

				// mark measure position
				measureCount++;
				measurePositions.Add(new Tuple<int, long>(measure.index, pulse));
				MeasureMarkerEvent measureMarker = new MeasureMarkerEvent(pulse);
				eventList.Add(measureMarker);

				double nextBpm = 0.0;
				Dictionary<long, double> bpmExValues = new Dictionary<long, double>();
				foreach (BMSChannel channel in measure.channelList)
				{
					long channelPulse = pulse;
					long beatPulse = (resolution * 4) / channel.values.Count;
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
								bmsEvent = new LandmineEvent(channelPulse, sound, lane, damage);
							}
							else
							{
								NoteEvent noteEvent = null;
								bool isLnObj = lnObjects.Contains(value);
								if (isLongChannel || isLnObj)
								{
									LongNoteEvent longNoteEvent = new LongNoteEvent(channelPulse, sound, lane, null);
									if (longNoteType == 2)
									{
										// ignore section filler events with #LNTYPE 2
										// skip all the events except the first one, and restore the last
										// skipped event at the end of long note.

										if (lastLNEvent.ContainsKey(channel.index))
										{
											if (value != 0)
											{
												lastLNEvent[channel.index] = longNoteEvent;
												continue;
											}
											else
											{
												// long note breaks here,
												// replace current event with last skipped LN event.

												LongNoteEvent startEvent = startLNEvent[channel.index];
												LongNoteEndEvent endEvent = new LongNoteEndEvent(channelPulse, sound, lane, startEvent);
												startEvent.endNote = endEvent;

												lastLNEvent.Remove(channel.index);
												startLNEvent.Remove(channel.index);

												noteEvent = endEvent;
											}
										}
										else if (value != 0)
										{
											lastLNEvent.Add(channel.index, longNoteEvent);
											startLNEvent.Add(channel.index, longNoteEvent);
											noteEvent = longNoteEvent;
										}
									}
									else if (longNoteType == 1 && value != 0)
									{
										LongNoteEvent lastLongNote = null;

										if (isLnObj)
										{
											NoteEvent lastEvent = lastPlayerEvent[lane];
											if (lastEvent != null)
											{
												lastPlayerEvent[lane] = null;
												lastLongNote = new LongNoteEvent(lastEvent.pulse, lastEvent.sounds, lastEvent.lane, null);

												bool foundNote = false;
												for (int j = measureEvents.Count - 1; j >= 0; j--)
												{
													if (measureEvents[j] != lastEvent)
														continue;

													measureEvents[j] = lastLongNote;
													foundNote = true;
													break;
												}
												if (!foundNote)
												{
													for (int j = eventList.Count - 1; j >= 0; j--)
													{
														if (eventList[j] != lastEvent)
															continue;

														eventList[j] = lastLongNote;
														foundNote = true;
														break;
													}
												}

												if (!foundNote)
													throw new ApplicationException("Could not find long note starting point");
											}
											else
												noteEvent = new NoteEvent(channelPulse, sound, lane);
										}
										else
											lastLNEvent.TryGetValue(channel.index, out lastLongNote);

										if (lastLongNote != null)
										{
											LongNoteEvent startEvent = lastLongNote;
											LongNoteEndEvent endEvent = new LongNoteEndEvent(channelPulse, sound, lane, startEvent);
											startEvent.endNote = endEvent;

											lastLNEvent.Remove(channel.index);
											noteEvent = endEvent;
										}
										else if (isLongChannel)
										{
											lastLNEvent.Add(channel.index, longNoteEvent);
											noteEvent = longNoteEvent;
										}
									}
								}
								else
								{
									noteEvent = new NoteEvent(channelPulse, sound, lane);
									lastPlayerEvent[lane] = noteEvent;
								}

								bmsEvent = noteEvent;
							}
						}
						else if (channel.index == (int)BMSChannel.Type.BPM)
						{
							bmsEvent = new BPMEvent(channelPulse, value);
							nextBpm = value;
						}
						else if (channel.index == (int)BMSChannel.Type.BPMExtended)
						{
							double bpmValue = 0.0;
							if (bpmObjects.TryGetValue(value, out bpmValue) && bpmValue != 0.0)
							{
								bmsEvent = new BPMEvent(channelPulse, bpmValue);
								nextBpm = bpmValue;
								bpmExValues.Add(channelPulse, bpmValue);
							}
						}
						else if (channel.index == (int)BMSChannel.Type.Stop)
						{
							double stopValue = 0;
							stopObjects.TryGetValue(value, out stopValue);

							long stopTime = (long)(stopValue / 192.0 * resolution * 4);
							bmsEvent = new StopEvent(channelPulse, stopTime);
						}
						else if (channel.index == (int)BMSChannel.Type.BGA ||
							channel.index == (int)BMSChannel.Type.BGALayer ||
							channel.index == (int)BMSChannel.Type.BGAPoor)
						{
							BGAObject bga = null;
							bgaObjects.TryGetValue(value, out bga);

							BGAEvent.BGAType type = BGAEvent.BGAType.BGA;
							if (channel.index == (int)BMSChannel.Type.BGALayer)
								type = BGAEvent.BGAType.LayerTransparentBlack;
							else if (channel.index == (int)BMSChannel.Type.BGAPoor)
								type = BGAEvent.BGAType.Poor;

							bmsEvent = new BGAEvent(channelPulse, bga, type);
						}
						else
							Log.Warning("Unsupported BMS channel: " + channel.index.ToString("X2"));

						if (bmsEvent == null)
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

				foreach (Event bmsEvent in measureEvents)
				{
					NoteEvent noteEvent = bmsEvent as NoteEvent;
					if (noteEvent != null)
					{
						playerEventCount++;

						if (noteEvent is LandmineEvent)
							landmineCount++;
						else
						{
							noteCount++;
							if (noteEvent is LongNoteEvent)
								longNoteCount++;
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
					else if (bmsEvent is StopEvent)
						timeEventList.Add(bmsEvent);

					eventList.Add(bmsEvent);
				}
			}

			GenerateTimestamps();

			if (eventList.Count > 0)
				songLength = eventList[eventList.Count - 1].timestamp;
		}
	}
}