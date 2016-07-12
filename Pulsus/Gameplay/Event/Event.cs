using System.Diagnostics;

namespace Pulsus.Gameplay
{
	[DebuggerDisplay("{ToString()}")]
	public class Event
	{
		public double timestamp;
		public int pulse;

		public Event(int pulse)
		{
			this.pulse = pulse;
		}

		public override string ToString()
		{
			return pulse.ToString() + ": " + GetType().Name;
		}
	}
}
