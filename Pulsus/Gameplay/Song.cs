using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace Pulsus.Gameplay
{
	public class Song : IDisposable
	{
		// currently selected data for playback
		public Chart chart;
		
		// path to song folder/package
		public string path { get; private set; }

		public bool repeat = false;

		static Dictionary<string, Type> parsers = new Dictionary<string, Type>
		{
			{ ".bms", typeof(BMSParser) },
			{ ".bme", typeof(BMSParser) },
			{ ".bml", typeof(BMSParser) },
			{ ".pms", typeof(BMSParser) },

			{ ".xxx", typeof(BMSParser) },	// not meant to be played, for sabun makers
		}; 

		static readonly string[] supportedFileFormats = parsers.Keys.ToArray();

		private static string fileSearchPattern;

		public Song(string path)
		{
			this.path = path;

			if (fileSearchPattern == null)
			{
				fileSearchPattern = "";

				foreach (string format in supportedFileFormats)
					fileSearchPattern += format + "|";

				if (fileSearchPattern.Contains("|"))
					fileSearchPattern = fileSearchPattern.Remove(fileSearchPattern.LastIndexOf("|"));
			}
		}

		public void Dispose()
		{
			if (chart != null)
				chart.Dispose();
		}

		public void Load(bool metaOnly = false)
		{
			if (!File.Exists(path))
				throw new ApplicationException("Failed to load song, file doesn't exist: " + path);

			Type parserType;
			string extension = Path.GetExtension(path).ToLowerInvariant();
			if (!parsers.TryGetValue(extension, out parserType))
				throw new ApplicationException("Parser for extension " + extension + " could not be found");

			if (!parserType.IsSubclassOf(typeof(ChartParser)))
				throw new ApplicationException("Parser for extension " + extension + " is not a subclass of " + nameof(ChartParser));

			ChartParser parser = Activator.CreateInstance(parserType) as ChartParser;
			
			Chart data;
			if (metaOnly)
				data = parser.LoadHeaders(path);
			else
				data = parser.Load(path);

			if (data == null)
				throw new ApplicationException("Failed to load song data from: " + path);

			if (chart == null)
				chart = data;
		}

		public void GenerateEvents()
		{
			if (chart == null)
				return;

			chart.eventList = chart.GenerateEvents();
		}
	}
}