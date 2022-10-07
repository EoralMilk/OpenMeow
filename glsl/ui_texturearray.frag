#version {VERSION}
#ifdef GL_ES
precision mediump float;
precision mediump sampler2DArray;
#endif

in vec2 TexCoords;
flat in ivec3 Tags;

out vec4 fragColor;

uniform sampler2DArray Textures;

void main()
{
	fragColor = texture(Textures, vec3(TexCoords, float(Tags.x)));
}
