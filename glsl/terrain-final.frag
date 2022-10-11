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


// r is water mask
uniform sampler2D Mask123;

uniform float WaterUVOffset;
uniform float WaterUVOffset2;


uniform sampler2D Caustics;

uniform sampler2D WaterNormal;
uniform sampler2D MaskCloud;

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
uniform int ShadowSampleType;
uniform float ShadowBias;
uniform float AmbientIntencity;

float CalShadow(){
	vec4 fragPosLightSpace = SunVP * vec4(vFragPos, 1.0);
	vec3 projCoords = fragPosLightSpace.xyz / fragPosLightSpace.w;
	projCoords = projCoords * 0.5f + 0.5f;
	float currentDepth = projCoords.z;

	float shadow = 0.0f;
	float bias = ShadowBias * 0.025f;

	// soft shadow
	if(projCoords.z <= 1.0f)
	{
		vec2 texelSize;
		float pcfDepth;
		switch (ShadowSampleType){
			case 0: // Directly
				pcfDepth = texture(ShadowDepthTexture, projCoords.xy).r; 
				shadow = currentDepth - bias > pcfDepth ? 1.0f : 0.0f;
				break;
			case 1: // SlashBlend
				texelSize = 1.0f / vec2(textureSize(ShadowDepthTexture, 0));

				pcfDepth = texture(ShadowDepthTexture, projCoords.xy + vec2(-1, -1) * texelSize).r; 
				shadow += currentDepth - bias > pcfDepth ? 1.0f : 0.0f;

				pcfDepth = texture(ShadowDepthTexture, projCoords.xy + vec2(0, 0) * texelSize).r; 
				shadow += currentDepth - bias > pcfDepth ? 1.0f : 0.0f;

				pcfDepth = texture(ShadowDepthTexture, projCoords.xy + vec2(1, 1) * texelSize).r; 
				shadow += currentDepth - bias > pcfDepth ? 1.0f : 0.0f;

				shadow /= 3.0f;
				break;
			case 2: // CrossBlend
				texelSize = 1.0f / vec2(textureSize(ShadowDepthTexture, 0));

				pcfDepth = texture(ShadowDepthTexture, projCoords.xy + vec2(-1, 0) * texelSize).r; 
				shadow += currentDepth - bias > pcfDepth ? 1.0f : 0.0f;

				pcfDepth = texture(ShadowDepthTexture, projCoords.xy + vec2(0, 0) * texelSize).r; 
				shadow += currentDepth - bias > pcfDepth ? 1.0f : 0.0f;

				pcfDepth = texture(ShadowDepthTexture, projCoords.xy + vec2(1, 0) * texelSize).r; 
				shadow += currentDepth - bias > pcfDepth ? 1.0f : 0.0f;

				pcfDepth = texture(ShadowDepthTexture, projCoords.xy + vec2(0, 1) * texelSize).r; 
				shadow += currentDepth - bias > pcfDepth ? 1.0f : 0.0f;

				pcfDepth = texture(ShadowDepthTexture, projCoords.xy + vec2(0, -1) * texelSize).r; 
				shadow += currentDepth - bias > pcfDepth ? 1.0f : 0.0f;

				shadow /= 5.0f;
				break;
			case 3: // TictactoeBlend
				texelSize = 1.0f / vec2(textureSize(ShadowDepthTexture, 0));
				for(int x = -1; x <= 1; ++x)
				{
					for(int y = -1; y <= 1; ++y)
					{
						pcfDepth = texture(ShadowDepthTexture, projCoords.xy + vec2(x, y) * texelSize).r; 
						shadow += currentDepth - bias > pcfDepth ? 1.0f : 0.0f;        
					}
				}
				shadow /= 9.0f;
				break;
			default: // Directly
				pcfDepth = texture(ShadowDepthTexture, projCoords.xy).r; 
				shadow = currentDepth - bias > pcfDepth ? 1.0f : 0.0f;
				break;
		}
	}

	return shadow;
}

vec4 CalcDirLight(DirLight light, vec4 color)
{
	vec3 mask = texture(Mask123, vTexCoord).rgb;

	vec3 normal;
	// water?
	if (mask.r > 0.0){
	// if (mask.r < -1.0){
		vec2 waterUV = vFragPos.xy;
		vec4 cm = texture(MaskCloud, waterUV / 8.0 + vec2(WaterUVOffset2, WaterUVOffset2));
		vec2 wuv = waterUV / 3.5 + vec2(-WaterUVOffset, 0);
		wuv = wuv - vec2(floor(wuv.x), floor(wuv.y));
		vec2 cuv = waterUV / 6.3 + vec2(-WaterUVOffset, 0);
		cuv = cuv - vec2(floor(cuv.x), floor(cuv.y));

		normal = mix(texture(BakedTerrainNormalTexture, vTexCoord).rgb, 
					texture(WaterNormal, wuv).rgb, 
					mask.r);

		float causticsBlend = mix(mix(0.66, 0.5, (mask.r - 0.66) / 0.34),mask.r,step(mask.r,0.66));

		vec4 caustics = texture(Caustics, cuv);
		float ci = mix(mix(caustics.r,caustics.g,cm.r), mix(caustics.b,caustics.a,cm.g), cm.b);

		color *= mix(1.0, ci * 1.75 + 0.75, causticsBlend);
	}
	else{
		normal = texture(BakedTerrainNormalTexture, vTexCoord).rgb;
	}



	vec3 tint = vec3(0.0);

	for (int i = 0; i < MAX_TERRAIN_LIGHT; ++i)
	{
		if (TerrainLightPos[i].xy == vec2(0.0))
			break;
		float dist = length(vFragPos.xyz - TerrainLightPos[i].xyz);
		if (dist > TerrainLightColorRange[i].a)
			continue;
		float falloff = (TerrainLightColorRange[i].a - dist) / TerrainLightColorRange[i].a;
		tint += falloff * TerrainLightColorRange[i].rgb;
	}
	float heightLight = (1.0 + TerrainLightHeightStep * vFragPos.z);

	normal = normalize(normal * 2.0 - 1.0);
	vec3 viewDir = CameraInvFront;
	vec3 lightDir = -light.direction;

	vec3 specular = vec3(0.0);

	// diffuse
	float diff = dot(normal, lightDir);

	// merge
	vec3 ambient  = light.ambient * heightLight;
	vec3 diffuse  = light.diffuse * max(diff, 0.0) * heightLight;

	ambient = ambient * color.rgb;
	diffuse = diffuse * color.rgb;

	// simulate self-cast shadow
	float shadow = 1.0 - max(CalShadow(), -diff);

	return vec4(ambient + diffuse * shadow + specular + tint, color.a);
}

void main()
{
	vec4 c = texture(BakedTerrainTexture, vTexCoord);
	
	c = CalcDirLight(dirLight, c);

	fragColor = c;
}
