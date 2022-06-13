#version {VERSION}
#ifdef GL_ES
precision mediump float;
#endif

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
out vec4 fragColor;
// #endif

struct DirLight {
	vec3 direction;

	vec3 ambient;
	vec3 diffuse;
	vec3 specular;
};

uniform DirLight dirLight;
uniform vec3 viewPos;

// Strangely, the VXL is too bright when scale smaller , so make color darker
vec3 CalcDirLight(DirLight light, vec3 normal, vec3 viewDir, vec3 color)
{
	vec3 lightDir = -light.direction;
	// diffuse
	float diff = max(dot(normal, lightDir), 0.0);
	// specular
	vec3 reflectDir = reflect(-lightDir, normal);
	float spec = pow(max(dot(viewDir, reflectDir), 0.0), 0.5f);
	// merge
	vec3 ambient  = light.ambient  * color * 1.2f;
	vec3 diffuse  = light.diffuse  * diff * color  * 0.4f;
	vec3 specular = light.specular * spec * 0.13f;
	return (ambient + diffuse) + specular;
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

	if (vTint.a < 0.0f)
		color = vec4(vTint.rgb, -vTint.a);
	else
	{
		color *= vTint;
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
		fragColor =vec4(result.rgb, color.a);
	}
}

