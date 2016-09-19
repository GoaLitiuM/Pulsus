using System.Collections.Generic;
using System.IO;
using System.Threading;
using Pulsus.Audio;

namespace Pulsus.Gameplay
{
	public class Loader : EventPlayer
	{
		const double preloadAheadTime = 1.0;

		public bool skipSound = false;
		public bool skipBGA = false;

		AudioEngine audio;

		Queue<SoundObject> soundQueue = new Queue<SoundObject>();
		Queue<BGAObject> bgaQueue = new Queue<BGAObject>();

		HashSet<SoundObject> soundUniques = new HashSet<SoundObject>();
		HashSet<BGAObject> bgaUniques = new HashSet<BGAObject>();

		string basePath;

		Thread loadThread;
		System.Diagnostics.Stopwatch loadTimer;

		// alternate paths where to look up missing files
		static string[] lookupPaths =
		{
			"",			// current directory
			"..\\",		// previous directory (compatibility fix for bms files in sub-folders)
		};

		static string[] lookupAudioExtensions =
		{
			".wav",
			".ogg",
			".m4a",
		};

		public Loader(Chart chart, AudioEngine audio)
			: base(chart)
		{
			this.audio = audio;

			loadThread = new Thread(new ThreadStart(LoadThread));
			loadThread.Name = "BackgroundLoaderThread";
			loadThread.IsBackground = true;

			basePath = chart.basePath;
		}

		public override void Dispose()
		{
			if (loadThread.IsAlive)
				loadThread.Abort();

			soundQueue.Clear();
			bgaQueue.Clear();
			soundUniques.Clear();
			bgaUniques.Clear();
		}

		public void Preload(double preloadAheadTime = Loader.preloadAheadTime)
		{
			Preload(!skipSound, !skipBGA, preloadAheadTime);
		}

		public void Preload(bool preloadSound, bool preloadBga, double preloadAheadTime = Loader.preloadAheadTime)
		{
			if (preloadSound && preloadBga)
				Log.Info("Preloading objects " + preloadAheadTime.ToString() + "s ahead");
			else if (preloadSound)
				Log.Info("Preloading sound objects " + preloadAheadTime.ToString() + "s ahead");
			else if (preloadBga)
				Log.Info("Preloading BGA objects " + preloadAheadTime.ToString() + "s ahead");

			double oldStartTime = startTime;

			Seek(0.0);
			Seek(oldStartTime + preloadAheadTime);

			StartPreload(preloadSound, preloadBga);

			Seek(oldStartTime);
		}

		public void PreloadAll()
		{
			PreloadAll(!skipSound, !skipBGA);
		}

		public void PreloadAll(bool preloadSound, bool preloadBga)
		{
			if (preloadSound && preloadBga)
				Log.Info("Preloading all objects");
			else if (preloadSound)
				Log.Info("Preloading all sound objects");
			else if (preloadBga)
				Log.Info("Preloading all BGA objects");

			double oldStartTime = startTime;

			Seek(0.0);
			SeekEnd();

			StartPreload(preloadSound, preloadBga);

			Seek(oldStartTime);
		}

		private void StartPreload(bool preloadSound, bool preloadBga)
		{
			if (chart == null)
				return;

			bool oldSkipSound = skipSound;
			bool oldSkipBGA = skipBGA;
			skipSound = !preloadSound;
			skipBGA = !preloadBga;

			UpdateSong();

			int soundCount = soundQueue.Count;
			int bgaCount = bgaQueue.Count;

			while (soundQueue.Count > 0)
				LoadSound(soundQueue.Dequeue());

			while (bgaQueue.Count > 0)
				bgaQueue.Dequeue().Load(basePath);

			Log.Info("Preloaded  {0} sound objects, {1} BGA objects", soundCount, bgaCount);

			skipSound = oldSkipSound;
			skipBGA = oldSkipBGA;
		}

		public override void OnPlayerStart()
		{
			SeekEnd();

			loadTimer = System.Diagnostics.Stopwatch.StartNew();
			loadThread.Start();
		}

		public override void OnSoundObject(SoundEvent soundEvent)
		{
			if (skipSound)
				return;

			if (soundEvent.sound == null)
				return;

			if (soundEvent.sound.loaded || soundUniques.Contains(soundEvent.sound))
				return;

			soundUniques.Add(soundEvent.sound);
			lock (soundQueue)
				soundQueue.Enqueue(soundEvent.sound);
		}

		public override void OnBGAObject(BGAEvent bgaEvent)
		{
			if (skipBGA)
				return;

			if (bgaEvent.bga == null)
				return;

			if (bgaEvent.bga.loaded || bgaUniques.Contains(bgaEvent.bga))
				return;

			bgaUniques.Add(bgaEvent.bga);
			lock (bgaQueue)
				bgaQueue.Enqueue(bgaEvent.bga);
		}

		private void LoadSound(SoundObject soundObject)
		{
			string path = Path.Combine(basePath, soundObject.soundFile.path);
			path = Utility.FindRealFile(path, lookupPaths, lookupAudioExtensions);
			if (File.Exists(path))
			{
				SoundData data = audio.LoadFromFile(path);
				soundObject.soundFile.SetData(data);
			}
			else
				Log.Error("Sound file not found: " + soundObject.soundFile.path);
		}

		private void LoadThread()
		{
			try
			{
				while (true)
				{
					SoundObject sound = null;
					BGAObject bga = null;

					if (soundQueue.Count > 0 && Monitor.TryEnter(soundQueue))
					{
						try
						{
							sound = soundQueue.Dequeue();
						}
						finally
						{
							Monitor.Exit(soundQueue);
						}
					}
					else if (bgaQueue.Count > 0 && Monitor.TryEnter(bgaQueue))
					{
						try
						{
							bga = bgaQueue.Dequeue();
						}
						finally
						{
							Monitor.Exit(bgaQueue);
						}
					}

					if (sound != null && !sound.loaded)
						LoadSound(sound);
					else if (bga != null && !bga.loaded)
						bga.Load(basePath);
					else if (!playing && soundQueue.Count == 0 && bgaQueue.Count == 0)
						break;
				}

				loadTimer.Stop();
				Log.Info("Background loading finished in " + loadTimer.Elapsed.TotalSeconds.ToString() + "s");
			}
			catch (ThreadAbortException)
			{
			}
		}
	}
}
