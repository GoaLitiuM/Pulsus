using Jil;
using System.IO;
using System.Text;
using System;

namespace Pulsus.Gameplay
{
	public class BMSONParser : ChartParser
	{
		public override Chart Load(string path)
		{
			BMSONChart chart = null;
			BMSONHeader bmsonHeader = null;

			string version = null;
			try
			{
				using (StreamReader streamReader = new StreamReader(path, Encoding.UTF8))
				{
					if (headerOnly)
						bmsonHeader = JSON.Deserialize<BMSONHeader>(streamReader);
					else
						bmsonHeader = JSON.Deserialize<BMSON>(streamReader);

					if (bmsonHeader != null)
						version = bmsonHeader.version;
				}
			}
			catch (Exception e)
			{
				Log.Error("Failed to parse BMSON file: " + e.Message);
				return null;
			}

			if (bmsonHeader != null)
				chart = new BMSONChart(Directory.GetParent(path).FullName, bmsonHeader);

			if (string.IsNullOrEmpty(version))
				Log.Error("BMSON version is not defined, legacy BMSON files are not supported.");

			return chart;
		}
	}
}
