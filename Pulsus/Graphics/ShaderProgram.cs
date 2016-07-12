using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using SharpBgfx;

namespace Pulsus.Graphics
{
	public class ShaderProgram : IDisposable
	{
		public SharpBgfx.Program programHandle;

		public ShaderProgram(string pathVS, string pathFS)
		{
			if (!File.Exists(pathVS))
				throw new FileNotFoundException("Failed to create ShaderProgram, shader not found: " + pathVS);
			else if (!File.Exists(pathFS))
				throw new FileNotFoundException("Failed to create ShaderProgram, shader not found: " + pathFS);
			
			programHandle = new SharpBgfx.Program(
				new SharpBgfx.Shader(MemoryBlock.FromArray(File.ReadAllBytes(pathVS))),
				new SharpBgfx.Shader(MemoryBlock.FromArray(File.ReadAllBytes(pathFS))), true);
		}

		public void Dispose()
		{
			programHandle.Dispose();
		}
	}
}
