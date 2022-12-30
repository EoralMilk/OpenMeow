#version {VERSION}

#ifdef GL_ES
precision highp float;
#endif

in vec2 TexCoords;
out vec4 FragColor;

uniform sampler2D screenTexture;
uniform sampler2D addtionTexture;
uniform sampler2D screenDepthTexture;
uniform sampler2D sunDepthTexture;

uniform bool FrameBufferShadow;
uniform bool FrameBufferPosition;

uniform mat4 SunVP;
uniform mat4 InvCameraVP;
uniform float FrameShadowBias;

uniform vec3 ScreenLight;
uniform float AmbientIntencity;
uniform float TestRadius;

uniform bool DrawUI;

const float offset = 1.0 / 512.0;
const float blurWeight[5] = float[] (0.227027, 0.1945946, 0.1216216, 0.054054, 0.016216);

const float _Radius = 0.2;
const float _Width = 0.06;
const float PI = 3.14159265359;

float ColorLuminance(vec3 rgb){
	return rgb.r * 0.299f + rgb.b * 0.587f + rgb.b * 0.114f;
}


void main()
{
	vec2 UV = TexCoords + texture(addtionTexture, TexCoords).xy;
	vec4 scolor = texture(screenTexture, UV);
	// vec4 scolor = vec4(TexCoords + texture(addtionTexture, TexCoords).xy, 0.0, 1.0);

	if (DrawUI)
	{
		if (scolor.a < 0.0001)
			discard;
		FragColor = scolor;
		return;
	}

	vec3 col = scolor.rgb;

	if (FrameBufferShadow || FrameBufferPosition){
		float depth = texture(screenDepthTexture, TexCoords).r;

		if (depth >= 0.99999)
			discard;

		vec4 fragP = InvCameraVP * vec4(TexCoords.x * 2.0 - 1.0, TexCoords.y * 2.0 - 1.0, depth * 2.0 - 1.0, 1.0);
		fragP = fragP / fragP.w;

		if (FrameBufferShadow){
			vec4 fragPosLightSpace = SunVP * fragP;
			vec3 projCoords = fragPosLightSpace.xyz / fragPosLightSpace.w;
			projCoords = projCoords * 0.5f + 0.5f;
			float currentDepth = projCoords.z;


			// float closestDepth = texture(sunDepthTexture, projCoords.xy).r;

			float shadow = 0.0f;
			float bias = FrameShadowBias * 0.025f;

			if(projCoords.z <= 1.0f)
			{
				vec2 texelSize = 1.0f / vec2(textureSize(sunDepthTexture, 0));
				for(int x = -1; x <= 1; ++x)
				{
					for(int y = -1; y <= 1; ++y)
					{
						float pcfDepth = texture(sunDepthTexture, projCoords.xy + vec2(x, y) * texelSize).r; 
						shadow += currentDepth - bias > pcfDepth ? 1.0f : 0.0f;        
					}
				}
				shadow /= 9.0f;
			}

			col = col * (1.0f - max(shadow - AmbientIntencity, 0.0f));

			FragColor = vec4(col, 1.0f);
			return;
		}
		else if (FrameBufferPosition){
			FragColor = vec4(vec3(fragP), 1.0f);
			return;
		}
	}

	// Blur the pixels when they are bright enough
	// float light = ColorLuminance(col);
	// if (light > 0.825f)
	// {
	// 	vec2 offsets[9] = vec2[](
	// 		vec2(-offset,  offset), // lt
	// 		vec2( 0.0f,    offset), // t
	// 		vec2( offset,  offset), // rt
	// 		vec2(-offset,  0.0f),   // l
	// 		vec2( 0.0f,    0.0f),   // m
	// 		vec2( offset,  0.0f),   // r
	// 		vec2(-offset, -offset), // lb
	// 		vec2( 0.0f,   -offset), // b
	// 		vec2( offset, -offset)  // rb
	// 	);

	// 	vec3 sampleTex[9];
	// 	for(int i = 0; i < 9; i++)
	// 	{
	// 		sampleTex[i] = vec3(texture(screenTexture, TexCoords.st + offsets[i]));
	// 	}

	// 	// blur
	// 	float kernel[9] = float[](
	// 		1.0f / 16.0f, 2.0f / 16.0f, 1.0f / 16.0f,
	// 		2.0f / 16.0f, 4.0f / 16.0f, 2.0f / 16.0f,
	// 		1.0f / 16.0f, 2.0f / 16.0f, 1.0f / 16.0f  
	// 	);

	// 	col = vec3(0.0);
	// 	for(int i = 0; i < 9; i++)
	// 		col += sampleTex[i] * kernel[i];
	// }
	// {
	// 	ivec2 tex_size = textureSize(screenTexture, 0); // gets size of single texel
	// 	vec2 tex_offset = vec2(1.0f/float(tex_size.x), 1.0f/float(tex_size.y));
	// 	vec3 result = texture(screenTexture, TexCoords).rgb * blurWeight[0]; // current fragment's contribution
		
	// 	{
	// 		for(int i = 1; i < 5; ++i)
	// 		{
	// 			result += texture(screenTexture, TexCoords + vec2(tex_offset.x * float(i), 0.0)).rgb * blurWeight[i];
	// 			result += texture(screenTexture, TexCoords - vec2(tex_offset.x * float(i), 0.0)).rgb * blurWeight[i];
	// 		}
	// 	}
		
	// 	{
	// 		for(int i = 1; i < 5; ++i)
	// 		{
	// 			result += texture(screenTexture, TexCoords + vec2(0.0, tex_offset.y * float(i))).rgb * blurWeight[i];
	// 			result += texture(screenTexture, TexCoords - vec2(0.0, tex_offset.y * float(i))).rgb * blurWeight[i];
	// 		}
	// 	}
	// 	col = result;
	// }
	

	// float horizontal[9] = float[](
	// 	1.0f, -2.0f, 1.0f,
	// 	2.0f, -4.0f, 2.0f,
	// 	1.0f, -2.0f, 1.0f
	// );
	// float vertical[9] = float[](
	// 	1.0f, -2.0f, 1.0f,
	// 	2.0f, -4.0f, 2.0f,
	// 	1.0f, -2.0f, 1.0f
	// );
	
	// vec3 hcol = vec3(0.0);
	// vec3 vcol = vec3(0.0);
	// for(int i = 0; i < 9; i++){
	// 	hcol += sampleTex[i] * horizontal[i];
	// 	vcol += sampleTex[i] * vertical[i];
	// }
	

	// sharpen
	// float kernel[9] = float[](
	// 	-1.0f, -1.0f, -1.0f,
	// 	-1.0f,  9.0f, -1.0f,
	// 	-1.0f, -1.0f, -1.0f
	// );

	// edge
	// float kernel[9] = float[](
	// 	1.0f, 1.0f, 1.0f,
	// 	1.0f, -8.0f, 1.0f,
	// 	1.0f, 1.0f, 1.0f
	// );

	FragColor = vec4(ScreenLight * col, 1.0);
}
