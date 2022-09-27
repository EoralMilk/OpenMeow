#version {VERSION}

#ifdef GL_ES
precision highp float;
#endif

in vec2 TexCoords;
layout (location = 0) out vec4 BakedMask1234;
layout (location = 1) out vec4 BakedMask5678;


uniform sampler2D InitMask1234;
uniform sampler2D InitMask5678;

uniform bool InitWithTextures;


void main()
{

	if (InitWithTextures)
	{
		vec4 m1234 = texture(InitMask1234, TexCoords);
		vec4 m5678 = texture(InitMask5678, TexCoords);
		BakedMask1234 = m1234;
		BakedMask5678 = m5678;
		return;
	}

}
