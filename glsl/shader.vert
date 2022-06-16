#version {VERSION}

#define MAX_BONE_LENGTH 4

in vec3 aVertexPos;
in vec3 aNormal;
in vec2 aTexCoords;
in uint aDrawPart;
in ivec4 aBoneId;
in vec4 aBoneWidget;

in vec4 iModelV1;
in vec4 iModelV2;
in vec4 iModelV3;
in vec4 iModelV4;
in vec4 iTint;
in vec3 iRemap;
in uint iDrawId;
in uint iDrawMask;

out vec2 TexCoords;
out vec3 Normal;
out vec3 FragPos;

out vec3 vRemap;
out vec4 vTint;

flat out int isDraw;

uniform mat4 view;
uniform mat4 projection;
uniform mat4 rotationFix;

uniform bool skeletonBinded;
uniform bool useDQB;

uniform sampler2D boneAnimTexture;


mat4 GetMat4ById(int id){
	ivec2 startuv= ivec2(id * 4, int(iDrawId) );

	vec4 c1 = texelFetch(boneAnimTexture, startuv, 0);
	vec4 c2 = texelFetch(boneAnimTexture, ivec2(startuv.x + 1,startuv.y), 0);
	vec4 c3 = texelFetch(boneAnimTexture, ivec2(startuv.x + 2,startuv.y), 0);
	vec4 c4 = texelFetch(boneAnimTexture, ivec2(startuv.x + 3,startuv.y), 0);

	return mat4(c1,c2,c3,c4);
}

mat2x4 GetMat2x4ById(int id){
	ivec2 startuv= ivec2(id * 3, int(iDrawId) );

	vec4 c1 = texelFetch(boneAnimTexture, startuv, 0);
	vec4 c2 = texelFetch(boneAnimTexture, ivec2(startuv.x + 1,startuv.y), 0);

	return mat2x4(c1,c2);
}

vec3 GetScaleVecById(int id){
	ivec2 startuv= ivec2(id * 3 + 2, int(iDrawId) );

	return texelFetch(boneAnimTexture, startuv, 0).rgb;
}

mat2x4 GetBoneTransform(ivec4 joints, vec4 weights)
{
	float sum_weight = weights.x + weights.y + weights.z + weights.w;

	// Fetch bones
	mat2x4 dq0 = GetMat2x4ById(joints.x);
	mat2x4 dq1 = GetMat2x4ById(joints.y);
	mat2x4 dq2 = GetMat2x4ById(joints.z);
	mat2x4 dq3 = GetMat2x4ById(joints.w);

	// Ensure all bone transforms are in the same neighbourhood
	weights.y *= sign(dot(dq0[0], dq1[0]));
	weights.z *= sign(dot(dq0[0], dq2[0]));
	weights.w *= sign(dot(dq0[0], dq3[0]));

	// Blend
	mat2x4 result =
		weights.x * dq0 +
		weights.y * dq1 +
		weights.z * dq2 +
		weights.w * dq3;

	result[0][3] += float(int(sum_weight < 1.0f)) * (1.0f - sum_weight);

	// Normalise
	float norm = length(result[0]);
	return result / norm;
}

mat4 GetSkinMatrix()
{
	ivec4 joints = aBoneId;// ivec4(aBoneId[0], aBoneId[1], aBoneId[2], aBoneId[3]);
	vec4 weights = aBoneWidget;//vec4(aBoneWidget[0],aBoneWidget[1],aBoneWidget[2],aBoneWidget[3]);
	mat2x4 bone = GetBoneTransform(joints, weights);

	vec4 r = bone[0];
	vec4 t = bone[1];

	return mat4(
		1.0 - (2.0 * r.y * r.y) - (2.0 * r.z * r.z),
			  (2.0 * r.x * r.y) + (2.0 * r.w * r.z),
			  (2.0 * r.x * r.z) - (2.0 * r.w * r.y),
		0.0,

			  (2.0 * r.x * r.y) - (2.0 * r.w * r.z),
		1.0 - (2.0 * r.x * r.x) - (2.0 * r.z * r.z),
			  (2.0 * r.y * r.z) + (2.0 * r.w * r.x),
		0.0,

			  (2.0 * r.x * r.z) + (2.0 * r.w * r.y),
			  (2.0 * r.y * r.z) - (2.0 * r.w * r.x),
		1.0 - (2.0 * r.x * r.x) - (2.0 * r.y * r.y),
		0.0,

		2.0 * (-t.w * r.x + t.x * r.w - t.y * r.z + t.z * r.y),
		2.0 * (-t.w * r.y + t.x * r.z + t.y * r.w - t.z * r.x),
		2.0 * (-t.w * r.z - t.x * r.y + t.y * r.x + t.z * r.w),
		1);
}

void main()
{
	if ((aDrawPart & iDrawMask) == uint(0x00000000))
	{
		isDraw = 0;
		return;
	}
	else{
		isDraw = 1;
	}

	mat4 mMatrix = mat4(0.0f);
	if (skeletonBinded)
	{
		mat4 scaleMatrix = mat4(0.0f);
		vec3 scale = vec3(0.0f);

		if (useDQB){
			// dqs混合 Skin
			if (aBoneWidget[0] == 0.0f)
				scaleMatrix = mat4(1.0f);
			else{
				for (int i = 0; i < MAX_BONE_LENGTH; i++){
					scale += GetScaleVecById(aBoneId[i]) * aBoneWidget[i];
				}
				scaleMatrix[0][0] = scale[0];
				scaleMatrix[1][1] = scale[1];
				scaleMatrix[2][2] = scale[2];
				scaleMatrix[3][3] = 1.0f;
			}
			mMatrix = GetSkinMatrix() * scaleMatrix;
		}
		else{
			// 线性混合 Skin
			for (int i = 0; i < MAX_BONE_LENGTH; i++)
				mMatrix += GetMat4ById(aBoneId[i]) * aBoneWidget[i];
			if (aBoneWidget[0] == 0.0f)
				mMatrix = mat4(1.0f);
		}
	}
	else
	{
		mMatrix =  mat4(iModelV1, iModelV2, iModelV3, iModelV4);
	}
	mMatrix = mMatrix * rotationFix;
	vec4 fragP = mMatrix * vec4(aVertexPos, 1.0f);
	vRemap = iRemap;
	vTint = iTint;
	gl_Position = projection * view * fragP;
	FragPos = fragP.xyz;
	TexCoords = aTexCoords;
	Normal = normalize(mat3(transpose(inverse(mMatrix))) * aNormal);
}


