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
// x is colormap index, y is combinedmap index, z is Shininess x 100, w is textureSizeType
// combined texture: r is remap, g is Specular, b is emission
flat in ivec4 fMaterial;

uniform sampler2DArray Textures64;
uniform sampler2DArray Textures128;
uniform sampler2DArray Textures256;
uniform sampler2DArray Textures512;
uniform sampler2DArray Textures1024;


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

// hair should has texture
vec4 GetColor(){
	switch(fMaterial.a){
		case 64:
			return texture(Textures64, vec3(TexCoords, float(fMaterial.r)));
		case 128:
			return texture(Textures128, vec3(TexCoords, float(fMaterial.r)));
		case 256:
			return texture(Textures256, vec3(TexCoords, float(fMaterial.r)));
		case 512:
			return texture(Textures512, vec3(TexCoords, float(fMaterial.r)));
		case 1024:
			return texture(Textures1024, vec3(TexCoords, float(fMaterial.r)));
	}
}

vec4 GetCombinedColor(){
	if (fMaterial.g < 0)
		return vec4(
			float((fMaterial.g >> 16) & 255) / 255.0,
			float((fMaterial.g >> 8) & 255) / 255.0,
			float(fMaterial.g & 255) / 255.0,
			1.0);
	else
		switch(fMaterial.a){
			case 64:
				return texture(Textures64, vec3(TexCoords, float(fMaterial.g)));
			case 128:
				return texture(Textures128, vec3(TexCoords, float(fMaterial.g)));
			case 256:
				return texture(Textures256, vec3(TexCoords, float(fMaterial.g)));
			case 512:
				return texture(Textures512, vec3(TexCoords, float(fMaterial.g)));
			case 1024:
				return texture(Textures1024, vec3(TexCoords, float(fMaterial.g)));
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

	// r: the base brightness value, g: the high light value
	vec4 color = GetColor();
	if (color.a == 0.0)
		discard;
	
	vec4 combined = GetCombinedColor();
	// vec3 col = mix(color.rgb, vRemap, combined.r);
	// vRemap is hair color
	vec3 col = vec3(color.r) * vRemap + vec3(color.g) * 0.2;

	// specular
	vec3 halfwayDir = normalize(lightDir + viewDir);
	float spec = pow(max(dot(viewDir, halfwayDir), 0.0), float(fMaterial.z) / 100.0) * combined.g * 2.0;
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

