using System.Collections.Generic;
using System.Diagnostics;

namespace Pulsus.Gameplay
{
	[DebuggerDisplay("{ToString()}")]
	public class LongNoteEndEvent : NoteEvent
	{
		public LongNoteEvent startNote;
		public LongNoteEndEvent(long pulse, SoundObject sound, int lane, LongNoteEvent startNote)
			: base(pulse, sound, lane)
		{
			this.startNote = startNote;
		}

		public LongNoteEndEvent(long pulse, List<SoundObject> sounds, int lane, LongNoteEvent startNote)
			: base(pulse, sounds, lane)
		{
			this.startNote = startNote;
		}
	}
}
