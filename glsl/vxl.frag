#version {VERSION}
#ifdef GL_ES
precision mediump float;
#endif

#Include:
3df_Noise.glsl
#End Include

{3df_Noise.glsl}

uniform sampler2D Palette, DiffuseTexture;
// uniform vec2 PaletteRows;
// uniform vec2 VplInfo;
uniform bool RenderDepthBuffer;

uniform bool EnableDepthPreview;
uniform vec2 DepthPreviewParams;

// #if __VERSION__ == 120
// varying vec4 vTexCoord;
// varying vec4 vChannelMask;
// varying vec4 vNormalsMask;
// varying mat3 normalTrans;
// varying vec3 FragPos;
// #else
in vec4 vTexCoord;
in vec4 vChannelMask;
in vec4 vNormalsMask;
in mat3 normalTrans;
in vec2 PaletteRows;
in mat4 inverseViewProjection;
in vec2 VplInfo;
in vec4 vTint;
in vec3 vLightModify;
in vec3 vFragPos;

// out vec4 fragColor;
layout (location = 0) out vec4 fragColor;
layout (location = 1) out vec4 AdditionFrag;
// #endif

struct DirLight {
	vec3 direction;

	vec3 ambient;
	vec3 diffuse;
	vec3 specular;
};

uniform DirLight dirLight;
uniform vec3 viewPos;

uniform sampler2D ShadowDepthTexture;
uniform mat4 SunVP;
uniform mat4 InvCameraVP;
uniform float ShadowBias;
uniform int ShadowSampleType;
uniform float AmbientIntencity;
uniform vec2 ViewPort;
uniform bool IsTwist;
uniform float TwistTime;
uniform float TwistMul;
uniform vec2 TwistScreenSize;

float CalShadow(DirLight light){
	// vec4 FragPos = InvCameraVP * vec4(gl_FragCoord.x/ViewPort.x * 2.0 - 1.0, gl_FragCoord.y/ViewPort.y * 2.0 - 1.0, gl_FragCoord.z * 2.0 - 1.0, 1.0);
	// FragPos = FragPos / FragPos.w;
	
	vec4 fragPosLightSpace = SunVP * vec4(vFragPos, 1.0);

	vec3 projCoords = fragPosLightSpace.xyz / fragPosLightSpace.w;
	projCoords = projCoords * 0.5f + 0.5f;
	float currentDepth = projCoords.z;

	float shadow = 0.0f;
	float bias = ShadowBias * 0.2f;

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

// Strangely, the VXL is too bright when scale smaller , so make color darker
vec3 CalcDirLight(DirLight light, vec3 normal, vec3 viewDir, vec3 color)
{
	vec3 lightDir = -light.direction;
	// diffuse
	float diff = max(dot(normal, lightDir), 0.0);
	// specular
	// vec3 reflectDir = reflect(-lightDir, normal);
	// float spec = pow(max(dot(viewDir, reflectDir), 0.0), 0.5f);

	vec3 halfwayDir = normalize(lightDir + viewDir);
	float spec = pow(max(dot(viewDir, halfwayDir), 0.0), 0.5f);
	// merge
	float shadowMul = (1.0f - CalShadow(light));
	vec3 ambient  = light.ambient  * color * vLightModify.y;
	vec3 diffuse  = light.diffuse  * diff * color  * vLightModify.x * shadowMul;
	vec3 specular = light.specular * spec * vLightModify.z * shadowMul;
	return ambient + diffuse + specular;
}

void main()
{
	vec4 x = texture(DiffuseTexture, vTexCoord.st);
	float colorIndex = dot(x, vChannelMask);
	vec4 color = texture(Palette, vec2(dot(x, vChannelMask), PaletteRows.x));
	if (color.a < 0.01)
		discard;

	if (RenderDepthBuffer){
		return;
	}
	
	if (EnableDepthPreview)
	{
		float intensity = 1.0 - gl_FragCoord.z;
		fragColor = vec4(vec3(intensity), 1.0);
	}
	else{
		vec4 y = texture(DiffuseTexture, vTexCoord.pq);
		float normaIndex = dot(y, vNormalsMask);
		vec4 normal = (2.0 * texture(Palette, vec2(normaIndex, PaletteRows.y)) - 1.0);
		normal.w = 0.0;
		normal = normalize(normal);
		vec3 worldNormal = normalTrans * normal.xyz;

		vec3 FragPos = (inverseViewProjection * gl_FragCoord).xyz;
		vec3 viewDir = normalize(viewPos - FragPos);
		vec3 result = vec3(0);
		result = CalcDirLight(dirLight, worldNormal, viewDir, color.xyz);
		if (vTint.a < 0.0f)
			result = vTint.rgb;
		else
			result *= vTint.rgb;

		fragColor =vec4(result.rgb, color.a);

		if (IsTwist){
			AdditionFrag = vec4(vec3((snoise(gl_FragCoord.xy / TwistScreenSize + vec2(TwistTime)) - 0.5) * vTint.a * -TwistMul), vTint.a);
		}
		else
		{
			AdditionFrag = vec4(0,0,0,1.0);
		}
	}
}

