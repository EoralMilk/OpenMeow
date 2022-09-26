#version {VERSION}

#define MAX_TERRAIN_LIGHT 64
#ifdef GL_ES
precision mediump float;
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
in uint aDrawType;

out vec2 vUV;
out vec2 vMaskUV;
out vec4 vTint;
out vec3 vTangent;
out vec3 vBitangent;
out vec3 vNormal;
out vec3 vFragPos;

flat out int mDrawType;

void main()
{
	if (aVertexTint.a == 0.0) 
	{
		mDrawType = DT_NONE;
		return;
	}
	else
		mDrawType = int(aDrawType);

	gl_Position = vec4(aVertexMaskUV.x * 2.0 - 1.0, aVertexMaskUV.y * 2.0 - 1.0, 0.0, 1.0);
	vUV = aVertexUV;
	vMaskUV = Offset + aVertexMaskUV * Range;
	vTint = aVertexTint;
	vTangent = aVertexTangent;
	vBitangent = aVertexBitangent;
	vNormal = aVertexNormal;
}
