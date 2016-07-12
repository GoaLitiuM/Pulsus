$input v_color0

#include "bgfx_shader.sh"

void main()
{
	vec4 tex = v_color0;
	//tex.xyz *= tex.a;
	gl_FragColor = tex;
}