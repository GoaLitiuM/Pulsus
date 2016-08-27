using System.Collections.Generic;
using System.Threading;

namespace Pulsus.Gameplay
{
	public class BackgroundLoader : EventPlayer
	{
		const double preloadAheadTime = 20.0;

		public bool skipSound = false;
		public bool skipBGA = false;

		Queue<SoundObject> soundQueue = new Queue<SoundObject>();
		Queue<BGAObject> bgaQueue = new Queue<BGAObject>();

		HashSet<SoundObject> soundUniques = new HashSet<SoundObject>();
		HashSet<BGAObject> bgaUniques = new HashSet<BGAObject>();

		string songBasePath;

		Thread loadThread;
		System.Diagnostics.Stopwatch loadTimer;

		public BackgroundLoader(Song song)
			: base(song)
		{
			loadThread = new Thread(new ThreadStart(LoadThread));
			loadThread.Name = "BackgroundLoaderThread";
			loadThread.IsBackground = true;

			songBasePath = song.path;
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

		public void Preload(double preloadAheadTime = BackgroundLoader.preloadAheadTime)
		{
			Preload(!skipSound, !skipBGA, preloadAheadTime);
		}

		public void Preload(bool preloadSound, bool preloadBga, double preloadAheadTime = BackgroundLoader.preloadAheadTime)
		{
			if (preloadSound && preloadBga)
				Log.Info("Preloading objects " + preloadAheadTime.ToString() + "s ahead");
			else if (preloadSound)
				Log.Info("Preloading sound objects " + preloadAheadTime.ToString() + "s ahead");
			else if (preloadBga)
				Log.Info("Preloading BGA objects " + preloadAheadTime.ToString() + "s ahead");

			double oldStartTime = startTime;
			
			Seek(0.0);
			Seek(oldStartTime+preloadAheadTime);

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
			{
				SoundObject value = soundQueue.Dequeue();
				value.Load(songBasePath);
			}

			while (bgaQueue.Count > 0)
			{
				BGAObject value = bgaQueue.Dequeue();
				value.Load(songBasePath);
			}

			Log.Info("Preloaded  {0} sound objects, {1} BGA objects", soundCount, bgaCount);

			skipSound = oldSkipSound;
			skipBGA = oldSkipBGA;
		}

		public override void StartPlayer()
		{
			if (playing)
				return;

			base.StartPlayer();

			SeekEnd();

			loadTimer = System.Diagnostics.Stopwatch.StartNew();
			loadThread.Start();
		}

		public override void OnSongEnd()
		{
			StopPlayer();
		}

		public override void OnPlayerStop()
		{
			base.OnPlayerStop();

			lock (soundQueue)
			{
				lock (bgaQueue)
				{
					if (soundQueue.Count == 0 && bgaQueue.Count == 0)
						loadThread.Abort();
				}
			}
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
			{
				soundQueue.Enqueue(soundEvent.sound);
			}
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
					sound.Load(songBasePath);
				else if (bga != null && !bga.loaded)
					bga.Load(songBasePath);
				else if (!playing && soundQueue.Count == 0 && bgaQueue.Count == 0)
					break;
			}

			loadTimer.Stop();
			Log.Warning("Background loading finished in " + loadTimer.Elapsed.TotalSeconds.ToString() + "s");
		}
	}
}
