
vec3 convertRgbToHsl(in vec3 c) { 
	vec4 K = vec4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0); 
	vec4 p = mix(vec4(c.bg, K.wz), vec4(c.gb, K.xy), step(c.b, c.g)); 
	vec4 q = mix(vec4(p.xyw, c.r), vec4(c.r, p.yzx), step(p.x, c.r)); 
	
	float d = q.x - min(q.w, q.y); 
	float e = 1.0e-10; 
	return vec3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x); 
} 

vec3 convertHslToRgb(in vec3 c) { 
	vec4 K = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0); 
	vec3 p = abs(fract(c.xxx + K.xyz) * 6.0 - K.www); 
	return c.z * mix(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
}

vec3 ApplyRemap(in vec3 color, in vec3 remap){
	vec3 hsl = convertRgbToHsl(color.rgb);
	vec3 rhsl = convertRgbToHsl(remap);
	hsl.r = rhsl.r; //replace hue
	hsl.g = min(rhsl.g, hsl.g);
	return convertHslToRgb(hsl);
}

vec3 RGBtoHSV(in vec3 RGB)
{
	vec4  P   = (RGB.g < RGB.b) ? vec4(RGB.bg, -1.0, 2.0/3.0) : vec4(RGB.gb, 0.0, -1.0/3.0);
	vec4  Q   = (RGB.r < P.x) ? vec4(P.xyw, RGB.r) : vec4(RGB.r, P.yzx);
	float C   = Q.x - min(Q.w, Q.y);
	float H   = abs((Q.w - Q.y) / (6.0 * C + Epsilon) + Q.z);
	vec3  HCV = vec3(H, C, Q.x);
	float S   = HCV.y / (HCV.z + Epsilon);
	return vec3(HCV.x, S, HCV.z);
}

vec3 HSVtoRGB(in vec3 HSV)
{
	float H   = HSV.x;
	float R   = abs(H * 6.0 - 3.0) - 1.0;
	float G   = 2.0 - abs(H * 6.0 - 2.0);
	float B   = 2.0 - abs(H * 6.0 - 4.0);
	vec3  RGB = clamp( vec3(R,G,B), 0.0, 1.0 );
	return ((RGB - 1.0) * HSV.y + 1.0) * HSV.z;
}
