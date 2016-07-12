using System.Diagnostics;

namespace Pulsus.Gameplay
{
	[DebuggerDisplay("{ToString()}")]
	public class MeasureMarkerEvent : Event
	{
		public MeasureMarkerEvent(int pulse)
			: base(pulse)
		{

		}
	}
}
