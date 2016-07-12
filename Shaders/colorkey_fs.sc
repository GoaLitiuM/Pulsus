$input v_texcoord0, v_color0

#include "bgfx_shader.sh"

uniform vec4 u_colorKey;

SAMPLER2D(s_texColor, 0);

void main()
{
	vec4 tex = texture2D(s_texColor, v_texcoord0);
	
	if (length(tex.rgb-u_colorKey.rgb) == 0.0f)
		discard;
	
	tex.xyz *= tex.a;
	gl_FragColor = tex * v_color0;
}