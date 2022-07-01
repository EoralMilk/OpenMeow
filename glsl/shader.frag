#version {VERSION}

#ifdef GL_ES
precision mediump float;
#endif

struct BlinnPhongMaterial {
	bool hasDiffuseMap;
	vec3      diffuseTint;
	sampler2D diffuse;

	bool hasSpecularMap;
	vec3      specularTint;
	float     shininess;
	sampler2D specular;
};

struct PBRMaterial {
	bool hasAlbedoMap;
	vec3      albedoTint;
	sampler2D albedoMap;

	bool hasRoughnessMap;
	float      roughness;
	sampler2D roughnessMap;

	bool hasMetallicMap;
	float      metallic;
	sampler2D metallicMap;

	bool hasAOMap;
	float      ao;
	sampler2D aoMap;
};

struct DirLight {
	vec3 direction;

	vec3 ambient;
	vec3 diffuse;
	vec3 specular;
};  

uniform DirLight dirLight;

uniform bool isCloth;
uniform bool usePBR;
uniform bool usePBRBody;
uniform BlinnPhongMaterial mainMaterial;
uniform BlinnPhongMaterial bodyMaterial;
uniform PBRMaterial pbrMaterial;
uniform PBRMaterial pbrBodyMaterial;
out vec4 FragColor;

in vec3 Normal;
in vec3 FragPos;
in vec2 TexCoords;
in vec4 vTint;
in vec3 vRemap;

flat in uint drawPart;
flat in int isDraw;

uniform vec3 viewPos;
uniform bool EnableDepthPreview;
uniform vec2 DepthPreviewParams;
uniform bool RenderDepthBuffer;
const float PI = 3.14159265359;

uniform sampler2D ShadowDepthTexture;
uniform mat4 SunVP;
uniform mat4 InvCameraVP;
uniform float ShadowBias;
uniform float AmbientIntencity;

float CalShadow(DirLight light, vec3 normal){
	vec4 fragPosLightSpace = SunVP * vec4(FragPos, 1.0);
	vec3 projCoords = fragPosLightSpace.xyz / fragPosLightSpace.w;
	projCoords = projCoords * 0.5f + 0.5f;
	float currentDepth = projCoords.z;

	float shadow = 0.0f;
	float bias = ShadowBias * max(0.02 * (1.0 - dot(normal, light.direction)), 0.0005);

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


vec4 CalcDirLight(DirLight light, vec3 normal, vec3 viewDir, const BlinnPhongMaterial material)
{
	vec3 lightDir = normalize(-light.direction);
	// diffuse
	float diff = max(dot(normal, lightDir), 0.0);
	// specular
	vec3 reflectDir = reflect(-lightDir, normal);
	float spec = pow(max(dot(viewDir, reflectDir), 0.0), material.shininess);
	// merge

	vec3 ambient  = light.ambient;
	vec3 diffuse  = light.diffuse * diff;

	if (material.hasDiffuseMap){
		vec4 color = texture(material.diffuse, TexCoords);

		if (color.a == 0.0)
			discard;
		
		// Hack: remap color used use alpha channel to store tint color
		if (color.a < 0.55)
			color = vec4(vRemap * color.rgb, 1.0);

		ambient = ambient * color.rgb;
		diffuse = diffuse * color.rgb;
	}
	ambient = ambient * material.diffuseTint;
	
	diffuse = diffuse * material.diffuseTint * (1.0f - max(CalShadow(light, normal) - AmbientIntencity, 0.0f));

	vec3 specular = light.specular * spec;
	if (material.hasSpecularMap){
		specular = specular * vec3(texture(material.specular, TexCoords));
	}
	specular = specular * material.specularTint;

	if (vTint.a < 0.0f)
	{
		diffuse =vTint.rgb;
		return vec4((ambient + diffuse + specular), -vTint.a);
	}
	else
	{
		diffuse *= vTint.rgb;
		return vec4((ambient + diffuse + specular), 1.0);
	}
}

// vec3 getNormalFromMap()
// {
// 	vec3 tangentNormal = texture(normalMap, TexCoords).xyz * 2.0 - 1.0;

// 	vec3 Q1  = dFdx(WorldPos);
// 	vec3 Q2  = dFdy(WorldPos);
// 	vec2 st1 = dFdx(TexCoords);
// 	vec2 st2 = dFdy(TexCoords);

// 	vec3 N   = normalize(Normal);
// 	vec3 T  = normalize(Q1*st2.t - Q2*st1.t);
// 	vec3 B  = -normalize(cross(N, T));
// 	mat3 TBN = mat3(T, B, N);

// 	return normalize(TBN * tangentNormal);
// }
// ----------------------------------------------------------------------------
vec3 fresnelSchlick(float cosTheta, vec3 F0)
{
	return F0 + (1.0 - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
}
// ----------------------------------------------------------------------------
float DistributionGGX(vec3 N, vec3 H, float roughness)
{
	float a = roughness*roughness;
	float a2 = a*a;
	float NdotH = max(dot(N, H), 0.0);
	float NdotH2 = NdotH*NdotH;

	float nom   = a2;
	float denom = (NdotH2 * (a2 - 1.0) + 1.0);
	denom = PI * denom * denom;

	return nom / denom;
}
// ----------------------------------------------------------------------------
float GeometrySchlickGGX(float NdotV, float roughness)
{
	float r = (roughness + 1.0);
	float k = (r*r) / 8.0;

	float nom   = NdotV;
	float denom = NdotV * (1.0 - k) + k;

	return nom / denom;
}
// ----------------------------------------------------------------------------
float GeometrySmith(vec3 N, vec3 V, vec3 L, float roughness)
{
	float NdotV = max(dot(N, V), 0.0);
	float NdotL = max(dot(N, L), 0.0);
	float ggx2 = GeometrySchlickGGX(NdotV, roughness);
	float ggx1 = GeometrySchlickGGX(NdotL, roughness);

	return ggx1 * ggx2;
}
// ----------------------------------------------------------------------------
vec4 CalcDirLightPBR(DirLight light, vec3 normal, vec3 viewDir, const PBRMaterial material)
{
	vec3 N = normal;
	vec3 V = viewDir;
	vec3 albedo;
	float metallic;
	float roughness;
	float ao;
	if (material.hasAlbedoMap){
		vec4 texColor = texture(material.albedoMap, TexCoords);
		if (texColor.a == 0.0)
			discard;
		// Hack: remap color used use alpha channel to store tint color
		if (texColor.a < 0.55)
			albedo = vRemap * texColor.rgb * material.albedoTint;
		else
			albedo = texColor.rgb * material.albedoTint;
	}
	else
		albedo = material.albedoTint;
	albedo =pow(albedo, vec3(2.2));

	if (material.hasMetallicMap)
		metallic = texture(material.metallicMap, TexCoords).r;
	else
		metallic = material.metallic;

	if (material.hasRoughnessMap)
		roughness = texture(material.roughnessMap, TexCoords).r;
	else
		roughness = material.roughness;

	if (material.hasAOMap)
		ao = texture(material.aoMap, TexCoords).r;
	else
		ao = material.ao;

	vec3 F0 = vec3(0.04); 
	F0 = mix(F0, albedo, metallic);

	// calculate per-light radiance
	vec3 L = normalize(-light.direction);
	vec3 H = normalize(V + L);

	// cook-torrance brdf
	float NDF = DistributionGGX(N, H, roughness);
	float G   = GeometrySmith(N, V, L, roughness);
	vec3 F    = fresnelSchlick(max(dot(H, V), 0.0), F0);

	vec3 kS = F;
	vec3 kD = vec3(1.0) - kS;
	kD *= 1.0 - metallic;

	vec3 nominator    = NDF * G * F;
	float denominator = 4.0 * max(dot(N, V), 0.0) * max(dot(N, L), 0.0) + 0.0001; 
	vec3 specular     = nominator / denominator;

	// add to outgoing radiance Lo
	float NdotL = max(dot(N, L), 0.0);
	vec3 Lo = (kD * albedo + specular) * (light.diffuse + light.ambient) * NdotL;

	vec3 ambient = albedo * light.ambient * light.ambient * ao;

	vec3 color = ambient + Lo * (1.0f - CalShadow(light, normal));

	color = color / (color + vec3(1.0));
	color = pow(color, vec3(1.0/2.2));  

	if (vTint.a < 0.0f)
	{
		color = vTint.rgb;
	}
	else
	{
		color *= vTint.rgb;
	}

	return vec4(color, 1.0);
} 

void main()
{
	if (RenderDepthBuffer){
		return;
	}
	if (isDraw == 0)
		discard;

	
	bool flag = false;
	if (isCloth && ((drawPart & uint(0x1FF)) !=  uint(0))){
		flag = true;
	}

	vec3 norm = normalize(Normal);
	vec3 viewDir = normalize(viewPos - FragPos);
	vec4 result;
	if (flag)
		if (usePBRBody)
			result = CalcDirLightPBR(dirLight, norm, viewDir, pbrBodyMaterial);
		else
			result = CalcDirLight(dirLight, norm, viewDir, bodyMaterial);
	else
		if (usePBR)
			result = CalcDirLightPBR(dirLight, norm, viewDir, pbrMaterial);
		else
			result = CalcDirLight(dirLight, norm, viewDir, mainMaterial);


	if (EnableDepthPreview)
	{
		float intensity = 1.0 - gl_FragCoord.z;//clamp(DepthPreviewParams.x * gl_FragCoord.z - 0.5 * DepthPreviewParams.x - DepthPreviewParams.y + 0.5, 0.0, 1.0);

		#if __VERSION__ == 120
		gl_FragColor = vec4(vec3(intensity), 1.0);
		#else
		FragColor = vec4(vec3(intensity), 1.0);
		#endif
	}
	else{
		FragColor = result;
	}
}

