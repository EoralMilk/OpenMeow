#version {VERSION}

uniform mat4 view;
uniform mat4 projection;

in vec4 aVertexPosition;
in vec2 aVertexTexCoord;
in vec2 aRW;

out vec2 TexCoords;
out vec2 RW;

void main()
{
	gl_Position = projection * view * aVertexPosition;
	TexCoords = aVertexTexCoord;
	RW = aRW;
}
