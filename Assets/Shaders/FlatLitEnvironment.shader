Shader "Custom/FlatLitEnvironment"
{
    Properties
    {
        [Header(Base)]
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _BaseMap ("Base Texture", 2D) = "white" {}
        _AmbientStrength ("Ambient Strength", Range(0,1)) = 0.35

        [Header(Edge Lines)]
        [Toggle(_USE_EDGE_LINES)] _UseEdgeLines ("Enable Edge Lines", Float) = 1
        _EdgeColor ("Edge Color", Color) = (0, 0, 0, 1)
        _EdgeWidth ("Edge Width", Range(0.0001, 0.01)) = 0.002
        _EdgeSensitivity ("Edge Sensitivity", Range(0.001, 0.3)) = 0.05

        [Header(Detail)]
        _DetailStrength ("Detail Visibility", Range(0, 1)) = 0.3

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

        // ─── PASS: ENVIRONMENT FLAT LIT + TEXTURE EDGE ────
        Pass
        {
            Name "EnvironmentForward"
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
            #pragma shader_feature_local _USE_EDGE_LINES
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseMap_ST;
                float4 _BaseMap_TexelSize; // texture boyut bilgisi
                float  _AmbientStrength;
                float  _ShadowSharpness;
                float  _LightSteps;
                float  _CelSmoothness;
                float  _NormalInfluence;
                float4 _EdgeColor;
                float  _EdgeWidth;
                float  _EdgeSensitivity;
                float  _DetailStrength;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            // Oda karartma (plan §3.3, S5) — UnityPerMaterial CBUFFER'ının DIŞINDA,
            // SRP Batcher'ı korumak için (RoomViewController.cs Shader.SetGlobalX ile besler).
            float4 _CargoRoomMin;
            float4 _CargoRoomMax;
            float  _CargoRoomFade;
            float  _CargoRoomDesat;
            float  _CargoRoomDim;

            // İki kutulu çapraz geçiş (oda değişince ani sıçrama yerine yumuşak aydınlanma).
            // _CargoRoomBlend tanımsızken 0 -> roomInside = insidePrev; prev de tanımsız/sıfırsa
            // her şey dışarıda, ama _CargoRoomFade de 0 olduğundan roomK=0 ve shader KİMLİK kalır.
            float4 _CargoRoomPrevMin;
            float4 _CargoRoomPrevMax;
            float  _CargoRoomBlend;   // 0 = tamamen eski kutu, 1 = tamamen yeni kutu

            // Karartma MASKESİ: karartma yalnız oda kutularının (sarı hacimler) İÇİNDE uygulanır —
            // hiçbir odaya ait olmayan dış mekan (yeşillik, tır avlusu) kendi renginde kalır.
            // Sayaç 0 (dizi hiç gönderilmemiş) -> insideAny 0 -> shader KİMLİK.
            // Dizi boyu RoomViewController.MaxMaskBoxes ile AYNI OLMAK ZORUNDA.
            #define CARGO_ROOM_MAX_BOXES 16
            float4 _CargoRoomMaskMin[CARGO_ROOM_MAX_BOXES];
            float4 _CargoRoomMaskMax[CARGO_ROOM_MAX_BOXES];
            float  _CargoRoomMaskCount;

            // Bu nokta HERHANGİ bir oda kutusunun içinde mi? (1 = evet -> karartılmaya aday)
            float CargoRoomInsideAny(float3 positionWS) {
                int count = min((int)_CargoRoomMaskCount, CARGO_ROOM_MAX_BOXES);
                float insideAny = 0.0;
                for (int i = 0; i < count; i++) {
                    insideAny = max(insideAny,
                        (all(positionWS >= _CargoRoomMaskMin[i].xyz) &&
                         all(positionWS <= _CargoRoomMaskMax[i].xyz)) ? 1.0 : 0.0);
                }
                return insideAny;
            }

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
            };

            // Luminance hesapla
            float Luminance3(float3 color) {
                return dot(color, float3(0.299, 0.587, 0.114));
            }

            Varyings vert(Attributes IN) {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);

                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS   = normalize(TransformObjectToWorldNormal(IN.normalOS));
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target {
                // ── Texture oku ──
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);

                // ── Detail: flat ve detayli renk arasi gecis ──
                float texLum = Luminance3(texColor.rgb);
                half3 flatColor = _BaseColor.rgb * texLum;
                half3 detailColor = texColor.rgb * _BaseColor.rgb;
                half3 baseColor = lerp(flatColor, detailColor, _DetailStrength);

                // ── Shadow coord ──
                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                    float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                #else
                    float4 shadowCoord = float4(0,0,0,0);
                #endif

                Light mainLight = GetMainLight(shadowCoord);

                // ── NdotL cel-shading ──
                float3 normalWS = normalize(IN.normalWS);
                float NdotL = dot(normalWS, mainLight.direction);
                NdotL = NdotL * 0.5 + 0.5;

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

                // ── Ambient + lit ──
                half3 ambient = baseColor * _AmbientStrength;
                half3 lit     = baseColor * totalLight * (1.0 - _AmbientStrength);
                half3 finalColor = ambient + lit;

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
                        finalColor += baseColor * addLight.color * addFactor 
                                 * addLight.distanceAttenuation 
                                 * addLight.shadowAttenuation * 0.5;
                    LIGHT_LOOP_END
                #endif

                // ── Texture-based Edge Detection ──
                // Texture'daki renk farklarindan kenar bul
                // (panel cizgileri, derzler, yuzey degisimleri)
                #ifdef _USE_EDGE_LINES
                    float texelOffset = _EdgeWidth;

                    // 4 komsu pikselden texture oku (Sobel-lite)
                    float lumC = Luminance3(texColor.rgb);
                    float lumR = Luminance3(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, 
                                 IN.uv + float2(texelOffset, 0)).rgb);
                    float lumL = Luminance3(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, 
                                 IN.uv + float2(-texelOffset, 0)).rgb);
                    float lumU = Luminance3(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, 
                                 IN.uv + float2(0, texelOffset)).rgb);
                    float lumD = Luminance3(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, 
                                 IN.uv + float2(0, -texelOffset)).rgb);

                    // Capraz ornekler (daha iyi kenar algilama)
                    float lumTR = Luminance3(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, 
                                  IN.uv + float2(texelOffset, texelOffset)).rgb);
                    float lumTL = Luminance3(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, 
                                  IN.uv + float2(-texelOffset, texelOffset)).rgb);
                    float lumBR = Luminance3(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, 
                                  IN.uv + float2(texelOffset, -texelOffset)).rgb);
                    float lumBL = Luminance3(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, 
                                  IN.uv + float2(-texelOffset, -texelOffset)).rgb);

                    // Sobel operatoru
                    float sobelX = (-lumTL - 2.0 * lumL - lumBL) 
                                 + ( lumTR + 2.0 * lumR + lumBR);
                    float sobelY = (-lumTL - 2.0 * lumU - lumTR) 
                                 + ( lumBL + 2.0 * lumD + lumBR);

                    float edgeMagnitude = sqrt(sobelX * sobelX + sobelY * sobelY);

                    // Sensitivity ile esikleme
                    float edgeLine = smoothstep(_EdgeSensitivity * 0.5, 
                                               _EdgeSensitivity, edgeMagnitude);

                    // Kenar rengini uygula
                    finalColor = lerp(finalColor, _EdgeColor.rgb, edgeLine * _EdgeColor.a);
                #endif

                // ── Oda karartma (plan §3.3, S5) — iki kutulu çapraz geçiş ──
                // k dışta çarpılır: _CargoRoomFade tanımsız/0 (oda sistemi kurulmamış sahne) ise shader kimliktir.
                float insideNew  = (all(IN.positionWS >= _CargoRoomMin.xyz)     && all(IN.positionWS <= _CargoRoomMax.xyz))     ? 1.0 : 0.0;
                float insidePrev = (all(IN.positionWS >= _CargoRoomPrevMin.xyz) && all(IN.positionWS <= _CargoRoomPrevMax.xyz)) ? 1.0 : 0.0;
                float roomInside = lerp(insidePrev, insideNew, _CargoRoomBlend);
                // insideAny ile çarpılır: oda kutularının dışında kalan dış mekan hiç karartılmaz.
                float roomK = _CargoRoomFade * (1.0 - roomInside) * CargoRoomInsideAny(IN.positionWS);
                finalColor = lerp(finalColor, Luminance3(finalColor).xxx, roomK * _CargoRoomDesat);
                finalColor *= lerp(1.0, _CargoRoomDim, roomK);

                return half4(finalColor, texColor.a * _BaseColor.a);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }

    FallBack "Universal Render Pipeline/Lit"
}
