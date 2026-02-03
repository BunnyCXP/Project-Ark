Shader "TheGlitch/OutlineHull"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0, 0.8, 1, 1)
        _Thickness ("Thickness", Range(0.0, 0.05)) = 0.015
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }

        Pass
        {
            Name "Outline"
            Tags { "LightMode"="UniversalForward" }

            Cull Front
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            float4 _OutlineColor;
            float _Thickness;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                // 把顶点沿法线“膨胀”一点点
                float3 posOS = IN.positionOS.xyz + IN.normalOS * _Thickness;

                float3 posWS = TransformObjectToWorld(posOS);
                OUT.positionHCS = TransformWorldToHClip(posWS);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}

