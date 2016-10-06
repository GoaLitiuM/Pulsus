using System;
using System.Collections.Generic;
using Jil;
using Pulsus.Audio;
using Pulsus.Gameplay;
using Pulsus.Graphics;
using Pulsus.Input;
using SDL2;

namespace Pulsus
{
	public class Settings
	{
		public string skin = "goa";

		// check for new updates at startup
		public bool checkUpdates = false;

		// fully load song data beforehand
		public bool songPreload = true;

		public List<string> songPaths = new List<string>()
		{
			"Songs/",
		};

		public EngineSettings engine = new EngineSettings();
		public VideoSettings video = new VideoSettings();
		public AudioSettings audio = new AudioSettings();
		public InputSettings input = new InputSettings();
		public GameplaySettings gameplay = new GameplaySettings();

		// settings used by commandline options

		[JilDirective(Ignore = true)]
		public string playPath;

		[JilDirective(Ignore = true)]
		public OutputMode outputMode;

		[JilDirective(Ignore = true)]
		public string outputPath;

		[JilDirective(Ignore = true)]
		public int startMeasure;

		[JilDirective(Ignore = true)]
		public bool showSettings;

		[JilDirective(Ignore = true)]
		public bool debug;
	}

	public class EngineSettings
	{
		// caches rendered fonts and font data in temp
		public bool cacheFonts = true;

		// logic updates per second
		public int tickrate = 2000;
	}

	public class VideoSettings
	{
		// renderer backend
		public RendererType renderer = RendererType.Direct3D11;

		// windowing mode
		public VideoMode mode = VideoMode.Windowed;

		// monitor vertical sync
		public bool vsync = false;

		// cap for frames rendered per second, -1 = auto (monitor refresh rate), 0 = no cap
		public int fpsLimit = -1;

		// resolution for rendering
		public uint width = 1280;
		public uint height = 720;
	}

	public class AudioSettings
	{
		public AudioDriver driver = AudioDriver.Default;
		public uint volume = 30;
		public uint sampleRate = 44100;
		public uint bufferLength = 768;
		public ResampleQuality resampleQuality = ResampleQuality.Low;
	}

	public class InputSettings
	{
		public string default5k = "5k";
		public string default7k = "7k";
		public string default9k = "9k";

		public Dictionary<string, InputLayout> layouts = new Dictionary<string, InputLayout>
		{
			["general"] = new InputLayout
			(
				new Tuple<string, InputType[]>("exit", new InputType[]
				{
					new InputKey(SDL.SDL_Scancode.SDL_SCANCODE_ESCAPE),
				}),
				new Tuple<string, InputType[]>("scrollSpeedInc", new InputType[]
				{
					new InputKey(SDL.SDL_Scancode.SDL_SCANCODE_KP_PLUS),
				}),
				new Tuple<string, InputType[]>("scrollSpeedDec", new InputType[]
				{
					new InputKey(SDL.SDL_Scancode.SDL_SCANCODE_KP_MINUS),
				})
			),
			["5k"] = new InputLayoutTT
			(
				new InputType[] // turntable
				{
					new InputKey(SDL.SDL_Scancode.SDL_SCANCODE_LSHIFT),
					new InputKey(SDL.SDL_Scancode.SDL_SCANCODE_RSHIFT),
					new InputJoystick(JoyInput.Axis1Up),
					new InputJoystick(JoyInput.Axis1Down),
				},
				new InputType[][] // keyboard layout: shift + zx_,.
				{
					new InputType[]
					{
						new InputKey(SDL.SDL_Scancode.SDL_SCANCODE_Z),
						new InputJoystick(JoyInput.Button4),
					},
					new InputType[]
					{
						new InputKey(SDL.SDL_Scancode.SDL_SCANCODE_X),
						new InputJoystick(JoyInput.Button7),
					},
					new InputType[]
					{
						new InputKey(SDL.SDL_Scancode.SDL_SCANCODE_C),
						new InputKey(SDL.SDL_Scancode.SDL_SCANCODE_SPACE),
						new InputKey(SDL.SDL_Scancode.SDL_SCANCODE_M),
						new InputJoystick(JoyInput.Button3),
					},
					new InputType[]
					{
						new InputKey(SDL.SDL_Scancode.SDL_SCANCODE_COMMA),
						new InputJoystick(JoyInput.Button8),
					},
					new InputType[]
					{
						new InputKey(SDL.SDL_Scancode.SDL_SCANCODE_PERIOD),
						new InputJoystick(JoyInput.Button2),
					},
				}
			),
			["7k"] = new InputLayoutTT
			(
				new InputType[] // turntable
				{
					new InputKey(SDL.SDL_Scancode.SDL_SCANCODE_LSHIFT),
					new InputKey(SDL.SDL_Scancode.SDL_SCANCODE_RSHIFT),
					new InputJoystick(JoyInput.Axis1Up),
					new InputJoystick(JoyInput.Axis1Down),
				},
				new InputType[][] // keyboard layout: shift + zxc_,.-
				{
					new InputType[]
					{
						new InputKey(SDL.SDL_Scancode.SDL_SCANCODE_Z),
						new InputJoystick(JoyInput.Button4),
					},
					new InputType[]
					{
						new InputKey(SDL.SDL_Scancode.SDL_SCANCODE_X),
						new InputJoystick(JoyInput.Button7),
					},
					new InputType[]
					{
						new InputKey(SDL.SDL_Scancode.SDL_SCANCODE_C),
						new InputJoystick(JoyInput.Button3),
					},
					new InputType[]
					{
						new InputKey(SDL.SDL_Scancode.SDL_SCANCODE_M),
						new InputKey(SDL.SDL_Scancode.SDL_SCANCODE_SPACE),
						new InputJoystick(JoyInput.Button8),
					},
					new InputType[]
					{
						new InputKey(SDL.SDL_Scancode.SDL_SCANCODE_COMMA),
						new InputJoystick(JoyInput.Button2),
					},
					new InputType[]
					{
						new InputKey(SDL.SDL_Scancode.SDL_SCANCODE_PERIOD),
						new InputJoystick(JoyInput.Button5),
					},
					new InputType[]
					{
						new InputKey(SDL.SDL_Scancode.SDL_SCANCODE_SLASH),
						new InputJoystick(JoyInput.Axis1Left),
					},
				}
			),
			["9k"] = new InputLayoutKeys
			(
				new InputType[][]	// keyboard layout: <zxc_m,.-
				{
					new InputType[]
					{
						new InputKey(SDL.SDL_Scancode.SDL_SCANCODE_NONUSBACKSLASH),
						new InputJoystick(JoyInput.Button1),
					},
					new InputType[]
					{
						new InputKey(SDL.SDL_Scancode.SDL_SCANCODE_Z),
						new InputJoystick(JoyInput.Button2),
					},
					new InputType[]
					{
						new InputKey(SDL.SDL_Scancode.SDL_SCANCODE_X),
						new InputJoystick(JoyInput.Button8),
					},
					new InputType[]
					{
						new InputKey(SDL.SDL_Scancode.SDL_SCANCODE_C),
						new InputJoystick(JoyInput.Button3),
					},
					new InputType[]
					{
						new InputKey(SDL.SDL_Scancode.SDL_SCANCODE_V),
						new InputKey(SDL.SDL_Scancode.SDL_SCANCODE_SPACE),
						new InputKey(SDL.SDL_Scancode.SDL_SCANCODE_N),
						new InputJoystick(JoyInput.Button7),
					},
					new InputType[]
					{
						new InputKey(SDL.SDL_Scancode.SDL_SCANCODE_M),
						new InputJoystick(JoyInput.Button4),
					},
					new InputType[]
					{
						new InputKey(SDL.SDL_Scancode.SDL_SCANCODE_COMMA),
						new InputJoystick(JoyInput.Button6),
					},
					new InputType[]
					{
						new InputKey(SDL.SDL_Scancode.SDL_SCANCODE_PERIOD),
						new InputJoystick(JoyInput.Axis1Up),
					},
					new InputType[]
					{
						new InputKey(SDL.SDL_Scancode.SDL_SCANCODE_SLASH),
						new InputJoystick(JoyInput.Button5),
					},
				}
			),
		};
	}

	public class GameplaySettings
	{
		[JilDirective(Ignore = true)]
		public bool autoplay { get { return assistMode == AssistMode.Autoplay; } }

		// scrolling time for notes in s
		public double scrollTime = 0.8;

		// judge text position, 0.5 = center
		public double judgePositionY = 0.75;

		// judge offset in ms
		public double judgeOffset;

		// disables loading and displaying BGA
		public bool disableBGA;

		public AssistMode assistMode;
		//public ScrollMode scrollMode;
		//public PlayMode playMode;
		//public LaneMode laneMode;
		public GaugeMode gaugeMode;
		//public RandomMode randomMode;
	}

	public enum OutputMode
	{
		None,
		Render,
		DumpTimestamps,
	}
}
