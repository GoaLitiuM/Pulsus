using System.Diagnostics;

namespace Pulsus.Gameplay
{
	[DebuggerDisplay("{ToString()}")]
	public class LandmineEvent : NoteEvent
	{
		int damage;

		public LandmineEvent(int pulse, SoundObject sound, int lane, int length, int damage)
			: base(pulse, sound, lane, length)
		{
			this.damage = damage;
		}
	}
}
