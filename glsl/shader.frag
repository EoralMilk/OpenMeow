#version {VERSION}

#ifdef GL_ES
precision mediump float;
#endif

struct Material {
	bool hasDiffuseMap;
	vec3      diffuseTint;
	sampler2D diffuse;

	bool hasSpecularMap;
	vec3      specularTint;
	float     shininess;
	sampler2D specular;
}; 


struct DirLight {
	vec3 direction;

	vec3 ambient;
	vec3 diffuse;
	vec3 specular;
};  

uniform DirLight dirLight;

uniform bool isCloth;
uniform Material mainMaterial;
uniform Material bodyMaterial;
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

vec4 CalcDirLight(DirLight light, vec3 normal, vec3 viewDir, const Material material)
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
	diffuse = diffuse * material.diffuseTint;

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
		result = CalcDirLight(dirLight, norm, viewDir, bodyMaterial);
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