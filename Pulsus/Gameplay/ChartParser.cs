namespace Pulsus.Gameplay
{
	public abstract class ChartParser
	{
		public bool headerOnly = false;
		public abstract Chart Load(string path);
	}
}
