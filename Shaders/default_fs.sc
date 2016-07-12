$input v_texcoord0, v_color0

#include "bgfx_shader.sh"

SAMPLER2D(s_texColor, 0);

void main()
{
	vec4 tex = texture2D(s_texColor, v_texcoord0);
	tex.xyz *= tex.a;
	
	//if (tex.a == 0.0f)
	//	tex = vec4(1.0f, 1.0f, 1.0f, 1.0f);
		
	gl_FragColor = tex * v_color0;
}