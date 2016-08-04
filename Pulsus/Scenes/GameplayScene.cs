using SDL2;
using Pulsus.Input;
using Pulsus.Gameplay;
using System;

namespace Pulsus
{
	public class GameplayScene : Scene
	{
		InputMapper inputMapper;
		Song song;
		EventPlayerGraph playerGraph;
		Skin skin;
		BGM songPlayer;
		BMSJudge judge;
		Player autoplay;
		Loader loader;

		public GameplayScene(Game game, Song song) : base(game)
		{
			Log.Info("Loading song: " + song.path);

			this.song = song;
			song.Load();

			Log.Info("Generating events...");
			Chart chart = song.chart;
			song.GenerateEvents();
	
			Settings settings = SettingsManager.instance;
			bool useAutoplay = settings.gameplay.autoplay;
			double judgeOffset = settings.gameplay.judgeOffset / 1000.0;

			Log.Info("Initializing players...");

			judge = new BMSJudge(song);
			skin = new Skin(song, renderer, judge);
			judge.OnNoteJudged += skin.OnNoteJudged;

			songPlayer = new BGM(audio, song);
			autoplay = new Player(audio, song, judge, skin);
			loader = new Loader(song);

			autoplay.autoplay = useAutoplay;

			// add players to graph
			playerGraph = new EventPlayerGraph();
			playerGraph.Add(judge);
			playerGraph.Add(autoplay);
			playerGraph.Add(songPlayer);
			playerGraph.Add(skin);
			playerGraph.Add(loader);
			
			// adjust offsets

			if (!useAutoplay)
				autoplay.startOffset += judgeOffset;

			double adjustTimeline = 0.0;
			long startPulse = 0;
			if (settings.startMeasure > 0)
				startPulse = chart.measurePositions[settings.startMeasure].Item3;
			else if (chart != null && chart.firstPlayerEvent != -1)
			{
				double noteTimestamp = chart.eventList[chart.firstPlayerEvent].timestamp;
				if (noteTimestamp < skin.startOffset)
					adjustTimeline += skin.baseScrollTime;
			}

			playerGraph.SetStartPosition(startPulse);
			playerGraph.AdjustTimeline(-adjustTimeline);

			// load sound and bga objects

			System.Diagnostics.Stopwatch loadTimer = System.Diagnostics.Stopwatch.StartNew();
			if (settings.songPreload)
				loader.PreloadAll();
			else
			{
				// preload all BGA objects
				loader.PreloadAll(false, true);
				loader.Preload(); // preload few seconds ahead
			}
			loadTimer.Stop();
			Log.Info("Preload finished in " + loadTimer.Elapsed.TotalSeconds.ToString() + "s");

			// bind input
			inputMapper = new InputMapper(game.inputManager);

			InputLayout keyLayout = null;
			InputLayout generalLayout = settings.input.layouts["general"];
			int laneCount = chart.playerChannels;
			if (laneCount == 6)
				settings.input.layouts.TryGetValue(settings.input.default5k, out keyLayout);
			if (laneCount == 8 || keyLayout == null)
				settings.input.layouts.TryGetValue(settings.input.default7k, out keyLayout);
			if (laneCount == 9 || keyLayout == null)
				settings.input.layouts.TryGetValue(settings.input.default9k, out keyLayout);

			InputLayout layout = null;
			if (keyLayout != null)
			{
				layout = new InputLayout();
				layout.keys = new System.Collections.Generic.Dictionary<string, string[]>(keyLayout.keys);
				foreach (var key in generalLayout.keys.Keys)
				{
					// override non-present keys
					if (!layout.keys.ContainsKey(key) || layout.keys[key].Length == 0)
						layout.keys[key] = generalLayout.keys[key];
				}
			}
			else
				layout = generalLayout;

			// ensure that exit is always bound to somewhere
			if (layout.keys["exit"].Length == 0)
				layout.keys["exit"] = new string[] { new InputKey(SDL.SDL_Scancode.SDL_SCANCODE_ESCAPE).Name };

			BindInputLayout(layout);

			// start all the players
			Log.Info("Starting player graph");
			playerGraph.Start();
		}

		public override void Dispose()
		{
			audio.StopAll();

			if (skin != null)
				skin.Dispose();
			if (songPlayer != null)
				songPlayer.Dispose();
			if (judge != null)
				judge.Dispose();
			if (autoplay != null)
				autoplay.Dispose();
			if (loader != null)
				loader.Dispose();
			if (song != null)
				song.Dispose();
		}

		private void BindInputLayout(InputLayout layout)
		{
			Settings settings = SettingsManager.instance;

			BindKey(layout.GetInputs("exit"), InputAction.OnPressed(() =>
			{
				Close();
			}));
			
			const double scrollStep = 0.001;

			BindKey(layout.GetInputs("scrollSpeedInc"), InputAction.OnDown(() =>
			{
				skin.baseScrollTime += scrollStep;
				if (skin.baseScrollTime >= 10.0)
					skin.baseScrollTime = 10.0;

				settings.gameplay.scrollTime = skin.baseScrollTime;
			}));

			BindKey(layout.GetInputs("scrollSpeedDec"), InputAction.OnDown(() =>
			{
				skin.baseScrollTime -= scrollStep;
				if (skin.baseScrollTime < scrollStep)
					skin.baseScrollTime = scrollStep;

				settings.gameplay.scrollTime = skin.baseScrollTime;
			}));
			
			if (!settings.gameplay.autoplay)
			{
				BindLaneKey(layout.GetInputs("turntable"), 0);

				int keyCount = 9;
				for (int key = 0; key <= keyCount; key++)
					BindLaneKey(layout.GetKeyInputs(key), key);
			}
		}

		private void BindKey(InputType[] inputs, InputAction inputAction)
		{
			foreach (InputType input in inputs)
			{
				if (input is InputKey)
					inputMapper.MapInput((input as InputKey).scancode, inputAction);
				else if (input is InputJoystick)
					inputMapper.MapInput((input as InputJoystick).button, inputAction);
				else
					throw new ApplicationException("Unable to bind unknown type of InputType");
			}
		}

		private void BindLaneKey(InputType[] inputs, int lane)
		{
			foreach (InputType input in inputs)
			{
				if (input is InputKey)
					BindLaneKey((input as InputKey).scancode, lane);
				else if (input is InputJoystick)
					BindLaneKey((input as InputJoystick).button, lane);
				else
					throw new ApplicationException("Unable to bind unknown type of InputType");
			}
		}

		private void BindLaneKey(JoyInput button, int lane)
		{
			inputMapper.MapInput(button, InputAction.OnPressedReleased(
				() => autoplay.PlayerPressKey(lane),
				() => autoplay.PlayerReleaseKey(lane)
			));
		}

		private void BindLaneKey(SDL.SDL_Scancode scancode, int lane)
		{
			inputMapper.MapInput(scancode, InputAction.OnPressedReleased(
				() => autoplay.PlayerPressKey(lane),
				() => autoplay.PlayerReleaseKey(lane)
			));
		}

		public override void Update(double deltaTime)
		{
			inputMapper.Update(deltaTime);
			playerGraph.Update(deltaTime);
		}

		public override void Draw(double deltaTime)
		{
			skin.Render(deltaTime);
		}
	}
}
