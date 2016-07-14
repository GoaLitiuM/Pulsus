using System.Diagnostics;

namespace Pulsus.Gameplay
{
	[DebuggerDisplay("{ToString()}")]
	public class NoteEvent : SoundEvent
	{
		public int lane { get; private set; }

		public NoteEvent(long pulse, SoundObject sound, int lane)
			: base(pulse, sound)
		{
			this.lane = lane;
		}
	}
}
