Shader "Hidden/ColorCorrection"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Gamma ("Gamma Correction", Range(0.5, 2.0)) = 1.0
        _Contrast ("Contrast", Range(0.5, 1.5)) = 1.0
        _Saturation ("Saturation", Range(0.0, 2.0)) = 1.0
        _Brightness ("Brightness", Range(0.5, 1.5)) = 1.0
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            
            sampler2D _MainTex;
            float _Gamma;
            float _Contrast;
            float _Saturation;
            float _Brightness;
            
            // Преобразование RGB в HSV
            float3 rgb_to_hsv(float3 rgb)
            {
                float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
                float4 p = lerp(float4(rgb.bg, K.wz), float4(rgb.gb, K.xy), step(rgb.b, rgb.g));
                float4 q = lerp(float4(p.xyw, rgb.r), float4(rgb.r, p.yzx), step(p.x, rgb.r));
                
                float d = q.x - min(q.w, q.y);
                float e = 1.0e-10;
                return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
            }
            
            // Преобразование HSV в RGB
            float3 hsv_to_rgb(float3 hsv)
            {
                float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                float3 p = abs(frac(hsv.xxx + K.xyz) * 6.0 - K.www);
                return hsv.z * lerp(K.xxx, saturate(p - K.xxx), hsv.y);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Получаем исходный цвет
                fixed4 col = tex2D(_MainTex, i.uv);
                
                // Применяем гамма-коррекцию
                float3 color = pow(col.rgb, 1.0 / _Gamma);
                
                // Применяем коррекцию яркости
                color *= _Brightness;
                
                // Применяем коррекцию контраста
                float3 grey = dot(color, float3(0.299, 0.587, 0.114));
                color = lerp(grey, color, _Contrast);
                
                // Применяем коррекцию насыщенности через HSV
                float3 hsv = rgb_to_hsv(color);
                hsv.y *= _Saturation; // Изменяем насыщенность
                color = hsv_to_rgb(hsv);
                
                return fixed4(color, col.a);
            }
            ENDCG
        }
    }
} 