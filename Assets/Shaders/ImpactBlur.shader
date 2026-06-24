Shader "TopDownRogue/ImpactBlur"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _Intensity ("Intensity", Float) = 0
    }

    SubShader
    {
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Intensity;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 offset = _MainTex_TexelSize.xy * (1.0 + _Intensity * 6.0);
                fixed4 color = tex2D(_MainTex, i.uv) * 0.34;
                color += tex2D(_MainTex, i.uv + float2(offset.x, 0)) * 0.11;
                color += tex2D(_MainTex, i.uv - float2(offset.x, 0)) * 0.11;
                color += tex2D(_MainTex, i.uv + float2(0, offset.y)) * 0.11;
                color += tex2D(_MainTex, i.uv - float2(0, offset.y)) * 0.11;
                color += tex2D(_MainTex, i.uv + offset) * 0.055;
                color += tex2D(_MainTex, i.uv - offset) * 0.055;
                color += tex2D(_MainTex, i.uv + float2(offset.x, -offset.y)) * 0.055;
                color += tex2D(_MainTex, i.uv + float2(-offset.x, offset.y)) * 0.055;
                return color;
            }
            ENDCG
        }
    }
}
