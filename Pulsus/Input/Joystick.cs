using System;

namespace Pulsus.Input
{
	public class Joystick
	{
		public IntPtr deviceHandle { get; internal set; }
		public string name { get; internal set; }
		public Guid guid { get; internal set; }
		public bool connected { get; internal set; }

		public Joystick(IntPtr deviceHandle, string name, Guid guid)
		{
			connected = true;
			this.deviceHandle = deviceHandle;
			this.name = name;
			this.guid = guid;
		}
	}
}
