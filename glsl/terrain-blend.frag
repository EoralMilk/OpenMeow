#version {VERSION}
#ifdef GL_ES
precision mediump float;
precision mediump sampler2DArray;
#endif

#define MAX_TERRAIN_LAYER 8

uniform sampler2D Mask1234;
uniform sampler2D Mask5678;

uniform sampler2DArray Tiles;

in vec2 vUV;
in vec2 vMaskUV;
in vec4 vTint;
in vec3 vTangent;
in vec3 vBitangent;
in vec3 vNormal;
in vec3 vFragPos;

flat in int mDrawType;

mat3 TBN;
vec2 uv;
vec4 mask1234;
vec4 mask5678;
float masks[MAX_TERRAIN_LAYER];
vec3 colors[MAX_TERRAIN_LAYER];

layout (location = 0) out vec4 ColorOutPut;
layout (location = 1) out vec4 NormalOutPut;

vec3 ProcessNormal(vec3 normal){
	normal = normalize(normal * 2.0 - 1.0);   
	return normalize(TBN * normal);
}

int GetTileIndex(int layer){
	return layer;
}

float LayerMask(int layer)
{
	float mask = 1.0;
	switch(layer){
		case 0: mask = mask1234.r;
		break;
		case 1: mask = mask1234.g;
		break;
		case 2: mask = mask1234.b;
		break;
		case 3: mask = mask1234.a;
		break;
		case 4: mask = mask5678.r;
		break;
		case 5: mask = mask5678.g;
		break;
		case 6: mask = mask5678.b;
		break;
		case 7: mask = mask5678.a;
		break;
		default:
		break;
	}
	return mask;
}



void main()
{
	TBN = mat3(vTangent, vBitangent, vNormal);
	uv = vUV - vec2(floor(vUV.x), floor(vUV.y));
	mask1234 = texture(Mask1234, vMaskUV);
	mask5678 = texture(Mask5678, vMaskUV);

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
