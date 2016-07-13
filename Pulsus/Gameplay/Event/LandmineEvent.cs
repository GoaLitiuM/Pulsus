using System.Diagnostics;

namespace Pulsus.Gameplay
{
	[DebuggerDisplay("{ToString()}")]
	public class LandmineEvent : NoteEvent
	{
		int damage;

		public LandmineEvent(int pulse, SoundObject sound, int lane, int damage)
			: base(pulse, sound, lane)
		{
			this.damage = damage;
		}
	}
}
