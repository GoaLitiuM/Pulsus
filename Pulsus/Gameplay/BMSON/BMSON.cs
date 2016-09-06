namespace Pulsus.Gameplay
{
	public class BMSONHeader
	{
		public string version;
		public BMSON.BMSONInfo info;
	}

	public class BMSON : BMSONHeader
	{
		public BarLine[] lines;
		public BMSONBpmEvent[] bpm_events = new BMSONBpmEvent[0];
		public BMSONStopEvent[] stop_events = new BMSONStopEvent[0];
		public SoundChannel[] sound_channels;
		public BGA bga = new BGA();

		// bmson 0.21
		public BMSONEventNote[] bpmEvents = new BMSONEventNote[0];
		public BMSONEventNote[] stopEvents = new BMSONEventNote[0];
		public SoundChannel[] soundChannel
		{
			get { return sound_channels; }
			set { sound_channels = value; }
		}

		public class BMSONInfo
		{
			public string title;
			public string subtitle = "";
			public string artist;
			public string[] subartists = new string[0];
			public string genre;
			public string mode_hint = "beat-7k";
			public string chart_name;
			public uint level;
			public double init_bpm;
			public double judge_rank = 100;
			public double total = 100;
			public string back_image;
			public string eyecatch_image;
			public string banner_image;
			public string preview_music;
			public uint resolution = 240;

			// aliases for bmson 0.21
			public double judgeRank
			{
				get { return judge_rank; }
				set { judge_rank = value; }
			}
			public double initBPM
			{
				get { return init_bpm; }
				set { init_bpm = value; }
			}
		}

		public class BMSONBpmEvent
		{
			public uint y;          // pulse
			public double bpm;
		}

		public class BMSONStopEvent
		{
			public uint y;          // pulse
			public uint duration;   // stop in pulses
		}

		public class BMSONEventNote
		{
			public uint y;          // pulse
			public double v;        // value (bpm or stop time in seconds)
		}

		public class BarLine
		{
			public uint y;
			public uint k;          // kind, deprecated in bmson 1.0.0
		}

		public class SoundChannel
		{
			public string name; // filename
			public Note[] notes;
		}

		public class Note
		{
			public int x;       // lane
			public uint y;      // pulse
			public uint l;      // long note length
			public bool c;      // continuation?
		}

		public class BGA
		{
			public BGAHeader[] bga_header;
			public BGAEvent[] bga_events;
			public BGAEvent[] layer_events;
			public BGAEvent[] poor_events;

			// aliases for bmson 0.21
			public BGAHeader[] bgaHeader
			{
				get { return bga_header; }
				set { bga_header = value; }
			}
			public BGAEvent[] bgaNotes
			{
				get { return bga_events; }
				set { bga_events = value; }
			}
			public BGAEvent[] layerNotes
			{
				get { return layer_events; }
				set { layer_events = value; }
			}
			public BGAEvent[] poorNotes
			{
				get { return poor_events; }
				set { poor_events = value; }
			}
		}

		public class BGAHeader
		{
			public uint id;
			public string name;     // filename

			// bmson 0.21 alias
			public uint ID
			{
				get { return id; }
				set { id = value; }
			}
		}

		public class BGAEvent
		{
			public uint y;          // pulse
			public uint id;         // header id

			// bmson 0.21 alias
			public uint ID
			{
				get { return id; }
				set { id = value; }
			}
		}
	}
}
