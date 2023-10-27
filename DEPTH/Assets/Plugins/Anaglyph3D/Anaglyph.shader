// Developed With Love by Ryan Boyer http://ryanjboyer.com <3

Shader "RenderFeature/Anaglyph" {
    Properties {
        [HideInInspector] _MainTex ("Main Texture", 2D) = "clear" {}
    }
    SubShader {
        Tags {
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "SimpleLit"
            "LightMode" = "SRPDefaultUnlit"
            "Queue" = "Geometry"
            "RenderType" = "Fullscreen"
            "ForceNoShadowCasting" = "True"
            "IgnoreProjector" = "True"
        }

        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off
        //Blend SrcAlpha OneMinusSrcAlpha

        Pass {
            HLSLPROGRAM
            #pragma target 2.0

            #pragma shader_feature_fragment _ _OPACITY_MODE_ADDITIVE _OPACITY_MODE_CHANNEL
            #pragma shader_feature_fragment _ _SINGLE_CHANNEL
            #pragma shader_feature_fragment _ _OVERLAY_EFFECT

            #pragma vertex vert
            #pragma fragment frag

		    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
		    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            struct Attributes {
                half4 positionOS : POSITION;
                half2 texcoord : TEXCOORD0;
            };

            struct Varyings {
                half4 positionCS : SV_POSITION;
                half2 texcoord : TEXCOORD0;
            };

            Varyings vert (Attributes IN) {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.texcoord = IN.texcoord;
                return OUT;
            }

            #if _OVERLAY_EFFECT
                TEXTURE2D(_MainTex);
                SAMPLER(sampler_MainTex);
            #endif

            TEXTURE2D(_LeftTex);
            SAMPLER(sampler_LeftTex);

            #if !_SINGLECHANNEL
                TEXTURE2D(_RightTex);
                SAMPLER(sampler_RightTex);
            #endif

            half4 frag (Varyings IN) : SV_Target {
                #if _OVERLAY_EFFECT
                    half4 colorMain = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.texcoord);
                    // colorMain.rgb = half3(0.663, 0.663, 0.5);
                #endif

                half4 output;

                half4 colorL = SAMPLE_TEXTURE2D(_LeftTex, sampler_LeftTex, IN.texcoord);
                half lumL = Luminance(colorL.xyz);
                half alphaL = colorL.a;
                output.a = colorL.a;

                #if _SINGLE_CHANNEL
                    half opacity = alphaL;
                    half anaglyph = lumL;
                #else
                    half4 colorR = SAMPLE_TEXTURE2D(_RightTex, sampler_RightTex, IN.texcoord);
                    half alphaR = colorR.a;
                    output.a = max(output.a, colorR.a);

                    #if _OPACITY_MODE_ADDITIVE
                        half3 opacity = (alphaL + alphaR) * half(0.5);
                    #elif _OPACITY_MODE_CHANNEL
                        half3 opacity = half3(alphaL, alphaR, alphaR);
                    #else
                        half3 opacity = max(alphaL, alphaR);
                    #endif

                    half lumR = Luminance(colorR.xyz);
                    half3 anaglyph = half3(lumL, lumR, lumR);
                #endif

                #if _OVERLAY_EFFECT
                    output.rgb = lerp(colorMain.rgb, anaglyph, opacity);
                    output.a = max(output.a, colorMain.a);
                #else
                    output.rgb = anaglyph;
                #endif

                return output;
            }
            ENDHLSL
        }
    }

    Fallback off
}