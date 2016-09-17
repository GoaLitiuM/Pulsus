using System.IO;
using Pulsus.Gameplay;
using System.Collections.Generic;

namespace Pulsus
{
	public class DumpTimestampsScene : Scene
	{
		public DumpTimestampsScene(Game game, string inputPath, string outputPath)
			: base(game, false)
		{
			if (string.IsNullOrEmpty(outputPath))
			{
				Log.Error("Output is missing");
				return;
			}

			Chart chart = Chart.Load(inputPath);
			chart.GenerateEvents();

			HashSet<double> uniqueTimestamps = new HashSet<double>();

			using (StreamWriter writer = new StreamWriter(Path.Combine(Program.basePath, outputPath)))
			{
				foreach (Event ev in chart.eventList)
				{
					if (ev is NoteEvent && (ev.GetType() == typeof(NoteEvent) || ev.GetType() == typeof(LongNoteEvent)))
					{
						double timestamp = ev.timestamp;
						if (uniqueTimestamps.Contains(timestamp))
							continue;

						writer.WriteLine((timestamp).ToString("R"));
						uniqueTimestamps.Add(timestamp);
					}
				}
			}
		}

		public override void Dispose()
		{
		}

		public override void Update(double deltaTime)
		{
		}

		public override void Draw(double deltaTime)
		{
		}
	}
}
