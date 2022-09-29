#version {VERSION}
#ifdef GL_ES
precision mediump float;
#endif

#define MAX_TERRAIN_LIGHT 64
// keep the non array uniform set at first
// When the first uniform variable is an array, it cannot be set to count < 1
uniform float TerrainLightHeightStep;
uniform vec3 TerrainLightPos[MAX_TERRAIN_LIGHT];
uniform vec4 TerrainLightColorRange[MAX_TERRAIN_LIGHT];


uniform vec2 Offset;
uniform vec2 Range;
// r is water mask
uniform sampler2D Mask123;

uniform float WaterUVOffset;

uniform sampler2D Caustics;
uniform sampler2D WaterNormal;

uniform sampler2D BakedTerrainTexture;
uniform sampler2D BakedTerrainNormalTexture;

in vec3 vFragPos;
in vec2 vTexCoord;

layout (location = 0) out vec4 fragColor;

struct DirLight {
	vec3 direction;
	vec3 ambient;
	vec3 diffuse;
	vec3 specular;
};

uniform DirLight dirLight;

uniform sampler2D ShadowDepthTexture;
uniform mat4 SunVP;
uniform mat4 InvCameraVP;
uniform vec3 viewPos;
uniform vec3 CameraInvFront;

uniform float ShadowBias;
uniform float AmbientIntencity;

float CalShadow(){
	vec4 fragPosLightSpace = SunVP * vec4(vFragPos, 1.0);
	vec3 projCoords = fragPosLightSpace.xyz / fragPosLightSpace.w;
	projCoords = projCoords * 0.5f + 0.5f;
	float currentDepth = projCoords.z;

	float shadow = 0.0f;
	float bias = ShadowBias * 0.025f;

	if(projCoords.z <= 1.0f)
	{
		vec2 texelSize = 1.0f / vec2(textureSize(ShadowDepthTexture, 0));
		for(int x = -1; x <= 1; ++x)
		{
			for(int y = -1; y <= 1; ++y)
			{
				float pcfDepth = texture(ShadowDepthTexture, projCoords.xy + vec2(x, y) * texelSize).r; 
				shadow += currentDepth - bias > pcfDepth ? 1.0f : 0.0f;        
			}
		}
		shadow /= 9.0f;
	}

	return shadow;
}

vec4 CalcDirLight(DirLight light, vec4 color)
{
	vec2 vMaskUV = Offset + vTexCoord * Range;
	vec3 mask = texture(Mask123, vMaskUV).rgb;

	vec3 normal;
	// water?
	if (mask.r > 0.003){
		vec2 waterUV = vFragPos.xy / 2.0 + vec2(-WaterUVOffset, 0);
		normal = mix(texture(BakedTerrainNormalTexture, vTexCoord).rgb, texture(WaterNormal, waterUV).rgb, mask.r);

		float caustics = texture(Caustics, waterUV).r;
		caustics *= 2.0;

		color *= mix(1.0, caustics, mask.r);
	}
	else{
		normal = texture(BakedTerrainNormalTexture, vTexCoord).rgb;
	}


	vec3 viewDir = CameraInvFront;
	vec3 lightDir = -light.direction;

	vec3 tint = vec3(0.0);

	for (int i = 0; i < MAX_TERRAIN_LIGHT; ++i)
	{
		if (TerrainLightPos[i].xy == vec2(0.0))
			break;
		float dist = length(vFragPos.xy - TerrainLightPos[i].xy);
		if (dist > TerrainLightColorRange[i].a)
			continue;
		float falloff = (TerrainLightColorRange[i].a - dist) / TerrainLightColorRange[i].a;
		tint += falloff * TerrainLightColorRange[i].rgb;
	}
	float heightLight = (1.0 + TerrainLightHeightStep * vFragPos.z);

	vec3 specular = vec3(0.0);

	// diffuse
	float diff = dot(normal, lightDir);

	// merge
	vec3 ambient  = light.ambient * heightLight;
	vec3 diffuse  = light.diffuse * diff * heightLight;

	ambient = ambient * color.rgb;
	diffuse = diffuse * color.rgb;
	float shadow = 1.0 - max(CalShadow(), 0.0);

	return vec4(ambient + diffuse * shadow  + specular + tint, color.a);
}

void main()
{
	vec4 c = texture(BakedTerrainTexture, vTexCoord);
	
	c = CalcDirLight(dirLight, c);

	fragColor = c;
}
