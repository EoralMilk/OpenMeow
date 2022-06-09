#version {VERSION}

// uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;

// #if __VERSION__ == 120
// attribute vec4 aVertexPosition;
// attribute vec4 aVertexTexCoord;
// attribute vec2 aVertexTexMetadata;
// attribute vec3 aVertexTint;
// varying vec4 vTexCoord;
// varying vec4 vChannelMask;
// varying vec4 vNormalsMask;
// varying mat4 normalTrans;
// varying vec3 FragPos;
// varying vec2 PaletteRows;
// #else
in vec4 aVertexPosition;
in vec4 aVertexTexCoord;
in vec2 aVertexTexMetadata;
in vec4 aVertexTint;
in vec4 iModelV1;
in vec4 iModelV2;
in vec4 iModelV3;
in vec4 iModelV4;
in vec2 iPaletteRows;
in vec2 iVplInfo;
in vec4 iVertexTint;

out vec4 vTexCoord;
out vec4 vChannelMask;
out vec4 vNormalsMask;
out mat3 normalTrans;
out vec2 PaletteRows;
out mat4 inverseViewProjection;
out vec2 VplInfo;
out vec4 vTint;
// #endif

vec4 DecodeMask(float x)
{
	if (x > 0.0)
		return (x > 0.5) ? vec4(1,0,0,0) : vec4(0,1,0,0);
	else
		return (x < -0.5) ? vec4(0,0,0,1) : vec4(0,0,1,0);
}

void main()
{
	mat4 model = mat4(iModelV1, iModelV2, iModelV3, iModelV4);

	gl_Position = projection * view * model *aVertexPosition;
	vTexCoord = aVertexTexCoord;
	vChannelMask = DecodeMask(aVertexTexMetadata.s);
	vNormalsMask = DecodeMask(aVertexTexMetadata.t);
	normalTrans = mat3(transpose(inverse(model)));
	PaletteRows = iPaletteRows;
	inverseViewProjection = inverse(projection * view);
	VplInfo = iVplInfo;
	vTint = iVertexTint;
}
