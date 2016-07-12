using System.Diagnostics;

namespace Pulsus.Gameplay
{
	[DebuggerDisplay("{ToString()}")]
	public class StopEvent : Event
	{
		public int stopPulse;

		public StopEvent(int pulse, int stopTime)
			: base(pulse)
		{
			this.stopPulse = stopTime;
		}
	}
}
