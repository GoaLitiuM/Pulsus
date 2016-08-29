using Pulsus.Input;
using SDL2;
using System.Collections.Generic;
using System;

namespace Pulsus
{
	public class InputLayout
	{
		public Dictionary<string, string[]> keys;

		public InputLayout()
		{
		}

		public InputLayout(params Tuple<string, InputType[]>[] otherInputs)
		{
			keys = new Dictionary<string, string[]>();

			if (otherInputs != null)
			{
				foreach (Tuple<string, InputType[]> input in otherInputs)
					keys[input.Item1] = GetKeyStrings(input.Item2);
			}
		}

		protected string[] GetKeyStrings(InputType[] inputs)
		{
			if (inputs == null)
				return new string[0];

			string[] array = new string[inputs.Length];
			for (int j = 0; j < inputs.Length; j++)
				array[j] = inputs[j].Name;

			return array;
		}

		/// <summary> Returns array of bound keys for given key number. </summary>
		/// <param name="key">Key number starting from index 1.</param>
		public InputType[] GetKeyInputs(int key)
		{
			return GetInputs("key" + key.ToString());
		}

		public InputType[] GetInputs(string keyName)
		{
			string[] keyArray;
			if (!keys.TryGetValue(keyName, out keyArray))
				return new InputType[0];

			InputType[] inputs = new InputType[keyArray.Length];
			for (int i = 0; i < keyArray.Length; i++)
			{
				string key = keyArray[i];

				SDL.SDL_Scancode scancode = SDL.SDL_Scancode.SDL_SCANCODE_UNKNOWN;
				JoyInput joyButton = JoyInput.Unknown;

				if (key.StartsWith("Joy") && Enum.TryParse(key.Remove(0, "Joy".Length), out joyButton))
					inputs[i] = new InputJoystick(joyButton);
				else if ((scancode = SDL.SDL_GetScancodeFromName(key)) != SDL.SDL_Scancode.SDL_SCANCODE_UNKNOWN)
					inputs[i] = new InputKey(scancode);
				else if (Enum.TryParse<SDL.SDL_Scancode>("SDL_SCANCODE_" + key, true, out scancode))
					inputs[i] = new InputKey(scancode);
				else
					throw new ApplicationException("Unable to parse input key: " + key);
			}

			return inputs;
		}
	}

	public class InputLayoutKeys : InputLayout
	{
		public InputLayoutKeys(InputType[][] keyInputs, params Tuple<string, InputType[]>[] otherInputs)
			: base(otherInputs)
		{
			if (keyInputs != null)
				for (int i = 0; i < keyInputs.Length; i++)
					keys["key" + (i + 1).ToString()] = GetKeyStrings(keyInputs[i]);
		}
	}

	public class InputLayoutTT : InputLayoutKeys
	{
		public InputLayoutTT(InputType[] ttInputs, InputType[][] keyInputs, params Tuple<string, InputType[]>[] otherInputs)
			: base(keyInputs, otherInputs)
		{
			keys["turntable"] = GetKeyStrings(ttInputs);
		}
	}

	public interface InputType
	{
		string Name { get; }
	}

	public class InputKey : InputType
	{
		public SDL.SDL_Scancode scancode;

		public InputKey(SDL.SDL_Scancode scancode)
		{
			this.scancode = scancode;
		}

		public string Name
		{
			get
			{
				string name = SDL.SDL_GetScancodeName(scancode);
				if (string.IsNullOrEmpty(name))
					name = scancode.ToString().Replace("SDL_SCANCODE_", "");
				return name;
			}
		}
	}

	public class InputJoystick : InputType
	{
		public JoyInput button;

		public InputJoystick(JoyInput button)
		{
			this.button = button;
		}

		public string Name { get { return "Joy" + button.ToString(); } }
	}
}
