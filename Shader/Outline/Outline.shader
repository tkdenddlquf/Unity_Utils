Shader "Custom/Outline"
{
    Properties
    {
        [PerRendererData]_MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        [HDR] _OutlineColor("Outline Color", Color) = (0,0,0,1)
        _OutlineThickness("Outline Thickness", Float) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "PreviewType"="Sprite"
            "CanUseSpriteAtlas"="True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        Lighting Off
        ZWrite Off

        Pass
        {
            Name "OUTLINE"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            half4 _OutlineColor;
            float _OutlineThickness;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.uv = TRANSFORM_TEX(IN.texcoord, _MainTex);
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float alphaThreshold = 0.1;
                float2 uv = IN.uv;
                
                float2 offset = _OutlineThickness / _ScreenParams.xy;
                
                int radius = ceil(_OutlineThickness);
                float outline = 0.0;
                float sampleCount = 0.0;
                
                for (int x = -radius; x <= radius; x++)
                {
                    for (int y = -radius; y <= radius; y++)
                    {
                        if (x == 0 && y == 0) continue;
                
                        float2 offsetUV = uv + float2(x, y) * offset;
                        fixed4 sample = tex2D(_MainTex, offsetUV);
                
                        outline += step(alphaThreshold, sample.a);
                        sampleCount += 1.0;
                    }
                }
                
                outline /= sampleCount;
                
                fixed4 original = tex2D(_MainTex, uv) * _Color;
                
                if (original.a < alphaThreshold)
                {
                    float smoothOutline = smoothstep(0.0, 0.5, outline);
                    return lerp(fixed4(0,0,0,0), _OutlineColor, smoothOutline);
                }
                
                return original;
            }
            ENDCG
        }
    }
}