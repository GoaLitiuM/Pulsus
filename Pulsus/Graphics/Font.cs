using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Pulsus.FFmpeg;
using SDL2;

namespace Pulsus.Graphics
{
	public class Font : IDisposable
	{
		private const int version = 2;

		public string familyName { get; private set; }
		public int pointSize { get; private set; }
		public FontGlyph[] glyphs { get; private set; }
		public FontStyle style { get; private set; }

		public List<Texture2D> textures = new List<Texture2D>();
		private IntPtr handle;
		private bool unicode;

		private const int textureSize = 1024;
		private static int loadedFonts = 0;
		private static bool cacheFonts;

		public Font(string path, int pointSize, FontStyle style = FontStyle.Normal, bool unicode = true)
		{
			cacheFonts = SettingsManager.instance.engine.cacheFonts;
			this.pointSize = pointSize;
			this.style = style;
			this.unicode = unicode;

			if (SDL_ttf.TTF_WasInit() == 0)
			{
				if (SDL_ttf.TTF_Init() != 0)
					throw new ApplicationException("Failed to initialize SDL_ttf: " + SDL.SDL_GetError());
			}

			handle = SDL_ttf.TTF_OpenFont(path, pointSize);
			if (handle == IntPtr.Zero)
				throw new ApplicationException("Failed to load font: " + SDL.SDL_GetError());

			SDL_ttf.TTF_SetFontStyle(handle, (int)style);

			familyName = SDL_ttf.TTF_FontFaceFamilyName(handle);

			ushort characterCount = unicode ? ushort.MaxValue : (ushort)256;

			bool generateTextures = true;
			if (cacheFonts)
			{
				if (LoadGlyphDataCached() && LoadTextureDataCached())
				{
					Log.Info("Font: Using cached font data");
					generateTextures = false;
				}
				else
				{
					glyphs = null;
					foreach (Texture2D texture in textures)
						texture.Dispose();
					textures.Clear();
				}
			}

			if (generateTextures)
			{
				GenerateGlyphData(characterCount);
				GenerateTextures(characterCount);

				if (cacheFonts)
					CacheGlyphData();
			}

			loadedFonts++;
		}

		public void Dispose()
		{
			if (handle == IntPtr.Zero)
				return;

			foreach (Texture2D texture in textures)
				texture.Dispose();
			textures.Clear();

			SDL_ttf.TTF_CloseFont(handle);
			handle = IntPtr.Zero;

			// unload SDL_ttf
			loadedFonts--;
			if (loadedFonts <= 0)
				SDL_ttf.TTF_Quit();
		}

		private void GenerateGlyphData(ushort characterCount)
		{
			Log.Info("Font: Generating glyph data for {0} characters", characterCount);

			glyphs = new FontGlyph[characterCount];

			for (ushort i = 0; i < characterCount; ++i)
			{
				if (SDL_ttf.TTF_GlyphIsProvided(handle, i) == 0)
					continue;

				if (SDL_ttf.TTF_GlyphMetrics(handle, i,
						out glyphs[i].minX, out glyphs[i].maxX, out glyphs[i].minY, out glyphs[i].maxY,
						out glyphs[i].advance) != 0)
				{
					throw new ApplicationException("Failed to get glyph metrics for glyph " + i + " : " + SDL.SDL_GetError());
				}
			}
		}

		private void GenerateTextures(ushort characterCount)
		{
			Log.Info("Font: Generating font textures");

			byte[] textureData = new byte[textureSize * textureSize * 4];
			textures.Add(new Texture2D(textureSize, textureSize));

			// sort the glyphs starting form widest one, prioritize
			// ASCII characters into first texture

			Comparison<ushort> glyphComparison = new Comparison<ushort>((g1, g2) =>
			{
				if (glyphs[g1].width < glyphs[g2].width && glyphs[g1].height < glyphs[g2].height)
					return 1;
				else if
					(glyphs[g1].width > glyphs[g2].width && glyphs[g1].height > glyphs[g2].height)
					return -1;
				else
					return 0;
			});

			List<ushort> sortedGlyphs = new List<ushort>(characterCount);
			for (ushort i = 0; i < Math.Min((ushort)256, characterCount); ++i)
				sortedGlyphs.Add(i);

			sortedGlyphs.Sort(glyphComparison);

			if (characterCount > 256)
			{
				List<ushort> sortedGlyphs2 = new List<ushort>(characterCount - 256);
				for (ushort i = 256; i < characterCount; ++i)
					sortedGlyphs2.Add(i);

				sortedGlyphs2.Sort(glyphComparison);

				sortedGlyphs.AddRange(sortedGlyphs2);
			}

			// render glyphs

			int[] columnPixels = new int[textureSize];
			int x = 0;
			int currentTexture = 0;
			Texture2D texture = textures[0];

			for (ushort g = 0; g < sortedGlyphs.Count; ++g)
			{
				ushort i = sortedGlyphs[g];
				if (glyphs[i].width * glyphs[i].height <= 0)
					continue;

				IntPtr surface = GetGlyphSurface(i);
				if (surface == IntPtr.Zero)
					continue;

				int surfaceWidth, surfaceHeight;
				byte[] glyphData = GetGlyphData(surface, glyphs[i], out surfaceWidth, out surfaceHeight);
				SDL.SDL_FreeSurface(surface);

				if (x + surfaceWidth >= textureSize)
					x = 0; // place the pen position back to left

				// find free spot 
				int y = columnPixels[x];
				for (int j = x + 1; j < x + surfaceWidth; j++)
				{
					if (columnPixels[j] > y)
						y = columnPixels[j];
				}

				if (y + surfaceHeight >= textureSize)
				{
					// texture is full, update it and move to next texture

					UpdateTexture(textureData, texture, currentTexture, textureSize, textureSize);

					for (int j = 0; j < textureSize * textureSize * 4; j++)
						textureData[j] = 0;

					for (int j = 0; j < textureSize; j++)
						columnPixels[j] = 0;

					textures.Add(new Texture2D(textureSize, textureSize));
					currentTexture++;
					texture = textures[currentTexture];

					x = 0;
					y = 0;
				}

				glyphs[i].textureId = currentTexture;
				glyphs[i].textureX = x;
				glyphs[i].textureY = y;

				// copy glyph data
				for (int j = 0; j < surfaceHeight; j++)
					Array.Copy(glyphData, j * surfaceWidth * 4, textureData, (j * textureSize * 4) + (y * textureSize * 4) + (x * 4), surfaceWidth * 4);

				for (int j = x; j < x + surfaceWidth; j++)
					columnPixels[j] = y + surfaceHeight;

				x += surfaceWidth;
			}

			UpdateTexture(textureData, texture, currentTexture, textureSize, textureSize);
		}

		private void UpdateTexture(byte[] data, Texture2D texture, int textureId, int width, int height)
		{
			texture.SetData(data, 0, 0, width, height);

			if (cacheFonts)
				CacheTextureData(data, textureId);
		}

		private string GetCacheFilename()
		{
			return familyName + "_" + pointSize.ToString() + "_" + (unicode ? "U" : "A") + ((int)style).ToString();
		}

		private bool LoadGlyphDataCached()
		{
			string glyphPath = Path.Combine(Program.cachePath, GetCacheFilename() + ".bin");

			if (!File.Exists(glyphPath))
				return false;

			using (Stream stream = File.Open(glyphPath, FileMode.Open))
			{
				using (BinaryReader reader = new BinaryReader(stream))
				{
					int fileVersion = reader.ReadInt32();
					if (fileVersion != version)
						return false;

					int length = reader.ReadInt32();
					glyphs = new FontGlyph[length];

					for (int i = 0; i < glyphs.Length; ++i)
						glyphs[i].BinaryRead(reader);
				}
			}

			return true;
		}

		private bool LoadTextureDataCached()
		{
			string fileTemplate = GetCacheFilename();

			int id = 0;
			while (true)
			{
				string texturePath = Path.Combine(Program.cachePath, fileTemplate + "_" + id.ToString() + ".png");
				if (!File.Exists(texturePath))
					break;

				Texture2D texture = new Texture2D(texturePath);
				if (texture == null)
					break;

				textures.Add(texture);
				id++;
			}
			return textures.Count > 0;
		}

		private void CacheGlyphData()
		{
			Log.Info("Font: Saving glyph data to cache...");
			string glyphPath = Path.Combine(Program.cachePath, GetCacheFilename() + ".bin");

			using (Stream stream = File.Open(glyphPath, FileMode.Create, FileAccess.Write))
			{
				using (BinaryWriter writer = new BinaryWriter(stream))
				{
					writer.Write(version);
					writer.Write(glyphs.Length);

					for (int i = 0; i < glyphs.Length; ++i)
						glyphs[i].BinaryWrite(writer);
				}
			}
		}

		private void CacheTextureData(byte[] data, int textureId)
		{
			Log.Info("Font: Saving texture data to cache...");
			string texturePath = Path.Combine(Program.cachePath, GetCacheFilename() + "_" + textureId.ToString() + ".png");
			FFmpegHelper.SaveImagePNG(texturePath, data, textureSize, textureSize);
		}

		private IntPtr GetGlyphSurface(ushort index)
		{
			SDL.SDL_Color color = new SDL.SDL_Color();
			color.r = color.g = color.b = color.a = 255;

			IntPtr surfacePtr = SDL_ttf.TTF_RenderGlyph_Blended(handle, index, color);
			if (surfacePtr == IntPtr.Zero)
				return IntPtr.Zero;//throw new Exception("Failed to render glyph: " + SDL.SDL_GetError());

			SDL.SDL_Surface surface = Marshal.PtrToStructure<SDL.SDL_Surface>(surfacePtr);
			SDL.SDL_PixelFormat surfaceFormat = Marshal.PtrToStructure<SDL.SDL_PixelFormat>(surface.format);

			if (surfaceFormat.format == SDL.SDL_PIXELFORMAT_INDEX8)
			{
				IntPtr convertedSurface = SDL.SDL_ConvertSurfaceFormat(surfacePtr, SDL.SDL_PIXELFORMAT_ARGB8888, 0);

				SDL.SDL_FreeSurface(surfacePtr);
				surfacePtr = convertedSurface;

				//surface = Marshal.PtrToStructure<SDL.SDL_Surface>(surfacePtr);
				//surfaceFormat = Marshal.PtrToStructure<SDL.SDL_PixelFormat>(surface.format);
			}
			else if (surfaceFormat.format != SDL.SDL_PIXELFORMAT_ARGB8888)
				throw new Exception("Failed to map SDL surface format to Bgfx texture format: " + SDL.SDL_GetPixelFormatName(surfaceFormat.format));

			return surfacePtr;
		}

		private byte[] GetGlyphData(IntPtr surfacePtr, FontGlyph glyph, out int width, out int height)
		{
			SDL.SDL_Surface surface = Marshal.PtrToStructure<SDL.SDL_Surface>(surfacePtr);
			SDL.SDL_PixelFormat surfaceFormat = Marshal.PtrToStructure<SDL.SDL_PixelFormat>(surface.format);

			IntPtr surfaceData = surface.pixels;
			int surfacePitch = surface.pitch;

			int ascent = GetAscent();
			int descent = GetDescent();

			byte[] data = new byte[glyph.width * glyph.height * surfaceFormat.BytesPerPixel];
			unsafe
			{
				byte* src = (byte*)surfaceData;
				int rowBytes = glyph.width * surfaceFormat.BytesPerPixel;
				int offsetX = glyph.minX;
				int offsetY = -glyph.minY + (ascent - glyph.height);

				// bug in SDL_ttf?:
				// some glyphs have parts rendered outside the surface area

				if (offsetY < 0)
					offsetY = 0;

				if (offsetX < 0)
					offsetX = 0;

				src += offsetY * surfacePitch;
				src += offsetX * surfaceFormat.BytesPerPixel;
				for (int i = 0; i < glyph.height; i++)
				{
					Marshal.Copy((IntPtr)src, data, i * rowBytes, rowBytes);
					src += surfacePitch;
				}
			}

			width = glyph.width;
			height = glyph.height;

			return data;
		}

		public Int2 MeasureSize(string text)
		{
			int width, height;
			if (SDL_ttf.TTF_SizeUTF8(handle, text, out width, out height) != 0)
				throw new ApplicationException("MeasureSize failed: " + SDL.SDL_GetError());
			return new Int2(width, height);
		}

		public int GetLineSkip()
		{
			return SDL_ttf.TTF_FontLineSkip(handle);
		}

		public int GetAscent()
		{
			return SDL_ttf.TTF_FontAscent(handle);
		}

		public int GetDescent()
		{
			return SDL_ttf.TTF_FontDescent(handle);
		}
	}

	[Flags]
	public enum FontStyle : int
	{
		Normal = SDL_ttf.TTF_STYLE_NORMAL,
		Bold = SDL_ttf.TTF_STYLE_BOLD,
		Italic = SDL_ttf.TTF_STYLE_ITALIC,
		Underline = SDL_ttf.TTF_STYLE_UNDERLINE,
		Strikethrough = SDL_ttf.TTF_STYLE_STRIKETHROUGH,
	}
}
