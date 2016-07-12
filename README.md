Pulsus
---
[Pulsus](https://github.com/GoaLitiuM/Pulsus) is a open source rhythm game and simulator/player for [BMS format](https://en.wikipedia.org/wiki/Be-Music_Source) written in C#. The project main focus is to provide support for all the common game modes BMS is known for: 5key, 7key and 9key modes including both single and double modes.

Features
---
- Supports following file formats (not completely):
 - bms/bme/bml/pms, RDM-LN, MGQ-LN
- Single-play support for 5key and 7key modes, DP and 9key are not supported at the moment
- Basic gameplay and judge using similar timing gates from LR2
- Engine Framework:
 - Window ([SDL2](https://www.libsdl.org/) + [SDL2-CS](https://github.com/flibitijibibo/SDL2-CS))
 - GUI widgets ([Eto.Forms](https://github.com/picoe/Eto))
 - Input from keyboard/joysticks/controllers (SDL2)
 - Graphics with following backends: D3D9, D3D11, OpenGL. ([bgfx](https://github.com/bkaradzic/bgfx) + [SharpBgfx](https://github.com/MikePopoloski/SharpBgfx))
 - Audio playback with following backends: XAudio2, DirectSound, PulseAudio, CoreAudio, and more. (SDL2)
 - Multimedia support for video, audio and image formats ([FFmpeg](https://ffmpeg.org/) + [FFmpeg.AutoGen](https://github.com/Ruslan-B/FFmpeg.AutoGen))
 - JSON ([Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json))

Requirements
---
Windows users may need to install the following packages:
- .NET Framework 4.5.1 or [later](https://www.microsoft.com/en-us/download/details.aspx?id=49981), or [Mono](http://www.mono-project.com/download/#download-win) (untested)

Linux users:
- Install the following packages:
 - mono
 - referenceassemblies-pcl / mono-pcl
 - SDL2
 - SDL2_ttf
 - FFmpeg (3.0.2)

Building
---
See ThirdParty/readme.md for building third party runtime dependencies.

License
---
GPL 2 or later.