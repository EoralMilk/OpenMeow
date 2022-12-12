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
