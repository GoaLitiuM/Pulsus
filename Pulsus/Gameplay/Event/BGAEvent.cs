using System.Diagnostics;

namespace Pulsus.Gameplay
{
	[DebuggerDisplay("{ToString()}")]
	public class BGAEvent : Event
	{
		public BGAObject bitmap;
		public BGAType type;

		public BGAEvent(int pulse, BGAObject bitmap, BGAType type = BGAType.BGA)
			: base(pulse)
		{
			this.bitmap = bitmap;
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
