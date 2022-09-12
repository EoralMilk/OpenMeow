#version {VERSION}

in vec2 aPosition;
in vec2 aTexCoords;

out vec2 TexCoords;

uniform bool DrawUI;
uniform vec2 UIPos;
uniform vec2 UIScale;

void main()
{

	TexCoords = aTexCoords;
	if (DrawUI){
		gl_Position = vec4(UIPos.x + aPosition.x * UIScale.x, UIPos.y + aPosition.y * UIScale.y, 0.0, 1.0);
	}
	else{
		gl_Position = vec4(aPosition.x, aPosition.y, 0.0, 1.0);
	}
}