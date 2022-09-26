#version {VERSION}
#ifdef GL_ES
precision mediump float;
precision mediump sampler2DArray;
#endif

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

layout (location = 0) out vec4 ColorOutPut;
layout (location = 1) out vec4 NormalOutPut;

vec3 ProcessNormal(vec3 normal){
	normal = normalize(normal * 2.0 - 1.0);   
	return normalize(TBN * normal);
}



void main()
{
	TBN = mat3(vTangent, vBitangent, vNormal);
	uv = vUV - vec2(floor(vUV.x), floor(vUV.y));

	vec4 m = texture(Mask1234, vMaskUV);
  
    vec3 color1 = texture(Tiles, vec3(uv, 0.0)).rgb;
    vec3 color2 = texture(Tiles, vec3(uv, 2.0)).rgb; 
 
    vec3 c = mix(color1, color2, m.b);

	ColorOutPut = vec4(c,1.0);
	NormalOutPut = vec4(vNormal,1.0);
}
