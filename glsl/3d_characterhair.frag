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
3df_LightCal.glsl
3df_LightMain.glsl
#End Include

{3du_Struct.glsl}

{3du_LightHeads.glsl}

{3df_Shadow.glsl}

{3df_Util.glsl}

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
	return vec4(
		float((fMaterial.g >> 16) & 255) / 255.0,
		float((fMaterial.g >> 8) & 255) / 255.0,
		float(fMaterial.g & 255) / 255.0,
		1.0);
}

void CalResultColor(){
	shininess = float(fMaterial.z) / 100.0;
	// vec3 col = mix(color.rgb, vRemap, combined.r);
	// vRemap is hair color
	color = vec4(vec3(color.r) * vRemap + vec3(color.g) * 0.15, color.a);
}

{3df_LightCal.glsl}

{3df_LightMain.glsl}
