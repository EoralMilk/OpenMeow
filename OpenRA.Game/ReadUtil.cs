using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GlmSharp;
using OpenRA.Graphics;
using TrueSync;

namespace OpenRA
{
	public class ReadYamlInfo
	{
		public static T LoadField<T>(Dictionary<string, MiniYaml> d, string key, T fallback)
		{
			if (d.TryGetValue(key, out var value))
				return FieldLoader.GetValue<T>(key, value.Value);

			return fallback;
		}

		public static mat4 LoadMat4(Dictionary<string, MiniYaml> d, string key)
		{
			float3 s = LoadField(d, key + "Scale", float3.Ones);
			float4 r = LoadField(d, key + "Rotation", float4.Identity);
			float3 t = LoadField(d, key + "Translation", float3.Zero);
			mat4 tm = mat4.Translate(new vec3(t.X, t.Y, t.Z));
			mat4 rm = new mat4(new quat(r.X, r.Y, r.Z, r.W));
			mat4 sm = mat4.Scale(new vec3(s.X, s.Y, s.Z));
			return tm * (sm * rm);
		}

		public static Transformation LoadTransformation(Dictionary<string, MiniYaml> d, string key, bool normalize = false)
		{
			float3 s = LoadField(d, key + "Scale", float3.Ones);
			float4 r = LoadField(d, key + "Rotation", float4.Identity);
			float3 t = LoadField(d, key + "Translation", float3.Zero);
			return new Transformation(new TSVector((FP)s.X, (FP)s.Y, (FP)s.Z),
				normalize ? (new TSQuaternion((FP)r.X, (FP)r.Y, (FP)r.Z, (FP)r.W)).Normalize() : new TSQuaternion((FP)r.X, (FP)r.Y, (FP)r.Z, (FP)r.W),
				new TSVector((FP)t.X, (FP)t.Y, (FP)t.Z));
		}
	}
}
