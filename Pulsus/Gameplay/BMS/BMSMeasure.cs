using System.Collections.Generic;

namespace Pulsus.Gameplay
{
	public class BMSMeasure
	{
		public int index;
		public int maxLength;
		public double meter = 1.0;

		public List<BMSChannel> channelList = new List<BMSChannel>();

		public BMSMeasure(int index, double meter = 1.0)
		{
			this.index = index;
			this.meter = meter;
		}

		public void Add(int channelIndex, int value)
		{
			BMSChannel channelValues = new BMSChannel(channelIndex);
			channelValues.values = new List<int>(1);
			channelValues.values.Add(value);
			channelList.Add(channelValues);
		}

		public void Add(int channelIndex, List<int> values)
		{
			BMSChannel channelValues = new BMSChannel(channelIndex);
			channelValues.values = values;
			channelList.Add(channelValues);
		}

		public bool HasChannel(BMSChannel.Type channel)
		{
			return HasChannel((int)channel);
		}

		public bool HasChannel(int channelIndex)
		{
			return GetChannel(channelIndex) != null;
		}

		public BMSChannel GetChannel(BMSChannel.Type channel)
		{
			return GetChannel((int)channel);
		}

		public BMSChannel GetChannel(int channelIndex)
		{
			foreach (BMSChannel channel in channelList)
				if (channel.index == channelIndex)
					return channel;

			return null;
		}
	}
}