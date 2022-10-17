#version {VERSION}

#ifdef GL_ES
precision mediump float;
precision lowp sampler2DArray;
#endif

struct DirLight {
	vec3 direction;

	vec3 ambient;
	vec3 diffuse;
	vec3 specular;
};  

uniform DirLight dirLight;

uniform bool additionalLayer;

out vec4 FragColor;

in vec3 Normal;
in vec3 FragPos;
in vec2 TexCoords;
in vec4 vTint;
in vec3 vRemap;
// x is colormap index, y is combinedmap index
// b is Shininess * 100
flat in ivec4 fMaterial;

uniform sampler2DArray Textures64;
uniform sampler2DArray Textures128;
uniform sampler2DArray Textures256;
uniform sampler2DArray Textures512;
uniform sampler2DArray Textures1024;

uniform bool BaseColorHasTexture;
uniform bool BaseCombinedHasTexture;
uniform vec3 BaseDiffuseColor;
uniform float BaseSpecular;
uniform sampler2D BaseColorTexture;
uniform sampler2D BaseCombinedTexture;

uniform vec3 viewPos;
uniform bool EnableDepthPreview;
uniform vec2 DepthPreviewParams;
uniform bool RenderDepthBuffer;
const float PI = 3.14159265359;

uniform sampler2D ShadowDepthTexture;
uniform mat4 SunVP;
uniform mat4 InvCameraVP;
uniform float ShadowBias;
uniform int ShadowSampleType;
uniform float AmbientIntencity;

vec4 GetColor(){
	if (fMaterial.r < 0)
	{
		if (BaseColorHasTexture)
			return texture(BaseColorTexture, TexCoords);
		else
			return vec4(BaseDiffuseColor, 1.0);
	}
	else{
		vec4 body;
		if (BaseColorHasTexture)
			body = texture(BaseColorTexture, TexCoords);
		else
			body = vec4(BaseDiffuseColor, 1.0);
		vec4 addition;
		switch(fMaterial.a){
			case 64:
				addition = texture(Textures64, vec3(TexCoords, float(fMaterial.r)));
			case 128:
				addition = texture(Textures128, vec3(TexCoords, float(fMaterial.r)));
			case 256:
				addition = texture(Textures256, vec3(TexCoords, float(fMaterial.r)));
			case 512:
				addition = texture(Textures512, vec3(TexCoords, float(fMaterial.r)));
			case 1024:
				addition = texture(Textures1024, vec3(TexCoords, float(fMaterial.r)));
		}

		return vec4(mix(body.rgb,addition.rgb,addition.a),max(body.a, addition.a));
	}
}

vec4 GetCombinedColor(){
	if (fMaterial.g < 0)
	{
		if (BaseCombinedHasTexture)
			return texture(BaseCombinedTexture, TexCoords);
		else
			return vec4(0, BaseSpecular, 0, 1.0);
	}
	else{
		vec4 body;
		if (BaseCombinedHasTexture)
			body = texture(BaseCombinedTexture, TexCoords);
		else
			body = vec4(0, BaseSpecular, 0, 1.0);
		vec4 addition;
		switch(fMaterial.a){
			case 64:
				addition = texture(Textures64, vec3(TexCoords, float(fMaterial.g)));
			case 128:
				addition =  texture(Textures128, vec3(TexCoords, float(fMaterial.g)));
			case 256:
				addition =  texture(Textures256, vec3(TexCoords, float(fMaterial.g)));
			case 512:
				addition =  texture(Textures512, vec3(TexCoords, float(fMaterial.g)));
			case 1024:
				addition =  texture(Textures1024, vec3(TexCoords, float(fMaterial.g)));
		}

		return mix(body,addition,addition.a);
	}
}

float CalShadow(DirLight light, vec3 normal){
	vec4 fragPosLightSpace = SunVP * vec4(FragPos, 1.0);
	vec3 projCoords = fragPosLightSpace.xyz / fragPosLightSpace.w;
	projCoords = projCoords * 0.5f + 0.5f;
	float currentDepth = projCoords.z;

	float shadow = 0.0f;
	float bias = ShadowBias * max(0.02 * (1.0 - dot(normal, light.direction)), 0.0005);

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


vec4 CalcDirLight(DirLight light, vec3 normal, vec3 viewDir)
{
	vec3 lightDir = normalize(-light.direction);
	// diffuse
	float diff = max(dot(normal, lightDir), 0.0);

	vec4 color = GetColor();
	if (color.a == 0.0)
		discard;
	
	vec4 combined = GetCombinedColor();
	vec3 col = mix(color.rgb, vRemap, combined.r);

	// specular
	vec3 halfwayDir = normalize(lightDir + viewDir);
	float spec = pow(max(dot(viewDir, halfwayDir), 0.0), float(fMaterial.b) / 100.0) * combined.g * 2.0;
	vec3 specular = light.specular * spec;

	// merge
	vec3 ambient  = light.ambient * col;
	vec3 diffuse  = light.diffuse * diff * col;
	
	// shadow
	diffuse = diffuse * (1.0f - max(CalShadow(light, normal) - AmbientIntencity, 0.0f));

	return vec4(mix((ambient + diffuse + specular),col,combined.b), color.a);
}

void main()
{
	if (RenderDepthBuffer){
		return;
	}

	if (EnableDepthPreview)
	{
		float intensity = 1.0 - gl_FragCoord.z;
		FragColor = vec4(vec3(intensity), 1.0);
	}
	else{
		vec4 result;
		result = CalcDirLight(dirLight, Normal, normalize(viewPos - FragPos));

		if (vTint.a < 0.0f)
		{
			result = vec4(vTint.rgb, -vTint.a);
		}
		else
		{
			result *= vTint;
		}

		FragColor = result;
	}
}
