vec4 CalcDirLight(DirLight light, vec3 normal, vec3 viewDir)
{
	vec3 lightDir = normalize(-light.direction);
	// diffuse
	float diff = max(dot(normal, lightDir), 0.0);

	// test
	// diff = linearRemap(0.21, 0.7, diff, 0.15, 1.0);

	color = GetColor();
	if (color.a == 0.0)
		discard;
	
	combined = GetCombinedColor();
	
	CalResultColor();

	vec3 col = color.rgb;

	// specular
	vec3 halfwayDir = normalize(lightDir + viewDir);
	float spec = pow(max(dot(normal, halfwayDir), 0.0), shininess) * combined.g;
	vec3 specular = light.specular * spec;

	// merge
	vec3 ambient  = mix(light.ambient * col,col * combined.b * 2.0,combined.b);
	vec3 diffuse  = light.diffuse * diff * col;
	
	// shadow
	diffuse = diffuse * (1.0f - max(CalShadow(light, normal) - AmbientIntencity, 0.0f));

	return vec4((ambient + diffuse + specular), color.a);
}
