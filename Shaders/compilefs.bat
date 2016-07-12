shaderc.exe -i bin -f "%*" --type f -o dx11/%~n1.bin  --platform windows -p ps_4_0 -O 3
shaderc.exe -i bin -f "%*" --type f -o dx9/%~n1.bin --platform windows -p ps_3_0 -O 3
shaderc.exe -i bin -f "%*" --type f -o opengl/%~n1.bin --platform linux