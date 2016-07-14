using System.Diagnostics;

namespace Pulsus.Gameplay
{
	[DebuggerDisplay("{ToString()}")]
	public class MeasureMarkerEvent : Event
	{
		public MeasureMarkerEvent(long pulse)
			: base(pulse)
		{

		}
	}
}
