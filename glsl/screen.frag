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
uniform float ViewportScale;

uniform bool DrawUI;
uniform bool FxAA;
uniform vec2 FxAA_texelStep;
// uniform bool FxAADrawEdge;
const float g_lumaThreshold = 0.5;
const float g_mulReduceReciprocal = 8.0;
const float g_minReduceReciprocal = 128.0;
const float FxAA_lumaThreshold = g_lumaThreshold;
const float FxAA_mulReduce = 1.0 / g_mulReduceReciprocal;
const float FxAA_minReduce = 1.0 / g_minReduceReciprocal;
const float FxAA_maxSpan = 8.0;

const float offset = 1.0 / 512.0;
const float blurWeight[5] = float[] (0.227027, 0.1945946, 0.1216216, 0.054054, 0.016216);

const float PI = 3.14159265359;

const vec3 viewDir = vec3(0, 1, 0.57735);

float ColorLuminance(vec3 rgb){
	return rgb.r * 0.299f + rgb.b * 0.587f + rgb.b * 0.114f;
}

vec2 ParallaxMapping()
{
	float height = texture(addtionTexture, TexCoords).r;
	vec2 p = viewDir.xy / viewDir.z * (height * (0.25 / ViewportScale));
	return p;    
}

void main()
{
	if (DrawUI)
	{
		vec4 scolor = texture(screenTexture, TexCoords);
		if (scolor.a < 0.0001)
			discard;
		FragColor = scolor;
		return;
	}

	vec2 UV = clamp(TexCoords - ParallaxMapping(), 0.0, 1.0);//texture(addtionTexture, TexCoords).xy;

	// vec4 scolor = vec4(TexCoords + texture(addtionTexture, TexCoords).xy, 0.0, 1.0);


	if	(FrameBufferShadow || FrameBufferPosition)
	{
		vec4 scolor = texture(screenTexture, UV);
		float depth = texture(screenDepthTexture, UV).r;
		vec3 col = scolor.rgb;
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
	else if (!FxAA)
	{
		vec4 scolor = texture(screenTexture, UV);
		FragColor = scolor;
		return;
	}

	vec3 rgbM = texture(screenTexture, UV).rgb;

	// Sampling neighbour texels. Offsets are adapted to OpenGL texture coordinates. 
	vec3 rgbNW = textureOffset(screenTexture, UV, ivec2(-1, 1)).rgb;
    vec3 rgbNE = textureOffset(screenTexture, UV, ivec2(1, 1)).rgb;
    vec3 rgbSW = textureOffset(screenTexture, UV, ivec2(-1, -1)).rgb;
    vec3 rgbSE = textureOffset(screenTexture, UV, ivec2(1, -1)).rgb;

	// see http://en.wikipedia.org/wiki/Grayscale
	const vec3 toLuma = vec3(0.299, 0.587, 0.114);
	
	// Convert from RGB to luma.
	float lumaNW = dot(rgbNW, toLuma);
	float lumaNE = dot(rgbNE, toLuma);
	float lumaSW = dot(rgbSW, toLuma);
	float lumaSE = dot(rgbSE, toLuma);
	float lumaM = dot(rgbM, toLuma);

	// Gather minimum and maximum luma.
	float lumaMin = min(lumaM, min(min(lumaNW, lumaNE), min(lumaSW, lumaSE)));
	float lumaMax = max(lumaM, max(max(lumaNW, lumaNE), max(lumaSW, lumaSE)));
	
	// If contrast is lower than a maximum threshold ...
	if (lumaMax - lumaMin <= lumaMax * FxAA_lumaThreshold)
	{
		// ... do no AA and return.
		FragColor = vec4(rgbM, 1.0);
		
		return;
	}  
	
	// Sampling is done along the gradient.
	vec2 samplingDirection;	
	samplingDirection.x = -((lumaNW + lumaNE) - (lumaSW + lumaSE));
    samplingDirection.y =  ((lumaNW + lumaSW) - (lumaNE + lumaSE));
    
    // Sampling step distance depends on the luma: The brighter the sampled texels, the smaller the final sampling step direction.
    // This results, that brighter areas are less blurred/more sharper than dark areas.  
    float samplingDirectionReduce = max((lumaNW + lumaNE + lumaSW + lumaSE) * 0.25 * FxAA_mulReduce, FxAA_minReduce);

	// Factor for norming the sampling direction plus adding the brightness influence. 
	float minSamplingDirectionFactor = 1.0 / (min(abs(samplingDirection.x), abs(samplingDirection.y)) + samplingDirectionReduce);
    
    // Calculate final sampling direction vector by reducing, clamping to a range and finally adapting to the texture size. 
    samplingDirection = clamp(samplingDirection * minSamplingDirectionFactor, vec2(-FxAA_maxSpan), vec2(FxAA_maxSpan)) * FxAA_texelStep;
	
	// Inner samples on the tab.
	vec3 rgbSampleNeg = texture(screenTexture, UV + samplingDirection * (1.0/3.0 - 0.5)).rgb;
	vec3 rgbSamplePos = texture(screenTexture, UV + samplingDirection * (2.0/3.0 - 0.5)).rgb;

	vec3 rgbTwoTab = (rgbSamplePos + rgbSampleNeg) * 0.5;  

	// Outer samples on the tab.
	vec3 rgbSampleNegOuter = texture(screenTexture, UV + samplingDirection * (0.0/3.0 - 0.5)).rgb;
	vec3 rgbSamplePosOuter = texture(screenTexture, UV + samplingDirection * (3.0/3.0 - 0.5)).rgb;
	
	vec3 rgbFourTab = (rgbSamplePosOuter + rgbSampleNegOuter) * 0.25 + rgbTwoTab * 0.5;   
	
	// Calculate luma for checking against the minimum and maximum value.
	float lumaFourTab = dot(rgbFourTab, toLuma);
	
	// Are outer samples of the tab beyond the edge ... 
	if (lumaFourTab < lumaMin || lumaFourTab > lumaMax)
	{
		// ... yes, so use only two samples.
		FragColor = vec4(rgbTwoTab, 1.0); 
	}
	else
	{
		// ... no, so use four samples. 
		FragColor = vec4(rgbFourTab, 1.0);
	}

	// Show edges for debug purposes.	
	// if (u_showEdges != 0)
	// {
	// 	FragColor.r = 1.0;
	// }

	FragColor.rgb *= ScreenLight;

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
}
