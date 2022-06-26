#version {VERSION}

#ifdef GL_ES
precision mediump float;
#endif

in vec2 TexCoords;
out vec4 FragColor;

uniform sampler2D screenTexture;
uniform sampler2D screenDepthTexture;
uniform sampler2D sunDepthTexture;

uniform bool FrameBufferShadow;
uniform bool FrameBufferPosition;

uniform mat4 SunVP;
uniform mat4 InvCameraVP;
uniform float FrameShadowBias;

uniform float AmbientIntencity;

void main()
{
    vec3 col = texture(screenTexture, TexCoords).rgb;

    if (FrameBufferShadow || FrameBufferPosition){
        float depth = texture(screenDepthTexture, TexCoords).r;

        if (depth >= 0.99999f)
            discard;

        vec4 fragP = InvCameraVP * vec4(TexCoords.x * 2.0f-1.0f, TexCoords.y * 2.0f-1.0f, depth * 2.0f - 1.0f, 1.0f);
        fragP = fragP / fragP.w;

        if (FrameBufferShadow){
            vec4 fragPosLightSpace = SunVP * fragP;
            vec3 projCoords = fragPosLightSpace.xyz / fragPosLightSpace.w;
            projCoords = projCoords * 0.5f + 0.5f;
            float currentDepth = projCoords.z;


            // float closestDepth = texture(sunDepthTexture, projCoords.xy).r;

            float shadow = 0.0f;
            float bias = FrameShadowBias * 0.01f;

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


    FragColor = vec4(col, 1.0);
} 