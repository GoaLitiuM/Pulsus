@echo off
if not exist dx11 mkdir dx11
if not exist dx9 mkdir dx9
if not exist opengl mkdir opengl

call compilefs.bat default_fs.sc
call compilevs.bat default_vs.sc
call compilefs.bat colorkey_fs.sc
call compilefs.bat color_fs.sc
call compilevs.bat color_vs.sc

pause