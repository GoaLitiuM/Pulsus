using System.Diagnostics;

namespace Pulsus.Gameplay
{
	[DebuggerDisplay("{ToString()}")]
	public class StopEvent : Event
	{
		public long stopTime;

		public StopEvent(long pulse, long stopTime)
			: base(pulse)
		{
			this.stopTime = stopTime;
		}
	}
}
