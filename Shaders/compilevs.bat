shaderc.exe -i bin -f "%*" --type v -o dx11/%~n1.bin  --platform windows -p vs_4_0 -O 3
shaderc.exe -i bin -f "%*" --type v -o dx9/%~n1.bin --platform windows -p vs_3_0 -O 3
shaderc.exe -i bin -f "%*" --type v -o opengl/%~n1.bin --platform linux