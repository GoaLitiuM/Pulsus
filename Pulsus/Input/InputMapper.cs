using System;
using System.Collections.Generic;
using SDL2;

namespace Pulsus.Input
{
	public class InputMapper
	{
		InputManager input;

		const float axisDeadzone = 0.1f;
		const short axisDeadzoneInt = (short)(Int16.MaxValue * axisDeadzone);

		Dictionary<InputAction, bool> blankState = new Dictionary<InputAction, bool>();
		Dictionary<InputAction, bool> currentState;
		Dictionary<InputAction, bool> lastState;

		List<Tuple<SDL.SDL_Scancode, InputAction>> keyboardMapping = new List<Tuple<SDL.SDL_Scancode, InputAction>>();
		List<Tuple<JoyInput, InputAction>> joystickMapping = new List<Tuple<JoyInput, InputAction>>();

		public InputMapper(InputManager input)
		{
			this.input = input;
		}

		public void MapInput(SDL.SDL_Scancode key, InputAction inputAction)
		{
			MapInput(inputAction);
			keyboardMapping.Add(new Tuple<SDL.SDL_Scancode, InputAction>(key, inputAction));
		}

		public void MapInput(JoyInput button, InputAction inputAction)
		{
			MapInput(inputAction);
			joystickMapping.Add(new Tuple<JoyInput, InputAction>(button, inputAction));
		}

		private void MapInput(InputAction inputAction)
		{
			if (inputAction == null)
				throw new ApplicationException("MapInput inputAction is null");
			else if (inputAction.pressed == null && inputAction.released == null && inputAction.down == null)
				throw new ApplicationException("MapInput inputAction has no actions");

			if (!blankState.ContainsKey(inputAction))
			{
				blankState.Add(inputAction, false);

				currentState = new Dictionary<InputAction, bool>(blankState);
				lastState = new Dictionary<InputAction, bool>(blankState);
			}
		}

		private void UpdateJoystick(int controllerIndex, Dictionary<InputAction, bool> inputState)
		{
			JoystickState joystickState = input.GetJoystickState(controllerIndex);
			if (!joystickState.connected)
				return;

			foreach (var t in joystickMapping)
			{
				JoyInput button = t.Item1;

				if (button < JoyInput.Axis1Up)
					inputState[t.Item2] |= joystickState.ButtonDown((int)button);
				else
				{
					int stick = 0;
					if (button >= JoyInput.Axis2Up)
					{
						stick = 1;
						button -= JoyInput.Axis2Up - JoyInput.Axis1Up;
					}

					if (button == JoyInput.Axis1Up)
						inputState[t.Item2] |= joystickState.GetAxisY(stick) < -axisDeadzoneInt;
					else if (button == JoyInput.Axis1Down)
						inputState[t.Item2] |= joystickState.GetAxisY(stick) > axisDeadzoneInt;
					else if (button == JoyInput.Axis1Left)
						inputState[t.Item2] |= joystickState.GetAxisX(stick) < -axisDeadzoneInt;
					else if (button == JoyInput.Axis1Right)
						inputState[t.Item2] |= joystickState.GetAxisX(stick) > axisDeadzoneInt;
					else if (button == JoyInput.Axis1Y)
						inputState[t.Item2] |= Math.Abs(joystickState.GetAxisY(stick)) > axisDeadzoneInt;
					else if (button == JoyInput.Axis1X)
						inputState[t.Item2] |= Math.Abs(joystickState.GetAxisX(stick)) > axisDeadzoneInt;
				}
			}
		}

		private void UpdateKeyboard(Dictionary<InputAction, bool> inputState)
		{
			foreach (var t in keyboardMapping)
				currentState[t.Item2] |= input.KeyDown(t.Item1);
		}

		public void Update()
		{
			var tempState = lastState;
			lastState = currentState;
			currentState = tempState;

			var keys = new List<InputAction>(currentState.Keys);
			foreach (var key in keys)
				if (currentState[key])
					currentState[key] = false;

			UpdateJoystick(0, currentState);
			UpdateKeyboard(currentState);

			foreach (var state in currentState)
			{
				bool pressed = state.Value;
				bool lastPressed = false;
				lastState.TryGetValue(state.Key, out lastPressed);

				if (pressed)
				{
					if (state.Key.down != null)
						state.Key.down();
					if (!lastPressed)
					{
						if (state.Key.pressed != null)
							state.Key.pressed();
					}
				}
				else if (!pressed && lastPressed)
				{
					if (state.Key.released != null)
						state.Key.released();
				}
			}
		}
	}
}