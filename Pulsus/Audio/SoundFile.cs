namespace Pulsus.Audio
{
	public class SoundFile
	{
		public SoundData data { get; private set; }
		public string path { get; private set; }

		public SoundFile(string path)
		{
			this.path = path;
		}

		internal void SetData(SoundData data)
		{
			this.data = data;
		}
	}
}
