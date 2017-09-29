using System.Collections.Generic;
using System.Diagnostics;

namespace Pulsus.Gameplay
{
	[DebuggerDisplay("{ToString()}")]
	public class NoteEvent : Event
	{
		public int lane { get; private set; }
		public List<SoundObject> sounds { get; private set; }

		public NoteEvent(long pulse, SoundObject sound, int lane)
			: base(pulse)
		{
			this.lane = lane;

			sounds = new List<SoundObject>();
			if (sound != null)
				sounds.Add(sound);
		}

		public NoteEvent(long pulse, List<SoundObject> sounds, int lane)
			: base(pulse)
		{
			this.lane = lane;
			this.sounds = sounds;
		}
	}
}
