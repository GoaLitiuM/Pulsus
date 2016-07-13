using System;
using System.Collections;
using System.Collections.Generic;
using SDL2;

namespace Pulsus.Input
{
	public class InputManager : IDisposable
	{
		List<Joystick> joysticks = new List<Joystick>();
		BitArray keyboardState = new BitArray((int)SDL.SDL_Scancode.SDL_NUM_SCANCODES, false);
		BitArray keyboardLastState = new BitArray((int)SDL.SDL_Scancode.SDL_NUM_SCANCODES, false);
		List<JoystickState> joystickStates = new List<JoystickState>();
		List<JoystickState> joystickLastStates = new List<JoystickState>();

		// list of known devices, give them prettier names
		Dictionary<Guid, string> joystickNames = new Dictionary<Guid, string>()
		{
			{ new Guid("{0368034c-0000-0000-0000-504944564944}"), "DJDAO Controller" },
		};

		public InputManager()
		{
			SDL.SDL_InitSubSystem(SDL.SDL_INIT_JOYSTICK);
		}

		public void Dispose()
		{
			SDL.SDL_QuitSubSystem(SDL.SDL_INIT_JOYSTICK);
		}

		public JoystickState GetJoystickState(int index)
		{
			for (int i = 0, j = 0; i < joysticks.Count; i++)
			{
				if (joysticks[i] == null)
					continue;

				if (j == index)
					return joystickStates[i];
				j++;
			}
			return new JoystickState(0, 0, 0, 0);
		}

		public void Update()
		{
			keyboardLastState = new BitArray(keyboardState);
		}

		public void HandleEvent(ref SDL.SDL_Event sdlEvent)
		{
			switch (sdlEvent.type)
			{
				case SDL.SDL_EventType.SDL_MOUSEBUTTONDOWN:
				case SDL.SDL_EventType.SDL_MOUSEBUTTONUP:
				case SDL.SDL_EventType.SDL_MOUSEMOTION:
				case SDL.SDL_EventType.SDL_MOUSEWHEEL:
					break;
				case SDL.SDL_EventType.SDL_KEYDOWN:
				case SDL.SDL_EventType.SDL_KEYUP:
					keyboardState[(int)sdlEvent.key.keysym.scancode] = sdlEvent.key.state == SDL.SDL_PRESSED;
					break;
				case SDL.SDL_EventType.SDL_JOYBUTTONDOWN:
				case SDL.SDL_EventType.SDL_JOYBUTTONUP:
					joystickStates[sdlEvent.jbutton.which].SetButton(sdlEvent.jbutton.button, sdlEvent.jbutton.state == SDL.SDL_PRESSED);
					break;
				case SDL.SDL_EventType.SDL_JOYAXISMOTION:
					joystickStates[sdlEvent.jbutton.which].SetAxis(sdlEvent.jaxis.axis, sdlEvent.jaxis.axisValue);
					break;
				case SDL.SDL_EventType.SDL_JOYBALLMOTION:
					joystickStates[sdlEvent.jbutton.which].SetBallX(sdlEvent.jball.ball, sdlEvent.jball.xrel);
					joystickStates[sdlEvent.jbutton.which].SetBallY(sdlEvent.jball.ball, sdlEvent.jball.yrel);
					break;
				case SDL.SDL_EventType.SDL_JOYHATMOTION:
					joystickStates[sdlEvent.jbutton.which].SetHat(sdlEvent.jhat.hat, sdlEvent.jhat.hatValue);
					break;
				case SDL.SDL_EventType.SDL_JOYDEVICEADDED:
					JoystickDeviceAdded(sdlEvent.jdevice.which);
					break;
				case SDL.SDL_EventType.SDL_JOYDEVICEREMOVED:
					JoystickDeviceRemoved(sdlEvent.jdevice.which);
					break;
				default:
					break;
			}
		}

		public void JoystickDeviceAdded(int deviceIndex)
		{
			IntPtr deviceHandle = SDL.SDL_JoystickOpen(deviceIndex);
			if (deviceHandle == IntPtr.Zero)
				return;
		
			string name = "";
			Guid guid = SDL.SDL_JoystickGetGUID(deviceHandle);
			if (!joystickNames.TryGetValue(guid, out name))
				name = SDL.SDL_JoystickName(deviceHandle);

			Joystick joystick = new Joystick(deviceHandle, name, guid);
			joysticks.Add(joystick);

			int buttonCount = SDL.SDL_JoystickNumButtons(deviceHandle);
			int axisCount = SDL.SDL_JoystickNumAxes(deviceHandle);
			int hatCount = SDL.SDL_JoystickNumHats(deviceHandle);
			int ballCount = SDL.SDL_JoystickNumBalls(deviceHandle);
			JoystickState state = new JoystickState(buttonCount, axisCount, hatCount, ballCount);
			joystickStates.Add(state);
		}

		public void JoystickDeviceRemoved(int deviceIndex)
		{
			joysticks[deviceIndex].connected = false;
			joysticks[deviceIndex] = null;
		}

		public bool KeyDown(SDL.SDL_Scancode scancode)
		{
			return keyboardState[(int)scancode];
		}

		public bool KeyPressed(SDL.SDL_Scancode scancode)
		{
			return keyboardState[(int)scancode] && !keyboardLastState[(int)scancode];
		}
	}

	public enum JoyInput : int
	{
		Unknown = 0,

		Button1,
		Button2,
		Button3,
		Button4,
		Button5,
		Button6,
		Button7,
		Button8,
		Button9,
		Button10,
		Button11,
		Button12,
		Button13,
		Button14,
		Button15,
		Button16,
		Button17,
		Button18,
		Button19,
		Button20,
		Button21,
		Button22,
		Button23,
		Button24,
		Button25,
		Button26,
		Button27,
		Button28,
		Button29,
		Button30,
		Button31,
		Button32,

		Axis1Up = 1024,
		Axis1Down,
		Axis1Y,
		Axis1Left,
		Axis1Right,
		Axis1X,

		Axis2Up,
		Axis2Down,
		Axis2Y,
		Axis2Left,
		Axis2Right,
		Axis2X,

		//Count,
	}
}
