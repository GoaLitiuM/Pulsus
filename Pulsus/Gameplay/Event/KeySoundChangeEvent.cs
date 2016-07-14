using System.Diagnostics;

namespace Pulsus.Gameplay
{
	[DebuggerDisplay("{ToString()}")]
	public class KeySoundChangeEvent : SoundEvent
	{
		public int lane;

		public KeySoundChangeEvent(long pulse, SoundObject sound, int lane)
			: base(pulse, sound)
		{
			this.lane = lane;
		}
	}
}
