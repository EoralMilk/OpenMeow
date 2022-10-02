#version {VERSION}
#ifdef GL_ES
precision mediump float;
precision mediump sampler2DArray;
#endif

#define MAX_TERRAIN_LAYER 8
#define MAX_TILES 128

uniform sampler2D Mask123;
uniform sampler2D Mask456;
uniform sampler2D Mask789;

uniform sampler2D MaskCloud;

uniform sampler2DArray Tiles;
uniform sampler2DArray TilesNorm;

uniform float TileScales[MAX_TILES];

in vec2 vUV;
in vec2 vMaskUV;
in vec4 vTint;
in vec3 vTangent;
in vec3 vBitangent;
in vec3 vNormal;
in vec3 vFragPos;

flat in ivec4 vTileType1234;
flat in ivec4 vTileType5678;

mat3 TBN;

vec4 mask123;
vec4 mask456;
vec4 mask789;
vec4 masknoise1;
vec4 masknoise2;


float masks[MAX_TERRAIN_LAYER + 2];
vec4 colors[MAX_TERRAIN_LAYER + 2];
vec3 norms[MAX_TERRAIN_LAYER + 2];


layout (location = 0) out vec4 ColorOutPut;
layout (location = 1) out vec4 NormalOutPut;

vec3 ProcessNormal(vec3 normal){
	normal = normalize(normal * 2.0 - 1.0);   
	return normalize(TBN * normal);
}

int GetTileIndex(int layer){
	int tile = 0;
	switch(layer){
		case 0: tile = vTileType1234.r;
		break;
		case 1: tile = vTileType1234.g;
		break;
		case 2: tile = vTileType1234.b;
		break;
		case 3: tile = vTileType1234.a;
		break;
		case 4: tile = vTileType5678.r;
		break;
		case 5: tile = vTileType5678.g;
		break;
		case 6: tile = vTileType5678.b;
		break;
		case 7: tile = vTileType5678.a;
		break;
		default:
		break;
	}
	return tile;
}

float LayerMask(int layer)
{
	float mask = 1.0;
	switch(layer){
		case 0: mask = mask123.r;
		break;
		case 1: mask = mask123.g;
		break;
		case 2: mask = mask123.b;
		break;

		case 3: mask = mask456.r;
		break;
		case 4: mask = mask456.g;
		break;
		case 5: mask = mask456.b;
		break;

		case 6: mask = mask789.r;
		break;
		case 7: mask = mask789.g;
		break;
		case 8: mask = mask789.b;
		break;
		default:
		break;
	}
	return mask;
}

float MaskNoise(int layer)
{
	float mask = 1.0;
	switch(layer % 3){
		case 0: mask = masknoise1.r;
		break;
		case 1: mask = masknoise1.g;
		break;
		case 2: mask = masknoise1.b;
		break;
		default:
		break;
	}
	return mask;
}

float MaskNoise2(int layer)
{
	float mask = 1.0;
	switch(layer % 3){
		case 0: mask = masknoise2.b;
		break;
		case 1: mask = masknoise2.r;
		break;
		case 2: mask = masknoise2.g;
		break;
		default:
		break;
	}
	return mask;
}

vec4 GetTileColor(int layer)
{
	int tileIndex = GetTileIndex(layer);
	float noiseUV = MaskNoise(layer);
	float noiseC = MaskNoise2(layer) * 0.25 + 0.85;
	vec4 color;
	if (TileScales[tileIndex] != 1.0)
	{
		vec2 cuv = (vUV + vec2(noiseUV)) / TileScales[tileIndex];
		cuv = cuv - vec2(floor(cuv.x), floor(cuv.y));
		color = texture(Tiles, vec3(cuv, float(tileIndex))) * noiseC;
	}
	else
	{
		vec2 cuv = vUV + vec2(noiseUV);
		cuv = cuv - vec2(floor(cuv.x), floor(cuv.y));
		color = texture(Tiles, vec3(cuv, float(tileIndex))) * noiseC;
	}
	if (layer == 0)
		return vec4(color.rgb, 0.3);
	else
		return color;
}

vec3 GetTileNormal(int layer)
{
	int tileIndex = GetTileIndex(layer);
	float noise = MaskNoise(layer);
	vec3 normal;
	if (TileScales[tileIndex] != 1.0)
	{
		vec2 cuv = (vUV + vec2(noise)) / TileScales[tileIndex];
		cuv = cuv - vec2(floor(cuv.x), floor(cuv.y));
		normal = texture(TilesNorm, vec3(cuv, float(tileIndex))).rgb;
	}
	else
	{
		vec2 cuv = vUV + vec2(noise);
		cuv = cuv - vec2(floor(cuv.x), floor(cuv.y));
		normal = texture(TilesNorm, vec3(cuv, float(tileIndex))).rgb;
	}

	if (normal == vec3(0.0))
		return vec3(0,0,1.0);
	else
		return normal;
}

void main()
{
	TBN = mat3(vTangent, vBitangent, vNormal);
	mask123 = texture(Mask123, vMaskUV);
	mask456 = texture(Mask456, vMaskUV);
	mask789 = texture(Mask789, vMaskUV);
	vec2 cuv = vUV / 40.0;
	cuv = cuv - vec2(floor(cuv.x), floor(cuv.y));
	masknoise1 = texture(MaskCloud, cuv);
	cuv = vUV / 60.0;
	cuv = cuv - vec2(floor(cuv.x), floor(cuv.y));
	masknoise2 = texture(MaskCloud, cuv);


	int layer = 0;
	// get last color
	while (layer < 9){
		masks[layer] = LayerMask(layer);
		if (masks[layer] > 0.993){
			colors[layer] = GetTileColor(layer);
			norms[layer] = GetTileNormal(layer);
			colors[layer+1] = GetTileColor(layer+1);
			norms[layer+1] = GetTileNormal(layer+1);
			break;
		}
		layer++;
	}

	if (layer >= 9){
		colors[8] = GetTileColor(-1);
		norms[8] = GetTileNormal(-1);
		masks[8] = LayerMask(8);
		layer = 8;
	}

	while (layer > 0)
	{
		layer--;
		if (masks[layer] < 0.005)
		{
			colors[layer] = colors[layer + 1];
			norms[layer] = norms[layer + 1];
			continue;
		}

		// there is a super strange bug???
		// if put the mix in the else block and it would not work???
		vec4 tilecolor = GetTileColor(layer);
		colors[layer] = vec4(mix(colors[layer + 1].rgb, tilecolor.rgb, masks[layer] * tilecolor.a), 1.0);
		norms[layer] = mix(norms[layer + 1].rgb, GetTileNormal(layer), masks[layer] * tilecolor.a);
	}

	vec3 tint = vec3(min(vTint.r * 4.0, 1.0), min(vTint.g * 4.0, 1.0), min(vTint.b * 4.0, 1.0));
	ColorOutPut = vec4(colors[0].rgb * tint,1.0);
	NormalOutPut = vec4(ProcessNormal(norms[0]),1.0);
}
