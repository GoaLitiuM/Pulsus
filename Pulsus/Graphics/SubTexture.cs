namespace Pulsus.Graphics
{
	public class SubTexture
	{
		public Texture2D texture { get; }
		public Rectangle sourceRect { get; }

		public int width { get { return sourceRect.width; } }
		public int height { get { return sourceRect.height; } }

		internal SubTexture(Texture2D texture, Rectangle sourceRect)
		{
			this.texture = texture;
			this.sourceRect = sourceRect;
		}
	}
}
