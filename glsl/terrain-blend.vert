#version {VERSION}

#define MAX_TERRAIN_LIGHT 64
#ifdef GL_ES
precision highp float;
#endif

#define DT_NONE -1

uniform vec2 Offset;
uniform vec2 Range;

in vec2 aVertexUV;
in vec2 aVertexMaskUV;
in vec4 aVertexTint;
in vec3 aVertexTangent;
in vec3 aVertexBitangent;
in vec3 aVertexNormal;
in ivec4 aTileType1234;
in ivec4 aTileType5678;


out vec2 vUV;
out vec2 vMaskUV;
out vec4 vTint;
out vec3 vTangent;
out vec3 vBitangent;
out vec3 vNormal;
out vec3 vFragPos;
flat out ivec4 vTileType1234;
flat out ivec4 vTileType5678;


void main()
{
	gl_Position = vec4(aVertexMaskUV.x * 2.0 - 1.0, aVertexMaskUV.y * 2.0 - 1.0, 0.0, 1.0);
	vUV = aVertexUV;
	vMaskUV = Offset + aVertexMaskUV * Range;
	vTint = aVertexTint;
	vTangent = aVertexTangent;
	vBitangent = aVertexBitangent;
	vNormal = aVertexNormal;
	vTileType1234 = aTileType1234;
	vTileType5678 = aTileType5678;
}
