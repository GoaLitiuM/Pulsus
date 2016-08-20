using System.Diagnostics;

namespace Pulsus.Gameplay
{
	[DebuggerDisplay("{ToString()}")]
	public class MeterEvent : Event
	{
		public double meter;

		public MeterEvent(long pulse, double meter = 1.0)
			: base(pulse)
		{
			this.meter = meter;
		}
	}
}
