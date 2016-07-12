using System;

namespace Pulsus.Input
{
	public class InputAction
	{
		public Action pressed = null;
		public Action released = null;
		public Action down = null;

		public static InputAction OnPressed(Action action)
		{
			InputAction ia = new InputAction();
			ia.pressed = action;
			return ia;
		}

		public static InputAction OnReleased(Action action)
		{
			InputAction ia = new InputAction();
			ia.released = action;
			return ia;
		}

		public static InputAction OnPressedReleased(Action pressed, Action released)
		{
			InputAction ia = new InputAction();
			ia.pressed = pressed;
			ia.released = released;
			return ia;
		}

		public static InputAction OnDown(Action action)
		{
			InputAction ia = new InputAction();
			ia.down = action;
			return ia;
		}
	}
}
