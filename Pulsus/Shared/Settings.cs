using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Pulsus.Graphics;
using Pulsus.Audio;
using Pulsus.Gameplay;

namespace Pulsus
{
	public class Settings
	{
		public string skin = "goa";
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

		[JsonIgnore]
		public string playPath;

		[JsonIgnore]
		public string renderPath;

		[JsonIgnore]
		public int startMeasure;

		[JsonIgnore]
		public bool songPreload = true;

		[JsonIgnore]
		public bool showSettings;
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
		[JsonConverter(typeof(StringEnumConverter))]
		public RendererType renderer = RendererType.Direct3D11;

		// windowing mode
		[JsonConverter(typeof(StringEnumConverter))]
		public VideoMode mode = VideoMode.Windowed;

		// monitor vertical sync
		public bool vsync = false;

		// cap for frames rendered per second, -1 = auto (monitor refresh rate), 0 = no cap
		public int fpsLimit = -1;

		// resolution for rendering
		public uint width = 1280;
		public uint height = 720;

		// window size
		public uint windowWidth = 0;
		public uint windowHeight = 0;
	}

	public class AudioSettings
	{
		[JsonConverter(typeof(StringEnumConverter))]
		public AudioDriver driver = AudioDriver.Default;

		public uint volume = 30;
		public uint sampleRate = 44100;
		public uint bufferLength = 768;
	}

	public class InputSettings
	{
		
	}

	public class GameplaySettings
	{
		[JsonIgnore]
		public bool autoplay { get { return assistMode == AssistMode.Autoplay; } }

		// scrolling time for notes in s
		public double scrollTime = 0.8;

		// judge text position, 0.5 = center
		public double judgePositionY = 0.75;

		// judge offset in ms
		public double judgeOffset;

		// disables loading and displaying BGA
		public bool disableBGA;

		[JsonConverter(typeof(StringEnumConverter))]
		public AssistMode assistMode;

		//[JsonConverter(typeof(StringEnumConverter))]
		//public ScrollMode scrollMode;

		//[JsonConverter(typeof(StringEnumConverter))]
		//public PlayMode playMode;

		//[JsonConverter(typeof(StringEnumConverter))]
		//public LaneMode laneMode;

		[JsonConverter(typeof(StringEnumConverter))]
		public GaugeMode gaugeMode;

		//[JsonConverter(typeof(StringEnumConverter))]
		//public RandomMode randomMode;
	}
}
