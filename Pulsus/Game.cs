using System;
using System.Diagnostics;
using System.IO;
using Pulsus.Audio;
using Pulsus.FFmpeg;
using Pulsus.Graphics;
using Pulsus.Input;
using SDL2;

namespace Pulsus
{
	public class Game : IDisposable
	{
		public double updateInterval = 0;
		public double renderInterval = 1.0 / 120.0;

		double updateTime = 0.0;
		double renderTime = 0.0;

		public SceneManager sceneManager = new SceneManager();
		public GameWindow window;
		public Renderer renderer;
		public AudioEngine audio;
		public InputManager inputManager;

		private readonly string debugFontPath = Path.Combine(Program.basePath, "Skins/goa/fonts/DroidSansFallback.ttf");
		public static Font debugFont;

		public Game()
		{
			Settings settings = SettingsManager.instance;

			Log.Info("Initializing FFmpeg...");
			FFmpegHelper.Init();

			Log.Info("Initializing Audio...");
			audio = new AudioEngine(null, settings.audio.driver,
				(int)settings.audio.sampleRate, (int)settings.audio.bufferLength,
				settings.audio.resampleQuality);
			audio.SetVolume(Math.Min(settings.audio.volume, 100) / 100.0f);
			Log.Info("Audio driver: " + audio.audioDriver.ToString());

			if (settings.outputMode != OutputMode.Render)
			{
				//SDL.SDL_version sdlVersion;
				//SDL.SDL_VERSION(out sdlVersion);

				Log.Info("Initializing SDL video subsystem...");
				if (SDL.SDL_InitSubSystem(SDL.SDL_INIT_VIDEO) != 0)
					throw new ApplicationException("Failed to initialize video subsystem: " + SDL.SDL_GetError());

				int display = 0;
				SDL.SDL_DisplayMode displayMode;

				if (SDL.SDL_GetCurrentDisplayMode(display, out displayMode) != 0)
					throw new ApplicationException("SDL_GetCurrentDisplayMode failed: " + SDL.SDL_GetError());

				RendererFlags rendererFlags = RendererFlags.None;
				int width = (int)settings.video.width;
				int height = (int)settings.video.height;

				if (settings.video.vsync)
					rendererFlags |= RendererFlags.Vsync;

				if (settings.video.mode == VideoMode.Fullscreen)
					rendererFlags |= RendererFlags.Fullscreen;

				if (width * height == 0 || width > displayMode.w || height > displayMode.h)
				{
					width = displayMode.w;
					height = displayMode.h;
				}

				Log.Info("Initializing Game window...");
				string windowTitle = string.Format("{0} {1} ({2}-bit)", Program.name, Program.versionDisplay, (IntPtr.Size * 8).ToString());
				window = new GameWindow(windowTitle, width, height, settings.video.mode);

				Log.Info("Initializing Renderer...");
				renderer = new Renderer(window, width, height, rendererFlags, settings.video.renderer);
				Log.Info("Renderer backend: " + renderer.rendererType.ToString());

				Log.Info("Initializing Input...");
				inputManager = new InputManager();

				Log.Info("Loading debug font...");
				debugFont = new Font(debugFontPath, 24, FontStyle.Normal, true);

				if (settings.engine.tickrate != 0)
					updateInterval = Math.Max(0.0, 1.0 / settings.engine.tickrate);

				if (settings.video.fpsLimit == 0)
					renderInterval = 0.0;
				else if (settings.video.fpsLimit == -1)
					renderInterval = 1.0 / displayMode.refresh_rate;
				else
					renderInterval = Math.Max(0.0, 1.0 / settings.video.fpsLimit);
			}


			Log.Clear();

			if (settings.outputMode == OutputMode.Render)
				sceneManager.Push(new RenderAudioScene(this, settings.playPath, settings.outputPath));
			else if (settings.outputMode == OutputMode.DumpTimestamps)
				sceneManager.Push(new DumpTimestampsScene(this, settings.playPath, settings.outputPath));
			else if (settings.playPath != null)
				sceneManager.Push(new GameplayScene(this, settings.playPath));
			else
				sceneManager.Push(new FileSelectScene(this));
		}

		public void Dispose()
		{
			if (debugFont != null)
				debugFont.Dispose();
			if (sceneManager != null)
				sceneManager.Dispose();
			if (window != null)
				window.Dispose();
			if (renderer != null)
				renderer.Dispose();
			if (audio != null)
				audio.Dispose();
			if (inputManager != null)
				inputManager.Dispose();

			SDL.SDL_Quit();
		}

		double sleepAccum = 0.0;
		public void Run()
		{
			if (sceneManager.currentScene == null)
				return;

			window.Show();

			Stopwatch runTimer = Stopwatch.StartNew();
			double lastTime = 0;
			double lastUpdate = 0;
			double lastRender = 0;

			while (!window.closing)
			{
				double elapsed = runTimer.Elapsed.TotalSeconds;
				double deltaTime = elapsed - lastTime;
				lastTime = elapsed;

				inputManager.Update();

				SDL.SDL_Event sdlEvent;
				while (window.PollEvent(out sdlEvent))
					inputManager.HandleEvent(ref sdlEvent);

				if (updateInterval != 0)
				{
					updateTime += deltaTime;
					if (updateTime >= updateInterval)
					{
						Update(elapsed - lastUpdate);
						lastUpdate = elapsed;

						updateTime -= updateInterval;
					}
				}
				else
					Update(deltaTime);

				if (renderInterval != 0)
				{
					renderTime += deltaTime;
					if (renderTime >= renderInterval)
					{
						Draw(elapsed - lastRender);
						lastRender = elapsed;

						renderTime -= renderInterval;
					}
				}
				else
					Draw(deltaTime);

				if (renderInterval != 0 && updateInterval != 0)
				{
					double nextRender = renderInterval - renderTime;
					double nextUpdate = updateInterval - updateTime;
					double next = Math.Max(Math.Min(nextRender, nextUpdate), 0);

					sleepAccum += next;
					if (sleepAccum > 0)
					{
						double sleepStart = runTimer.Elapsed.TotalSeconds;
						Utility.USleep(1);
						double sleepLength = runTimer.Elapsed.TotalSeconds - sleepStart;
						sleepAccum -= sleepLength;
					}
				}
			}
		}

		long updateTicks = 0;
		public void Update(double deltaTime)
		{
			updateTicks++;
			sceneManager.Update(deltaTime);

			if (sceneManager.currentScene == null)
				window.Dispose();
		}

		const double debugUpdateInterval = 0.5;
		double debugTime;
		double debugFrametime;
		double debugTickrate;
		double debugFrametimeAccum;

		int debugFrametimes;
		public void Draw(double deltaTime)
		{
			debugFrametimeAccum += deltaTime;
			debugFrametimes++;
			debugTime += deltaTime;
			if (debugTime >= debugUpdateInterval)
			{
				debugTime %= debugUpdateInterval;

				debugFrametime = debugFrametimeAccum / debugFrametimes;
				debugFrametimeAccum = debugFrametimes = 0;
				debugTickrate = updateTicks / debugUpdateInterval;
				updateTicks = 0;
			}

			sceneManager.Draw(deltaTime);

			renderer.spriteRenderer.Begin();
			renderer.spriteRenderer.DrawText(debugFont,
				"FPS: " + (1.0 / debugFrametime).ToString("0.0") + " TPS: " + debugTickrate.ToString("0"),
				new Int2(0, 0), Color.White);
			renderer.spriteRenderer.End();

			renderer.Present();
		}
	}
}
