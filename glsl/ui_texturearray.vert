#version {VERSION}

uniform vec3 Scroll;
uniform vec3 r1, r2;

in vec2 aPosition;
in vec2 aTexCoords;
in ivec3 aTags;

out vec2 TexCoords;
flat out ivec3 Tags;

void main()
{
	TexCoords = aTexCoords;
	Tags = aTags;
	vec3 pos = vec3(aPosition, 0);
	gl_Position = vec4((pos - Scroll.xyz) * r1 + r2, 1);
	// gl_Position = vec4(aPosition.x, aPosition.y, 0.0, 1.0);
}