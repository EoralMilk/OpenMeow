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
	vec3 ambient  = light.ambient  * color;
	vec3 diffuse  = light.diffuse  * diff * color  * 0.5f;
	vec3 specular = light.specular * spec * 0.1f;
	return (ambient + diffuse) + specular;
}

void main()
{
	// #if __VERSION__ == 120

	// vec4 x = texture2D(DiffuseTexture, vTexCoord.st);
	// vec4 color = texture2D(Palette, vec2(dot(x, vChannelMask), PaletteRows.x));
	// if (color.a < 0.01)
	// 	discard;
	// vec4 y = texture2D(DiffuseTexture, vTexCoord.pq);
	// vec4 normal = normalize((2.0 * texture2D(Palette, vec2(dot(y, vNormalsMask), PaletteRows.y)) - 1.0));
	// vec3 intensity = AmbientLight + DiffuseLight * max(dot(normal, LightDirection), 0.0);
	// gl_FragColor = vec4(intensity * color.rgb, color.a);

	// #else
	vec4 x = texture(DiffuseTexture, vTexCoord.st);
	float colorIndex = dot(x, vChannelMask);
	vec4 color = texture(Palette, vec2(dot(x, vChannelMask), PaletteRows.x));
	if (color.a < 0.01)
		discard;
	if (vTint.a < 0.0f)
		color = vec4(vTint.rgb, -vTint.a);
	else
		color *= vTint;
	
	if (EnableDepthPreview)
	{
		float intensity = 1.0 - gl_FragCoord.z;//clamp(DepthPreviewParams.x * gl_FragCoord.z - 0.5 * DepthPreviewParams.x - DepthPreviewParams.y + 0.5, 0.0, 1.0);
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



	// //AcosAngle --> 0 is more lighter
	// float acosAngle = acos(dot(worldNormal.xyz, -dirLight.direction));
	// float halfPi =1.5707963;
	// float vxlPaletteStartIndex=VplInfo.x;
	// float PaletteHeight =VplInfo.y;
	// //AcosAngle Rangle  0 -> PI
	// if (acosAngle >= halfPi)
	// {
	// 		vec4 cc = texture(Palette, vec2(colorIndex, (vxlPaletteStartIndex+0.5)/PaletteHeight));
	// 		color = texture(Palette, vec2(cc.r, PaletteRows.x));
	// }
	// else
	// {
	// 		float nIndex = float(31 - int(acosAngle/halfPi*32.0));
	// 		if(nIndex > 31.0)
	// 		{
	// 			nIndex =31.0;
	// 		}
	// 		if(nIndex <0.0)
	// 		{
	// 			nIndex =31.0;
	// 		}
	// 		vec4 cc = texture(Palette, vec2(colorIndex, (vxlPaletteStartIndex+nIndex +0.5)/PaletteHeight));
	// 		color = texture(Palette, vec2(cc.r, PaletteRows.x));
	// }
	// fragColor =vec4(color.rgb,color.a);

	// #endif
}

