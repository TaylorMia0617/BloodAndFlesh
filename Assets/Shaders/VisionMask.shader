Shader "TopDownRogue/VisionMask"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _MaskColor ("Mask Color", Color) = (0, 0, 0, 1)
        _PlayerPosition ("Player Position", Vector) = (0, 0, 0, 0)
        _CameraCenter ("Camera Center", Vector) = (0, 0, 0, 0)
        _RayDistanceTex ("Ray Distance", 2D) = "white" {}
        _RayCount ("Ray Count", Float) = 192
        _UseRayDistanceTex ("Use Ray Distance Texture", Float) = 0
        _OrthographicSize ("Orthographic Size", Float) = 8
        _Aspect ("Aspect", Float) = 1.777778
        _VisionRadius ("Vision Radius", Float) = 5
        _Feather ("Feather", Float) = 0.75
        _HealthDarkness ("Health Darkness", Float) = 0
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
            sampler2D _RayDistanceTex;
            float4 _MainTex_TexelSize;
            fixed4 _MaskColor;
            float4 _PlayerPosition;
            float4 _CameraCenter;
            float _RayCount;
            float _UseRayDistanceTex;
            float _OrthographicSize;
            float _Aspect;
            float _VisionRadius;
            float _Feather;
            float _HealthDarkness;

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
                fixed4 sceneColor = tex2D(_MainTex, i.uv);
                float2 normalized = i.uv - 0.5;
                float2 worldPos = _CameraCenter.xy + float2(
                    normalized.x * _OrthographicSize * 2.0 * _Aspect,
                    normalized.y * _OrthographicSize * 2.0
                );

                float distanceToPlayer = distance(worldPos, _PlayerPosition.xy);
                float alpha = smoothstep(_VisionRadius, _VisionRadius + max(_Feather, 0.001), distanceToPlayer);
                if (_UseRayDistanceTex > 0.5 && distanceToPlayer > 0.001)
                {
                    float2 fromPlayer = worldPos - _PlayerPosition.xy;
                    float angle = atan2(fromPlayer.y, fromPlayer.x);
                    float angle01 = frac(angle / 6.28318530718);
                    float normalizedRayDistance = tex2D(_RayDistanceTex, float2(angle01, 0.5)).r;
                    float rayDistance = normalizedRayDistance * (_VisionRadius + max(_Feather, 0.001));
                    float occlusion = smoothstep(rayDistance - 0.08, rayDistance + max(_Feather * 0.25, 0.03), distanceToPlayer);
                    alpha = max(alpha, occlusion);
                }
                fixed4 masked = lerp(sceneColor, _MaskColor, alpha * _MaskColor.a);
                float visibleDarkness = saturate(_HealthDarkness) * (1.0 - alpha);
                masked.rgb = lerp(masked.rgb, 0, visibleDarkness);
                return masked;
            }
            ENDCG
        }
    }
}
