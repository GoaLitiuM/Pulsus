using System.Diagnostics;

namespace Pulsus.Gameplay
{
	[DebuggerDisplay("{ToString()}")]
	public class BGAEvent : Event
	{
		public BGAObject bga;
		public BGAType type;

		public BGAEvent(long pulse, BGAObject bga, BGAType type = BGAType.BGA)
			: base(pulse)
		{
			this.bga = bga;
			this.type = type;
		}

		public enum BGAType
		{
			BGA,
			Poor,
			Layer1,
			Layer2,
		}
	}
}
