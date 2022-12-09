#version {VERSION}

#ifdef GL_ES
precision mediump float;
precision lowp sampler2DArray;
#endif

#Include:
3du_Struct.glsl
3du_LightHeads.glsl
3df_Util.glsl
3df_Shadow.glsl
#End Include

{3du_Struct.glsl}

{3du_LightHeads.glsl}


uniform bool BaseColorHasTexture;
uniform bool BaseCombinedHasTexture;
uniform vec3 BaseDiffuseColor;
uniform float BaseSpecular;
uniform float BaseShininess;
uniform sampler2D BaseColorTexture;
uniform sampler2D BaseCombinedTexture;

float additionAlpha = 0.0;

{3df_Shadow.glsl}

{3df_Util.glsl}

vec4 GetColor(){
	vec4 body, addition;
	if (BaseColorHasTexture)
		body = texture(BaseColorTexture, TexCoords);
	else
		body = vec4(BaseDiffuseColor, 1.0);

	if (fMaterial.r < 0)
	{
		addition = vec4(0.0);
	}
	else{
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
	}

	additionAlpha = addition.a;
	return vec4(mix(body.rgb,addition.rgb,additionAlpha),max(body.a, additionAlpha));
}

vec4 GetCombinedColor(){
	vec4 body, addition;
	if (BaseCombinedHasTexture)
		body = texture(BaseCombinedTexture, TexCoords);
	else
		body = vec4(0, BaseSpecular, 0, 1.0);

	if (fMaterial.g < 0)
	{
		addition = vec4(
			float((fMaterial.g >> 16) & 255) / 255.0,
			float((fMaterial.g >> 8) & 255) / 255.0,
			float(fMaterial.g & 255) / 255.0,
			1.0);
	}
	else{
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
	}

	return mix(body,addition,additionAlpha);
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
	float spec = pow(max(dot(normal, halfwayDir), 0.0), mix(BaseShininess,float(fMaterial.z) / 100.0,additionAlpha)) * combined.g;
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

