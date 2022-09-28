#version {VERSION}
#ifdef GL_ES
precision mediump float;
precision mediump sampler2DArray;
#endif

#define MAX_TERRAIN_LAYER 8

uniform sampler2D Mask123;
uniform sampler2D Mask456;
uniform sampler2D Mask789;

uniform sampler2DArray Tiles;

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
vec2 uv;
vec4 mask123;
vec4 mask456;
vec4 mask789;

float masks[MAX_TERRAIN_LAYER];
vec3 colors[MAX_TERRAIN_LAYER + 1];

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
		case 8: mask = 1.0;
		break;
		default:
		break;
	}
	return mask;
}

float ClampMask(float mask){
	return clamp(mask * 3.0, 0.0, 1.0);
}

void main()
{
	TBN = mat3(vTangent, vBitangent, vNormal);
	uv = vUV;
	uv = uv - vec2(floor(uv.x), floor(uv.y));
	mask123 = texture(Mask123, vMaskUV);
	mask456 = texture(Mask456, vMaskUV);
	mask789 = texture(Mask789, vMaskUV);

	int layer = 0;
	// get last color
	while (layer < 9){
		masks[layer] = LayerMask(layer);
		if (masks[layer] > 0.993){
			colors[layer] = texture(Tiles, vec3(uv, float(GetTileIndex(layer)))).rgb;
			break;
		}
		layer++;
	}

	if (layer == 9){
		colors[8] = texture(Tiles, vec3(uv, 0.0)).rgb;
		layer = 8;
	}

	while (layer > 0){
		layer--;
		vec3 c2 = texture(Tiles, vec3(uv, float(GetTileIndex(layer)))).rgb;
		colors[layer] = mix(colors[layer + 1], c2, masks[layer]);
	}

	// if (colors[0] == vec3(0.0))
	// 	discard;

	ColorOutPut = vec4(colors[0],1.0);
	NormalOutPut = vec4(vNormal,1.0);
}
