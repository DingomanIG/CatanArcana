Shader "UI/BalatroCard"
{
    Properties
    {
        [PerRendererData] _MainTex ("Card Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Mask)]
        _MaskTex ("Effect Mask (R=holographic, G=glow)", 2D) = "black" {}

        [Header(Holographic)]
        _HoloIntensity ("Holo Intensity", Range(0, 2)) = 0.6
        _HoloSpeed ("Holo Speed", Range(0, 5)) = 1.0
        _HoloScale ("Holo Scale", Range(0.5, 10)) = 3.0
        _HoloSaturation ("Holo Saturation", Range(0, 2)) = 1.0
        _HoloBrightness ("Holo Brightness", Range(0, 2)) = 1.0

        [Header(Tilt Reaction)]
        _TiltX ("Tilt X (auto)", Float) = 0
        _TiltY ("Tilt Y (auto)", Float) = 0
        _TiltInfluence ("Tilt Influence", Range(0, 3)) = 1.5

        [Header(Glow)]
        _GlowColor ("Glow Color", Color) = (1, 0.8, 0.3, 1)
        _GlowIntensity ("Glow Intensity", Range(0, 3)) = 1.0
        _GlowPulseSpeed ("Glow Pulse Speed", Range(0, 5)) = 2.0
        _GlowPulseMin ("Glow Pulse Min", Range(0, 1)) = 0.4

        [Header(Shine Sweep)]
        _ShineTex ("Shine Texture (optional)", 2D) = "white" {}
        _ShineSpeed ("Shine Speed", Range(0, 3)) = 0.5
        _ShineWidth ("Shine Width", Range(0.01, 0.5)) = 0.15
        _ShineIntensity ("Shine Intensity", Range(0, 2)) = 0.8
        _ShineAngle ("Shine Angle (degrees)", Range(0, 360)) = 30

        // UI Stencil / Masking support
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "BalatroCard"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                float4 worldPos   : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);    SAMPLER(sampler_MainTex);
            TEXTURE2D(_MaskTex);    SAMPLER(sampler_MaskTex);
            TEXTURE2D(_ShineTex);   SAMPLER(sampler_ShineTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MaskTex_ST;
                half4  _Color;

                // Holographic
                half   _HoloIntensity;
                half   _HoloSpeed;
                half   _HoloScale;
                half   _HoloSaturation;
                half   _HoloBrightness;

                // Tilt
                half   _TiltX;
                half   _TiltY;
                half   _TiltInfluence;

                // Glow
                half4  _GlowColor;
                half   _GlowIntensity;
                half   _GlowPulseSpeed;
                half   _GlowPulseMin;

                // Shine
                half   _ShineSpeed;
                half   _ShineWidth;
                half   _ShineIntensity;
                half   _ShineAngle;

                // UI clipping
                float4 _ClipRect;
            CBUFFER_END

            // === Helper: HSV ↔ RGB ===
            half3 HSVtoRGB(half3 hsv)
            {
                half h = hsv.x;
                half s = hsv.y;
                half v = hsv.z;
                half3 rgb = saturate(abs(frac(h + half3(0.0, 2.0/3.0, 1.0/3.0)) * 6.0 - 3.0) - 1.0);
                return v * lerp(half3(1,1,1), rgb, s);
            }

            // === Helper: Rainbow color from phase ===
            half3 Rainbow(half phase, half saturation, half brightness)
            {
                return HSVtoRGB(half3(frac(phase), saturation, brightness));
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                o.worldPos = float4(v.positionOS.xyz, 1.0);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // Base card color
                half4 baseColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;

                // Sample mask: R = holographic, G = glow
                float2 maskUV = i.uv * _MaskTex_ST.xy + _MaskTex_ST.zw;
                half4 mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, maskUV);
                half holoMask = mask.r;
                half glowMask = mask.g;

                half3 finalColor = baseColor.rgb;
                half  finalAlpha = baseColor.a;

                // ===== HOLOGRAPHIC EFFECT =====
                if (holoMask > 0.01)
                {
                    float time = _Time.y * _HoloSpeed;

                    // Tilt offset — 카드 기울기가 무지개 위상을 이동시킴
                    half tiltOffset = (_TiltX + _TiltY) * _TiltInfluence * 0.05;

                    // Multi-directional phase for rainbow variation
                    half phase1 = i.uv.x * _HoloScale + i.uv.y * _HoloScale * 0.5 + time + tiltOffset;
                    half phase2 = i.uv.y * _HoloScale - i.uv.x * _HoloScale * 0.3 + time * 0.7 - tiltOffset * 0.6;

                    half3 rainbow1 = Rainbow(phase1, _HoloSaturation, _HoloBrightness);
                    half3 rainbow2 = Rainbow(phase2, _HoloSaturation * 0.8, _HoloBrightness);

                    half3 holoColor = lerp(rainbow1, rainbow2, 0.4);

                    // Tilt-based brightness — 기울일수록 반짝임 강해짐
                    half tiltMag = saturate(length(half2(_TiltX, _TiltY)) * 0.08);
                    half tiltBoost = lerp(0.6, 1.4, tiltMag);

                    // Shimmer: subtle brightness oscillation + tilt influence
                    half shimmer = sin(i.uv.x * 20.0 + i.uv.y * 15.0 + time * 3.0 + tiltOffset * 10.0) * 0.15 + 0.85;
                    holoColor *= shimmer * tiltBoost;

                    // Blend holographic onto base
                    finalColor = lerp(finalColor, finalColor + holoColor * _HoloIntensity, holoMask);
                }

                // ===== GLOW EFFECT =====
                if (glowMask > 0.01)
                {
                    half pulse = lerp(_GlowPulseMin, 1.0, (sin(_Time.y * _GlowPulseSpeed) * 0.5 + 0.5));
                    half3 glow = _GlowColor.rgb * _GlowIntensity * pulse;
                    finalColor = lerp(finalColor, finalColor + glow, glowMask);
                }

                // ===== SHINE SWEEP =====
                {
                    half angleRad = _ShineAngle * 3.14159265 / 180.0;
                    half2 dir = half2(cos(angleRad), sin(angleRad));
                    half projected = dot(i.uv - 0.5, dir);

                    // Sweep position: time + tilt로 빛줄기 위치 반응
                    half tiltShift = _TiltX * _TiltInfluence * 0.03;
                    half sweepPos = sin(_Time.y * _ShineSpeed) * 0.8 + tiltShift;
                    half dist = abs(projected - sweepPos);
                    half shine = saturate(1.0 - dist / _ShineWidth);
                    shine = shine * shine; // Soften edges

                    // Apply shine only where mask allows (use max of both channels)
                    half shineMask = max(holoMask, glowMask);
                    finalColor += shine * _ShineIntensity * shineMask * half3(1, 1, 1);
                }

                half4 result = half4(finalColor, finalAlpha);

                // Premultiply alpha (UI standard)
                result.rgb *= result.a;

                // UI clipping
                #ifdef UNITY_UI_CLIP_RECT
                    float2 inside = step(_ClipRect.xy, i.worldPos.xy) * step(i.worldPos.xy, _ClipRect.zw);
                    result.a *= inside.x * inside.y;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                    clip(result.a - 0.001);
                #endif

                return result;
            }
            ENDHLSL
        }
    }

    FallBack "UI/Default"
}
