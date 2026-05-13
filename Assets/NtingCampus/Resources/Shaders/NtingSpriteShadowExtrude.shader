Shader "Nting Campus/2D/Sprite Alpha Extruded Shadow"
{
    Properties
    {
        _MainTex("Sprite Texture", 2D) = "white" {}
        _ShadowColor("Shadow Color", Color) = (0.04, 0.06, 0.09, 1)
        _ShadowAlpha("Shadow Alpha", Range(0, 1)) = 0.5
        _ShadowLength("Shadow Length", Float) = 1
        _SampleCount("Sample Count", Float) = 32
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        CBUFFER_START(UnityPerMaterial)
            float4 _SpriteUVMinSize;
            float4 _SourceCenterSize;
            float4 _SourceRight;
            float4 _SourceUp;
            float4 _ShadowDir;
            float4 _ShadowColor;
            float4 _Flip;
            float _ShadowLength;
            float _ShadowAlpha;
            float _SampleCount;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
        };

        struct Varyings
        {
            float4 positionHCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
        };

        Varyings vert(Attributes input)
        {
            Varyings output;
            output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
            output.positionHCS = TransformWorldToHClip(output.positionWS);
            return output;
        }

        float SampleSpriteAlpha(float2 sampleWorld)
        {
            float2 fromCenter = sampleWorld - _SourceCenterSize.xy;
            float2 uv01;
            uv01.x = dot(fromCenter, _SourceRight.xy) / max(_SourceCenterSize.z, 0.0001) + 0.5;
            uv01.y = dot(fromCenter, _SourceUp.xy) / max(_SourceCenterSize.w, 0.0001) + 0.5;
            uv01 = lerp(uv01, 1.0 - uv01, saturate(_Flip.xy));

            float inside =
                step(0.0, uv01.x) *
                step(uv01.x, 1.0) *
                step(0.0, uv01.y) *
                step(uv01.y, 1.0);

            float2 uv = _SpriteUVMinSize.xy + uv01 * _SpriteUVMinSize.zw;
            return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a * inside;
        }

        half4 frag(Varyings input) : SV_Target
        {
            float2 direction = normalize(_ShadowDir.xy);
            int sampleCount = (int)clamp(_SampleCount, 1.0, 256.0);
            float shadow = 0.0;

            [loop]
            for (int i = 0; i < sampleCount; i++)
            {
                float t = sampleCount > 1 ? (float)i / (float)(sampleCount - 1) : 0.0;
                float dist = t * max(_ShadowLength, 0.0);
                float2 sampleWorld = input.positionWS.xy - direction * dist;
                float falloff = saturate(1.0 - t * 0.72);
                shadow = max(shadow, SampleSpriteAlpha(sampleWorld) * falloff);
            }

            float alpha = saturate(shadow * _ShadowAlpha);
            return half4(_ShadowColor.rgb, alpha);
        }
        ENDHLSL

        Pass
        {
            Name "SpriteAlphaExtrudedShadow2D"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }

        Pass
        {
            Name "SpriteAlphaExtrudedShadowForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }

        Pass
        {
            Name "SpriteAlphaExtrudedShadowDefaultUnlit"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }
    }

    Fallback Off
}
