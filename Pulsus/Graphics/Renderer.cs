using System;
using System.IO;
using System.Runtime.InteropServices;
using SDL2;
using SharpBgfx;

namespace Pulsus.Graphics
{
	public class Renderer : IDisposable
	{
		GameWindow window;

		Uniform textureColor;

		public ShaderProgram defaultProgram { get; private set; }
		
		public SpriteRenderer spriteRenderer { get; internal set; }
		public RendererType rendererType { get { return (RendererType)Bgfx.GetCurrentBackend(); } }

		public static RenderState AlphaBlendNoDepth =
			RenderState.BlendNormal | RenderState.CullClockwise |
			RenderState.ColorWrite | RenderState.AlphaWrite;

		public static RenderState AlphaBlend = AlphaBlendNoDepth |
			RenderState.DepthWrite | RenderState.DepthTestLess;

		public Renderer(GameWindow sdlWindow, int width, int height, RendererFlags flags = RendererFlags.None, RendererType type = RendererType.Default)
		{
			window = sdlWindow;

			// retrieve platform specific data and pass them to bgfx
			SDL.SDL_SysWMinfo wmi = window.GetPlatformWindowInfo();
			PlatformData platformData = new PlatformData();
			switch (wmi.subsystem)
			{
				case SDL.SDL_SYSWM_TYPE.SDL_SYSWM_WINDOWS:
					platformData.WindowHandle = wmi.info.win.window;
					break;
				case SDL.SDL_SYSWM_TYPE.SDL_SYSWM_X11:
					platformData.DisplayType = wmi.info.x11.display;
					platformData.WindowHandle = wmi.info.x11.window;
					break;
				case SDL.SDL_SYSWM_TYPE.SDL_SYSWM_COCOA:
					platformData.WindowHandle = wmi.info.cocoa.window;
					break;
				default:
					throw new ApplicationException("Failed to initialize renderer, unsupported platform detected");
			}

			if (platformData.WindowHandle == IntPtr.Zero)
				throw new ApplicationException("Failed to initialize renderer, invalid platform window handle");

			Bgfx.SetPlatformData(platformData);
			Bgfx.Init((RendererBackend)type);

			if (width * height <= 0)
			{
				Int2 windowSize = window.ClientSize;
				width = windowSize.x;
				height = windowSize.y;
			}

			Reset(width, height, flags);
			spriteRenderer = new SpriteRenderer(this, width, height);

			string shaderPath = "";
			switch (rendererType)
			{
				case RendererType.Direct3D11:
					shaderPath = Path.Combine("Shaders", "dx11");
					break;
				case RendererType.Direct3D9:
					shaderPath = Path.Combine("Shaders", "dx9");
					break;
				case RendererType.OpenGL:
					shaderPath = Path.Combine("Shaders", "opengl");
					break;
				default:
					break;
			}
	
			defaultProgram = new ShaderProgram(
				Path.Combine(shaderPath, "default_vs.bin"),
				Path.Combine(shaderPath, "default_fs.bin"));

			textureColor = new Uniform("s_texColor", UniformType.Int1);

			//for (int i=0; i<256; i++)
			//	Bgfx.SetViewSequential((byte)i, true);
		}

		public void Dispose()
		{
			defaultProgram.Dispose();
			textureColor.Dispose();
			spriteRenderer.Dispose();

			Bgfx.Shutdown();
		}

		public void Reset(int width, int height, RendererFlags flags)
		{
			Bgfx.Reset(width, height, (ResetFlags)flags);
		}

		public void Present()
		{
			Bgfx.Frame();
			spriteRenderer.currentViewport = 0;
		}
		
		public void SetViewport(int viewport, int x, int y, int width, int height)
		{
			Bgfx.SetViewRect((byte)viewport, x, y, width, height);
		}

		public void SetViewTransform(int viewport, Matrix4 view)
		{
			unsafe
			{
				Bgfx.SetViewTransform((byte)viewport, view.Pointer(), null);
			}
		}

		public void SetViewTransform(int viewport, Matrix4 view, Matrix4 projection)
		{
			unsafe
			{
				Bgfx.SetViewTransform((byte)viewport, view.Pointer(), projection.Pointer());
			}
		}

		public void SetProjectionTransform(int viewport, Matrix4 projection)
		{
			unsafe
			{
				Bgfx.SetViewTransform((byte)viewport, null, projection.Pointer());
			}
		}

		/// <summary> Clears color and depth buffers. </summary>
		public void Clear(int viewport, Color color)
		{
			Clear(viewport, color, ClearTargets.Color | ClearTargets.Depth);
		}

		public void Clear(int viewport, Color color, ClearTargets clearTargets)
		{
			Bgfx.SetViewClear((byte)viewport, clearTargets, (int)color.GetRGBA());
			Bgfx.Touch((byte)viewport);
		}

		public void SetTranform(Matrix4 matrix)
		{
			unsafe
			{
				int cacheIndex = Bgfx.SetTransform(matrix.Pointer());
			}
		}

		public void SetVertexBuffer(VertexBuffer vb, int startIndex = 0, int count = -1)
		{
			Bgfx.SetVertexBuffer(vb.handle, startIndex, count);
		}

		public void SetVertexBuffer(TransientVertexBuffer vb, int startIndex = 0, int count = -1)
		{
			Bgfx.SetVertexBuffer(vb, startIndex, count);
		}

		/*public void SetVertexBuffer(DynamicVertexBuffer vb, int count = -1)
		{
			Bgfx.SetVertexBuffer(vb.vbHandle, count);
		}*/

		public void SetIndexBuffer(IndexBuffer ib, int startIndex = 0, int count = -1)
		{
			Bgfx.SetIndexBuffer(ib.handle, startIndex, count);
		}

		public void SetIndexBuffer(TransientIndexBuffer ib, int startIndex = 0, int count = -1)
		{
			Bgfx.SetIndexBuffer(ib, startIndex, count);
		}


		/*public void SetIndexBuffer(DynamicIndexBuffer ib, int startIndex = 0, int count = -1)
		{
			Bgfx.SetIndexBuffer(ib.ibHandle, startIndex, count);
		}*/

		public void SetRenderState(RenderState state)
		{
			Bgfx.SetRenderState(state);
		}

		public void SetRenderState(RenderState state, Color color)
		{
			Bgfx.SetRenderState(state, (int)color.GetRGBA());
		}

		public void SetTexture(int id, Texture2D texture)
		{
			Bgfx.SetTexture((byte)id, textureColor, texture.handle);
		}

		public void SetTexture(int id, Texture2D texture, TextureFlags flags)
		{
			Bgfx.SetTexture((byte)id, textureColor, texture.handle, flags);
		}

		public void SetTexture(int id, Texture2D texture, Uniform sampler)
		{
			Bgfx.SetTexture((byte)id, sampler, texture.handle);
		}

		public void SetViewFrameBuffer(int viewport, FrameBuffer framebuffer)
		{
			if (framebuffer != null)
				Bgfx.SetViewFrameBuffer((byte)viewport, framebuffer.handle);
			else
				Bgfx.SetViewFrameBuffer((byte)viewport, SharpBgfx.FrameBuffer.Invalid);
		}

		public void SetUniform(Uniform uniform, int value)
		{
			unsafe
			{
				GCHandle handle = GCHandle.Alloc(value, GCHandleType.Pinned);	
				Bgfx.SetUniform(uniform, handle.AddrOfPinnedObject());
				handle.Free();
			}
		}

		public void SetUniform(Uniform uniform, float value)
		{
			Bgfx.SetUniform(uniform, value);
		}

		public void SetUniform(Uniform uniform, Float4 value)
		{
			unsafe
			{
				IntPtr ptr = Marshal.AllocHGlobal(sizeof(Float4));
				Marshal.StructureToPtr(value, ptr, true);		
				Bgfx.SetUniform(uniform, ptr);
				Marshal.FreeHGlobal(ptr);
			}
		}

		public void SetUniform(Uniform uniform, Matrix4 value)
		{
			unsafe
			{
				IntPtr ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal(sizeof(Matrix4));
				Bgfx.SetUniform(uniform, ptr);
				System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr);
			}
		}

		public void SetTextureColor(Color color)
		{
			SetUniform(textureColor, color.AsFloat4());
		}

		public void Submit(int viewport, ShaderProgram program)
		{
			Bgfx.Submit((byte)viewport, program.programHandle);
		}
	}

	public enum RendererType
	{
		Default = RendererBackend.Default,
		Direct3D11 = RendererBackend.Direct3D11,
		Direct3D9 = RendererBackend.Direct3D9,
		OpenGL = RendererBackend.OpenGL,
	}

	[Flags]
	public enum RendererFlags
	{
		None = ResetFlags.None,
		Fullscreen = ResetFlags.Fullscreen,
		Vsync = ResetFlags.Vsync,
		Flip = ResetFlags.FlipAfterRender,
		Flush = ResetFlags.FlushAfterRender,
	}
}
