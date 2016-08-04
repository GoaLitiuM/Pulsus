using System.Collections.Generic;
using System.Threading;

namespace Pulsus.Gameplay
{
	public class Loader : EventPlayer
	{
		Thread loadThread;

		Queue<SoundObject> soundQueue = new Queue<SoundObject>();
		Queue<BGAObject> bgaQueue = new Queue<BGAObject>();

		HashSet<SoundObject> soundUniques = new HashSet<SoundObject>();
		HashSet<BGAObject> bgaUniques = new HashSet<BGAObject>();

		bool loadSounds = true;
		bool loadBgas = true;
		const double preloadAheadTime = 20.0;
		bool disableBGA;

		System.Diagnostics.Stopwatch loadTimer;

		public Loader(Song song)
			: base(song)
		{
			loadThread = new Thread(new ThreadStart(LoadThread));
			loadThread.Name = "ObjectLoaderThread";
			loadThread.IsBackground = true;

			disableBGA = SettingsManager.instance.gameplay.disableBGA;
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

		public void Preload(bool preloadSound = true, bool preloadBga = true, double preloadAheadTime = Loader.preloadAheadTime)
		{
			loadSounds = preloadSound;
			loadBgas = preloadBga;

			if (loadSounds && loadBgas)
				Log.Info("Preloading objects " + preloadAheadTime.ToString() + "s ahead");
			else if (loadSounds)
				Log.Info("Preloading sound objects " + preloadAheadTime.ToString() + "s ahead");
			else if (loadBgas)
				Log.Info("Preloading BGA objects " + preloadAheadTime.ToString() + "s ahead");

			Seek(0.0);
			Seek(preloadAheadTime);
			
			StartPreload();
		}

		public void PreloadAll(bool preloadSound = true, bool preloadBga = true)
		{
			loadSounds = preloadSound;
			loadBgas = preloadBga;

			if (loadSounds && loadBgas)
				Log.Info("Preloading all objects");
			else if (loadSounds)
				Log.Info("Preloading all sound objects");
			else if (loadBgas)
				Log.Info("Preloading all BGA objects");

			Seek(0.0);
			Seek(eventList[eventList.Count-1]);
			StartPreload();
		}

		private void StartPreload()
		{
			if (chart == null)
				return;

			UpdateSong();

			int soundCount = soundQueue.Count;
			int bgaCount = bgaQueue.Count;

			while (soundQueue.Count > 0)
			{
				SoundObject value = soundQueue.Dequeue();
				value.Load(song.path);
			}

			while (bgaQueue.Count > 0)
			{
				BGAObject value = bgaQueue.Dequeue();
				value.Load(song.path);
			}

			Log.Info("Preloaded  {0} sound objects, {1} BGA objects", soundCount, bgaCount);

			loadSounds = true;
			loadBgas = true;
		}

		public override void StartPlayer()
		{
			base.StartPlayer();

			Seek(0.0);
			Seek(eventList[eventList.Count-1]);

			loadTimer = System.Diagnostics.Stopwatch.StartNew();
			loadThread.Start();
		}

		public override void OnSongEnd()
		{
			StopPlayer(false);
		}

		public override void OnPlayerStop(bool forced)
		{
			base.OnPlayerStop(forced);

			if (forced)
			{
				// do not stop the thread until queue is empty
				lock (soundQueue)
				{
					loadThread.Abort();
				}
			}
		}

		public override void OnSoundObject(SoundEvent soundEvent)
		{
			if (!loadSounds)
				return;

			if (soundEvent.sound == null)
				return;

			if (soundEvent.sound.loaded || soundUniques.Contains(soundEvent.sound))
				return;

			soundUniques.Add(soundEvent.sound);
			lock (soundQueue)
			{
				soundQueue.Enqueue(soundEvent.sound);
			}
		}

		public override void OnBGAObject(BGAEvent bgaEvent)
		{
			if (disableBGA)
				return;

			if (!loadBgas)
				return;

			if (bgaEvent.bga == null)
				return;

			if (bgaEvent.bga.loaded || bgaUniques.Contains(bgaEvent.bga))
				return;

			bgaUniques.Add(bgaEvent.bga);
			lock (bgaQueue)
			{
				bgaQueue.Enqueue(bgaEvent.bga);
			}
		}

		private void LoadThread()
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
					sound.Load(song.path);
				else if (bga != null && !bga.loaded)
					bga.Load(song.path);
				else if (!playing && soundQueue.Count == 0 && bgaQueue.Count == 0)
					break;
			}

			loadTimer.Stop();
			Log.Warning("Background loading finished in " + loadTimer.Elapsed.TotalSeconds.ToString() + "s");
		}
	}
}
