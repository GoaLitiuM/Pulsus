using System;

namespace Pulsus.Input
{
	public class InputAction
	{
		public Action onPressed { get; private set; }
		public Action onReleased { get; private set; }
		public Action onDown { get; private set; }
		public bool useRepeatRate { get; private set; }

		public double lastPressed = double.MinValue;
		public double lastProcessed = double.MinValue;

		public static InputAction OnPressed(Action action)
		{
			InputAction ia = new InputAction();
			ia.onPressed = action;
			return ia;
		}

		public static InputAction OnReleased(Action action)
		{
			InputAction ia = new InputAction();
			ia.onReleased = action;
			return ia;
		}

		public static InputAction OnPressedReleased(Action pressed, Action released)
		{
			InputAction ia = new InputAction();
			ia.onPressed = pressed;
			ia.onReleased = released;
			return ia;
		}

		public static InputAction OnDown(Action action, bool useRepeatRate = true)
		{
			InputAction ia = new InputAction();
			ia.onDown = action;
			ia.useRepeatRate = useRepeatRate;
			return ia;
		}
	}
}
