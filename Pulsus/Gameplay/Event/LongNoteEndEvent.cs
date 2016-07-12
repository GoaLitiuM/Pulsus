using System.Diagnostics;

namespace Pulsus.Gameplay
{
	[DebuggerDisplay("{ToString()}")]
	public class LongNoteEndEvent : NoteEvent
	{
		public NoteEvent startNote;
		public LongNoteEndEvent(int pulse, SoundObject sound, int lane, int length, NoteEvent startNote)
			: base(pulse, sound, lane, length)
		{
			this.startNote = startNote;
		}
	}
}
