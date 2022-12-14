#version {VERSION}

#define MAX_BONE_LENGTH 4
#define BONE_TEX_OFFSET 6

#ifdef GL_ES
precision mediump float;
#endif

in vec3 aVertexPos;
in vec3 aNormal;
in vec2 aTexCoords;
in ivec4 aBoneId;
in vec4 aBoneWeights;

in vec4 iModelV1;
in vec4 iModelV2;
in vec4 iModelV3;
in vec4 iModelV4;
in vec4 iTint;
in vec3 iRemap;
in int iDrawId;
in ivec4 iMaterial;

out vec2 TexCoords;
out vec3 Normal;
out vec3 FragPos;
out vec3 vRemap;
out vec4 vTint;
flat out ivec4 fMaterial;

uniform mat4 view;
uniform mat4 projection;
uniform mat4 rotationFix;

uniform bool useDQB;

// bind pose might contains modified adjust bone's rest pose from rig bone
// but there is no different to shader anyway.
uniform bool FixedBindTransform;
uniform int BoneTexOffset;
uniform mat4 BindTransformData[128];

uniform sampler2D boneAnimTexture;
// uniform int skinBoneCount;
uniform int skinBoneTexWidth;

// mat2x4 bindDq1, bindDq2, bindDq3, bindDq4;
// vec3 bindS1, bindS2, bindS3, bindS4;
mat4 m1, m2, m3 ,m4;
vec3 s1, s2, s3, s4;
mat4 mMatrix = mat4(0.0f);

int animTexoffset;
mat4 GetMat4ById(int id){
	int y = (animTexoffset + id * BoneTexOffset) / skinBoneTexWidth;
	int x = (animTexoffset + id * BoneTexOffset) % skinBoneTexWidth;
	vec4 r1 = texelFetch(boneAnimTexture, ivec2(x, y), 0);
	y = (animTexoffset + id * BoneTexOffset + 1) / skinBoneTexWidth;
	x = (animTexoffset + id * BoneTexOffset + 1) % skinBoneTexWidth;
	vec4 r2 = texelFetch(boneAnimTexture, ivec2(x, y), 0);
	y = (animTexoffset + id * BoneTexOffset + 2) / skinBoneTexWidth;
	x = (animTexoffset + id * BoneTexOffset + 2) % skinBoneTexWidth;
	vec4 r3 = texelFetch(boneAnimTexture, ivec2(x, y), 0);

	return  mat4(
		vec4(r1.x, r2.x, r3.x, 0),
		vec4(r1.y, r2.y, r3.y, 0),
		vec4(r1.z, r2.z, r3.z, 0),
		vec4(r1.w, r2.w, r3.w, 1));
}

mat4 GetBindMat4ById(int id){
	if (FixedBindTransform)
		return BindTransformData[id];

	int y = (animTexoffset + id * BoneTexOffset + 3) / skinBoneTexWidth;
	int x = (animTexoffset + id * BoneTexOffset + 3) % skinBoneTexWidth;
	vec4 r1 = texelFetch(boneAnimTexture, ivec2(x, y), 0);
	y = (animTexoffset + id * BoneTexOffset + 4) / skinBoneTexWidth;
	x = (animTexoffset + id * BoneTexOffset + 4) % skinBoneTexWidth;
	vec4 r2 = texelFetch(boneAnimTexture, ivec2(x, y), 0);
	y = (animTexoffset + id * BoneTexOffset + 5) / skinBoneTexWidth;
	x = (animTexoffset + id * BoneTexOffset + 5) % skinBoneTexWidth;
	vec4 r3 = texelFetch(boneAnimTexture, ivec2(x, y), 0);

	return  mat4(
		vec4(r1.x, r2.x, r3.x, 0),
		vec4(r1.y, r2.y, r3.y, 0),
		vec4(r1.z, r2.z, r3.z, 0),
		vec4(r1.w, r2.w, r3.w, 1));
}

vec3 ExtractScale(const mat4 m){
	return vec3(length(m[0]), length(m[1]), length(m[2]));
}

// extract quaternion from matrix
vec4 ExtractRotation(const mat4 mm, const vec3 scale)
{
	mat3 m;
	m[0] = mm[0].xyz / scale.x;
	m[1] = mm[1].xyz / scale.y;
	m[2] = mm[2].xyz / scale.z;

	float fourXSquaredMinus1 = m[0][0] - m[1][1] - m[2][2];
	float fourYSquaredMinus1 = m[1][1] - m[0][0] - m[2][2];
	float fourZSquaredMinus1 = m[2][2] - m[0][0] - m[1][1];
	float fourWSquaredMinus1 = m[0][0] + m[1][1] + m[2][2];
	int biggestIndex = 0;
	float fourBiggestSquaredMinus1 = fourWSquaredMinus1;
	if(fourXSquaredMinus1 > fourBiggestSquaredMinus1)
	{
		fourBiggestSquaredMinus1 = fourXSquaredMinus1;
		biggestIndex = 1;
	}
	if(fourYSquaredMinus1 > fourBiggestSquaredMinus1)
	{
		fourBiggestSquaredMinus1 = fourYSquaredMinus1;
		biggestIndex = 2;
	}
	if(fourZSquaredMinus1 > fourBiggestSquaredMinus1)
	{
		fourBiggestSquaredMinus1 = fourZSquaredMinus1;
		biggestIndex = 3;
	}

	float biggestVal = sqrt(fourBiggestSquaredMinus1 + 1.0) * 0.5;
	float mult = 0.25 / biggestVal;
	vec4 result;
	switch(biggestIndex)
	{
		case 0: 
			result[3] = biggestVal;
			result[0] = (m[1][2] - m[2][1]) * mult;
			result[1] = (m[2][0] - m[0][2]) * mult;
			result[2] = (m[0][1] - m[1][0]) * mult;
			return result;
		case 1:
			result[3] = (m[1][2] - m[2][1]) * mult;
			result[0] = biggestVal;
			result[1] = (m[0][1] + m[1][0]) * mult;
			result[2] = (m[2][0] + m[0][2]) * mult;
			return result; 
		case 2: 
			result[3] = (m[2][0] - m[0][2]) * mult;
			result[0] = (m[0][1] + m[1][0]) * mult;
			result[1] = biggestVal;
			result[2] = (m[1][2] + m[2][1]) * mult;
			return result;
		default: 
			result[3] = (m[0][1] - m[1][0]) * mult;
			result[0] = (m[2][0] + m[0][2]) * mult;
			result[1] = (m[1][2] + m[2][1]) * mult;
			result[2] = biggestVal;
			return result;
	}
}

vec4 NormaliseQuat(vec4 q)
{
	float len = length(q);
	if (len > 0.0)
	{
		return q / len;
	}
	else
	{
		return vec4(0.0, 0.0, 0.0, 1.0);
	}
}

vec4 QuatMultiply(const vec4 left, const vec4 right)
{
	vec4 result;
	result[3] = left[3] * right[3] - left[0] * right[0] - left[1] * right[1] - left[2] * right[2];
	result[0] = left[3] * right[0] + left[0] * right[3] + left[1] * right[2] - left[2] * right[1];
	result[1] = left[3] * right[1] + left[1] * right[3] + left[2] * right[0] - left[0] * right[2];
	result[2] = left[3] * right[2] + left[2] * right[3] + left[0] * right[1] - left[1] * right[0];
	return result;
}

mat2x4 GetDualQuat(const mat4 m, const vec3 s){
	vec4 r = NormaliseQuat(ExtractRotation(m,s));
	vec4 tq = vec4(m[3].xyz, 0);
	return mat2x4(r, QuatMultiply(tq, r) * 0.5);
}

float signnozero(float x){
	return step(0.0, x) * 2.0 - 1.0;
}

/*
mat2x4 DualQuaternionFromMat4(mat4 m, vec3 scale)
{
	mat2x4 dq;

	m[0] = m[0] / scale.x;
	m[1] = m[1] / scale.y;
	m[2] = m[2] / scale.z;

	dq[0].w = sqrt(max(0.0, 1.0 + m[0][0] + m[1][1] + m[2][2])) / 2.0;
	dq[0].x = sqrt(max(0.0, 1.0 + m[0][0] - m[1][1] - m[2][2])) / 2.0;
	dq[0].y = sqrt(max(0.0, 1.0 - m[0][0] + m[1][1] - m[2][2])) / 2.0;
	dq[0].z = sqrt(max(0.0, 1.0 - m[0][0] - m[1][1] + m[2][2])) / 2.0;
	dq[0].x *= signnozero(m[1][2] - m[2][1]);
	dq[0].y *= signnozero(m[2][0] - m[0][2]);
	dq[0].z *= signnozero(m[0][1] - m[1][0]);

	dq[0] = NormaliseQuat(dq[0]);	// ensure unit quaternion

	dq[1] = vec4(m[3].xyz, 0.0);// float4(m[0][3], m[1][3], m[2][3], 0);
	dq[1] = QuatMultiply(dq[1], dq[0]) * 0.5;

	return dq;
}

mat2x4 DualQuaternionMultiply(mat2x4 dq1, mat2x4 dq2)
{
	mat2x4 result;

	result[1] = QuatMultiply(dq1[0], dq2[1]) + 
		QuatMultiply(dq1[1], dq2[0]);
	
	result[0] = QuatMultiply(dq1[0], dq2[0]);

	float mag = length(result[0]);
	return result / mag;
}
*/

mat2x4 GetBoneTransform(vec4 weights)
{
	float sum_weight = weights.x + weights.y + weights.z + weights.w;

	// Fetch bones
	mat2x4 dq0 = GetDualQuat(m1, s1);// DualQuaternionFromMat4(m1, s1);
	mat2x4 dq1 = GetDualQuat(m2, s2);// DualQuaternionFromMat4(m2, s2);
	mat2x4 dq2 = GetDualQuat(m3, s3);// DualQuaternionFromMat4(m3, s3);
	mat2x4 dq3 = GetDualQuat(m4, s4);// DualQuaternionFromMat4(m4, s4);

	// Ensure all bone transforms are in the same neighbourhood
	weights.y *= signnozero(dot(dq0[0], dq1[0]));
	weights.z *= signnozero(dot(dq0[0], dq2[0]));
	weights.w *= signnozero(dot(dq0[0], dq3[0]));

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
	mat2x4 bone = GetBoneTransform(aBoneWeights);

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
	if (iDrawId != -1 && aBoneId[0] != -1 && aBoneWeights[0] != 0.0f)
	{
		animTexoffset = iDrawId;
		if (useDQB){
			// dqbs Skin
			m1 = GetMat4ById(aBoneId[0]) * GetBindMat4ById(aBoneId[0]);
			m2 = GetMat4ById(aBoneId[1]) * GetBindMat4ById(aBoneId[1]);
			m3 = GetMat4ById(aBoneId[2]) * GetBindMat4ById(aBoneId[2]);
			m4 = GetMat4ById(aBoneId[3]) * GetBindMat4ById(aBoneId[3]);
			s1 = ExtractScale(m1);// vec3(length(m1[0]), length(m1[1]), length(m1[2]));
			s2 = ExtractScale(m2);// vec3(length(m2[0]), length(m2[1]), length(m2[2]));
			s3 = ExtractScale(m3);// vec3(length(m3[0]), length(m3[1]), length(m3[2]));
			s4 = ExtractScale(m4);// vec3(length(m4[0]), length(m4[1]), length(m4[2]));

			vec3 scale = 
				s1 * aBoneWeights[0] + 
				s2 * aBoneWeights[1] +
				s3 * aBoneWeights[2] +
				s4 * aBoneWeights[3];
			
			mat4 scaleMatrix = mat4(0.0f);

			scaleMatrix[0][0] = scale[0];
			scaleMatrix[1][1] = scale[1];
			scaleMatrix[2][2] = scale[2];
			scaleMatrix[3][3] = 1.0f;
			mMatrix = GetSkinMatrix();
			mMatrix *= scaleMatrix;

		}
		else{
			// LBS Skin
			for (int i = 0; i < MAX_BONE_LENGTH; i++)
				mMatrix += (GetMat4ById(aBoneId[i]) * GetBindMat4ById(aBoneId[i])) * aBoneWeights[i];
		}
	}
	else
	{
		mMatrix =  mat4(iModelV1, iModelV2, iModelV3, iModelV4);
	}

	vec4 fragP = mMatrix * vec4(aVertexPos, 1.0f);
	vRemap = vec3(float(iRemap.x) / 255.0f, float(iRemap.y) / 255.0f, float(iRemap.z) / 255.0f);
	vTint = iTint; // instance tint as vert tint
	gl_Position = projection * view * fragP;
	FragPos = fragP.xyz;
	TexCoords = aTexCoords;
	Normal = normalize(inverse(transpose(mat3(mMatrix))) * aNormal);
	fMaterial = iMaterial;
}


