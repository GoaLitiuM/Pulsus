using System;
using System.Collections.Generic;
using System.Diagnostics;
using Pulsus;

[DebuggerDisplay("{((Type)index).ToString()}")]
public class BMSChannel
{
	public int index;
	public bool longNoteActive;
	public List<int> values = new List<int>();

	// TODO: use hexdecimals

	public enum Type : int
	{
		BGM = 1,
		Meter = 2,
		BPM = 3,
		BGA = 4,
		Extended = 5,
		BGAPoor = 6,
		BGALayer = 7,
		BPMExtended = 8,
		Stop = 9,

		P1KeyFirst = 11,
		P1KeyLast = 19,

		P2KeyFirst = 21,
		P2KeyLast = 29,

		P1InvisibleFirst = 31,
		P1InvisibleLast = 39,

		P2InvisibleFirst = 41,
		P2InvisibleLast = 49,

		P1LongFirst = 51,
		P1LongLast = 59,

		P2LongFirst = 61,
		P2LongLast = 69,

		//TextChange = 99,
		//RankChange = 0xA0,

		P1LandmineFirst = 0xD1,
		P1LandmineLast = 0xD9,

		P2LandmineFirst = 0xE1,
		P2LandmineLast = 0xE9,
	};

	public enum KeyBMS : int
	{
		P1Key1 = 11,
		P1Key2 = 12,
		P1Key3 = 13,
		P1Key4 = 14,
		P1Key5 = 15,
		P1Scratch = 16,
		P1FreeZone = 17,
		P1Key6 = 18,
		P1Key7 = 19,

		P2Key1 = 21,
		P2Key2 = 22,
		P2Key3 = 23,
		P2Key4 = 24,
		P2Key5 = 25,
		P2Scratch = 26,
		P2FreeZone = 27,
		P2Key6 = 28,
		P2Key7 = 29,
	}

	public enum KeyPMS : int
	{
		// Single Play

		P1Key1 = 11,
		P1Key2 = 12,
		P1Key3 = 13,
		P1Key4 = 14,
		P1Key5 = 15,
		P1Key6 = 22,
		P1Key7 = 23,
		P1Key8 = 24,
		P1Key9 = 25,

		// Double Play / Alternative SP

		P1DPKey1 = 11,
		P1DPKey2 = 12,
		P1DPKey3 = 13,
		P1DPKey4 = 14,
		P1DPKey5 = 15,
		P1DPKey6 = 18,
		P1DPKey7 = 19,
		P1DPKey8 = 16,
		P1DPKey9 = 17,

		P2DPKey1 = 21,
		P2DPKey2 = 22,
		P2DPKey3 = 23,
		P2DPKey4 = 24,
		P2DPKey5 = 25,
		P2DPKey6 = 28,
		P2DPKey7 = 29,
		P2DPKey8 = 26,
		P2DPKey9 = 27,
	}

	public BMSChannel(int index)
	{
		this.index = index;
	}

	public static bool IsStop(int index)
	{
		return index == (int)Type.Stop;
	}

	public static bool IsBPM(int index)
	{
		return index == (int)Type.BPM
			|| index == (int)Type.BPMExtended;
	}

	public static bool IsSupported(int index)
	{
		return Enum.IsDefined(typeof(Type), index)
			|| IsPlayer(index);
	}

	public static bool IsSound(int index)
	{
		return index == (int)Type.BGM
			|| IsPlayer(index);
	}

	public static bool IsPlayer(int index)
	{
		return IsKey(index)
			|| IsInvisible(index)
			|| IsLong(index)
			|| IsLandmine(index);
	}

	public static bool IsKey(int index)
	{
		return IsP1Key(index)
			|| IsP2Key(index);
	}

	public static bool IsP1Key(int index)
	{
		return index >= (int)Type.P1KeyFirst
			&& index <= (int)Type.P1KeyLast;
	}

	public static bool IsP2Key(int index)
	{
		return index >= (int)Type.P2KeyFirst
			&& index <= (int)Type.P2KeyLast;
	}

	public static bool IsInvisible(int index)
	{
		return IsP1Invisible(index)
			|| IsP2Invisible(index);
	}

	public static bool IsP1Invisible(int index)
	{
		return index >= (int)Type.P1InvisibleFirst
			&& index <= (int)Type.P1InvisibleLast;
	}

	public static bool IsP2Invisible(int index)
	{
		return index >= (int)Type.P2InvisibleFirst
			&& index <= (int)Type.P2InvisibleLast;
	}

	public static bool IsLong(int index)
	{
		return IsP1Long(index)
			|| IsP2Long(index);
	}

	public static bool IsP1Long(int index)
	{
		return index >= (int)Type.P1LongFirst
			&& index <= (int)Type.P1LongLast;
	}

	public static bool IsP2Long(int index)
	{
		return index >= (int)Type.P2LongFirst
			&& index <= (int)Type.P2LongLast;
	}

	public static bool IsLandmine(int index)
	{
		return IsP1Landmine(index)
			|| IsP2Landmine(index);
	}

	public static bool IsP1Landmine(int index)
	{
		return index >= (int)Type.P1LandmineFirst
			&& index <= (int)Type.P1LandmineLast;
	}

	public static bool IsP2Landmine(int index)
	{
		return index >= (int)Type.P2LandmineFirst
			&& index <= (int)Type.P2LandmineLast;
	}

	public static void MergeChannels(BMSChannel destination, BMSChannel source, bool lnWorkaround = false)
	{
		MergeChannels(destination, source.values, lnWorkaround);
	}

	// merges over values of zeroes with non-zero values from source channel
	public static void MergeChannels(BMSChannel destination, List<int> sourceList, bool lnWorkaround = false)
	{
		if (destination.values.Count != sourceList.Count)
		{
			int newLength = Utility.lcm(destination.values.Count, sourceList.Count);
			NormalizeChannel(destination, newLength, lnWorkaround ? -1 : 0);
			sourceList = NormalizeChannelValues(sourceList, newLength, lnWorkaround ? -1 : 0);
		}

		if (!lnWorkaround)
		{
			for (int i = 0; i < destination.values.Count; i++)
			{
				if (destination.values[i] == 0)
					destination.values[i] = sourceList[i];
			}
		}
		else
		{
			// filler values are replaced with previous object values
			// so the consecutive repeating values are not broken in
			// final normalized list.

			int lastValue = 0, lastValue2 = 0;
			for (int i = 0; i < destination.values.Count; i++)
			{

				if (destination.values[i] <= 0)
				{
					destination.values[i] = sourceList[i];
					if (destination.values[i] == -1)
						destination.values[i] = lastValue2;
				}
				if (destination.values[i] == -1)
					destination.values[i] = lastValue;

				lastValue = destination.values[i];
				lastValue2 = sourceList[i];
			}
		}
	}

	public static void NormalizeChannel(BMSChannel channel, int maxLength, int fillerValue = 0)
	{
		channel.values = NormalizeChannelValues(channel.values, maxLength, fillerValue);
	}

	// pads the channel data with zeroes to match with desired length
	public static List<int> NormalizeChannelValues(List<int> channelValues, int newLength, int fillerValue = 0)
	{
		List<int> values = channelValues;
		int channelLength = values.Count;
		if (values.Count < newLength)
		{
			if (values.Count % newLength != 0)
				newLength = Utility.lcm(values.Count, newLength);

			int pad = (newLength / channelLength) - 1;

			List<int> newValues = new List<int>(newLength);
			newValues.AddRange(System.Linq.Enumerable.Repeat(fillerValue, newLength));

			for (int j = 0, k = 0; j < channelLength; j++)
			{
				newValues[k] = values[j];
				k += pad + 1;
			}

			return newValues;
		}
		return values;
	}

	// maps BMS channels to lane numbers
	public static int GetLaneIndex(int channel, int keyCount, int playerCount)
	{
		int noteChannel = -1;

		// long note
		if (channel >= (int)BMSChannel.Type.P1LongFirst && channel <= (int)BMSChannel.Type.P2LongLast)
			channel -= (int)BMSChannel.Type.P1LongFirst - (int)BMSChannel.Type.P1KeyFirst;

		// landmine
		if (channel >= (int)BMSChannel.Type.P1LandmineFirst && channel <= (int)BMSChannel.Type.P2LandmineLast)
			channel -= (int)BMSChannel.Type.P1LandmineFirst - (int)BMSChannel.Type.P1KeyFirst;

		int offset = 0;
		if (keyCount != 9)
		{
			if (playerCount == 2 && BMSChannel.IsP2Key(channel))
			{
				offset = keyCount;
				channel -= BMSChannel.KeyBMS.P1Key1 - BMSChannel.KeyBMS.P2Key1;
			}

			if (BMSChannel.IsP1Key(channel))
			{
				if (channel >= (int)BMSChannel.KeyBMS.P1Key1 && channel <= (int)BMSChannel.KeyBMS.P1Key5)
					noteChannel = 1 + channel - (int)BMSChannel.KeyBMS.P1Key1;
				else if (channel >= (int)BMSChannel.KeyBMS.P1Key6 && channel <= (int)BMSChannel.KeyBMS.P1Key7)
					noteChannel = 1 + 5 + channel - (int)BMSChannel.KeyBMS.P1Key6;
				else if (channel == (int)BMSChannel.KeyBMS.P1Scratch)
					noteChannel = 0;
			}
		}
		else if (keyCount == 9)
		{
			if (playerCount == 1)
			{
				if (channel >= (int)BMSChannel.KeyPMS.P1Key1 && channel <= (int)BMSChannel.KeyPMS.P1Key5)
					noteChannel = channel - (int)BMSChannel.KeyPMS.P1Key1;
				else if (channel >= (int)BMSChannel.KeyPMS.P1Key6 && channel <= (int)BMSChannel.KeyPMS.P1Key9)
					noteChannel = 5 + (channel - (int)BMSChannel.KeyPMS.P1Key6);
			}
			else if (playerCount == 2)
			{
				if (channel >= (int)BMSChannel.KeyPMS.P2DPKey1)
				{
					offset = 9;
					channel -= BMSChannel.KeyPMS.P2DPKey1 - BMSChannel.KeyPMS.P1DPKey1;
				}

				if (channel >= (int)BMSChannel.KeyPMS.P1DPKey1 && channel <= (int)BMSChannel.KeyPMS.P1DPKey5)
					noteChannel = channel - (int)BMSChannel.KeyPMS.P1DPKey1;
			}

			if (channel == (int)BMSChannel.KeyPMS.P1DPKey6)
				noteChannel = 5;
			else if (channel == (int)BMSChannel.KeyPMS.P1DPKey7)
				noteChannel = 6;
			else if (channel == (int)BMSChannel.KeyPMS.P1DPKey8)
				noteChannel = 7;
			else if (channel == (int)BMSChannel.KeyPMS.P1DPKey9)
				noteChannel = 8;

			noteChannel += offset;
		}
		return noteChannel;
	}
}
