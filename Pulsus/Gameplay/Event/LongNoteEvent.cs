using System.Diagnostics;

namespace Pulsus.Gameplay
{
	[DebuggerDisplay("{ToString()}")]
	public class LongNoteEvent : NoteEvent
	{
		public LongNoteEndEvent endNote;

		public int length { get { return endNote.pulse - pulse; } }

		public LongNoteEvent(int pulse, SoundObject sound, int lane, LongNoteEndEvent endNote)
			: base(pulse, sound, lane)
		{
			this.endNote = endNote;
		}
	}
}
