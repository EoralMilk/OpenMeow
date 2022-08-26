#version {VERSION}
#ifdef GL_ES
precision mediump float;
#endif

#define DT_NONE -1

#define DT_WATER 22
#define DT_SHORE 23
#define DT_CLIFF 25
#define DT_GRASS 30
#define DT_SMUDGE 100
#define DT_ADDON 100

#define GrassNormal Texture4
#define CliffNormal Texture5
#define Cliff		Texture6
#define SlopeNormal Texture7
#define Slope		Texture8
#define WaterNormal Texture9
#define Caustics 	Texture10

#define Scroch		Texture4

uniform sampler2D Texture0;
uniform sampler2D Texture1;
uniform sampler2D Texture2;
uniform sampler2D Texture3;
uniform sampler2D Texture4;
uniform sampler2D Texture5;
uniform sampler2D Texture6;
uniform sampler2D Texture7;
uniform sampler2D Texture8;
uniform sampler2D Texture9;
uniform sampler2D Texture10;

// uniform sampler2D WaterNormal;
// uniform sampler2D Caustics;

// uniform sampler2D GrassNormal;
// uniform sampler2D CliffNormal;
// uniform sampler2D Cliff;
// uniform sampler2D SlopeNormal;
// uniform sampler2D Slope;

// uniform sampler2D Scroch;

uniform float WaterUVOffset;
uniform float GrassUVOffset;

uniform sampler2D Palette;
uniform sampler2D ColorShifts;

uniform bool EnableDepthPreview;
uniform vec2 DepthPreviewParams;
// uniform float DepthTextureScale;
uniform float AntialiasPixelsPerTexel;

uniform bool RenderShroud;
uniform bool RenderDepthBuffer;


in vec4 vColor;

in vec4 vTexCoord;
in vec2 vTexMetadata;
in vec4 vChannelMask;
in vec2 vTexSampler;
in vec2 vTileTexCoord;

in vec4 vColorFraction;
in vec4 vRGBAFraction;
in vec4 vPalettedFraction;
in vec4 vTint;
in vec3 vNormal;
in vec3 vFragPos;
in float vSunLight;

in vec3 tSunDirection;
in vec3 tFragPos;
in vec3 tViewPos;
in vec3 tNormal;

flat in int mDrawType;


out vec4 fragColor;

struct DirLight {
	vec3 direction;
	vec3 ambient;
	vec3 diffuse;
	vec3 specular;
};

uniform DirLight dirLight;

uniform sampler2D ShadowDepthTexture;
uniform mat4 SunVP;
uniform mat4 InvCameraVP;
uniform float ShadowBias;
uniform float AmbientIntencity;
uniform vec2 ViewPort;
uniform vec3 viewPos;

float CalShadow(){
	vec4 fragPosLightSpace = SunVP * vec4(vFragPos, 1.0);
	vec3 projCoords = fragPosLightSpace.xyz / fragPosLightSpace.w;
	projCoords = projCoords * 0.5f + 0.5f;
	float currentDepth = projCoords.z;

	float shadow = 0.0f;
	float bias = ShadowBias * 0.025f;
	// float bias = ShadowBias * max(0.02 * (1.0 - dot(vNormal, light.direction)), 0.0005);

	if(projCoords.z <= 1.0f)
	{
		vec2 texelSize = 1.0f / vec2(textureSize(ShadowDepthTexture, 0));
		for(int x = -1; x <= 1; ++x)
		{
			for(int y = -1; y <= 1; ++y)
			{
				float pcfDepth = texture(ShadowDepthTexture, projCoords.xy + vec2(x, y) * texelSize).r; 
				shadow += currentDepth - bias > pcfDepth ? 1.0f : 0.0f;        
			}
		}
		shadow /= 9.0f;
	}

	return shadow;
}

vec4 CalcDirLight(DirLight light, vec4 color)
{
	vec3 normal = tNormal;
	vec3 viewDir = normalize(tViewPos - tFragPos);
	vec3 lightDir = normalize(-tSunDirection);

	vec3 specular = vec3(0.0);


	if (mDrawType == DT_SMUDGE){
		// test smudge
		// color = texture(Scroch, vTileTexCoord);
	}
	else if (mDrawType != DT_ADDON){
		vec2 uv = vTileTexCoord;

		// slope and cliff
		if (vNormal != vec3(0,0,1))
		{
			float nndot = dot(vNormal, vec3(0,0,1));

			// if (mDrawType > 0 && mDrawType < 21)
			{
				float mul = max(min((1.0 - nndot) * 13.0, 1.0), 0.0);
				normal = normal + (normalize(texture(SlopeNormal, uv).rgb * 2.0 - 1.0) - normal)*mul;
				color = vec4(color.rgb + (texture(Slope, uv).rgb - color.rgb)*min(mul, 0.7), color.a);
			}

			// if (mDrawType == DT_CLIFF)
			{
				float mul = max(min((1.0 - nndot) * 5.0, 1.0), 0.0);
				normal = normal + (normalize(texture(CliffNormal, uv).rgb * 2.0 - 1.0) - normal)*mul;
				color = vec4(color.rgb + (texture(Cliff, uv).rgb - color.rgb)*mul, color.a);
			}
			
			vec3 reflectDir = reflect(-lightDir, normal);
			float spec = pow(max(dot(viewDir, reflectDir), 0.0), 32.0);
			specular = light.specular * spec;
		}
		// water pix
		else if (mDrawType == DT_WATER && color.b > color.r)
		{
			uv = uv + vec2(WaterUVOffset, WaterUVOffset);
			normal = normalize(texture(WaterNormal, uv).rgb * 2.0 - 1.0);

			vec4 water = texture(Caustics, uv);
			water *= 2.0;

			color *= (water + (vec4(1.0) - water) * max(color.r/color.b, 0.5));

			vec3 reflectDir = reflect(-lightDir, normal);
			float spec = pow(max(dot(viewDir, reflectDir), 0.0), 32.0);
			specular = light.specular * spec;
		}
		// grass pix
		else if ((mDrawType == DT_GRASS && color.g > color.r && color.g > color.b))
		// else if (mDrawType != 99)
		{
			uv = uv + vec2(GrassUVOffset, GrassUVOffset);
			normal = normalize(texture(GrassNormal, uv).rgb * 2.0 - 1.0);

			vec3 reflectDir = reflect(-lightDir, normal);
			float spec = pow(max(dot(viewDir, reflectDir), 0.0), 32.0);
			specular = light.specular * spec;
		}
	}
	

	// diffuse
	float diff = dot(normal, lightDir);

	// merge
	vec3 ambient  = light.ambient;
	vec3 diffuse  = light.diffuse * diff;

	ambient = ambient * color.rgb;
	diffuse = diffuse * color.rgb;
	float shadow = 1.0 - max(CalShadow(), 0.0);

	return vec4((ambient * vSunLight + diffuse * shadow  + specular) * vTint.rgb, color.a);
}


vec3 rgb2hsv(vec3 c)
{
	// From http://lolengine.net/blog/2013/07/27/rgb-to-hsv-in-glsl
	vec4 K = vec4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
	vec4 p = c.g < c.b ? vec4(c.bg, K.wz) : vec4(c.gb, K.xy);
	vec4 q = c.r < p.x ? vec4(p.xyw, c.r) : vec4(c.r, p.yzx);
	float d = q.x - min(q.w, q.y);
	float e = 1.0e-10;
	return vec3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

vec3 hsv2rgb(vec3 c)
{
	// From http://lolengine.net/blog/2013/07/27/rgb-to-hsv-in-glsl
	vec4 K = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
	vec3 p = abs(fract(c.xxx + K.xyz) * 6.0 - K.www);
	return c.z * mix(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
}

float srgb2linear(float c)
{
	// Standard gamma conversion equation: see e.g. http://entropymine.com/imageworsener/srgbformula/
	return c <= 0.04045f ? c / 12.92f : pow((c + 0.055f) / 1.055f, 2.4f);
}

vec4 srgb2linear(vec4 c)
{
	// The SRGB color has pre-multiplied alpha which we must undo before removing the the gamma correction
	return c.a * vec4(srgb2linear(c.r / c.a), srgb2linear(c.g / c.a), srgb2linear(c.b / c.a), 1.0f);
}

float linear2srgb(float c)
{
	// Standard gamma conversion equation: see e.g. http://entropymine.com/imageworsener/srgbformula/
	return c <= 0.0031308 ? c * 12.92f : 1.055f * pow(c, 1.0f / 2.4f) - 0.055f;
}

vec4 linear2srgb(vec4 c)
{
	// The linear color has pre-multiplied alpha which we must undo before applying the the gamma correction
	return c.a * vec4(linear2srgb(c.r / c.a), linear2srgb(c.g / c.a), linear2srgb(c.b / c.a), 1.0f);
}

#if __VERSION__ == 120
vec2 Size(float samplerIndex)
{
	if (samplerIndex < 0.5)
		return Texture0Size;
	else if (samplerIndex < 1.5)
		return Texture1Size;
	else if (samplerIndex < 2.5)
		return Texture2Size;

	return Texture3Size;
}

vec4 Sample(float samplerIndex, vec2 pos)
{
	if (samplerIndex < 0.5)
		return texture2D(Texture0, pos);
	else if (samplerIndex < 1.5)
		return texture2D(Texture1, pos);
	else if (samplerIndex < 2.5)
		return texture2D(Texture2, pos);
	
	return texture2D(Texture3, pos);
}
#else
ivec2 Size(float samplerIndex)
{
	if (samplerIndex < 0.5)
		return textureSize(Texture0, 0);
	else if (samplerIndex < 1.5)
		return textureSize(Texture1, 0);
	else if (samplerIndex < 2.5)
		return textureSize(Texture2, 0);

	return textureSize(Texture3, 0);
}

vec4 Sample(float samplerIndex, vec2 pos)
{
	if (samplerIndex < 0.5)
		return texture(Texture0, pos);
	else if (samplerIndex < 1.5)
		return texture(Texture1, pos);
	else if (samplerIndex < 2.5)
		return texture(Texture2, pos);

	return texture(Texture3, pos);
}
#endif

vec4 SamplePalettedBilinear(float samplerIndex, vec2 coords, vec2 textureSize)
{
	vec2 texPos = (coords * textureSize) - vec2(0.5);
	vec2 interp = fract(texPos);
	vec2 tl = (floor(texPos) + vec2(0.5)) / textureSize;
	vec2 px = 1.0 / textureSize;

	vec4 x1 = Sample(samplerIndex, tl);
	vec4 x2 = Sample(samplerIndex, tl + vec2(px.x, 0.));
	vec4 x3 = Sample(samplerIndex, tl + vec2(0., px.y));
	vec4 x4 = Sample(samplerIndex, tl + px);

	#if __VERSION__ == 120
	vec4 c1 = texture2D(Palette, vec2(dot(x1, vChannelMask), vTexMetadata.s));
	vec4 c2 = texture2D(Palette, vec2(dot(x2, vChannelMask), vTexMetadata.s));
	vec4 c3 = texture2D(Palette, vec2(dot(x3, vChannelMask), vTexMetadata.s));
	vec4 c4 = texture2D(Palette, vec2(dot(x4, vChannelMask), vTexMetadata.s));
	#else
	vec4 c1 = texture(Palette, vec2(dot(x1, vChannelMask), vTexMetadata.s));
	vec4 c2 = texture(Palette, vec2(dot(x2, vChannelMask), vTexMetadata.s));
	vec4 c3 = texture(Palette, vec2(dot(x3, vChannelMask), vTexMetadata.s));
	vec4 c4 = texture(Palette, vec2(dot(x4, vChannelMask), vTexMetadata.s));
	#endif

	return mix(mix(c1, c2, interp.x), mix(c3, c4, interp.x), interp.y);
}

vec4 ColorShift(vec4 c, float p)
{
	#if __VERSION__ == 120
	vec4 shift = texture2D(ColorShifts, vec2(0.5, p));
	#else
	vec4 shift = texture(ColorShifts, vec2(0.5, p));
	#endif

	vec3 hsv = rgb2hsv(srgb2linear(c).rgb);
	if (hsv.r >= shift.b && shift.a >= hsv.r)
		c = linear2srgb(vec4(hsv2rgb(vec3(hsv.r + shift.r, clamp(hsv.g + shift.g, 0.0, 1.0), hsv.b)), c.a));

	return c;
}

void main()
{
	if (mDrawType == DT_NONE)
		discard;
	if (RenderDepthBuffer){
		return;
	}

	vec2 coords = vTexCoord.st;
	vec4 c;

	if (mDrawType == DT_SMUDGE){
		if (vTileTexCoord.x < 0.0 || vTileTexCoord.x > 1.0 || vTileTexCoord.y < 0.0 || vTileTexCoord.y > 1.0)
			discard;
	}
	
	{
		if (AntialiasPixelsPerTexel > 0.0)
		{
			vec2 textureSize = vec2(Size(vTexSampler.s));
			vec2 offset = fract(coords.st * textureSize);

			// Offset the sampling point to simulate bilinear intepolation in window coordinates instead of texture coordinates
			// https://csantosbh.wordpress.com/2014/01/25/manual-texture-filtering-for-pixelated-games-in-webgl/
			// https://csantosbh.wordpress.com/2014/02/05/automatically-detecting-the-texture-filter-threshold-for-pixelated-magnifications/
			// ik is defined as 1/k from the articles, set to 1/0.7 because it looks good
			float ik = 1.43;
			vec2 interp = clamp(offset * ik * AntialiasPixelsPerTexel, 0.0, .5) + clamp((offset - 1.0) * ik * AntialiasPixelsPerTexel + .5, 0.0, .5);
			coords = (floor(coords.st * textureSize) + interp) / textureSize;

			if (vPalettedFraction.x > 0.0)
				c = SamplePalettedBilinear(vTexSampler.s, coords, textureSize);
		}

		if (!(AntialiasPixelsPerTexel > 0.0 && vPalettedFraction.x > 0.0))
		{
			vec4 x = Sample(vTexSampler.s, coords);
			vec2 p = vec2(dot(x, vChannelMask), vTexMetadata.s);
			#if __VERSION__ == 120
			c = vPalettedFraction * texture2D(Palette, p) + vRGBAFraction * x + vColorFraction * vTexCoord;
			#else
			c = vPalettedFraction * texture(Palette, p) + vRGBAFraction * x + vColorFraction * vTexCoord;
			#endif
		}

		// Discard any transparent fragments (both color and depth)
		if (c.a == 0.0)
			discard;
		
		if (vRGBAFraction.r > 0.0 && vTexMetadata.s > 0.0)
			c = ColorShift(c, vTexMetadata.s);
	}

	if (EnableDepthPreview)
	{
		float intensity = 1.0 - gl_FragCoord.z;
		#if __VERSION__ == 120
		gl_FragColor = vec4(vec3(intensity), 1.0);
		#else
		fragColor = vec4(vec3(intensity), 1.0);
		#endif
	}
	else
	{
		if (!RenderShroud){
			// a < 0 is ignoreTint
			if (vTint.a < 0.0)
				c = vec4(c.rgb * vSunLight, c.a * (-vTint.a));
			else{
				c = CalcDirLight(dirLight, c);
				c = c * vTint.a;
				
			}
		}

		#if __VERSION__ == 120
		gl_FragColor = c;
		#else
		fragColor = c;
		#endif
	}
}
