using System;
using System.IO;
using Pulsus.FFmpeg;
using Pulsus.Gameplay;

namespace Pulsus
{
	public class RenderAudioScene : Scene
	{
		BGM songPlayer;
		Player autoplay;

		public RenderAudioScene(Game game, string inputPath, string outputPath)
			: base(game, false)
		{
			if (string.IsNullOrEmpty(outputPath))
			{
				Log.Error("Output is missing");
				return;
			}

			Chart chart = Chart.Load(inputPath);
			chart.GenerateEvents();

			if (string.IsNullOrWhiteSpace(Path.GetExtension(outputPath)))
				outputPath += ".wav";

			// preload all audio
			Loader loader = new Loader(chart, audio);
			loader.PreloadAll(true, false);

			songPlayer = new BGM(chart, audio);
			autoplay = new Player(chart, audio, null, null);

			songPlayer.realtime = false;
			autoplay.realtime = false;

			autoplay.autoplay = true;

			songPlayer.StartPlayer();
			autoplay.StartPlayer();

			songPlayer.Update(1.0f);
			autoplay.Update(1.0f);

			if (songPlayer.playing || autoplay.playing)
				throw new ApplicationException("Players did not finish as expected");

			byte[] audioData = audio.RenderAudio();
			if (audioData.Length > 0)
			{
				FFmpegHelper.SaveSound(outputPath,
					audioData, audioData.Length / 4, audio.audioSpec.freq);
			}
		}

		public override void Dispose()
		{
			autoplay.Dispose();
			songPlayer.Dispose();
		}

		public override void Draw(double deltaTime)
		{
		}

		public override void Update(double deltaTime)
		{
		}
	}
}
