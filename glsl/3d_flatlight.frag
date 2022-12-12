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
3df_LightMain.glsl
#End Include

{3du_Struct.glsl}

{3du_LightHeads.glsl}

vec4 GetColor(){
	if (fMaterial.r < 0)
		return vec4(
			float((fMaterial.r >> 16) & 255) / 255.0,
			float((fMaterial.r >> 8) & 255) / 255.0,
			float(fMaterial.r & 255) / 255.0,
			1.0);
	else
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


vec4 CalcDirLight(DirLight light, vec3 normal, vec3 viewDir)
{
	vec4 color = GetColor();
	if (color.a == 0.0)
		discard;

	return color;
}

{3df_LightMain.glsl}