using System;
using Pulsus.Graphics;
using Pulsus.Audio;
using Pulsus.Input;

namespace Pulsus
{
	public abstract class Scene : IDisposable
	{
		public bool active = true;

		protected readonly Game game;
		protected readonly GameWindow window;
		protected readonly Renderer renderer;
		protected readonly AudioEngine audio;
		protected readonly InputManager input;

		public Scene(Game game)
		{
			this.game = game;
			window = game.window;
			renderer = game.renderer;
			audio = game.audio;
			input = game.inputManager;
		}

		public abstract void Update(double deltaTime);
		public abstract void Draw(double deltaTime);
		public abstract void Dispose();
	}
}
