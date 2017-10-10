Pulsus
---
[Pulsus](https://github.com/GoaLitiuM/Pulsus) is a open source rhythm game and simulator/player for [BMS format](https://en.wikipedia.org/wiki/Be-Music_Source) written in C#. The project main focus is to provide support for all the common game modes BMS is known for: 5key, 7key and 9key modes including both single and double modes.

Features
---
- Supports following file formats (not completely):
  - bms/bme/bml/pms, RDM-LN, MGQ-LN
  - bmson 1.0.0
- Single-Play support for 5key, 7key and 9key modes, Double-Play is not supported at the moment
- Basic gameplay and judge using similar timing gates from LR2
- Functions as a preview tool with chart editors like iBMSC/µBMSC/BmsONE
  - Usage: Pulsus.exe <bms|bmson file> -p -m <measure number 0-999>
- Can output audio to .wav files
  - Usage: Pulsus.exe <bms|bmson file> --render <output.wav>
- Engine Framework:
  - Window ([SDL2](https://www.libsdl.org/) + [SDL2-CS](https://github.com/flibitijibibo/SDL2-CS))
  - GUI widgets ([Eto.Forms](https://github.com/picoe/Eto))
  - Input from keyboard/joysticks/controllers (SDL2)
  - Graphics with following backends: D3D9, D3D11, OpenGL. ([bgfx](https://github.com/bkaradzic/bgfx) + [SharpBgfx](https://github.com/MikePopoloski/SharpBgfx))
  - Audio playback with following backends: XAudio2, DirectSound, PulseAudio, CoreAudio, and more. (SDL2)
  - Multimedia support for video, audio and image formats ([FFmpeg](https://ffmpeg.org/) + [FFmpeg.AutoGen](https://github.com/Ruslan-B/FFmpeg.AutoGen))
  - JSON ([Jil](https://github.com/kevin-montrose/Jil))

Requirements
---
Windows users may need to install the following packages:
- Vista/7: [.NET Framework 4.5.1 or later](https://www.microsoft.com/en-us/download/details.aspx?id=49981)
- 8/8.1/10: No need to install anything
- Mono: Using Mono Runtime instead of .NET Framework should work with some tweaks, not recommended.

Linux users:
- Install or build the following packages:
  - mono
  - referenceassemblies-pcl / mono-pcl
  - SDL2
  - SDL2_ttf
  - FFmpeg (3.0.2)
- Build bgfx and Eto binaries
- Download the Windows binaries of Pulsus and append the package with your built bgfx and Eto binaries (and other binaries if you chose to build them instead)
- Run Pulsus: mono Pulsus.exe

Building
---
Project utilizes C# 6.0 language features, so Visual Studio 2015 (Mono 4.0) or later is required to build the project.

For building third party runtime dependencies, see [ThirdParty/README.md](../ThirdParty/README.md).

License
---
GPL 2 or later.
