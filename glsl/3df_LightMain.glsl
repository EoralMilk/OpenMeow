void main()
{
	if (RenderDepthBuffer){
		return;
	}

	if (EnableDepthPreview)
	{
		float intensity = 1.0 - gl_FragCoord.z;
		FragColor = vec4(vec3(intensity), 1.0);
	}
	else{
		vec4 result;
		result = CalcDirLight(dirLight, Normal, normalize(viewPos - FragPos));

		if (IsTwist){
			AdditionFrag = vec4(vec3((snoise(TexCoords + vec2(TwistTime)) - 0.5) * result.a * -0.04 * TwistTime), result.a);
		}		
		else
		{
			AdditionFrag = vec4(0,0,0,1.0);
		}

		if (vTint.a < 0.0f)
		{
			result = vec4(vTint.rgb, -vTint.a);
		}
		else
		{
			result *= vTint;
		}

		FragColor = result;
	}
}
