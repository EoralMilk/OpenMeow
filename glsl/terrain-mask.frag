#version {VERSION}
#ifdef GL_ES
precision mediump float;
precision mediump sampler2DArray;
#endif

in vec2 TexCoords;
flat in ivec3 BrushType;

layout (location = 0) out vec4 BakedMask123;
layout (location = 1) out vec4 BakedMask456;
layout (location = 2) out vec4 BakedMask789;


uniform sampler2D InitMask123;
uniform sampler2D InitMask456;
uniform sampler2D InitMask789;

uniform sampler2DArray Brushes;

uniform bool InitWithTextures;


void main()
{
	vec4 none = vec4(0.0, 0.0, 0.0, 1.0);

	// init
	if (BrushType.x == -1){
		if (InitWithTextures)
		{
			vec4 m123 = texture(InitMask123, TexCoords);
			vec4 m456 = texture(InitMask456, TexCoords);
			vec4 m789 = texture(InitMask789, TexCoords);

			BakedMask123 = vec4(m123.r, m123.g, m123.b, 1.0);
			BakedMask456 = vec4(m456.r, m456.g, m456.b, 1.0);
			BakedMask789 = vec4(m789.r, m789.g, m789.b, 1.0);
			return;
		}
		else{
			BakedMask123 = none;
			BakedMask456 = none;
			BakedMask789 = none;
			return;
		}
	}
	else{
		// we should use Additive blend mode to add brush intensity
		// or Subtractive blend mode to erase
		vec4 brush = texture(Brushes, vec3(TexCoords, float(BrushType.x)));
		float intensity = brush.r * brush.a * float(BrushType.z) / 255.0;

		switch (BrushType.y){
			case 0: 
				brush = vec4(intensity, 0, 0, 1.0);
				BakedMask123 = brush;
				BakedMask456 = none;
				BakedMask789 = none;
				return;
			case 1: 
				brush = vec4(0, intensity, 0, 1.0);
				BakedMask123 = brush;
				BakedMask456 = none;
				BakedMask789 = none;
				return;
			case 2: 
				brush = vec4(0, 0, intensity, 1.0);
				BakedMask123 = brush;
				BakedMask456 = none;
				BakedMask789 = none;
				return;
			
			case 3:
				brush = vec4(intensity, 0, 0, 1.0);
				BakedMask123 = none;
				BakedMask456 = brush;
				BakedMask789 = none;
				return;
			case 4:
				brush = vec4(0, intensity, 0, 1.0);
				BakedMask123 = none;
				BakedMask456 = brush;
				BakedMask789 = none;
				return;
			case 5:
				brush = vec4(0, 0, intensity, 1.0);
				BakedMask123 = none;
				BakedMask456 = brush;
				BakedMask789 = none;
				return;

			case 6:
				brush = vec4(intensity, 0, 0, 1.0);
				BakedMask123 = none;
				BakedMask456 = none;
				BakedMask789 = brush;
				return;
			case 7:
				brush = vec4(0, intensity, 0, 1.0);
				BakedMask123 = none;
				BakedMask456 = none;
				BakedMask789 = brush;
				return;
			case 8:
				brush = vec4(0, 0, intensity, 1.0);
				BakedMask123 = none;
				BakedMask456 = none;
				BakedMask789 = brush;
				return;
			default: discard;
				return;
		}
	}


}
