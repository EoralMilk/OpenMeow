#version {VERSION}

#if __VERSION__ == 120
attribute vec3 aPos;
attribute vec3 aNormal;
attribute vec2 aTexCoords;

varying vec3 Normal;
varying vec3 FragPos;
varying vec2 TexCoords;
#else
in vec3 aPos;
in vec3 aNormal;
in vec2 aTexCoords;
in vec4 iModelV1;
in vec4 iModelV2;
in vec4 iModelV3;
in vec4 iModelV4;

out vec3 Normal;
out vec3 FragPos;
out vec2 TexCoords;
#endif

// uniform mat4 model;

uniform mat4 view;
uniform mat4 projection;

void main()
{
	mat4 model = mat4(iModelV1, iModelV2, iModelV3, iModelV4);
	gl_Position = projection * view * model * vec4(aPos.xyz, 1.0);
	FragPos = vec3(model * vec4(aPos.xyz, 1.0));
	Normal = mat3(transpose(inverse(model))) * aNormal.xyz;
	TexCoords = aTexCoords;
}