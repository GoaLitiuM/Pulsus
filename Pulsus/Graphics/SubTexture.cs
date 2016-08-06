namespace Pulsus.Graphics
{
	public class SubTexture
	{
		public Texture2D texture { get; }
		public Rectangle sourceRect { get; }

		internal SubTexture(Texture2D texture, Rectangle sourceRect)
		{
			this.texture = texture;
			this.sourceRect = sourceRect;
		}
	}
}
