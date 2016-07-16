using System;
using System.Collections.Generic;
using SDL2;

namespace Pulsus.Input
{
	public class InputMapper
	{
		private InputManager input;
		private double timer;
		private double repeatRate = 1.0 / 60.0;
		private double repeatDelay = 0.250;

		private const float axisDeadzone = 0.1f;
		private const short axisDeadzoneInt = (short)(Int16.MaxValue * axisDeadzone);

		private Dictionary<InputAction, bool> blankState = new Dictionary<InputAction, bool>();
		private Dictionary<InputAction, bool> currentState;
		private Dictionary<InputAction, bool> lastState;

		private List<Tuple<SDL.SDL_Scancode, InputAction>> keyboardMapping = new List<Tuple<SDL.SDL_Scancode, InputAction>>();
		private List<Tuple<JoyInput, InputAction>> joystickMapping = new List<Tuple<JoyInput, InputAction>>();

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
			else if (inputAction.onPressed == null && inputAction.onReleased == null && inputAction.onDown == null)
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

		public void Update(double deltaTime)
		{
			timer += deltaTime;

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
					if (!lastPressed)
					{
						if (state.Key.onPressed != null)
							state.Key.onPressed();

						if (state.Key.onDown != null && state.Key.useRepeatRate)
							state.Key.onDown();

						state.Key.lastPressed = timer;
					}

					if (state.Key.onDown != null)
					{
						if (state.Key.useRepeatRate)
						{
							if (timer - state.Key.lastProcessed > repeatRate && timer - state.Key.lastPressed > repeatDelay)
							{
								state.Key.onDown();
								state.Key.lastProcessed = timer;
							}
						}
						else
						{
							state.Key.onDown();
							state.Key.lastProcessed = timer;
						}
					}
				}
				else if (!pressed && lastPressed)
				{
					if (state.Key.onReleased != null)
						state.Key.onReleased();
				}
			}
		}
	}
}