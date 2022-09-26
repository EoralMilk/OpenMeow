#version {VERSION}

#define MAX_TERRAIN_LIGHT 64
#ifdef GL_ES
precision mediump float;
#endif

uniform mat4 view;
uniform mat4 projection;
uniform vec3 ViewOffset;

in vec4 aVertexPosition;
in vec2 aVertexTexCoord;

out vec3 vFragPos;
out vec2 vTexCoord;

void main()
{
	gl_Position = projection * view * (aVertexPosition + vec4(ViewOffset, 0.0));
	vFragPos = aVertexPosition.xyz;
	vTexCoord = aVertexTexCoord;
}
