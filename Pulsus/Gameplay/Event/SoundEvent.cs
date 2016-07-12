using System.Diagnostics;

namespace Pulsus.Gameplay
{
	[DebuggerDisplay("{ToString()}")]
	public class SoundEvent : Event
	{
		public SoundObject sound;

		public SoundEvent(int pulse, SoundObject sound)
			: base(pulse)
		{
			this.sound = sound;
		}
	}
}
