const float Epsilon = 0.0001;

uniform DirLight dirLight;

out vec4 FragColor;

in vec3 Normal;
in vec3 FragPos;
in vec2 TexCoords;
in vec4 vTint;
in vec3 vRemap;
// x is colormap index, y is combinedmap index, z is Shininess x 100, w is texture size
// combined texture: r is remap, g is Specular, b is emission
flat in ivec4 fMaterial;

uniform sampler2DArray Textures64;
uniform sampler2DArray Textures128;
uniform sampler2DArray Textures256;
uniform sampler2DArray Textures512;
uniform sampler2DArray Textures1024;


uniform vec3 viewPos;
uniform bool EnableDepthPreview;
uniform vec2 DepthPreviewParams;
uniform bool RenderDepthBuffer;
const float PI = 3.14159265359;

uniform sampler2D ShadowDepthTexture;
uniform mat4 SunVP;
uniform mat4 InvCameraVP;
uniform float ShadowBias;
uniform int ShadowSampleType;
uniform float AmbientIntencity;

vec4 color, combined;
float shininess;