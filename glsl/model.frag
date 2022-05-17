#version {VERSION}
#ifdef GL_ES
precision mediump float;
#endif

uniform sampler2D Palette, DiffuseTexture;
uniform vec2 PaletteRows;

uniform vec4 LightDirection;
uniform vec3 AmbientLight, DiffuseLight;
uniform vec2 VplInfo;
uniform vec3 WorldLight;
#if __VERSION__ == 120
varying vec4 vTexCoord;
varying vec4 vChannelMask;
varying vec4 vNormalsMask;
#else
in vec4 vTexCoord;
in vec4 vChannelMask;
in vec4 vNormalsMask;
in mat4 transMat;
out vec4 fragColor;
#endif


void main()
{
	#if __VERSION__ == 120
	vec4 x = texture2D(DiffuseTexture, vTexCoord.st);
	vec4 color = texture2D(Palette, vec2(dot(x, vChannelMask), PaletteRows.x));
	if (color.a < 0.01)
		discard;
	vec4 y = texture2D(DiffuseTexture, vTexCoord.pq);
	vec4 normal = normalize((2.0 * texture2D(Palette, vec2(dot(y, vNormalsMask), PaletteRows.y)) - 1.0));
	vec3 intensity = AmbientLight + DiffuseLight * max(dot(normal, LightDirection), 0.0);
	gl_FragColor = vec4(intensity * color.rgb, color.a);
	#else
	vec4 x = texture(DiffuseTexture, vTexCoord.st);
	float  colorIndex= dot(x, vChannelMask);
	vec4 color = texture(Palette, vec2(dot(x, vChannelMask), PaletteRows.x));
	if (color.a < 0.01)
		discard;
	vec4 y = texture(DiffuseTexture, vTexCoord.pq);
	float normaIndex = dot(y, vNormalsMask);
	vec4 normal = (2.0 * texture(Palette, vec2(normaIndex, PaletteRows.y)) - 1.0);
	normal.w = 0.0;
    normal =normalize(normal);
	vec3  worldNormal = mat3(transpose(inverse(transMat))) * normal.xyz;
	//AcosAngle --> 0 is more lighter
	float acosAngle = acos(dot(worldNormal.xyz, -WorldLight));
	float halfPi =1.5707963;
	float vxlPaletteStartIndex=VplInfo.x;
	float PaletteHeight =VplInfo.y;
	//AcosAngle Rangle  0 -> PI
	if (acosAngle >= halfPi)
	{
			vec4 cc = texture(Palette, vec2(colorIndex, (vxlPaletteStartIndex+0.5)/PaletteHeight));
			color = texture(Palette, vec2(cc.r, PaletteRows.x));
	}
	else
	{
			float nIndex = float(31 - int(acosAngle/halfPi*32.0));
			if(nIndex > 31.0)
			{
				nIndex =31.0;
			}
			if(nIndex <0.0)
			{
				nIndex =31.0;
			}
			vec4 cc = texture(Palette, vec2(colorIndex, (vxlPaletteStartIndex+nIndex +0.5)/PaletteHeight));
			color = texture(Palette, vec2(cc.r, PaletteRows.x));
	}
	fragColor =vec4(color.rgb,color.a);
	#endif
}
