using System.IO;

namespace Pulsus.Graphics
{
	public struct FontGlyph
	{
		public int width { get { return maxX - minX; } }
		public int height { get { return maxY - minY; } }

		public int textureId;
		public int textureX;
		public int textureY;	
		public int minX;		// bearing
		public int minY;		// bearing
		public int maxX;
		public int maxY;
		public int advance;     // advance to next glyph

		public void BinaryWrite(BinaryWriter writer)
		{
			writer.Write(textureId);
			writer.Write(textureX);
			writer.Write(textureY);
			writer.Write(minX);
			writer.Write(minY);
			writer.Write(maxX);
			writer.Write(maxY);
			writer.Write(advance);
		}

		public void BinaryRead(BinaryReader reader)
		{
			textureId = reader.ReadInt32();
			textureX = reader.ReadInt32();
			textureY = reader.ReadInt32();
			minX = reader.ReadInt32();
			minY = reader.ReadInt32();
			maxX = reader.ReadInt32();
			maxY = reader.ReadInt32();
			advance = reader.ReadInt32();
		}
	}
}
