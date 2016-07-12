using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pulsus.Graphics
{
	public class TextureAtlas : IDisposable
	{
		public Texture2D texture { get; }
		public int frameCount { get; }
		public int gridWidth { get; }
		public int gridHeight { get; }

		public TextureAtlas(Texture2D texture, int gridWidth, int gridHeight, int frameCount)
		{
			this.texture = texture;
			this.gridWidth = gridWidth;
			this.gridHeight = gridHeight;
			this.frameCount = frameCount;
		}

		public void Dispose()
		{
			texture.Dispose();
		}

		public Rectangle GetSourceRect(int frame)
		{
			if (frame < 0 || frame >= frameCount)
				throw new IndexOutOfRangeException();

			int x = (frame * gridWidth) % texture.width;
			int y = ((frame * gridWidth) / texture.width) * gridHeight;

			return new Rectangle(x, y, gridWidth, gridHeight);
		}
	}
}
