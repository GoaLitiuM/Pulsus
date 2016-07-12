using System.Diagnostics;

namespace Pulsus.Gameplay
{
	[DebuggerDisplay("{ToString()}")]
	public class NoteEvent : SoundEvent
	{
		public int lane { get; private set; }
		public int length;

		public bool isLongNote { get { return length != 0; } }

		public NoteEvent(int pulse, SoundObject sound, int lane, int length)
			: base(pulse, sound)
		{
			this.lane = lane;
			this.length = length;
		}
	}
}
