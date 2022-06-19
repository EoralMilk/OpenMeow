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
in int iDrawId;
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

uniform bool useDQB;
uniform mat4 BindTransformData[128];

uniform sampler2D boneAnimTexture;


mat4 m1, m2, m3 ,m4;
vec3 s1, s2, s3, s4;
int next[3];

mat4 GetMat4ById(int id){
	ivec2 startuv= ivec2(id * 4, iDrawId);

	vec4 c1 = texelFetch(boneAnimTexture, startuv, 0);
	vec4 c2 = texelFetch(boneAnimTexture, ivec2(startuv.x + 1,startuv.y), 0);
	vec4 c3 = texelFetch(boneAnimTexture, ivec2(startuv.x + 2,startuv.y), 0);
	vec4 c4 = texelFetch(boneAnimTexture, ivec2(startuv.x + 3,startuv.y), 0);

	return mat4(c1,c2,c3,c4) * BindTransformData[id];
}

// extract quaternion from matrix
vec4 ExtractRotation(const mat4 mm, const vec3 scale)
{
	mat4 m = mat4(mm[0] / scale.x, mm[1] / scale.y, mm[2] / scale.z, vec4(0, 0, 0, 1.0f));
	vec4 q = vec4(0, 0, 0, 1.0f);
	float trace = m[0][0] + m[1][1] + m[2][2];
	if (trace > 0.0f)
	{
		float s = sqrt(trace + 1.0f);
		q.w = s * 0.5f;
		s = 0.5f / s;
		q.x = (m[2][1] - m[1][2]) * s;
		q.y = (m[0][2] - m[2][0]) * s;
		q.z = (m[1][0] - m[0][1]) * s;
	}
	else
	{
		int i = 0;
		if (m[1][1] > m[0][0])
			i = 1;
		if (m[2][2] > m[i][i])
			i = 2;
		int j = next[i];
		int k = next[j];
		float s = sqrt((m[i][i] - (m[j][j] + m[k][k])) + 1.0f);
		q[i] = s * 0.5f;
		s = 0.5f / s;
		q[j] = (m[i][j] + m[j][i]) * s;
		q[k] = (m[i][k] + m[k][i]) * s;
		q[3] = (m[j][k] - m[k][j]) * s;
	}
	return q;
}

vec4 QuatMultiply(const vec4 left, const vec4 right)
{
	vec4 result;
	result.w = left.w * right.w - left.x * right.x - left.y * right.y - left.z * right.z;
	result.x = left.w * right.x + left.x * right.w + left.y * right.z - left.z * right.y;
	result.y = left.w * right.y + left.y * right.w + left.z * right.x - left.x * right.z;
	result.z = left.w * right.z + left.z * right.w + left.x * right.y - left.y * right.x;
	return result;
}

mat2x4 GetDualQuat(const mat4 m, const vec3 s){
	vec4 r = ExtractRotation(m,s);
	vec4 tq = vec4(0, m[3].xyz);
	return mat2x4(r, QuatMultiply(tq, r) * .5f);
}

mat2x4 GetBoneTransform(vec4 weights)
{
	float sum_weight = weights.x + weights.y + weights.z + weights.w;

	// Fetch bones
	mat2x4 dq0 = GetDualQuat(m1, s1);
	mat2x4 dq1 = GetDualQuat(m2, s1);
	mat2x4 dq2 = GetDualQuat(m3, s1);
	mat2x4 dq3 = GetDualQuat(m4, s1);

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
	vec4 weights = aBoneWidget;
	mat2x4 bone = GetBoneTransform(weights);

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
	if (iDrawId != -1)
	{
		mat4 scaleMatrix = mat4(0.0f);
		vec3 scale = vec3(0.0f);

		if (aBoneWidget[0] == 0.0f){
			mMatrix =  mat4(iModelV1, iModelV2, iModelV3, iModelV4);
			mMatrix = mMatrix * rotationFix;
		}
		else if (useDQB){
			// dqs混合 Skin
			next[0] = 1;
			next[1] = 2;
			next[2] = 0;
			m1 = GetMat4ById(aBoneId[0]);
			m2 = GetMat4ById(aBoneId[1]);
			m3 = GetMat4ById(aBoneId[2]);
			m4 = GetMat4ById(aBoneId[3]);
			s1 = vec3(length(m1[0]), length(m1[1]), length(m1[2]));
			s2 = vec3(length(m1[0]), length(m1[1]), length(m1[2]));
			s3 = vec3(length(m2[0]), length(m2[1]), length(m2[2]));
			s4 = vec3(length(m3[0]), length(m3[1]), length(m3[2]));
			scale= s1 * aBoneWidget[0] + s2 * aBoneWidget[1] + s3 * aBoneWidget[2] + s4 * aBoneWidget[3];

			scaleMatrix[0][0] = scale[0];
			scaleMatrix[1][1] = scale[1];
			scaleMatrix[2][2] = scale[2];
			scaleMatrix[3][3] = 1.0f;
			mMatrix = GetSkinMatrix() * scaleMatrix;
		}
		else{
			// 线性混合 Skin
			for (int i = 0; i < MAX_BONE_LENGTH; i++)
				mMatrix += GetMat4ById(aBoneId[i]) * aBoneWidget[i];
		}
	}
	else
	{
		mMatrix =  mat4(iModelV1, iModelV2, iModelV3, iModelV4);
		mMatrix = mMatrix * rotationFix;
	}
	vec4 fragP = mMatrix * vec4(aVertexPos, 1.0f);
	vRemap = iRemap;
	vTint = iTint;
	gl_Position = projection * view * fragP;
	FragPos = fragP.xyz;
	TexCoords = aTexCoords;
	Normal = normalize(mat3(transpose(inverse(mMatrix))) * aNormal);
}


