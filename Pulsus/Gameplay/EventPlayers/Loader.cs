using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Pulsus.Audio;
using Pulsus.FFmpeg;

namespace Pulsus.Gameplay
{
	public class Loader : EventPlayer
	{
		const double preloadAheadTime = 1.0;

		public bool skipSound = false;
		public bool skipBGA = false;

		AudioEngine audio;
		object rendererLock = new object();

		ConcurrentQueue<SoundObject> soundQueue = new ConcurrentQueue<SoundObject>();
		ConcurrentQueue<BGAObject> bgaQueue = new ConcurrentQueue<BGAObject>();

		HashSet<SoundObject> soundUniques = new HashSet<SoundObject>();
		HashSet<BGAObject> bgaUniques = new HashSet<BGAObject>();

		string basePath;

		Thread[] loadThreads;
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

		static string[] lookupImageExtensions =
		{
			// image formats
			".bmp",
			".png",
			".jpg",
			".tga",

			// video formats
			".gif",
			".mpg",
			".avi",
			".mp4",
			".flv",
			".mkv",
			".wmv",
			".ogv",
			".webm",
			".mov",
			".swf",
			".3gp",
			".asf",
			".m4v",
		};

		public Loader(Chart chart, AudioEngine audio)
			: base(chart)
		{
			this.audio = audio;

			int threads = Utility.GetProcessorThreadCount();
			loadThreads = new Thread[threads];

			basePath = chart.basePath;

			loadTimer = new System.Diagnostics.Stopwatch();
		}

		public override void Dispose()
		{
			for (int i = 0; i < loadThreads.Length; i++)
				if (loadThreads[i].IsAlive)
					loadThreads[i].Abort();

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

			double firstSoundTimestamp = 0.0;
			foreach (Event @event in eventList)
			{
				if (!(@event is SoundEvent))
					continue;

				firstSoundTimestamp = @event.timestamp;
				break;
			}

			double oldStartTime = startTime;
			double preloadEndTime = Math.Max(oldStartTime, firstSoundTimestamp) + preloadAheadTime;

			Seek(0.0);
			Seek(preloadEndTime);

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

			for (int i = 0; i < loadThreads.Length; i++)
			{
				loadThreads[i] = new Thread(new ThreadStart(LoadThread));
				loadThreads[i].Name = "LoaderThread";
				loadThreads[i].IsBackground = true;
			}

			for (int i = 0; i < loadThreads.Length; i++)
				loadThreads[i].Start();

			for (int i = 0; i < loadThreads.Length; i++)
				loadThreads[i].Join();

			Log.Info("Preloaded  {0} sound objects, {1} BGA objects", soundCount, bgaCount);

			skipSound = oldSkipSound;
			skipBGA = oldSkipBGA;
		}

		public override void OnPlayerStart()
		{
			SeekEnd();

			loadTimer.Start();

			for (int i = 0; i < loadThreads.Length; i++)
			{
				loadThreads[i] = new Thread(new ThreadStart(LoadThread));
				loadThreads[i].Name = "LoaderThread";
				loadThreads[i].IsBackground = true;

				loadThreads[i].Start();
			}
		}

		public override void Update(double deltaTime)
		{
			base.Update(deltaTime);

			if (loadTimer.IsRunning && soundQueue.IsEmpty && bgaQueue.IsEmpty)
			{
				loadTimer.Stop();
				Log.Info("Background loading finished in " + loadTimer.Elapsed.TotalSeconds.ToString() + "s");
			}
		}

		public override void OnSoundObject(SoundObject sound)
		{
			if (skipSound)
				return;

			if (sound == null)
				return;

			if (sound.loaded || soundUniques.Contains(sound))
				return;

			soundUniques.Add(sound);
			soundQueue.Enqueue(sound);
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
			bgaQueue.Enqueue(bgaEvent.bga);
		}

		private void LoadSound(SoundObject soundObject)
		{
			string path = Path.Combine(basePath, soundObject.soundFile.path);
			path = Utility.FindRealFile(path, lookupPaths, lookupAudioExtensions);
			if (File.Exists(path))
			{
				try
				{
					SoundData data = audio.LoadFromFile(path);
					soundObject.soundFile.SetData(data);
				}
				catch (ThreadAbortException)
				{
				}
				catch (Exception e)
				{
					Log.Error("Failed to load sound '" + Path.GetFileName(soundObject.soundFile.path) + "': " + e.Message);
					soundObject.soundFile.SetData(new SoundData(new byte[0]));
				}
			}
			else
				Log.Error("Sound file not found: " + soundObject.soundFile.path);
		}

		private void LoadBGA(BGAObject bgaObject)
		{
			string path = Path.Combine(basePath, bgaObject.path);
			path = Utility.FindRealFile(path, lookupPaths, lookupImageExtensions);
			if (File.Exists(path))
			{
				if (Path.GetExtension(bgaObject.path).ToLower() == ".lua")
				{
					Log.Error("Failed to load BGA '" + bgaObject.path + "', scripted BGAs are not supported");
					return;
				}

				FFmpegVideo video = new FFmpegVideo();
				try
				{
					video.Load(path);

					byte[] bytes = video.ReadFrame();

					bgaObject.SetVideo(video);
					lock (rendererLock)
						video.OnNextFrame(bytes);
				}
				catch (ThreadAbortException)
				{
				}
				catch (Exception e)
				{
					Log.Error("Failed to load BGA '" + Path.GetFileName(bgaObject.path) + "': " + e.Message);
				}
				finally
				{
					if (!video.isVideo)
						video.Dispose();
				}
			}
			else
				Log.Warning("BGA file not found: " + bgaObject.path);
		}

		private void LoadThread()
		{
			try
			{
				while (true)
				{
					SoundObject sound = null;
					BGAObject bga = null;

					if (!soundQueue.TryDequeue(out sound))
						bgaQueue.TryDequeue(out bga);

					if (sound != null && !sound.loaded)
						LoadSound(sound);
					else if (bga != null && !bga.loaded)
						LoadBGA(bga);
					else if (!playing && soundQueue.Count == 0 && bgaQueue.Count == 0)
						break;
				}
			}
			catch (ThreadAbortException)
			{
			}
		}
	}
}
