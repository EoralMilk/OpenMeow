#version {VERSION}

in vec2 aPosition;
in vec2 aTexCoords;

out vec2 TexCoords;

void main()
{
    TexCoords = aTexCoords;
    gl_Position = vec4(aPosition.x, aPosition.y, 0.0, 1.0); 
}  