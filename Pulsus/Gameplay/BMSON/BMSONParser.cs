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

			try
			{
				using (StreamReader streamReader = new StreamReader(path, Encoding.UTF8))
				{
					if (headerOnly)
						bmsonHeader = JSON.Deserialize<BMSONHeader>(streamReader);
					else
						bmsonHeader = JSON.Deserialize<BMSON>(streamReader);
				}
			}
			catch (Exception e)
			{
				Log.Error("Failed to parse BMSON file: " + e.Message);
				return null;
			}

			if (bmsonHeader != null)
				chart = new BMSONChart(Directory.GetParent(path).FullName, bmsonHeader);

			return chart;
		}
	}
}
