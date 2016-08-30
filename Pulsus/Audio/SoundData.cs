namespace Pulsus.Audio
{
	public class SoundData
	{
		public static ushort targetFormat;	//SDL2 format
		public static int targetFreq;

		public byte[] data;
		public int sampleCount;
		public int sampleRate;
		public int channels;
		//public int polyphony;	// maximum number of playing SoundInstances
		public int instances;	// currently active SoundInstances

		public SoundData(byte[] data, int sampleCount, int sampleRate, int channels/*, int polyphony = 0*/)
		{
			this.data = data;
			this.sampleCount = sampleCount;
			this.sampleRate = sampleRate;
			this.channels = channels;
			//this.polyphony = polyphony;
		}
	}
}
