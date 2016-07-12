using System;
using System.Runtime.InteropServices;
using SDL2;

namespace Pulsus
{
	public class GameWindow : IDisposable
	{
		public bool closing;

		private IntPtr handle;

		public string Title
		{
			get	{ return SDL.SDL_GetWindowTitle(handle); }
			set { SDL.SDL_SetWindowTitle(handle, value); }
		}

		public Int2 ClientSize
		{
			get
			{
				int width, height;
				GetClientSize(out width, out height);
				return new Int2(width, height);
			}
			set
			{
				SetClientSize(value.x, value.y);
			}
		}
	
		public GameWindow(string title)
		{
			CreateWindow(title, int.MaxValue, int.MaxValue, 640, 480, VideoMode.Windowed);
		}

		public GameWindow(string title, int width, int height, VideoMode mode = VideoMode.Windowed)
		{
			CreateWindow(title, int.MaxValue, int.MaxValue, width, height, mode);
		}

		public GameWindow(string title, int x, int y, int width, int height, VideoMode mode = VideoMode.Windowed)
		{
			CreateWindow(title, x, y, width, height, mode);
		}

		private void CreateWindow(string title, int x, int y, int width, int height, VideoMode mode)
		{
			if (x == int.MaxValue)
				x = SDL.SDL_WINDOWPOS_CENTERED;
			if (y == int.MaxValue)
				y = SDL.SDL_WINDOWPOS_CENTERED;

			SDL.SDL_WindowFlags flags = SDL.SDL_WindowFlags.SDL_WINDOW_SHOWN & SDL.SDL_WindowFlags.SDL_WINDOW_OPENGL;
			if (mode == VideoMode.Borderless)
				flags |= SDL.SDL_WindowFlags.SDL_WINDOW_BORDERLESS;
			//else if (mode == VideoMode.Fullscreen)
			//	flags |= SDL.SDL_WindowFlags.SDL_WINDOW_FULLSCREEN;

			handle = SDL.SDL_CreateWindow(title, x, y, width, height, flags);

			if (handle == IntPtr.Zero)
				throw new ApplicationException("SDL_CreateWindow failed: " + SDL.SDL_GetError());

			// Windows only: spawning settings window early may cause
			// the game window to not appear on foreground.

			SDL.SDL_SysWMinfo wmi = GetPlatformWindowInfo();
			if (wmi.subsystem == SDL.SDL_SYSWM_TYPE.SDL_SYSWM_WINDOWS)
				SetForegroundWindow(wmi.info.win.window); 

			Title = title;
		}

		~GameWindow()
		{
			Dispose();
		}

		public void Dispose()
		{
			if (handle == IntPtr.Zero)
				return;

			SDL.SDL_DestroyWindow(handle);
			handle = IntPtr.Zero;

			closing = true;
		}

		public SDL.SDL_SysWMinfo GetPlatformWindowInfo()
		{
			SDL.SDL_SysWMinfo wmi = new SDL.SDL_SysWMinfo();
			SDL.SDL_VERSION(out wmi.version);
			if (SDL.SDL_GetWindowWMInfo(handle, ref wmi) != SDL.SDL_bool.SDL_TRUE)
				throw new ApplicationException("SDL_GetWindowWMInfo failed");
			return wmi;
		}

		public bool PollEvent(out SDL.SDL_Event sdlEvent)
		{
			bool ret = (SDL.SDL_PollEvent(out sdlEvent) != 0);
			if (ret && sdlEvent.type == SDL.SDL_EventType.SDL_QUIT)
				closing = true;
			return ret;
		}

		private void GetClientSize(out int width, out int height)
		{
			SDL.SDL_GetWindowSize(handle, out width, out height);
		}

		private void SetClientSize(int width, int height)
		{
			SDL.SDL_SetWindowSize(handle, width, height);
		}

		public void Show()
		{
			SDL.SDL_ShowWindow(handle);
		}

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		static extern bool SetForegroundWindow(IntPtr hWnd);
	}

	public enum VideoMode : int
	{
		Fullscreen = 0,
		Windowed = 1,
		Borderless = 2,
	}
}
