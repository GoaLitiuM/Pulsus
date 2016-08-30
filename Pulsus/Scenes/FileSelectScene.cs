using System;
using SDL2;
using System.IO;
using Pulsus.Gameplay;
using Eto.Forms;

namespace Pulsus
{
	public class FileSelectScene : Scene
	{
		public FileSelectScene(Game game) : base(game)
		{
			Settings settings = SettingsManager.instance;

			if (settings.songPaths.Count > 0)
			{
				string startPath = settings.songPaths[0];
				if (!Path.IsPathRooted(startPath))
					startPath = Path.Combine(Program.basePath, startPath);

				if (Directory.Exists(startPath))
					Environment.CurrentDirectory = startPath;
			}
		}

		public override void Dispose()
		{
		}

		private void ShowDialog()
		{
			string filePath = null;
			DialogResult result = DialogResult.Cancel;
			Program.EtoInvoke(() =>
			{
				OpenFileDialog dialog = new OpenFileDialog();
				dialog.Title = "Open Song...";
				dialog.CheckFileExists = true;
				dialog.Filters.Add(new FileDialogFilter(
					"All Supported Files",
					new string[] { ".bms", ".bme", ".bml", ".pms", ".bmson" }));
				dialog.Filters.Add(new FileDialogFilter(
					"Be-Music Source Files",
					new string[] { ".bms", ".bme", ".bml", ".pms" }));
				dialog.Filters.Add(new FileDialogFilter(
					"BMSON Files",
					new string[] { ".bmson" }));
				
				result = dialog.ShowDialog(Program.etoApplication.MainForm);
				filePath = dialog.FileName;
			});

			game.window.Focus();

			if (result != DialogResult.Ok)
			{
				Close();
				return;
			}

			game.sceneManager.Push(new GameplayScene(game, new Song(filePath)));
		}

		public override void Update(double deltaTime)
		{
			if (input.KeyPressed(SDL.SDL_Scancode.SDL_SCANCODE_ESCAPE))
			{
				Close();
				return;
			}

			// select file dialog
			ShowDialog();
		}

		public override void Draw(double deltaTime)
		{
		}
	}
}
