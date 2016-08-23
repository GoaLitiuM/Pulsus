using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SDL2;

namespace Pulsus.Audio
{
	public class AudioEngine : IDisposable
	{
		public SDL.SDL_AudioSpec audioSpec;
		public AudioDriver audioDriver;

		public double audioDelay { get { return (double)audioSpec.samples / audioSpec.freq * 2; } }
		public double instanceCount { get { return audibleSounds.Count; } }

		SDL.SDL_AudioCallback audioCallback;
		uint outputDevice;

		readonly List<SoundInstance> audibleSounds = new List<SoundInstance>(256);
		uint bytesPerSample;
		byte[] emptyBuffer;
		byte[] audioBuffer;
		Stopwatch bufferTimer;
		double lastCallback;
		public double lastCallbackDelay;
		public double lastMixTime;
		public double delayCount;
		long buffersMixedCount = 0;
		float volume = 1.0f;

		public AudioEngine(string audioDevice = null, AudioDriver driver = AudioDriver.Default, int sampleRate = 44100, int bufferLength = 1024)
		{
			SDL.SDL_InitSubSystem(SDL.SDL_INIT_AUDIO);

			audioCallback = AudioCallback;
			SDL.SDL_AudioSpec desired = new SDL.SDL_AudioSpec()
			{
				freq = sampleRate,
				format = SDL.AUDIO_S16,
				channels = 2,
				samples = (ushort)bufferLength,
				callback = audioCallback,
			};

			if (driver == AudioDriver.File)
			{
				audioSpec = desired;
				audioDriver = driver;
			}
			else
			{
				string[] audioDrivers = new string[SDL.SDL_GetNumAudioDrivers()];
				for (int i = 0; i < audioDrivers.Length; i++)
					audioDrivers[i] = SDL.SDL_GetAudioDriver(i);

				string driverName = audioDrivers[0];
				string driverFallbackName = audioDrivers.Length > 1 ? audioDrivers[1] : null;

				if (driver != AudioDriver.Default)
					driverName = driver.ToString();

				int init = SDL.SDL_AudioInit(driverName.ToLower());
				if (init != 0 && driverName == AudioDriver.XAudio2.ToString().ToLower() && driverFallbackName != null)
				{
					// supplied SDL.dll does not support XAudio2, fallback to next driver
					driverName = driverFallbackName;
					init = SDL.SDL_AudioInit(driverName.ToLower());
				}
				if (init != 0)
					throw new ApplicationException("Failed to initialize audio driver " + driverName + ": " + SDL.SDL_GetError());

				Enum.TryParse(driverName, true, out audioDriver);

				if (audioDevice == null)
				{
					string[] audioDevices = new string[SDL.SDL_GetNumAudioDevices(0)];
					for (int i = 0; i < audioDevices.Length; i++)
						audioDevices[i] = SDL.SDL_GetAudioDeviceName(i, 0);

					audioDevice = audioDevices.Length > 0 ? audioDevices[0] : null;
				}

				outputDevice = SDL.SDL_OpenAudioDevice(audioDevice, 0, ref desired, out audioSpec, 0);
				if (outputDevice == 0)
					throw new ApplicationException("Failed to open audio device " + audioDevice + ": " + SDL.SDL_GetError());
			}

			if (audioSpec.format == SDL.AUDIO_S32)
				bytesPerSample = (uint)audioSpec.channels * 4;
			else if (audioSpec.format == SDL.AUDIO_S16 || audioSpec.format == SDL.AUDIO_F32)
				bytesPerSample = (uint)audioSpec.channels * 2;
			else if (audioSpec.format == SDL.AUDIO_S8 || audioSpec.format == SDL.AUDIO_U8)
				bytesPerSample = (uint)audioSpec.channels * 1;

			if (audioSpec.size == 0)
				audioSpec.size = audioSpec.samples * bytesPerSample;

			emptyBuffer = new byte[audioSpec.size];
			audioBuffer = new byte[audioSpec.size];
			//Sound.audioEngine = this;
			Sound.targetFormat = audioSpec.format;
			Sound.targetFreq = audioSpec.freq;

			lastCallback = 0.0;
			bufferTimer = Stopwatch.StartNew();

			if (outputDevice != 0)
				SDL.SDL_PauseAudioDevice(outputDevice, 0);
		}

		public void Dispose()
		{
			if (outputDevice != 0)
				SDL.SDL_CloseAudioDevice(outputDevice);

			SDL.SDL_AudioQuit();
		}

		public byte[] RenderAudio()
		{
			if (audioDriver != AudioDriver.File)
				return null;

			uint streamLength = 0;
			for (int i = 0; i < audibleSounds.Count; ++i)
			{
				SoundInstance instance = audibleSounds[i];
				if (streamLength < instance.offsetStart+instance.length)
					streamLength = instance.offsetStart+instance.length;
			}
			
			if (streamLength == 0)
				return null;

			byte[] bytes = new byte[(int)streamLength];
			GCHandle stream = GCHandle.Alloc(bytes, GCHandleType.Pinned);

			for (int i = 0; i < audibleSounds.Count; ++i)
			{
				SoundInstance instance = audibleSounds[i];
				
				int playLength = (int)(instance.offsetStop - instance.offsetStart);
				if (playLength > instance.length)
					playLength = (int)instance.length;
				else if (playLength <= 0)
					playLength = (int)instance.length;

				IntPtr streamOffset = stream.AddrOfPinnedObject();
				streamOffset += (int)instance.offsetStart;

				byte finalVolume = (byte)Math.Round(Math.Min(volume * instance.volume * SDL.SDL_MIX_MAXVOLUME, SDL.SDL_MIX_MAXVOLUME));
				SDL_MixAudioFormat(streamOffset, instance.sound.data, audioSpec.format, (uint)playLength, finalVolume);
			}
			
			stream.Free();
			audibleSounds.Clear();

			return bytes;
		}

		private void AudioCallback(IntPtr userData, IntPtr stream, int length)
		{
			double mixStartTime = bufferTimer.Elapsed.TotalSeconds;
			buffersMixedCount++;

			// fill the stream buffer with silence
			Marshal.Copy(emptyBuffer, 0, stream, length);

			lock (audibleSounds)
			{
				MixAudio(userData, stream, length);
			}

			lastCallbackDelay = bufferTimer.Elapsed.TotalSeconds - lastCallback;
			lastCallback = bufferTimer.Elapsed.TotalSeconds;
			lastMixTime = lastCallback - mixStartTime;
		}

		private void MixAudio(IntPtr userData, IntPtr stream, int length)
		{
			for (int i = 0; i < audibleSounds.Count; ++i)
			{
				SoundInstance instance = audibleSounds[i];

				// fill temporary buffer with silence
				Array.Copy(emptyBuffer, 0, audioBuffer, 0, length);

				CopySample(instance, userData, stream, length);

				byte finalVolume = (byte)Math.Round(Math.Min(volume * instance.volume * SDL.SDL_MIX_MAXVOLUME, SDL.SDL_MIX_MAXVOLUME));
				SDL_MixAudioFormat(stream, audioBuffer, audioSpec.format, (uint)length, finalVolume);

				if (instance.remove)
				{
					audibleSounds.RemoveAt(i);
					instance.sound.instances--;
					i--;
				}
			}
		}

		private void CopySample(SoundInstance instance, IntPtr userData, IntPtr stream, int length)
		{
			uint sampleLeft = Math.Min(instance.length - instance.position, (uint)length);

			uint startOffset = instance.offsetStart;
			uint pauseOffset = instance.offsetStop;
			if (startOffset < length)
			{
				uint copyLength = Math.Min(sampleLeft + startOffset, (uint)length) - startOffset;

				if (instance.paused)
				{
					if (startOffset <= pauseOffset)
						copyLength = Math.Min(pauseOffset-startOffset, copyLength);
					else
						copyLength = 0;
					instance.offsetStop = 0;
				}

				// audio clip starts playing at starting offset
				Array.Copy(instance.sound.data, instance.position, audioBuffer, startOffset, copyLength);

				instance.position += copyLength;
				instance.offsetStart = 0;
			}
			else
				instance.offsetStart -= (uint)length;
			
			if (instance.position >= instance.length)
				instance.remove = true;
		}

		public uint GetBufferOffset()
		{
			double percentage = (bufferTimer.Elapsed.TotalSeconds - lastCallback) / ((double)audioSpec.samples / audioSpec.freq);
			uint offset = (uint)(percentage * audioSpec.samples) * bytesPerSample;
			return offset;
		}

		public SoundInstance Play(Sound sound, float volume = 1.0f)
		{
			SoundInstance instance = new SoundInstance(sound, volume);
			AddInstance(instance);

			return instance;
		}

		public SoundInstance PlayScheduled(double position, Sound sound, float volume = 1.0f)
		{
			SoundInstance instance = new SoundInstance(sound, volume);

			double bufferPosition = (position - lastCallback) * ((double)audioSpec.freq);
			uint offset = (uint)(bufferPosition * bytesPerSample);
			if (offset % 4 != 0)
				offset += 4 - (offset % 4);//offset = offset;
			lock (audibleSounds)
			{
				instance.offsetStart = offset;
			}

			if (sound.polyphony > 0 && sound.instances >= sound.polyphony)
			{
				int removeCount = sound.instances - sound.polyphony + 1;
				lock (audibleSounds)
				{
					for (int i = 0; i < audibleSounds.Count; ++i)
					{
						if (audibleSounds[i].sound != sound)
							continue;

						if (audibleSounds[i].remove)
							continue;

						// mark instance for removal
						audibleSounds[i].paused = true;
						audibleSounds[i].remove = true;
						audibleSounds[i].offsetStop = offset;

						removeCount--;
						if (removeCount <= 0)
							break;
					}
				}
			}

			lock (audibleSounds)
			{
				audibleSounds.Add(instance);
				sound.instances++;
			}

			return instance;
		}
		
		public SoundInstance PlayLooped(Sound sound, float volume = 1.0f)
		{
			throw new NotImplementedException();
		}

		public SoundInstance PlayLooped(Sound sound, uint startSample, uint endSample, float volume = 1.0f)
		{
			throw new NotImplementedException();
		}

		private void AddInstance(SoundInstance instance)
		{
			instance.offsetStart = 0;
			lock (audibleSounds)
			{
				instance.offsetStart += GetBufferOffset();

				Sound sound = instance.sound;
				if (sound.polyphony > 0 && sound.instances >= sound.polyphony)
				{
					int removeCount = sound.instances - sound.polyphony + 1;
					for (int i = 0; i < audibleSounds.Count; ++i)
					{
						if (audibleSounds[i].sound != sound)
							continue;

						if (audibleSounds[i].remove)
							continue;

						// mark instance for removal
						audibleSounds[i].paused = true;
						audibleSounds[i].remove = true;
						audibleSounds[i].offsetStop = GetBufferOffset();

						removeCount--;
						if (removeCount <= 0)
							break;
					}	
				}

				audibleSounds.Add(instance);
				sound.instances++;
			}
		}

		// continues playing from paused state
		public void Play(SoundInstance instance)
		{
			lock (audibleSounds)
			{
				if (!instance.paused)
					return;

				instance.paused = false;
				instance.offsetStart = GetBufferOffset();
			}
		}

		public void Pause(SoundInstance instance)
		{
			lock (audibleSounds)
			{
				if (instance.paused)
					return;

				instance.paused = true;
				instance.offsetStop = GetBufferOffset();
			}
		}

		// removes SoundInstance from audio pool (irreversible)
		public void Stop(SoundInstance instance)
		{
			lock (audibleSounds)
			{
				if (instance.remove)
					return;

				// mark instance for removal
				instance.paused = true;
				instance.remove = true;
				instance.offsetStop = GetBufferOffset();
			}
		}

		// removes all sounds from audio pool
		public void StopAll()
		{
			lock (audibleSounds)
			{
				for (int i = 0; i < audibleSounds.Count; i++)
				{
					audibleSounds[i].paused = true;
					audibleSounds[i].remove = true;
					audibleSounds[i].offsetStop = GetBufferOffset();
				}
			}
		}

		public void SetVolume(float volume)
		{
			lock (audibleSounds)
			{
				this.volume = volume;
			}
		}

		public int GetSoundCount()
		{
			lock (audibleSounds)
			{
				return audibleSounds.Count;
			}
		}

		[DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
		static extern void SDL_MixAudioFormat(IntPtr dst, byte[] src, ushort format, uint len, int volume);
	}

	public enum AudioDriver
	{
		Default,
		File,

		// Windows
		XAudio2,
		DirectSound,
		WinMM,

		// Linux
		PulseAudio,
		Alsa,
		Dsp, // OSS
		Esd,
		
		// OS X
		CoreAudio,
	}
}
