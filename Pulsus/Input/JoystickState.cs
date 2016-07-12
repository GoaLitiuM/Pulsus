using System.Collections;

namespace Pulsus.Input
{
	public struct JoystickState
	{
		public bool connected;
		internal BitArray buttonState;
		private short[] axisState;
		private byte[] hatState;
		private short[] ballState;

		public JoystickState(int buttonCount, int axisCount, int hatCount, int ballCount)
		{
			connected = buttonCount != 0 || axisCount != 0 || hatCount != 0 || ballCount != 0;
			buttonState = new BitArray(buttonCount);
			axisState = new short[axisCount];
			hatState = new byte[hatCount];
			ballState = new short[ballCount*2];
		}

		public bool ButtonDown(int button)
		{
			if (button >= buttonState.Count)
				return false;

			return buttonState[button];
		}

		public void SetButton(int button, bool value)
		{
			buttonState[button] = value;
		}

		public short GetAxis(int axis)
		{
			if (axis >= axisState.Length)
				return 0;

			return axisState[axis];
		}

		public void SetAxis(int axis, short value)
		{
			axisState[axis] = value;
		}

		public byte GetHat(int hat)
		{
			if (hat >= hatState.Length)
				return 0;

			return hatState[hat];
		}

		public void SetHat(int hat, byte value)
		{
			hatState[hat] = value;
		}

		public short GetAxisX(int stick = 0)
		{
			return GetAxis(stick*2);
		}

		public short GetAxisY(int stick = 0)
		{
			return GetAxis((stick*2)+1);
		}

		public short GetBallX(int ball)
		{
			return GetBall(ball*2);
		}

		public short GetBallY(int ball)
		{
			return GetBall((ball*2)+1);
		}

		private short GetBall(int ballIndex)
		{
			if (ballIndex >= ballState.Length)
				return 0;

			return ballState[ballIndex];
		}

		public void SetBallX(int ball, short value)
		{
			ballState[(ball*2)] = value;
		}

		public void SetBallY(int ball, short value)
		{
			ballState[(ball*2)+1] = value;
		}
	}
}
