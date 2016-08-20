using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Pulsus;
using Pulsus.Gameplay;

public class BMSParser : ChartParser
{
	// compatibility settings

	// song starts from first defined measure, skipping undefined measure 0
	const bool skipToFirstMeasure = false;

	public override Chart LoadHeaders(string path)
	{
		BMSChart data = new BMSChart();

		using (FileStream fileStream = new FileStream(path, FileMode.Open,
			FileAccess.Read, FileShare.Read, 8192, FileOptions.SequentialScan))
		{
			using (StreamReader stream = new StreamReader(fileStream, System.Text.Encoding.GetEncoding("shift_jis"), true))
			{
				string line;
				while ((line = stream.ReadLine()) != null)
				{
					if (line.Length <= 1)
						continue;

					if (char.IsWhiteSpace(line[0]))
					{
						line = line.Trim();
						if (line.Length <= 1)
							continue;
					}

					if (line[0] != '#')
						continue;
					else if (line[1] >= '0' && line[1] <= '9')
						continue;
					else if (line.FastStartsWith("#WAV"))
						continue;
					else if (line.FastStartsWith("#BMP"))
						continue;
					else if (line.FastStartsWith("#STOP"))
						continue;
					else if (line.FastStartsWith("#LNOBJ"))
						continue;
					else if (line.FastStartsWith("#RANDOM"))
						continue;
					else if (line.FastStartsWith("#ENDRANDOM"))
						continue;
					else if (line.FastStartsWith("#IF"))
						continue;
					else if (line.FastStartsWith("#ENDIF"))
						continue;
					else if (line.FastStartsWith("#BPM"))
						OnBPM(data, line);
					else
						OnHeader(data, line);
				}
			}
		}

		return data;
	}

	Dictionary<int, int> channelRefs = new Dictionary<int, int>();

	public override Chart Load(string path)
	{
		BMSChart data = new BMSChart();
		BMSMeasure lastMeasure = null;

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

					if (line.Length <= 1)
						continue;

					if (char.IsWhiteSpace(line[0]))
					{
						line = line.Trim();
						if (line.Length <= 1)
							continue;
					}

					if (line[0] != '#')
						continue;

					if (line.FastStartsWith("#WAV"))
						OnWAV(data, line);
					else if (line.FastStartsWith("#BMP"))
						OnBMP(data, line);
					else if (line.FastStartsWith("#BPM"))
						OnBPM(data, line);
					else if (line.FastStartsWith("#STOP"))
						OnStop(data, line);
					else if (line.FastStartsWith("#LNOBJ"))
						OnLongNote(data, line);
					else if (line.FastStartsWith("#RANDOM"))
						throw new NotSupportedException("#RANDOM charts are not supported");
					else if (line[1] >= '0' && line[1] <= '9')
						OnChannel(data, line, ref lastMeasure);
					else if (line.FastStartsWith("#PLAYER"))
						continue;   // not supported, determine from channel data
					else
						OnHeader(data, line);
				}
			}
		}

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

		data.resolution = resolution;

		return data;
	}

	private void OnChannel(BMSChart data, string line, ref BMSMeasure lastMeasure)
	{
		// channel sentences
		// # <measure:3> <channel:2> : <object data>
		// for object data, 00 = musical rest

		int measureIndex;
		int channelIndex;
		string channelValue;

		try
		{
			measureIndex = int.Parse(line.Substring(1, 3));
			channelValue = line.Substring(7).Trim();
			string str = line.Substring(4, 2);
			if (!int.TryParse(str, out channelIndex))
			{
				channelIndex = int.Parse(str, NumberStyles.HexNumber,
					CultureInfo.InvariantCulture);
			}
		}
		catch
		{
			Log.Warning("Invalid channel line");
			return;
		}

		if (channelRefs.ContainsKey(channelIndex))
			channelRefs[channelIndex]++;
		else
			channelRefs.Add(channelIndex, 1);

		if (!BMSChannel.IsSupported(channelIndex))
			Log.Warning("Unsupported channel type " + line.Substring(4, 2));

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
				int value = 0;
				try
				{
					string str = channelValue.Substring(i, 2);
					if (channelIndex == (int)BMSChannel.Type.BPM)
						value = int.Parse(str, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
					else
						value = Utility.FromBase36(str);
				}
				catch
				{
					Log.Warning("Invalid data in channel");
					channelLength = objects.Count;
					break;
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

	private static void OnWAV(BMSChart data, string line)
	{
		int pathPos = line.IndexOf(" ", 5);
		string base36 = line.Substring(4, pathPos - 4).Trim();
		string filepath = line.Substring(pathPos + 1);
		int index = Utility.FromBase36(base36);

		data.soundObjects[index] = new SoundObject(filepath, base36);
	}

	private static void OnBMP(BMSChart data, string line)
	{
		int pathPos = line.IndexOf(" ", 5);
		string base36 = line.Substring(4, pathPos - 4).Trim();
		string filepath = line.Substring(pathPos + 1);
		int index = Utility.FromBase36(base36);

		data.bgaObjects[index] = new BGAObject(filepath, base36);
	}

	private static void OnBPM(BMSChart data, string line)
	{
		int index = 0;
		if (!line.ToUpper().StartsWith("#BPM "))
			index = Utility.FromBase36(line.Substring(4, 2));

		string valueStr = line.Substring(line.IndexOf(" ") + 1);
		double value = 0.0;

		if (!double.TryParse(valueStr,
				NumberStyles.Number | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out value))
		{
			valueStr = valueStr.Replace(",", ".");
			if (!double.TryParse(valueStr,
			NumberStyles.Number | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out value))
			{
				Log.Error("Unable to parse BPM value: " + valueStr.ToString());
			}
		}

		data.bpmObjects[index] = value;
	}

	private static void OnStop(BMSChart data, string line)
	{
		int index = Utility.FromBase36(line.Substring(5, 2));
		double value = double.Parse(line.Substring(line.IndexOf(" ") + 1));

		data.stopObjects[index] = value;
	}

	private static void OnLongNote(BMSChart data, string line)
	{
		string valueStr = line.Substring(line.IndexOf(" ") + 1);
		int value = Utility.FromBase36(valueStr);

		data.lnObjects.Add(value);
	}

	private static void OnHeader(BMSChart data, string line)
	{
		string key = null;
		try
		{
			key = line.ToUpper().Split(new char[] { ' ' }, 2)[0];
			key = key.TrimStart(new char[] { '#' });
		}
		catch { }

		if (!string.IsNullOrEmpty(key) && data.HeaderDefined(key))
		{
			string value = null;
			int valueInt = 0;
			double valueDouble = 0.0;
			try
			{
				value = line.Substring(key.Length + 1);
				value = value.Trim();
			}
			catch
			{
			}

			if (data.HasHeader(key))
			{
				if (data.HasHeader<bool>(key) && value == null)
					data.SetHeader<bool>(key, true);
				else if (data.HasHeader<int>(key) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out valueInt))
					data.SetHeader<int>(key, valueInt);
				else if (data.HasHeader<double>(key) && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out valueDouble))
					data.SetHeader<double>(key, valueDouble);
				else
					data.SetHeader<string>(key, value);
			}
			else
			{
				if (value == null)
					data.SetHeader<bool>(key, true);
				else if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out valueInt))
					data.SetHeader<int>(key, valueInt);
				else if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out valueDouble))
					data.SetHeader<double>(key, valueDouble);
				else
					data.SetHeader<string>(key, value);
			}
		}
		else
			Log.Warning("Unsupported BMS line: " + line);
	}
}
