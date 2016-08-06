using System;

namespace Pulsus.Graphics
{
	public static class TextureAtlas
	{
		/// <summary> Creates an array of sub-textures from texture atlas where sub-images are aligned in a grid pattern</summary>
		/// <param name="texture">Texture atlas</param>
		/// <param name="elementWidth">Width of a single element in the grid</param>
		/// <param name="elementHeight">Height of a single element in the grid</param>
		/// <param name="elementCount">Number of elements in the grid</param>
		public static SubTexture[] CreateFromGrid(Texture2D texture, int elementWidth, int elementHeight, int elementCount)
		{
			if (elementCount <= 0)
				throw new ArgumentOutOfRangeException("Invalid number of elements");

			SubTexture[] subTextures = new SubTexture[elementCount];

			for (int i = 0; i < elementCount; i++)
			{
				int x = (i * elementWidth) % texture.width;
				int y = ((i * elementWidth) / texture.width) * elementHeight;
				subTextures[i] = new SubTexture(texture, new Rectangle(x, y, elementWidth, elementHeight));
			}

			return subTextures;
		}
	}
}
