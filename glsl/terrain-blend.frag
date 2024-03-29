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
uniform sampler2D MapColorTint;

uniform sampler2DArray Tiles;
uniform sampler2DArray TilesNorm;

uniform bool UseMapTint;

uniform vec2 Offset;
uniform vec2 Range;

uniform float TileScales[MAX_TILES];

in vec2 vUV;
in vec2 vMaskUV;
in vec2 vMapUV;
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
vec4 combines[MAX_TERRAIN_LAYER + 2];


layout (location = 0) out vec4 ColorOutPut;
layout (location = 1) out vec4 NormalOutPut;


vec3 ProcessNormal(vec3 normal){
	// return normalize(TBN * normal);
	normal = normalize(normal * 2.0 - 1.0);   
	return (normalize(TBN * normal) + 1.0) / 2.0;
}

int GetTileIndex(int layer){
	int tile = 0;
	switch(layer){
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
		default: tile = vTileType1234.r; // 1st for clear bse
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

// alpha channel use for height map
vec4 GetTileColor(int layer)
{
	int tileIndex = GetTileIndex(layer);
	// float noiseUV = MaskNoise(layer);
	// float noiseUV2 = MaskNoise(layer+1);

	float noiseC = MaskNoise2(layer) * 0.25 + 0.85;
	vec4 color;

	// vec2 cuv = (vUV + vec2(noiseUV, noiseUV2)) / TileScales[tileIndex];
	vec2 cuv = vUV / TileScales[tileIndex];
	// cuv = cuv - vec2(floor(cuv.x), floor(cuv.y));
	color = texture(Tiles, vec3(cuv, float(tileIndex)));
	color.rgb *= noiseC;
	return color;
}

vec4 GetTileCombines(int layer)
{
	int tileIndex = GetTileIndex(layer);
	// float noise = MaskNoise(layer);
	// float noiseB = MaskNoise(layer+1);

	vec4 combines;
	// vec2 cuv = (vUV + vec2(noise, noiseB)) / TileScales[tileIndex];
	vec2 cuv = vUV / TileScales[tileIndex];
	// cuv = cuv - vec2(floor(cuv.x), floor(cuv.y));
	combines = texture(TilesNorm, vec3(cuv, float(tileIndex)));

	return combines;
}

vec4 Blend2Tiles(vec4 col1, vec4 col2, float mask){
	float a1 = col1.a * (1.0 - mask);
	float a2 = mask * col2.a;
	return vec4(mix(col1, col2, step(a1, a2) * 0.4 + (a2 / (a1 + a2)) * 0.6));
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

	// skip water layer
	int layer = 1;
	// get last color
	while (layer < 9){
		masks[layer] = LayerMask(layer);
		if (masks[layer] > 0.993){
			colors[layer] = GetTileColor(layer);
			combines[layer] = GetTileCombines(layer);
			colors[layer+1] = GetTileColor(layer+1);
			combines[layer+1] = GetTileCombines(layer+1);
			break;
		}
		layer++;
	}

	if (layer >= 9){
		colors[8] = GetTileColor(-1);
		combines[8] = GetTileCombines(-1);
		masks[8] = LayerMask(8);
		layer = 8;
	}

	// skip water layer
	while (layer > 1)
	{
		layer--;
		if (masks[layer] < 0.005)
		{
			colors[layer] = colors[layer + 1];
			combines[layer] = combines[layer + 1];
			continue;
		}

		// there is a super strange bug???
		// if put the mix in the else block and it would not work???
		vec4 tilecolor = GetTileColor(layer);
		colors[layer] = Blend2Tiles(colors[layer + 1], tilecolor, masks[layer]);
		combines[layer] = Blend2Tiles(combines[layer + 1], GetTileCombines(layer), masks[layer]);
		// colors[layer] = vec4(mix(colors[layer + 1], tilecolor, step(colors[layer + 1].a * (1 - masks[layer]), masks[layer] * tilecolor.a)));
		// combines[layer] = mix(combines[layer + 1], GetTileCombines(layer), step(colors[layer + 1].a * (1 - masks[layer]), masks[layer] * tilecolor.a));
	}

	// skip water layer
	if (UseMapTint)
		// ColorOutPut = vec4(colors[1].rgb * vec3(vMapUV, 0).rgb,1.0);
		// ColorOutPut = vec4(colors[1].rgb * clamp(texture(MapColorTint, vMapUV).rgb * 3.5, 0.0, 1.0),1.0);
		ColorOutPut = vec4(colors[1].rgb * clamp(texture(MapColorTint, mix(Offset, Offset + Range, vMaskUV)).rgb * 1.75, 0.15, 1.25),1.0);
	else
		ColorOutPut = vec4(colors[1].rgb * vTint.rgb,1.0);
	NormalOutPut = vec4(ProcessNormal(combines[1].rgb),combines[1].a);
}
