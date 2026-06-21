Shader "TopDownRogue/AttackWave"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 0.9)
        _Progress ("Progress", Range(0, 1)) = 0
        _Shape ("Shape", Float) = 0
        _Fade ("Fade", Range(0, 1)) = 0
        _Expand ("Expand", Range(0, 1)) = 0
        _Thickness ("Thickness", Range(0.02, 0.5)) = 0.16
        _Softness ("Softness", Range(0.001, 0.25)) = 0.08
    }
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        Lighting Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            fixed4 _Color;
            float _Progress;
            float _Shape;
            float _Fade;
            float _Expand;
            float _Thickness;
            float _Softness;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 centered = float2((i.uv.x - 0.5) * 2.0, i.uv.y);
                float alpha = 0.0;
                float innerLine = 0.0;

                if (_Shape < 0.5)
                {
                    float release = smoothstep(0.14, 0.24, _Progress);
                    float body = 1.05 * release;
                    float outerEdge = smoothstep(0.62, 1.0, i.uv.y);
                    float innerEdge = 1.0 - smoothstep(0.0, 0.18, i.uv.y);
                    float edgeHot = max(outerEdge, innerEdge * 0.45);
                    float cutGlint = 1.0 - smoothstep(0.02, 0.075, abs(i.uv.y - 0.94));
                    float sweepHead = saturate((_Progress - 0.16) / 0.58);
                    float tailFade = saturate((i.uv.x - (sweepHead - 0.58)) / 0.5);
                    float sharpenTip = smoothstep(0.18, 0.5, i.uv.x) * smoothstep(0.2, 0.72, sweepHead);
                    float edgeKeep = max(outerEdge, cutGlint);
                    float sheathWindow = 1.0 - smoothstep(0.06, 0.2, _Progress);
                    float drawLine = 1.0 - smoothstep(0.035, 0.085, abs((centered.x + 0.18) * 0.75 + (i.uv.y - 0.18)));
                    float drawCap = smoothstep(0.04, 0.14, i.uv.y) * (1.0 - smoothstep(0.35, 0.48, i.uv.y)) * smoothstep(-0.66, -0.16, centered.x);

                    float bodyWithTail = body * lerp(0.06, 1.0, tailFade);
                    alpha = max(bodyWithTail + edgeHot * 0.16 + edgeKeep * 0.28 + sharpenTip * 0.18, drawLine * drawCap * sheathWindow) * _Color.a * (1.0 - _Fade);
                    innerLine = max(edgeHot, max(cutGlint, sharpenTip));
                }
                else if (_Shape < 1.5)
                {
                    float head = lerp(0.08, 1.0, _Progress);
                    float reveal = smoothstep(0.0, head, i.uv.y) * (1.0 - smoothstep(head + 0.025, head + 0.085, i.uv.y));
                    float beamWidth = lerp(_Thickness * 0.44, _Thickness * 0.16, saturate(i.uv.y));
                    float core = 1.0 - smoothstep(beamWidth, beamWidth + _Softness * 0.48, abs(centered.x));
                    float aura = 1.0 - smoothstep(beamWidth * 3.0, beamWidth * 7.0, abs(centered.x));
                    float edge = 1.0 - smoothstep(0.008, 0.04, abs(abs(centered.x) - beamWidth * 1.9));
                    float sideShard = 1.0 - smoothstep(0.01, 0.05, abs(centered.x - sin(i.uv.y * 38.0) * 0.08));
                    sideShard *= smoothstep(0.18, 0.36, i.uv.y) * (1.0 - smoothstep(0.58, 0.82, i.uv.y));
                    float tip = 1.0 - smoothstep(0.0, 0.1, abs(i.uv.y - head) + abs(centered.x) * 0.12);
                    alpha = max(max(core, aura * 0.38), max(edge * 0.36, max(sideShard * 0.32, tip))) * reveal * _Color.a * (1.0 - _Fade);
                    innerLine = max(core, tip);
                }
                else if (_Shape < 2.5)
                {
                    float head = lerp(0.18, 0.98, _Progress);
                    float body = smoothstep(0.02, 0.11, i.uv.y) * (1.0 - smoothstep(head, head + 0.12, i.uv.y));
                    float taper = lerp(0.9, 0.22, saturate(i.uv.y / max(head, 0.1)));
                    float core = 1.0 - smoothstep(_Thickness * taper, _Thickness * taper + _Softness, abs(centered.x));
                    float aura = 1.0 - smoothstep(_Thickness * 4.2 * taper, _Thickness * 5.3 * taper, abs(centered.x));
                    float spearTip = 1.0 - smoothstep(0.0, 0.16, abs(i.uv.y - head) + abs(centered.x) * 0.18);
                    float sideJet = 1.0 - smoothstep(0.018, 0.07, abs(abs(centered.x) - (0.08 + i.uv.y * 0.13)));
                    float jetGate = body * smoothstep(0.18, 0.62, i.uv.y) * (1.0 - smoothstep(head - 0.1, head + 0.08, i.uv.y));
                    float shock = 1.0 - smoothstep(0.018, 0.055, abs(length(float2(centered.x * 1.6, i.uv.y - head)) - 0.12));
                    alpha = max(max(core * body, aura * body * 0.42), max(spearTip, max(sideJet * jetGate * 0.4, shock * 0.55))) * _Color.a * (1.0 - _Fade);
                    innerLine = max(spearTip, max(core, shock));
                }
                else
                {
                    float2 orbUv = (i.uv - 0.5) * 2.0;
                    float radius = length(orbUv);
                    float orbRadius = lerp(0.58, 0.92, _Expand);
                    float core = 1.0 - smoothstep(orbRadius * 0.72, orbRadius * 0.72 + _Softness, radius);
                    float shell = 1.0 - smoothstep(_Thickness * 0.9, _Thickness * 1.7, abs(radius - orbRadius * 0.78));
                    float glow = 1.0 - smoothstep(orbRadius * 0.74, orbRadius + 0.45, radius);
                    float tailCenter = saturate((0.48 - i.uv.y) / 0.52);
                    float tailWidth = lerp(0.08, 0.42, tailCenter);
                    float trail = (1.0 - smoothstep(tailWidth, tailWidth + 0.12, abs(centered.x))) * smoothstep(0.04, 0.22, tailCenter) * (1.0 - smoothstep(0.82, 1.0, tailCenter));
                    float pulse = 0.75 + 0.25 * sin(_Progress * 24.0);
                    alpha = max(max(core, shell * 0.82), max(glow * 0.56, trail * 0.55 * pulse)) * _Color.a * (1.0 - _Fade);
                    innerLine = max(core, shell);
                }

                fixed3 hotCore = lerp(fixed3(1.0, 0.96, 0.78), fixed3(0.72, 0.9, 1.0), step(2.5, _Shape));
                fixed3 color = lerp(_Color.rgb * 0.72, hotCore, saturate(innerLine));
                return fixed4(color, alpha);
            }
            ENDCG
        }
    }
}
