#version {VERSION}

#ifdef GL_ES
precision mediump float;
#endif

in vec2 TexCoords;
in vec2 RW;

out vec4 FragColor;

const float PI = 3.14159265359;


void main()
{
	vec2 center = vec2(0.5,0.5);
	vec2 dir = TexCoords - center;
	float R = RW.x * 0.5;
	float edgeWidth = clamp(length(dir)-(R - RW.y), 0.0, RW.y) / RW.y ;
	vec2 UV = dir * (sin(edgeWidth * PI) * pow(1.0 - RW.x, 2.0));

	FragColor = vec4(UV, 0.0, 0.0);
}
