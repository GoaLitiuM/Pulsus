using System;
using SDL2;
using System.IO;
using Pulsus.Gameplay;
using Eto.Forms;

namespace Pulsus
{
	public class FileSelectScene : Scene
	{
		string lastPath;
		public FileSelectScene(Game game) : base(game)
		{
			lastPath = Environment.CurrentDirectory;
			Settings settings = SettingsManager.instance;

			if (settings.songPaths.Count > 0)
				lastPath = settings.songPaths[0];
		}

		public override void Dispose()
		{
		}

		private void ShowDialog(string path)
		{
			path = Path.GetFullPath(path).TrimEnd(new char[] { '/', '\\', });
			if (!File.Exists(path))
				path = null;

			string oldCurrentDirectory = Environment.CurrentDirectory;

			string filePath = null;
			DialogResult result = DialogResult.Cancel;
			Program.EtoInvoke(() =>
			{
				OpenFileDialog dialog = new OpenFileDialog();
				dialog.Title = "Open Song...";
				dialog.CheckFileExists = true;
				dialog.Filters.Add(new FileDialogFilter(
					"Be-Music Source Files",
					new string[] { ".bms", ".bme", ".bml", ".pms" }));
				
				//dialog.Directory = new Uri(path);
				dialog.FileName = path;
				result = dialog.ShowDialog(Program.etoApplication.MainForm);
				filePath = dialog.FileName;
			});

			// file dialog messes up the current directory path under Linux systems
			Environment.CurrentDirectory = oldCurrentDirectory;

			game.window.Focus();

			if (result != DialogResult.Ok)
			{
				active = false;
				return;
			}

			lastPath = Directory.GetParent(filePath).FullName;

			game.sceneManager.Push(new GameplayScene(game, new Song(filePath)));
		}

		public override void Update(double deltaTime)
		{
			if (input.KeyPressed(SDL.SDL_Scancode.SDL_SCANCODE_ESCAPE))
			{
				active = false;
				return;
			}

			// select file dialog
			ShowDialog(lastPath);
		}

		public override void Draw(double deltaTime)
		{
		}
	}
}
