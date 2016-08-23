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

		static Dictionary<string, Type> parsers = new Dictionary<string, Type>
		{
			{ ".bms", typeof(BMSParser) },
			{ ".bme", typeof(BMSParser) },
			{ ".bml", typeof(BMSParser) },
			{ ".pms", typeof(BMSParser) },
		};

		public static readonly string[] supportedFileFormats = parsers.Keys.ToArray();

		private static string _supportedSearchPattern;
		public static string supportedSearchPattern
		{
			get
			{
				if (_supportedSearchPattern == null)
				{
					_supportedSearchPattern = "";

					foreach (string format in supportedFileFormats)
						_supportedSearchPattern += format + "|";

					if (_supportedSearchPattern.Contains("|"))
						_supportedSearchPattern = _supportedSearchPattern.Remove(_supportedSearchPattern.LastIndexOf("|"));
				}
				return _supportedSearchPattern;
			}
		}

		public Song(string path)
		{
			this.path = path;
		}

		public void Dispose()
		{
			if (chart != null)
				chart.Dispose();
		}

		public void Load(bool headerOnly = false)
		{
			if (!File.Exists(path))
				throw new ApplicationException("Failed to load song, file doesn't exist: " + path);

			Type parserType;
			string extension = Path.GetExtension(path).ToLowerInvariant();
			if (!parsers.TryGetValue(extension, out parserType))
				throw new ApplicationException("Parser for extension " + extension + " could not be found");

			if (!parserType.IsSubclassOf(typeof(ChartParser)))
				throw new ApplicationException("Parser " + parserType.Name + " is not a subclass of " + nameof(ChartParser));

			ChartParser parser = Activator.CreateInstance(parserType) as ChartParser;
			parser.headerOnly = headerOnly;

			chart = parser.Load(path);
			if (chart == null)
				throw new ApplicationException("Failed to load chart data from: " + path);
		}

		public void GenerateEvents()
		{
			if (chart == null)
				return;

			chart.eventList = chart.GenerateEvents();
		}
	}
}