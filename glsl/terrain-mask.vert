#version {VERSION}

in vec2 aPosition;
in vec2 aTexCoords;

// x is the brush TextureArray index
// y is mask layer to draw on
// z is paint intensity float(z / 255)
in ivec3 aBrushType;

out vec2 TexCoords;
flat out ivec3 BrushType;

void main()
{
	TexCoords = aTexCoords;
	BrushType = aBrushType;
	
	gl_Position = vec4(aPosition.x, aPosition.y, 0.0, 1.0);
}