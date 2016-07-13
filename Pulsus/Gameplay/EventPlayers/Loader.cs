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

		bool queueObjects;
		const double preloadAheadTime = 20.0;
		bool disableBGA;

		public Loader(Song song)
			: base(song)
		{
			timeMultiplier = 5000.0;

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

		public void Preload()
		{
			Log.Info("Preloading objects");
			currentTime = preloadAheadTime;
			pulse = chart.GetPulseFromTime(preloadAheadTime);
			StartPreload();
		}

		public void PreloadAll()
		{
			Log.Info("Preloading all objects");
			var last = eventList[eventList.Count-1];
			currentTime = last.timestamp;
			pulse = last.pulse;

			StartPreload();
		}

		private void StartPreload()
		{
			if (chart == null)
				return;

			queueObjects = true;
			UpdateSong();

			while (soundQueue.Count > 0)
			{
				SoundObject value = soundQueue.Dequeue();
				bool success = value.Load(song.path);
			}

			while (bgaQueue.Count > 0)
			{
				BGAObject value = bgaQueue.Dequeue();
				bool success = value.Load(song.path);
			}
		}

		public override void StartPlayer()
		{
			base.StartPlayer();
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
			if (!queueObjects)
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

			if (!queueObjects)
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
				{
					int count = 0;
					lock (soundQueue)
						count = soundQueue.Count;

					while (count > 0)
					{
						SoundObject value = null;
						lock (soundQueue)
						{
							value = soundQueue.Dequeue();
							count = soundQueue.Count;
						}

						if (!value.loaded)
							value.Load(song.path);
					}
				}

				{
					int count = 0;
					lock (bgaQueue)
						count = bgaQueue.Count;

					while (count > 0)
					{
						BGAObject value = null;
						lock (bgaQueue)
						{
							value = bgaQueue.Dequeue();
							count = bgaQueue.Count;
						}

						if (!value.loaded)
							value.Load(song.path);
					}
				}

				// no more objects are getting queued, stop here
				if (!playing)
					break;

				Thread.Sleep(1);
			}
		}
	}
}
