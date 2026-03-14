Shader "ShibaLab/Stage Spotlight Text"
{
    Properties
    {
        [NoScaleOffset] _MainTex("Main Tex", 2D) = "white" {}
        _HiddenColor("Hidden Color", Color) = (1,1,1,0)
        _LitColor("Lit Color", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float4 _MainTex_ST;
            float4 _HiddenColor;
            float4 _LitColor;

            float4 _StageSpotlightPosition;
            float4 _StageSpotlightDirection;
            float _StageSpotlightRange;
            float _StageSpotlightCosOuter;
            float _StageSpotlightCosInner;
            float _StageSpotlightEnabled;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float4 color : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                return output;
            }

            half ComputeSpotFactor(float3 positionWS)
            {
                if (_StageSpotlightEnabled < 0.5)
                {
                    return 0.0h;
                }

                float3 toPixel = positionWS - _StageSpotlightPosition.xyz;
                float distanceToPixel = length(toPixel);
                if (distanceToPixel <= 0.0001 || distanceToPixel >= _StageSpotlightRange)
                {
                    return 0.0h;
                }

                float3 directionToPixel = toPixel / distanceToPixel;
                float cosAngle = dot(normalize(_StageSpotlightDirection.xyz), directionToPixel);
                float angular = saturate((cosAngle - _StageSpotlightCosOuter) / max(0.0001, _StageSpotlightCosInner - _StageSpotlightCosOuter));
                float distanceFade = saturate(1.0 - (distanceToPixel / max(0.0001, _StageSpotlightRange)));
                return angular * distanceFade;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 sampled = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half spot = ComputeSpotFactor(input.positionWS);
                half4 color = lerp(_HiddenColor, _LitColor, spot);
                color.a *= sampled.a;
                return color;
            }
            ENDHLSL
        }
    }
}