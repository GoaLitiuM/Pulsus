using System.Diagnostics;

namespace Pulsus.Gameplay
{
	[DebuggerDisplay("{ToString()}")]
	public class KeySoundChangeEvent : SoundEvent
	{
		public int lane;

		public KeySoundChangeEvent(int pulse, SoundObject sound, int lane)
			: base(pulse, sound)
		{
			this.lane = lane;
		}
	}
}
