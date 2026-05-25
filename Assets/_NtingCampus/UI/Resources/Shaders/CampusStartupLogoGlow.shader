Shader "NtingCampus/UI/Startup Logo Glow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _GlowColor ("Glow Color", Color) = (0.44,0.54,0.44,0.28)
        _GlowStrength ("Glow Strength", Range(0,1)) = 0.12
        _GlowRadius ("Glow Radius", Range(0,32)) = 14
        _BodyBoost ("Body Boost", Range(0.5,2)) = 1.08

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
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _MainTex_TexelSize;
            float4 _ClipRect;
            fixed4 _GlowColor;
            float _GlowStrength;
            float _GlowRadius;
            float _BodyBoost;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            float SampleAlpha(float2 uv, float2 direction, float radius)
            {
                return tex2D(_MainTex, uv + direction * _MainTex_TexelSize.xy * radius).a;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 body = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;
                body.rgb *= _BodyBoost;

                float radius = max(0.0, _GlowRadius);
                float nearRadius = radius * 0.42;
                float farRadius = radius;

                float glowSource = 0.0;
                glowSource = max(glowSource, SampleAlpha(IN.texcoord, float2( 1,  0), nearRadius));
                glowSource = max(glowSource, SampleAlpha(IN.texcoord, float2(-1,  0), nearRadius));
                glowSource = max(glowSource, SampleAlpha(IN.texcoord, float2( 0,  1), nearRadius));
                glowSource = max(glowSource, SampleAlpha(IN.texcoord, float2( 0, -1), nearRadius));
                glowSource = max(glowSource, SampleAlpha(IN.texcoord, float2( 0.7071,  0.7071), farRadius));
                glowSource = max(glowSource, SampleAlpha(IN.texcoord, float2(-0.7071,  0.7071), farRadius));
                glowSource = max(glowSource, SampleAlpha(IN.texcoord, float2( 0.7071, -0.7071), farRadius));
                glowSource = max(glowSource, SampleAlpha(IN.texcoord, float2(-0.7071, -0.7071), farRadius));

                float outsideGlow = saturate(glowSource - body.a);
                float glowAlpha = outsideGlow * _GlowColor.a * _GlowStrength;
                float bodyAlpha = body.a;
                float outputAlpha = bodyAlpha + glowAlpha * (1.0 - bodyAlpha);

                fixed3 outputRgb = body.rgb;
                if (outputAlpha > 0.0001)
                {
                    outputRgb = (body.rgb * bodyAlpha + _GlowColor.rgb * glowAlpha * (1.0 - bodyAlpha)) / outputAlpha;
                }

                fixed4 result = fixed4(outputRgb, outputAlpha);

                #ifdef UNITY_UI_CLIP_RECT
                result.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(result.a - 0.001);
                #endif

                return result;
            }
            ENDCG
        }
    }
}
