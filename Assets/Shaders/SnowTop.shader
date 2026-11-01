// Platforms: faces pointing up (the snow on top) show their texture colour as it is, whatever the light, like snow
// glaring in the sun; sides and undersides are lit as usual (Lambert, shadows). Used by Make3D for the platforms.
Shader "BipolarBear/SnowTop"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_TopFrom ("Top from (normal y)", Range(0, 1)) = 0.6
		_TopTo ("Fully top at (normal y)", Range(0, 1)) = 0.85
		_TopBrightness ("Top brightness", Range(0, 1.5)) = 1.0
	}
	SubShader
	{
		Tags { "RenderType" = "Opaque" }
		CGPROGRAM
		#pragma surface surf Lambert fullforwardshadows
		#pragma target 3.0

		sampler2D _MainTex;
		half _TopFrom, _TopTo, _TopBrightness;

		struct Input
		{
			float2 uv_MainTex;
			float3 worldNormal;
		};

		void surf (Input IN, inout SurfaceOutput o)
		{
			fixed3 c = tex2D(_MainTex, IN.uv_MainTex).rgb;
			half top = smoothstep(_TopFrom, _TopTo, IN.worldNormal.y);
			o.Albedo = c * (1 - top);                    // lit part: the sides
			o.Emission = c * top * _TopBrightness;       // unlit part: the top
		}
		ENDCG
	}
	Fallback "Diffuse"
}
