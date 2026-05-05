Shader "UI/WaveEmotion"
{
    Properties
    {
        _Color ("Color", Color) = (0,0.6,1,1)
        
        [Header(Emotion)]
        _Width ("Wave Width", Range(0,100)) = 0
        _Height ("Wave Height", Range(0,100)) = 0

        _Phase ("Phase Offset", Float) = 0
        
        [Header(Ather)]
        _Thickness ("Line Thickness", Float) = 0.015

        _Frequency ("Frequency", Float) = 4
        _Falloff ("Edge Falloff", Float) = 2

        _NoiseStrength ("Noise Strength", Float) = 0.05
        _NoiseScale ("Noise Scale", Float) = 20
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float4 _Color;

            int _Width;
            int _Height;

            float _Phase;

            float _Thickness;

            float _Frequency;
            float _Falloff;

            float _NoiseStrength;
            float _NoiseScale;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float EdgeFalloff(float x)
            {
                float centered = x * 2 - 1;
                float v = cos(centered * UNITY_PI * 0.5);
                return pow(saturate(v), _Falloff);
            }

            float hash(float n) { return frac(sin(n) * 43758.5453); }

            float noise(float x)
            {
                float i = floor(x);
                float f = frac(x);
                float u = f * f * (3.0 - 2.0 * f);

                return lerp(hash(i), hash(i + 1.0), u);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float x = i.uv.x;

                float width = lerp(10, 15, _Width * 0.01);
                float height = lerp(0, 1, _Height * 0.01);

                float phase = x * width * _Frequency + _Phase;
                float wave = sin(phase);

                float n = noise(x * _NoiseScale + _Phase * 0.5);

                wave += (n - 0.5) * _NoiseStrength * height;

                float amp = 0.25 * (0.5 + height);
                float falloff = EdgeFalloff(x);

                float y = 0.5 + wave * amp * falloff;

                float dWave_dx = cos(phase) * (width * _Frequency);
                float slope = dWave_dx * amp * falloff;
                float dist = abs(i.uv.y - y) / sqrt(1 + slope * slope);

                float mask = smoothstep(_Thickness, 0, dist);

                return float4(_Color.rgb, mask);
            }
            ENDCG
        }
    }
}
