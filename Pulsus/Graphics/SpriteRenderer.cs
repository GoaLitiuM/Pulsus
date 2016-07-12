using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.IO;
using SharpBgfx;

namespace Pulsus.Graphics
{
	public class SpriteRenderer : IDisposable
	{
		[System.Diagnostics.DebuggerDisplay("{(texture != null ? texture.path : color.ToString())} {depth}")]
		class Sprite : IComparable<Sprite>
		{
			public Texture2D texture;
			public Int2 position;
			public Int2 size;
			public float rotation;
			public float originX;
			public float originY;
			public Rectangle sourceRect;
			public Color color;
			public int depth;

			public int CompareTo(Sprite other)
			{
				int hash = texture != null ? texture.GetHashCode() : 0;
				int hashOther = other.texture != null ? other.texture.GetHashCode() : 0;
				return hash.CompareTo(hashOther);
			}
		}

		Renderer renderer;
		Texture2D pixel;

		const int maxStreamVertices = (16*1024);
		const int maxStreamIndices = maxStreamVertices*6;

		ShaderProgram currentProgram = null;
		TextureFlags textureFlags = TextureFlags.None;

		Uniform colorKeyUniform;

		Matrix4 ortho;

		int width;
		int height;

		List<Sprite> spriteBatch = new List<Sprite>(128);

		public ShaderProgram colorProgram { get; private set; }
		public ShaderProgram colorKeyProgram { get; private set; }
		public int currentViewport;

		readonly VertexTextureColor[] rectangleVertices = new VertexTextureColor[]
		{
			new VertexTextureColor(0.0f, 0.0f, 0.0f, Color.White, 0.0f, 0.0f),
			new VertexTextureColor(1.0f, 0.0f, 0.0f, Color.White, 1.0f, 0.0f),
			new VertexTextureColor(1.0f, 1.0f, 0.0f, Color.White, 1.0f, 1.0f),
			new VertexTextureColor(0.0f, 1.0f, 0.0f, Color.White, 0.0f, 1.0f),
		};

		static ushort[] rectangleIndices = new ushort[]
		{
			2, 1, 0,
			3, 2, 0,
		};

		public SpriteRenderer(Renderer renderer, int width, int height)
		{
			this.renderer = renderer;
			this.width = width;
			this.height = height;

			pixel = new Texture2D(new byte[] { 255, 255, 255, 255 }, 1, 1);

			ortho = Matrix4.Ortho(0, 0, width, -height, 0, -1000000f);

			string shaderPath = "";
			switch (renderer.rendererType)
			{
				case RendererType.Direct3D11:
					shaderPath = Path.Combine("shaders", "dx11");
					break;
				case RendererType.Direct3D9:
					shaderPath = Path.Combine("shaders", "dx9");
					
					// half texel offset
					ortho[12] += -0.5f * ortho[0];
					ortho[13] += -0.5f * ortho[5];
					break;
				case RendererType.OpenGL:
					shaderPath = Path.Combine("shaders", "opengl");
					break;
				default:
					break;
			}

			colorKeyProgram = new ShaderProgram(
				Path.Combine(shaderPath, "default_vs.bin"),
				Path.Combine(shaderPath, "colorkey_fs.bin"));
			colorKeyUniform = new Uniform("u_colorKey", UniformType.Vector4);

			colorProgram = new ShaderProgram(
				Path.Combine(shaderPath, "color_vs.bin"),
				Path.Combine(shaderPath, "color_fs.bin"));

			renderer.SetUniform(colorKeyUniform, Color.Black.AsARGB().AsFloat4());
		}

		public void Dispose()
		{
			colorKeyProgram.Dispose();
			colorProgram.Dispose();
			colorKeyUniform.Dispose();
			pixel.Dispose();
		}

		public void Begin()
		{
			Begin(renderer.defaultProgram, TextureFlags.None);
		}

		public void Begin(TextureFlags flags)
		{
			Begin(renderer.defaultProgram, flags);
		}

		public void Begin(ShaderProgram program, TextureFlags flags = TextureFlags.None)
		{
			if (currentProgram != null)
				throw new ApplicationException("Current batch must be flushed with End() call");

			if (program == null)
				throw new ArgumentException("Invalid ShaderProgram");

			renderer.SetProjectionTransform(currentViewport, ortho);
			renderer.SetViewport(currentViewport, 0, 0, width, height);
			currentProgram = program;
			textureFlags = flags;
		}

		public void End()
		{
			if (currentProgram == null)
				throw new ApplicationException("Unexpected End call, start a new batch with Begin()");

			if (spriteBatch.Count == 0)
			{
				currentProgram = null;
				return;
			}

			// TODO: sort sprites by shader and texture

			VertexTextureColor[] vertices = new VertexTextureColor[4];
			ushort[] indices = new ushort[6];

			Array.Copy(rectangleVertices, vertices, 4);
			Array.Copy(rectangleIndices, indices, 6);

			GCHandle handleVertices = GCHandle.Alloc(vertices, GCHandleType.Pinned);

			TransientVertexBuffer vertexBuffer = new TransientVertexBuffer();
			TransientIndexBuffer indexBuffer = new TransientIndexBuffer();

			for (int i = 0, batchLeft = 0, batchSize = 0; i < spriteBatch.Count; i++)
			{
				Texture2D texture = spriteBatch[i].texture;
				int depth = -(spriteBatch.Count-spriteBatch[i].depth);

				if (batchLeft == 0)
				{
					for (int j = i; j < spriteBatch.Count; j++)
					{
						if (spriteBatch[j].texture == texture)
							batchLeft++;
						else
							break;
					}

					vertexBuffer = new TransientVertexBuffer(batchLeft*4, VertexTextureColor.vertexLayout);
					indexBuffer = new TransientIndexBuffer(batchLeft*6);
				}

				vertices[0].x = vertices[0].u = 0.0f;
				vertices[0].y = vertices[0].v = 0.0f;
				vertices[1].x = vertices[1].u = 1.0f;
				vertices[1].y = vertices[1].v = 0.0f;
				vertices[2].x = vertices[2].u = 1.0f;
				vertices[2].y = vertices[2].v = 1.0f;
				vertices[3].x = vertices[3].u = 0.0f;
				vertices[3].y = vertices[3].v = 1.0f;

				// translation and rotation
				for (int j = 0; j < 4; ++j)
				{
					vertices[j].x *= spriteBatch[i].size.x;
					vertices[j].y *= spriteBatch[i].size.y;

					if (spriteBatch[i].rotation != 0.0f)
					{
						vertices[j].x -= spriteBatch[i].originX;
						vertices[j].y -= spriteBatch[i].originY;

						double rotX = spriteBatch[i].originX + vertices[j].x * Math.Cos(spriteBatch[i].rotation)
							- vertices[j].y * Math.Sin(spriteBatch[i].rotation);
						double rotY = spriteBatch[i].originY + vertices[j].x * Math.Sin(spriteBatch[i].rotation)
							+ vertices[j].y * Math.Cos(spriteBatch[i].rotation);
						
						vertices[j].x = (float)rotX;
						vertices[j].y = (float)rotY;
						
					}

					vertices[j].x += spriteBatch[i].position.x - (int)spriteBatch[i].originX;
					vertices[j].y += spriteBatch[i].position.y - (int)spriteBatch[i].originY;
					//vertices[j].z = depth;
					vertices[j].color = spriteBatch[i].color;
				}

				// texture coordinates
				if (texture != null && spriteBatch[i].sourceRect.width * spriteBatch[i].sourceRect.height != 0)
				{
					vertices[0].u = vertices[3].u = (float)spriteBatch[i].sourceRect.x / texture.width;
					vertices[0].v = vertices[1].v = (float)spriteBatch[i].sourceRect.y / texture.height;
					vertices[2].u = vertices[1].u = (float)(spriteBatch[i].sourceRect.x + spriteBatch[i].sourceRect.width) / texture.width;
					vertices[2].v = vertices[3].v = (float)(spriteBatch[i].sourceRect.y + spriteBatch[i].sourceRect.height) / texture.height;
				}

				// copy data to buffers
				unsafe
				{
					int offset = (batchSize * 4);
					VertexTextureColor* vertexData = (VertexTextureColor*)handleVertices.AddrOfPinnedObject();
					VertexTextureColor* vbData = (VertexTextureColor*)(vertexBuffer.Data) + offset;
					ushort* ibData = (ushort*)(indexBuffer.Data) + (batchSize * 6);

					for (int j = 0; j < 4; ++j)
					{
						*vbData = *vertexData;
						vbData++;
						vertexData++;
					}
					for (int j = 0; j < 6; ++j)
					{
						*ibData = (ushort)(indices[j] + offset);
						ibData++;
					}
				}
						
				batchSize++;
				batchLeft--;
				if (batchLeft == 0)
				{
					// draw current batch

					renderer.SetRenderState(Renderer.AlphaBlendNoDepth);
					renderer.SetVertexBuffer(vertexBuffer, 0, batchSize*4);
					renderer.SetIndexBuffer(indexBuffer, 0, batchSize*6);

					if (texture != null)
						renderer.SetTexture(0, texture, textureFlags);

					renderer.Submit(currentViewport, currentProgram);
					batchSize = 0;
				}
			}

			handleVertices.Free();
			spriteBatch.Clear();

			currentViewport++;
			currentProgram = null;
		}

		public void Clear(Color color)
		{
			renderer.Clear(currentViewport, color);
		}

		public void Clear(Color color, ClearTargets clearTargets)
		{
			renderer.Clear(currentViewport, color, clearTargets);
		}

		public void SetFrameBuffer(FrameBuffer framebuffer)
		{
			renderer.SetViewFrameBuffer(currentViewport, framebuffer);
		}

		// used with color key shader
		public void SetColorKey(Color color)
		{
			renderer.SetUniform(colorKeyUniform, color.AsFloat4());
		}

		public void Draw(Texture2D texture, Int2 pos, Color color)
		{
			AddSprite(texture, pos, new Int2(texture.width, texture.height), 0, 0, 0, new Rectangle(0, 0, 0, 0), color);
		}

		public void Draw(Texture2D texture, Int2 pos, float rotation, Color color)
		{
			AddSprite(texture, pos, new Int2(texture.width, texture.height), rotation, texture.width/2.0f, texture.height/2.0f, new Rectangle(0, 0, 0, 0), color);
		}

		public void Draw(Texture2D texture, Rectangle rect, Color color)
		{
			AddSprite(texture, new Int2(rect.x, rect.y), new Int2(rect.width, rect.height), 0, 0, 0, new Rectangle(0, 0, 0, 0), color);
		}

		public void Draw(Texture2D texture, Rectangle rect, float rotation, Color color)
		{
			AddSprite(texture, new Int2(rect.x, rect.y), new Int2(rect.width, rect.height), rotation, rect.width/2.0f, rect.height/2.0f, new Rectangle(0, 0, 0, 0), color);
		}

		public void Draw(TextureAtlas textureAtlas, int frame, Int2 pos, Color color)
		{
			Texture2D texture = textureAtlas.texture;
			Rectangle sourceRect = textureAtlas.GetSourceRect(frame);
			AddSprite(texture, pos, new Int2(sourceRect.width, sourceRect.height), 0, 0, 0, sourceRect, color);
		}

		public void Draw(TextureAtlas textureAtlas, int frame, Int2 pos, float rotation, Color color)
		{
			Texture2D texture = textureAtlas.texture;
			Rectangle sourceRect = textureAtlas.GetSourceRect(frame);
			AddSprite(texture, pos, new Int2(sourceRect.width, sourceRect.height), rotation, texture.width/2.0f, texture.height/2.0f, sourceRect, color);
		}

		public void Draw(TextureAtlas textureAtlas, int frame, Rectangle rect, Color color)
		{
			Texture2D texture = textureAtlas.texture;
			Rectangle sourceRect = textureAtlas.GetSourceRect(frame);
			AddSprite(texture, new Int2(rect.x, rect.y), new Int2(rect.width, rect.height), 0, 0, 0, sourceRect, color);
		}

		public void Draw(TextureAtlas textureAtlas, int frame, Rectangle rect, float rotation, Color color)
		{
			Texture2D texture = textureAtlas.texture;
			Rectangle sourceRect = textureAtlas.GetSourceRect(frame);
			AddSprite(texture, new Int2(rect.x, rect.y), new Int2(rect.width, rect.height), rotation, rect.width/2.0f, rect.height/2.0f, sourceRect, color);
		}

		public void DrawColor(Int2 pos, Int2 size, Color color)
		{
			AddSprite(pixel, pos, new Int2(size.x, size.y), 0, 0, 0, new Rectangle(0, 0, 0, 0), color);
		}

		public void DrawColor(Rectangle rect, Color color)
		{
			AddSprite(pixel, new Int2(rect.x, rect.y), new Int2(rect.width, rect.height), 0, 0, 0, new Rectangle(0, 0, 0, 0), color);
		}

		public void DrawPart(Texture2D texture, Int2 pos, Rectangle sourceRect, Color color)
		{
			Int2 size;
			if (sourceRect.width*sourceRect.height != 0)
				size = new Int2(sourceRect.width, sourceRect.height);
			else
				size = new Int2(texture.width, texture.height);

			AddSprite(texture, pos, size, 0, 0, 0, sourceRect, color);
		}

		public void DrawPart(Texture2D texture, Int2 pos, Int2 size, Rectangle sourceRect, Color color)
		{
			AddSprite(texture, pos, size, 0, 0, 0, sourceRect, color);
		}

		public void DrawText(Font font, string text, Int2 pos, Color color)
		{
			Int2 penPos = pos;
			int lineSkip = font.GetLineSkip();
			int ascent = font.GetAscent();
			for (int i = 0; i < text.Length; i++)
			{
				char chr = text[i];
				if (chr == '\n')
				{
					penPos.x = pos.x;
					penPos.y += lineSkip;
					continue;
				}
				if (chr >= font.glyphs.Length)
					chr = '?';

				int width = font.glyphs[chr].width;
				int height = font.glyphs[chr].height;

				if (width * height == 0 && font.glyphs[chr].advance == 0)
				{
					// font doesn't support this glyph
					chr = '?';
					width = font.glyphs[chr].width;
					height = font.glyphs[chr].height;
				}

				if (width * height > 0)
				{
					Texture2D texture = font.textures[font.glyphs[chr].textureId];
					Rectangle sourceRect = new Rectangle(
						font.glyphs[chr].textureX, font.glyphs[chr].textureY,
						width, height);

					Int2 glyphPos = penPos + new Int2(font.glyphs[chr].minX, ascent - height - font.glyphs[chr].minY);
					AddSprite(texture, glyphPos, new Int2(width, height), 0, 0, 0, sourceRect, color);
				}

				penPos.x += font.glyphs[chr].advance;
			}
		}

		public void DrawTextOutline(Font font, string text, Int2 pos, Color color, int outlineSize)
		{
			// TODO: use shaders for text outline effects

			DrawText(font, text, pos + new Int2(-outlineSize, -outlineSize), color);
			DrawText(font, text, pos + new Int2(-outlineSize, 0), color);
			DrawText(font, text, pos + new Int2(-outlineSize, outlineSize), color);
			DrawText(font, text, pos + new Int2(0, -outlineSize), color);
			DrawText(font, text, pos + new Int2(0, outlineSize), color);
			DrawText(font, text, pos + new Int2(outlineSize, -outlineSize), color);
			DrawText(font, text, pos + new Int2(outlineSize, 0), color);
			DrawText(font, text, pos + new Int2(outlineSize, outlineSize), color);
		}

		private void AddSprite(Texture2D texture, Int2 pos, Int2 size, float rotation, float originX, float originY, Rectangle sourceRect, Color color)
		{
			if (currentProgram == null)
				throw new ApplicationException("Unexpected draw call, start a new batch with Begin()");

			Sprite sprite = new Sprite();
			sprite.texture = texture;
			sprite.position = pos;
			sprite.size = size;
			sprite.rotation = rotation;
			sprite.originX = originX;
			sprite.originY = originY;
			sprite.sourceRect = sourceRect;
			sprite.depth = spriteBatch.Count;
				
			// premultiply alpha
			sprite.color = new Color(color * (color.alpha / 255.0f), color.alpha).AsARGB();

			spriteBatch.Add(sprite);
		}
	}
}
