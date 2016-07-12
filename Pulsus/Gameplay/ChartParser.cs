namespace Pulsus.Gameplay
{
	public abstract class ChartParser
	{
		public abstract Chart LoadHeaders(string path);
		public abstract Chart Load(string path);
	}
}
