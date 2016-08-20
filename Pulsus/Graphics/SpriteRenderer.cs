using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.IO;
using SharpBgfx;

namespace Pulsus.Graphics
{
	public class SpriteRenderer : IDisposable
	{
		[System.Diagnostics.DebuggerDisplay("{(texture != null ? texture.path : color.ToString())}")]
		private class Sprite : IComparable<Sprite>
		{
			public readonly Texture2D texture;
			public readonly Int2 position;
			public readonly Int2 size;
			public readonly float rotation;
			public readonly float originX;
			public readonly float originY;
			public readonly Rectangle sourceRect;
			public readonly Color color;

			public Sprite(Texture2D texture, Int2 position, Color color)
				: this(texture, position, new Int2(texture.width, texture.height),
					default(float), default(float), default(float), new Rectangle(0, 0, texture.width, texture.height), color)
			{
			}

			public Sprite(Texture2D texture, Int2 position, Int2 size, Rectangle sourceRect, Color color)
				: this(texture, position, size, default(float), default(float), default(float), sourceRect, color)
			{
			}

			public Sprite(Texture2D texture, Int2 position, Int2 size, float rotation, float originX, float originY, Rectangle sourceRect, Color color)
			{
				this.texture = texture;
				this.position = position;
				this.size = size;
				this.rotation = rotation;
				this.originX = originX;
				this.originY = originY;
				this.sourceRect = sourceRect;
				this.color = color.AsARGBPremultiplied();
			}

			public int CompareTo(Sprite other)
			{
				int hash = texture != null ? texture.GetHashCode() : 0;
				int hashOther = other.texture != null ? other.texture.GetHashCode() : 0;
				return hash.CompareTo(hashOther);
			}
		}

		Renderer renderer;
		Texture2D pixel;

		const int maxStreamVertices = (16 * 1024);
		const int maxStreamIndices = maxStreamVertices * 6;

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

			ortho = Matrix4.Ortho(0, 0, width, -height, 0f, 1000000f);

			string shaderPath = renderer.shaderPath;
			switch (renderer.rendererType)
			{
				case RendererType.Direct3D9:
					// half texel offset
					ortho[12] += -0.5f * ortho[0];
					ortho[13] += -0.5f * ortho[5];
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

			renderer.SetViewFrameBuffer(currentViewport, null);
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

			VertexTextureColor[] vertices = new VertexTextureColor[4];
			ushort[] indices = new ushort[6];

			Array.Copy(rectangleVertices, vertices, 4);
			Array.Copy(rectangleIndices, indices, 6);

			GCHandle handleVertices = GCHandle.Alloc(vertices, GCHandleType.Pinned);

			TransientVertexBuffer vertexBuffer = new TransientVertexBuffer();
			TransientIndexBuffer indexBuffer = new TransientIndexBuffer();

			for (int i = 0, batchLeft = 0, batchSize = 0; i < spriteBatch.Count; i++)
			{
				Sprite sprite = spriteBatch[i];
				Texture2D texture = sprite.texture;

				if (batchLeft == 0)
				{
					for (int j = i; j < spriteBatch.Count; j++)
					{
						if (spriteBatch[j].texture != texture)
							break;

						batchLeft++;
					}

					batchLeft = Math.Min(batchLeft, ushort.MaxValue / 4);

					vertexBuffer = new TransientVertexBuffer(batchLeft * 4, VertexTextureColor.vertexLayout);
					indexBuffer = new TransientIndexBuffer(batchLeft * 6);
				}

				vertices[0].x = vertices[0].u = 0.0f;
				vertices[0].y = vertices[0].v = 0.0f;
				vertices[1].u = 1.0f;
				vertices[1].y = vertices[1].v = 0.0f;
				vertices[2].u = 1.0f;
				vertices[2].v = 1.0f;
				vertices[3].x = vertices[3].u = 0.0f;
				vertices[3].v = 1.0f;

				vertices[1].x = vertices[2].x = sprite.size.x;
				vertices[2].y = vertices[3].y = sprite.size.y;

				// translation and rotation
				if (sprite.rotation == 0.0f)
				{
					for (int j = 0; j < 4; ++j)
					{
						vertices[j].x += sprite.position.x;
						vertices[j].y += sprite.position.y;
						vertices[j].color = sprite.color;
					}
				}
				else
				{
					for (int j = 0; j < 4; ++j)
					{
						double cosRot = Math.Cos(sprite.rotation);
						double sinRot = Math.Sin(sprite.rotation);

						double rotX = sprite.originX + (vertices[j].x - sprite.originX) * cosRot
							- (vertices[j].y - sprite.originY) * sinRot;
						double rotY = sprite.originY + (vertices[j].x - sprite.originX) * sinRot
							+ (vertices[j].y - sprite.originY) * cosRot;

						vertices[j].x = (float)(rotX + sprite.position.x - (int)sprite.originX);
						vertices[j].y = (float)(rotY + sprite.position.y - (int)sprite.originY);
						vertices[j].color = sprite.color;
					}
				}

				// texture coordinates
				if (texture != null)
				{
					float left = sprite.sourceRect.x;
					float top = sprite.sourceRect.y;
					float right = sprite.sourceRect.x + sprite.sourceRect.width;
					float bottom = sprite.sourceRect.y + sprite.sourceRect.height;

					if (sprite.size == sprite.sourceRect.size)
					{
						vertices[0].u = vertices[3].u = left / texture.width;
						vertices[0].v = vertices[1].v = top / texture.height;
						vertices[2].u = vertices[1].u = right / texture.width;
						vertices[2].v = vertices[3].v = bottom / texture.height;
					}
					else
					{
						// when stretching textures, texture sampling points needs to be adjusted
						// by half pixel in order to prevent neighbouring subtextures bleeding
						// around the edges.
						vertices[0].u = vertices[3].u = (left + 0.5f) / texture.width;
						vertices[0].v = vertices[1].v = (top + 0.5f) / texture.height;
						vertices[2].u = vertices[1].u = (right - 0.5f) / texture.width;
						vertices[2].v = vertices[3].v = (bottom - 0.5f) / texture.height;
					}
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
					renderer.SetVertexBuffer(vertexBuffer, 0, batchSize * 4);
					renderer.SetIndexBuffer(indexBuffer, 0, batchSize * 6);

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
			spriteBatch.Add(new Sprite(texture, pos, color));
		}

		public void Draw(Texture2D texture, Int2 pos, float rotation, Color color)
		{
			spriteBatch.Add(new Sprite(texture, pos, new Int2(texture.width, texture.height),
				rotation, texture.width / 2.0f, texture.height / 2.0f, new Rectangle(0, 0, texture.width, texture.height), color));
		}

		public void Draw(Texture2D texture, Rectangle rect, Color color)
		{
			spriteBatch.Add(new Sprite(texture, new Int2(rect.x, rect.y), new Int2(rect.width, rect.height),
				new Rectangle(0, 0, texture.width, texture.height), color));
		}

		public void Draw(Texture2D texture, Rectangle rect, float rotation, Color color)
		{
			spriteBatch.Add(new Sprite(texture, new Int2(rect.x, rect.y), new Int2(rect.width, rect.height),
				rotation, rect.width / 2.0f, rect.height / 2.0f, new Rectangle(0, 0, texture.width, texture.height), color));
		}

		public void Draw(SubTexture subTexture, Int2 pos, Color color)
		{
			spriteBatch.Add(new Sprite(subTexture.texture, pos, new Int2(subTexture.sourceRect.width, subTexture.sourceRect.height),
				subTexture.sourceRect, color));
		}

		public void Draw(SubTexture subTexture, Int2 pos, float rotation, Color color)
		{
			spriteBatch.Add(new Sprite(subTexture.texture, pos, new Int2(subTexture.sourceRect.width, subTexture.sourceRect.height),
				rotation, subTexture.texture.width / 2.0f, subTexture.texture.height / 2.0f, subTexture.sourceRect, color));
		}

		public void Draw(SubTexture subTexture, Rectangle rect, Color color)
		{
			spriteBatch.Add(new Sprite(subTexture.texture, new Int2(rect.x, rect.y), new Int2(rect.width, rect.height),
				subTexture.sourceRect, color));
		}

		public void Draw(SubTexture subTexture, Rectangle rect, float rotation, Color color)
		{
			spriteBatch.Add(new Sprite(subTexture.texture, new Int2(rect.x, rect.y), new Int2(rect.width, rect.height),
				rotation, rect.width / 2.0f, rect.height / 2.0f, subTexture.sourceRect, color));
		}

		public void DrawColor(Rectangle rect, Color color)
		{
			spriteBatch.Add(new Sprite(pixel, rect.position, rect.size,
				new Rectangle(0, 0, pixel.width, pixel.height), color));
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
					spriteBatch.Add(new Sprite(texture, glyphPos, new Int2(width, height), sourceRect, color));
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
	}
}
