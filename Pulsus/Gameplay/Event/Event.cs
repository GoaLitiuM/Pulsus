using System.Diagnostics;

namespace Pulsus.Gameplay
{
	[DebuggerDisplay("{ToString()}")]
	public class Event
	{
		public double timestamp;
		public long pulse;

		public Event(long pulse)
		{
			this.pulse = pulse;
		}

		public override string ToString()
		{
			return pulse.ToString() + ": " + GetType().Name;
		}
	}
}
