using System;
using System.Collections.Generic;

namespace Pulsus
{
	public class SceneManager : IDisposable
	{
		private Stack<Scene> sceneStack = new Stack<Scene>();

		public Scene currentScene;

		// first Update() and Draw() should be skipped
		// after scene change due to reset erratic deltaTime.
		private int sceneChanged = 0;

		public SceneManager()
		{
		}

		public void Dispose()
		{
			while (currentScene != null)
			{
				currentScene.Dispose();
				Pop();
			}
		}

		public void Push(Scene scene)
		{
			if (!scene.isActive)
				return;

			if (currentScene != null)
				sceneStack.Push(currentScene);

			if (currentScene != scene && currentScene != null)
				sceneChanged = 2;

			currentScene = scene;
		}

		private bool Pop()
		{
			if (currentScene != null)
				currentScene.Dispose();

			if (sceneStack.Count > 0)
				currentScene = sceneStack.Pop();
			else
				currentScene = null;

			return (currentScene != null);
		}

		public void Update(double deltaTime)
		{
			if (currentScene == null || !currentScene.isActive)
			{
				if (!Pop())
					return;
			}

			if (sceneChanged == 0)
				currentScene.Update(deltaTime);
			else
				sceneChanged--;
		}

		public void Draw(double deltaTime)
		{
			if (currentScene == null || !currentScene.isActive)
				return;

			if (sceneChanged == 0)
				currentScene.Draw(deltaTime);
			else
				sceneChanged--;
		}
	}
}
