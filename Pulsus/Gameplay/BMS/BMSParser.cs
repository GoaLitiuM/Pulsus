using Pulsus.Audio;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System;

namespace Pulsus.Gameplay
{
	public class BMSParser : ChartParser
	{
		// compatibility settings

		// song starts from first defined measure, skipping undefined measure 0
		const bool skipToFirstMeasure = false;

		Dictionary<int, int> channelRefs = new Dictionary<int, int>();
		string basePath;

		public override Chart Load(string path)
		{
			basePath = Directory.GetParent(path).FullName;
			BMSChart data = new BMSChart(basePath);
			BMSMeasure lastMeasure = null;

			bool warnRandom = false;

			using (FileStream fileStream = new FileStream(path, FileMode.Open,
				FileAccess.Read, FileShare.Read, 8192, FileOptions.SequentialScan))
			{
				using (StreamReader stream = new StreamReader(fileStream, System.Text.Encoding.GetEncoding("shift_jis"), true))
				{
					string line;
					int lineNumber = -1;
					while ((line = stream.ReadLine()) != null)
					{
						lineNumber++;
						
						string command, value;
						if (!ParseCommandLine(line, out command, out value))
							continue;

						if (command.StartsWithFast("WAV"))
							OnWAV(data, command, value);
						else if (command.StartsWithFast("BMP"))
							OnBMP(data, command, value);
						else if (command.StartsWithFast("BPM") || command.StartsWithFast("EXBPM"))
							OnBPM(data, command, value);
						else if (command.StartsWithFast("STOP"))
							OnStop(data, command, value);
						else if (command.StartsWithFast("LNOBJ"))
							OnLongNote(data, command, value);
						else if (command == "RANDOM")
							warnRandom = true;
						else if (command[0] >= '0' && command[0] <= '9')
							OnChannel(data, line, ref lastMeasure);
						else if (BMSChart.unsupportedObjects.Contains(command))
							continue; // unsupported command, silently ignored
						else
							OnHeader(data, command, value);
					}
				}
			}

			if (warnRandom)
				Log.Warning("#RANDOM charts are not supported");

			data.measureList.Sort(new Comparison<BMSMeasure>((m1, m2) =>
			{
				return m1.index > m2.index ? 1 : (m1.index < m2.index ? -1 : 0);
			}));

			// insert empty measures at gaps
			for (int i = 0, skipped = 0; i < data.measureList.Count; i++)
			{
				if (data.measureList[i].index == i + skipped)
					continue;

				if (skipToFirstMeasure && skipped == 0)
				{
					skipped = data.measureList[i].index;
					continue;
				}

				BMSMeasure measure = new BMSMeasure(i + skipped);
				BMSChannel channel = new BMSChannel(1);

				channel.values.Add(0);
				measure.channelList.Add(channel);
				measure.maxLength = 1;

				data.measureList.Insert(i, measure);
			}

			// check against overlapping long and regular notes
			int longNoteType = data.GetHeader<int>("LNTYPE");
			for (int i = 0; i < data.measureList.Count; i++)
			{
				for (int c = (int)BMSChannel.Type.P1KeyFirst; c < (int)BMSChannel.Type.P2KeyLast; ++c)
				{
					BMSChannel keyChannel = data.measureList[i].GetChannel(c);
					if (keyChannel == null)
						continue;

					for (int cl = 0; cl < data.measureList[i].channelList.Count; cl++)
					{
						if (data.measureList[i].channelList[cl].index != c + (BMSChannel.Type.P1LongFirst - BMSChannel.Type.P1KeyFirst))
							continue;

						BMSChannel lnChannel = data.measureList[i].channelList[cl];
						if (keyChannel.values.Count != lnChannel.values.Count)
						{
							int newLength = Utility.lcm(keyChannel.values.Count, lnChannel.values.Count);
							bool lnWorkaround = longNoteType == 2;
							BMSChannel.NormalizeChannel(keyChannel, newLength, 0);
							BMSChannel.NormalizeChannel(lnChannel, newLength, lnWorkaround ? -1 : 0);
						}

						int lnValue = -1;
						for (int j = 0; j < keyChannel.values.Count; j++)
						{
							if (lnChannel.values[j] != -1)
								lnValue = lnChannel.values[j];
							else
								lnChannel.values[j] = lnValue;

							if (keyChannel.values[j] == 0 || lnValue == 0)
								continue;
							else if (lnValue != 0)
							{
								// Both channels have overlapping notes.
								// Charter probably added both for compatibility reasons
								// if the client doesn't support long notes.

								keyChannel.values[j] = 0;
							}
						}
					}
				}
			}

			if (longNoteType != 1)
				Log.Warning("#LNTYPE " + longNoteType.ToString());

			int p1KeyCount = 0;
			int p2KeyCount = 0;

			// merge long note references with regular keys
			List<int> channelKeys = channelRefs.Keys.ToList();
			foreach (int channelIndex in channelKeys)
			{
				if (BMSChannel.IsP1Long(channelIndex) || BMSChannel.IsP2Long(channelIndex))
				{
					int offset = BMSChannel.Type.P1LongFirst - BMSChannel.Type.P1KeyFirst;
					int newChannel = channelIndex - offset;
					int refs = channelRefs[channelIndex];

					if (channelRefs.ContainsKey(newChannel))
						channelRefs[newChannel] += refs;
					else
						channelRefs.Add(newChannel, refs);
				}
			}

			foreach (var channel in channelRefs)
			{
				if (BMSChannel.IsP1Key(channel.Key))
					p1KeyCount++;
				else if (BMSChannel.IsP2Key(channel.Key))
					p2KeyCount++;
			}

			data.playerChannels = Math.Max(p1KeyCount, p2KeyCount);
			if (data.playerChannels != 6 && data.playerChannels != 8 && data.playerChannels != 9)
			{
				// non-standard format or some channels were left empty,
				// fallback to using specific key count based on the file extension.
				int keyCount = 0;
				string extension = Path.GetExtension(path).ToLowerInvariant();
				if (extension == ".bms")
					keyCount = 6;
				else if (extension == ".bme" || extension == ".bml")
					keyCount = 8;
				else if (extension == ".pms")
				{
					keyCount = 9;

					if (p1KeyCount <= 5 && p2KeyCount <= 4)
					{
						// actually one player chart
						p2KeyCount = 0;
					}
				}

				if (keyCount != 0)
				{
					if (p1KeyCount > 0)
						p1KeyCount = keyCount;
					if (p2KeyCount > 0)
						p2KeyCount = keyCount;
				}
			}

			data.playerChannels = Math.Max(p1KeyCount, p2KeyCount);
			data.hasTurntable = data.playerChannels == 6 || data.playerChannels == 8;

			if (p2KeyCount > 0)
				data.players = 2;
			else if (p1KeyCount > 0)
				data.players = 1;

			// find the most optimal resolution for this chart
			long resolution = 1;
			const long maxResolution = long.MaxValue / (1000 * 4);
			try
			{
				foreach (BMSMeasure measure in data.measureList)
				{
					foreach (BMSChannel channel in measure.channelList)
					{
						if (resolution % channel.values.Count != 0)
							resolution = Utility.lcm(resolution, channel.values.Count);
					}
				}
			}
			catch (ArithmeticException)
			{
				resolution = 0;
			}
			finally
			{
				if (resolution <= 0 || resolution > maxResolution)
				{
					resolution = maxResolution;
					Log.Warning("Required object resolution is too high for accurate playback");
				}
			}

			data.resolution_ = resolution;

			return data;
		}

		/// <summary> Parses command and value from command line </summary>
		/// <param name="command"> Command without '#' character and converted to uppercase letters </param>
		/// <param name="value"> Value </param>
		/// <returns> Returns true if command was succesfully parsed from command line </returns>
		public static bool ParseCommandLine(string line, out string command, out string value)
		{
			int pos = 0;
			command = null;
			value = null;

			// find command start position
			for (; pos < line.Length; pos++)
			{
				char c = line[pos];
				if (c == ' ' || c == '\t')
					continue;

				if (c != '#')
					return false;

				pos++;
				break;
			}

			// find command end position
			int commandStartPos = pos;
			char[] commandStr = new char[line.Length - commandStartPos];
			for (; pos < line.Length; pos++)
			{
				char c = line[pos];
				if (c == ' ' || c == '\t')
					break;

				if (c >= 'a' && c <= 'z')
					commandStr[pos - commandStartPos] = (char)(c + 'A' - 'a');
				else
					commandStr[pos - commandStartPos] = c;
			}

			if (pos <= commandStartPos)
				return false;

			command = new string(commandStr, 0, pos - commandStartPos);


			// find value start position
			for (; pos < line.Length; pos++)
			{
				char c = line[pos];
				if (c == ' ' || c == '\t')
					continue;

				break;
			}


			// find value end position and trim trailing whitespace
			int valueStart = pos;
			for (pos = line.Length - 1; pos >= valueStart; pos--)
			{
				char c = line[pos];
				if (c == ' ' || c == '\t')
					continue;

				pos++;
				break;
			}

			if (pos >= valueStart)
				value = line.Substring(valueStart, pos - valueStart);

			return true;
		}

		/// <summary> Parses channel measure index, channel index, and channel values from channel line </summary>
		/// <param name="measure"> Channel measure index (0-999) </param>
		/// <param name="value"> Channel index </param>
		/// <returns> Returns true if parsing succeeds </returns>
		public static bool ParseChannelLine(string line, out int measure, out int channel, out string value)
		{
			int i = 0;
			measure = 0;
			channel = 0;
			value = null;

			// find command start position
			for (; i < line.Length; i++)
			{
				char c = line[i];
				if (c == ' ' || c == '\t')
					continue;

				if (c != '#')
					return false;

				i++;
				break;
			}

			if (!int.TryParse(line.Substring(i, 3), NumberStyles.Integer,
				CultureInfo.InvariantCulture, out measure))
			{
				return false;
			}

			string channelStr = line.Substring(i + 3, 2);
			if (!int.TryParse(channelStr, NumberStyles.Integer,
				CultureInfo.InvariantCulture, out channel))
			{
				if (!int.TryParse(channelStr, NumberStyles.HexNumber,
					CultureInfo.InvariantCulture, out channel))
				{
					return false;
				}
			}

			i += 5;
			if (line[i] != ':')
				return false;

			// read channel values, ignoring all whitespace
			int start = i++;
			int end = 0;
			char[] str = new char[line.Length - start - 1];
			int strLength = 0;
			for (; i < line.Length; i++)
			{
				char c = line[i];
				if (c == ' ' || c == '\t')
					continue;

				if (c == ';' || (c == '/' && i + 1 < line.Length && line[i + 1] == '/'))
				{
					// comment starting with '//' or ';'
					end = i;
					break;
				}

				str[strLength++] = c;
			}
			if (end == 0)
				end = i;

			if (end < line.Length)
			{
				for (i = end; i >= start; i--)
				{
					char c = line[i];
					if (c == ' ' || c == '\t')
					{
						strLength--;
						continue;
					}

					i++;
					break;
				}
			}

			value = new string(str, 0, strLength);

			return true;
		}

		private void OnChannel(BMSChart data, string line, ref BMSMeasure lastMeasure)
		{
			// channel sentences
			// # <measure:3> <channel:2> : <object data>
			// for object data, 00 = musical rest

			if (headerOnly)
				return;

			int measureIndex;
			int channelIndex;
			string channelValue;
			if (!ParseChannelLine(line, out measureIndex, out channelIndex, out channelValue))
			{
				Log.Error("Invalid channel line");
				return;
			}

			if (channelRefs.ContainsKey(channelIndex))
				channelRefs[channelIndex]++;
			else
				channelRefs.Add(channelIndex, 1);

			if (!BMSChannel.IsSupported(channelIndex))
			{
				Log.Error("Unsupported channel type " + line.Substring(4, 2));
				return;
			}

			bool isSoundChannel = BMSChannel.IsSound(channelIndex);
			bool isPlayerChannel = BMSChannel.IsPlayer(channelIndex);

			BMSMeasure measure = null;
			if (lastMeasure != null && lastMeasure.index == measureIndex)
				measure = lastMeasure;
			else
			{
				measure = data.GetMeasure(measureIndex);
				if (measure == null)
				{
					measure = new BMSMeasure(measureIndex);
					data.measureList.Add(measure);
				}
			}
			lastMeasure = measure;

			int channelLength = 1;
			if (channelIndex == (int)BMSChannel.Type.Meter)
			{
				double meter = 1.0;
				if (!double.TryParse(channelValue,
					NumberStyles.Float, CultureInfo.InvariantCulture, out meter))
				{
					channelValue = channelValue.Replace(",", ".");
					if (!double.TryParse(channelValue,
					NumberStyles.Float, CultureInfo.InvariantCulture, out meter))
					{
						Log.Error("Unable to parse meter value: " + channelValue.ToString());
					}
				}
				measure.meter = meter;
			}
			else
			{
				channelLength = channelValue.Length / 2;
				if (channelValue.Length % 2 != 0)
					Log.Warning("Channel data length not divisible by 2");

				List<int> objects = new List<int>(channelLength);
				for (int i = 0; i < channelValue.Length; i += 2)
				{
					int value;
					if (channelIndex == (int)BMSChannel.Type.BPM)
					{
						string bpmStr = channelValue.Substring(i, 2);
						if (!int.TryParse(bpmStr, NumberStyles.HexNumber,
							CultureInfo.InvariantCulture, out value))
						{
							Log.Warning("Invalid BPM value in channel line: " + bpmStr);
							channelLength = objects.Count;
							break;
						}
					}
					else
					{
						if (!Utility.TryFromBase36(channelValue[i], channelValue[i + 1], out value))
						{
							Log.Warning("Invalid value in channel line: " + channelValue.Substring(i, 2));
							channelLength = objects.Count;
							break;
						}
					}

					objects.Add(value);

					if (value != 0)
						data.eventCount++;
				}

				// new channel data is applied to existing channel data (excluding BGM channel)
				if (channelIndex != (int)BMSChannel.Type.BGM && !BMSChannel.IsLong(channelIndex) &&
					measure.HasChannel(channelIndex))
				{
					int longNoteType = data.GetHeader<int>("LNTYPE");

					foreach (BMSChannel channel in measure.channelList)
					{
						if (channel.index != channelIndex)
							continue;

						// when merging data with LNTYPE 2, section filler must not be
						// filled with 0's in order to not close the long notes at wrong times.
						bool lnWorkaround = longNoteType == 2 && BMSChannel.IsLong(channel.index);
						BMSChannel.MergeChannels(channel, objects, lnWorkaround);

						channelLength = channel.values.Count;
						break;
					}
				}
				else if (objects.Count > 0)
					measure.Add(channelIndex, objects);
			}

			if (channelLength > measure.maxLength)
				measure.maxLength = channelLength;
		}

		private void OnWAV(BMSChart data, string command, string value)
		{
			if (headerOnly)
				return;

			string indexStr = command.Substring(3);
			int index = Utility.FromBase36(indexStr);

			data.soundObjects[index] = new SoundObject(new SoundFile(Path.Combine(basePath, value)), 1, indexStr);
		}

		private void OnBMP(BMSChart data, string command, string value)
		{
			if (headerOnly)
				return;

			string indexStr = command.Substring(3);
			int index = Utility.FromBase36(indexStr);

			data.bgaObjects[index] = new BGAObject(Path.Combine(basePath, value), indexStr);
		}

		private void OnBPM(BMSChart data, string command, string value)
		{
			int index = 0;
			if (command != "BPM")
				index = Utility.FromBase36(command.Substring(command.Length-2));

			double bpmValue = 0.0;

			if (!double.TryParse(value,
					NumberStyles.Number | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out bpmValue))
			{
				value = value.Replace(",", ".");
				if (!double.TryParse(value,
				NumberStyles.Number | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out bpmValue))
				{
					Log.Error("Unable to parse BPM value: " + value.ToString());
					return;
				}
			}

			data.bpmObjects[index] = bpmValue;
		}

		private void OnStop(BMSChart data, string command, string value)
		{
			if (headerOnly)
				return;

			string indexStr = command.Substring(4);
			int index = Utility.FromBase36(indexStr);

			double stopValue = double.Parse(value);

			data.stopObjects[index] = stopValue;
		}

		private void OnLongNote(BMSChart data, string command, string value)
		{
			if (headerOnly)
				return;

			if (value == null)
				value = command.Substring(5);

			int index = Utility.FromBase36(value);

			data.lnObjects.Add(index);
		}

		private void OnHeader(BMSChart data, string command, string value)
		{
			if (!data.HeaderDefined(command))
				Log.Error("Unsupported header command: " + command);

			int valueInt = 0;
			double valueDouble = 0.0;

			if (data.HasHeader(command))
			{
				if (data.HasHeader<bool>(command) && value == null)
					data.SetHeader<bool>(command, true);
				else if (data.HasHeader<int>(command) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out valueInt))
					data.SetHeader<int>(command, valueInt);
				else if (data.HasHeader<double>(command) && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out valueDouble))
					data.SetHeader<double>(command, valueDouble);
				else
					data.SetHeader<string>(command, value);
			}
			else
			{
				if (value == null)
					data.SetHeader<bool>(command, true);
				else if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out valueInt))
					data.SetHeader<int>(command, valueInt);
				else if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out valueDouble))
					data.SetHeader<double>(command, valueDouble);
				else
					data.SetHeader<string>(command, value);
			}
		}
	}
}