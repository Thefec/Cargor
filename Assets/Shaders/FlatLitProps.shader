Shader "Custom/FlatLitProps"
{
    Properties
    {
        [Header(Base)]
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _BaseMap ("Base Texture", 2D) = "white" {}
        _AmbientStrength ("Ambient Strength", Range(0,1)) = 0.25

        [Header(Detail)]
        _DetailStrength ("Detail Visibility", Range(0, 1)) = 0.3

        [Header(Normal Map)]
        [Toggle(_USE_NORMAL_MAP)] _UseNormalMap ("Use Normal Map", Float) = 0
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0, 2)) = 1.0

        [Header(Lighting)]
        _ShadowSharpness ("Shadow Sharpness", Range(0.01, 1.0)) = 0.5
        _LightSteps ("Light Steps (Cel)", Range(1, 5)) = 3
        _CelSmoothness ("Cel Smoothness", Range(0, 1)) = 0.5
        _NormalInfluence ("Normal Influence", Range(0, 1)) = 0.6
    }

    SubShader
    {
        Tags 
        { 
            "RenderType"="Opaque" 
            "RenderPipeline"="UniversalPipeline" 
            "Queue"="Geometry" 
        }

        // ─── PASS: PROPS FLAT LIT ─────────────────────────
        Pass
        {
            Name "PropsForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma shader_feature_local _USE_NORMAL_MAP
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseMap_ST;
                float4 _NormalMap_ST;
                float  _NormalStrength;
                float  _AmbientStrength;
                float  _ShadowSharpness;
                float  _LightSteps;
                float  _CelSmoothness;
                float  _NormalInfluence;
                float  _DetailStrength;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 tangentWS   : TEXCOORD2;
                float3 bitangentWS : TEXCOORD3;
                float2 uv          : TEXCOORD4;
            };

            Varyings vert(Attributes IN) {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);

                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;

                VertexNormalInputs normalInputs = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);
                OUT.normalWS    = normalInputs.normalWS;
                OUT.tangentWS   = normalInputs.tangentWS;
                OUT.bitangentWS = normalInputs.bitangentWS;

                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target {
                // ── Texture oku ──
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);

                // ── Detay cikarimi ──
                // Texture'daki renk farklarini (detaylari) ayikla
                // Luminance = texture'in parlaklik degeri
                half texLum = dot(texColor.rgb, half3(0.299, 0.587, 0.114));

                // Flat renk: BaseColor * texture ortalama tonu
                half3 flatColor = _BaseColor.rgb * texLum;
                
                // Detayli renk: gercek texture renkleri
                half3 detailColor = texColor.rgb * _BaseColor.rgb;
                
                // DetailStrength ile flat ve detayli arasi gecis
                // 0 = tamamen flat (duz renk), 1 = tamamen texture detayli
                half3 baseColor = lerp(flatColor, detailColor, _DetailStrength);

                // ── Normal ──
                float3 normalWS;
                #ifdef _USE_NORMAL_MAP
                    half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, IN.uv));
                    normalTS.xy *= _NormalStrength;
                    normalTS = normalize(normalTS);

                    float3x3 TBN = float3x3(
                        normalize(IN.tangentWS),
                        normalize(IN.bitangentWS),
                        normalize(IN.normalWS)
                    );
                    normalWS = normalize(mul(normalTS, TBN));
                #else
                    normalWS = normalize(IN.normalWS);
                #endif

                // ── Shadow ──
                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                    float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                #else
                    float4 shadowCoord = float4(0,0,0,0);
                #endif

                Light mainLight = GetMainLight(shadowCoord);

                // ── NdotL cel-shading ──
                float NdotL = dot(normalWS, mainLight.direction);
                NdotL = NdotL * 0.5 + 0.5; // Half-Lambert

                float steps = max(1.0, _LightSteps);
                float x  = NdotL * steps;
                float fl = floor(x);
                float fr = x - fl;
                // _CelSmoothness=0 -> eski sert davranis; artikca bant kenari yumusar.
                float edge0 = 1.0 - max(_CelSmoothness, 1e-4); // smoothness=0'da sifira bolme koru
                float soft = smoothstep(edge0, 1.0, fr);
                float celShade = (fl + soft) / steps;
                float lightFactor = lerp(1.0, celShade, _NormalInfluence);

                // ── Shadow sharpness ──
                float shadow = mainLight.shadowAttenuation;
                shadow = smoothstep(0.5 - _ShadowSharpness * 0.5, 
                                    0.5 + _ShadowSharpness * 0.5, shadow);

                float totalLight = lightFactor * shadow;

                // ── Sonuc ──
                half3 ambient = baseColor * _AmbientStrength;
                half3 lit     = baseColor * totalLight * (1.0 - _AmbientStrength);
                half3 final   = ambient + lit;

                // ── Detay ekleme: golgedeki alanlarda bile 
                //    texture farklilikları hafifce gorunsun ──
                half3 detailBoost = (detailColor - flatColor) * _DetailStrength * 0.3;
                final += detailBoost * (1.0 - shadow * 0.5);

                final = saturate(final);

                // ── Additional lights (Forward+ compatible) ──
                #if defined(_ADDITIONAL_LIGHTS)
                    InputData inputData = (InputData)0;
                    inputData.positionWS = IN.positionWS;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);

                    uint additionalLightsCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(additionalLightsCount)
                        Light addLight = GetAdditionalLight(lightIndex, IN.positionWS);
                        float addNdotL = saturate(dot(normalWS, addLight.direction));
                        float axf = addNdotL * steps;
                        float afl = floor(axf);
                        float afr = axf - afl;
                        float asoft = smoothstep(1.0 - max(_CelSmoothness, 1e-4), 1.0, afr);
                        float addCel = (afl + asoft) / steps;
                        float addFactor = lerp(1.0, addCel, _NormalInfluence);
                        final += baseColor * addLight.color * addFactor 
                                 * addLight.distanceAttenuation 
                                 * addLight.shadowAttenuation * 0.5;
                    LIGHT_LOOP_END
                #endif

                return half4(final, texColor.a * _BaseColor.a);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }

    FallBack "Universal Render Pipeline/Lit"
}
