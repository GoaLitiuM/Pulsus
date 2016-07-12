using System.Collections.Generic;
using System.Threading;

namespace Pulsus.Gameplay
{
	public class Loader : EventPlayer
	{
		Thread loadThread;

		Queue<SoundObject> soundQueue = new Queue<SoundObject>();
		Queue<BGAObject> bitmapQueue = new Queue<BGAObject>();

		HashSet<SoundObject> soundUniques = new HashSet<SoundObject>();
		HashSet<BGAObject> bitmapUniques = new HashSet<BGAObject>();

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
			bitmapQueue.Clear();
			soundUniques.Clear();
			bitmapUniques.Clear();
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

			while (bitmapQueue.Count > 0)
			{
				BGAObject value = bitmapQueue.Dequeue();
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

		public override void OnSoundObject(int eventIndex, SoundEvent value)
		{
			if (!queueObjects)
				return;

			if (value.sound == null)
				return;

			if (value.sound.loaded || soundUniques.Contains(value.sound))
				return;

			soundUniques.Add(value.sound);
			lock (soundQueue)
			{
				soundQueue.Enqueue(value.sound);
			}
		}

		public override void OnImageObject(int eventIndex, BGAEvent value)
		{
			if (disableBGA)
				return;

			if (!queueObjects)
				return;

			if (value.bitmap == null)
				return;

			if (value.bitmap.loaded || bitmapUniques.Contains(value.bitmap))
				return;

			bitmapUniques.Add(value.bitmap);
			lock (bitmapQueue)
			{
				bitmapQueue.Enqueue(value.bitmap);
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
					lock (bitmapQueue)
						count = bitmapQueue.Count;

					while (count > 0)
					{
						BGAObject value = null;
						lock (bitmapQueue)
						{
							value = bitmapQueue.Dequeue();
							count = bitmapQueue.Count;
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
