using SDL2;
using System;

namespace Pulsus
{
	class UpdateWindow
	{
		public static void Show(UpdateInfo updateInfo, out bool updateApplied)
		{
			SDL.SDL_MessageBoxButtonData[] buttons =
			{
				new SDL.SDL_MessageBoxButtonData()
				{
					buttonid = 0,
					flags = SDL.SDL_MessageBoxButtonFlags.SDL_MESSAGEBOX_BUTTON_ESCAPEKEY_DEFAULT,
					text = "No",
				},
				new SDL.SDL_MessageBoxButtonData()
				{
					buttonid = 1,
					flags = SDL.SDL_MessageBoxButtonFlags.SDL_MESSAGEBOX_BUTTON_RETURNKEY_DEFAULT,
					text = "Yes",
				},
			};

			SDL.SDL_MessageBoxData data = new SDL.SDL_MessageBoxData
			{
				flags = SDL.SDL_MessageBoxFlags.SDL_MESSAGEBOX_INFORMATION,
				window = IntPtr.Zero,
				title = Program.name + " Update",
				message =
					"New update available for " + Program.name + "\n\n" +
					"Download and apply update " + Utility.GetVersionString(updateInfo.version) + "?",
				numbuttons = buttons.Length,
				buttons = buttons,
				colorScheme = null
			};

			int result = -1;
			SDL.SDL_ShowMessageBox(ref data, out result);

			if (result == 1)
			{
				string updateFile = Updater.DownloadUpdate(updateInfo);
				Updater.ApplyUpdate(updateFile, updateInfo);
				updateApplied = true;
			}
			else
				updateApplied = false;
		}
	}
}
