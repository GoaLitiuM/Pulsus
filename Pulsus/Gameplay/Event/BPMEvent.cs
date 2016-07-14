using System.Diagnostics;

namespace Pulsus.Gameplay
{
	[DebuggerDisplay("{ToString()}")]
	public class BPMEvent : Event
	{
		public double bpm;

		public BPMEvent(long pulse, double bpm)
			: base(pulse)
		{
			this.bpm = bpm;
		}

		public override string ToString()
		{
			return base.ToString() + ", " + bpm.ToString() + " BPM";
		}
	}
}
