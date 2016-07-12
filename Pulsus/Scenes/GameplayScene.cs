using SDL2;
using Pulsus.Input;
using Pulsus.Gameplay;

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
			int startPulse = 0;
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

			// load sound and bitmap objects
			if (settings.songPreload)
				loader.PreloadAll();
			else
				loader.Preload(); // preload few seconds ahead

			// bind input
			inputMapper = new InputMapper(game.inputManager);
			BindInput();

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

		private void BindInput()
		{
			inputMapper.MapInput(SDL.SDL_Scancode.SDL_SCANCODE_ESCAPE, InputAction.OnPressed(() =>
			{
				active = false;
			}));
			
			double scrollStep = 0.01;

			inputMapper.MapInput(SDL.SDL_Scancode.SDL_SCANCODE_KP_PLUS, InputAction.OnPressed(() =>
			{
				skin.baseScrollTime += scrollStep;
				if (skin.baseScrollTime >= 10.0)
					skin.baseScrollTime = 10.0;

				SettingsManager.instance.gameplay.scrollTime = skin.baseScrollTime;
				Log.Info("scrollTime: " + skin.baseScrollTime.ToString("0.00"));
			}));

			inputMapper.MapInput(SDL.SDL_Scancode.SDL_SCANCODE_KP_MINUS, InputAction.OnPressed(() =>
			{
				skin.baseScrollTime -= scrollStep;
				if (skin.baseScrollTime < scrollStep)
					skin.baseScrollTime = scrollStep;

				SettingsManager.instance.gameplay.scrollTime = skin.baseScrollTime;
				Log.Info("scrollTime: " + skin.baseScrollTime.ToString("0.00"));
			}));

			if (!SettingsManager.instance.gameplay.autoplay)
			{
				BindPlayerKey(SDL.SDL_Scancode.SDL_SCANCODE_A, 1);
				BindPlayerKey(SDL.SDL_Scancode.SDL_SCANCODE_Z, 1);
				BindPlayerKey(SDL.SDL_Scancode.SDL_SCANCODE_X, 2);
				BindPlayerKey(SDL.SDL_Scancode.SDL_SCANCODE_C, 3);
				BindPlayerKey(SDL.SDL_Scancode.SDL_SCANCODE_M, 4);
				BindPlayerKey(SDL.SDL_Scancode.SDL_SCANCODE_SPACE, 4);
				BindPlayerKey(SDL.SDL_Scancode.SDL_SCANCODE_COMMA, 5);
				BindPlayerKey(SDL.SDL_Scancode.SDL_SCANCODE_PERIOD, 6);
				BindPlayerKey(SDL.SDL_Scancode.SDL_SCANCODE_SLASH, 7);
				BindPlayerKey(SDL.SDL_Scancode.SDL_SCANCODE_LSHIFT, 0);
				BindPlayerKey(SDL.SDL_Scancode.SDL_SCANCODE_RSHIFT, 0);

				// standard 7K layout
				BindPlayerKey(JoyButtons.Button4, 1);
				BindPlayerKey(JoyButtons.Button7, 2);
				BindPlayerKey(JoyButtons.Button3, 3);
				BindPlayerKey(JoyButtons.Button8, 4);
				BindPlayerKey(JoyButtons.Button2, 5);
				BindPlayerKey(JoyButtons.Button5, 6);
				BindPlayerKey(JoyButtons.Axis1Left, 7);
				BindPlayerKey(JoyButtons.Axis1Up, 0);
				BindPlayerKey(JoyButtons.Axis1Down, 0);

				//BindPlayerKey(JoyButtons.Button10, Start);
				//BindPlayerKey(JoyButtons.Button9, Select);
			}
		}

		private void BindPlayerKey(JoyButtons button, int lane)
		{
			inputMapper.MapInput(button, InputAction.OnPressedReleased(
				() => autoplay.PlayerPressKey(lane),
				() => autoplay.PlayerReleaseKey(lane)
			));
		}

		private void BindPlayerKey(SDL.SDL_Scancode scancode, int lane)
		{
			inputMapper.MapInput(scancode, InputAction.OnPressedReleased(
				() => autoplay.PlayerPressKey(lane),
				() => autoplay.PlayerReleaseKey(lane)
			));
		}

		public override void Update(double deltaTime)
		{
			inputMapper.Update();
			playerGraph.Update(deltaTime);
		}

		public override void Draw(double deltaTime)
		{
			skin.Render(deltaTime);
		}
	}
}
