Shader "Hidden/WallPaint"
{
    Properties
    {
        _MainTex ("Camera Texture", 2D) = "white" {}
        _MaskTex ("Segmentation Mask", 2D) = "black" {}
        _PaintColor ("Paint Color", Color) = (1,0,0,1)
        _PaintOpacity ("Paint Opacity", Range(0,1)) = 0.7
        _PreserveShadows ("Preserve Shadows", Range(0,1)) = 0.8
        _SmoothEdges ("Smooth Edges", Range(0,1)) = 0.0
        [Toggle] _DebugView ("Debug View", Float) = 0
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
            sampler2D _MaskTex;
            fixed4 _PaintColor;
            float _PaintOpacity;
            float _PreserveShadows;
            float _SmoothEdges;
            float _DebugView;

            fixed4 frag (v2f i) : SV_Target
            {
                // Получаем оригинальный цвет и маску
                fixed4 origColor = tex2D(_MainTex, i.uv);
                
                // Применяем сглаживание к маске, если включено
                float2 texelSize = float2(1.0 / _ScreenParams.x, 1.0 / _ScreenParams.y);
                float maskValue = 0.0;
                
                if (_SmoothEdges > 0.0)
                {
                    // Выполняем размытие по 9 точкам для сглаживания краев маски
                    maskValue = 0.0;
                    float totalWeight = 0.0;
                    
                    for (int y = -1; y <= 1; y++)
                    {
                        for (int x = -1; x <= 1; x++)
                        {
                            float2 offset = float2(x, y) * texelSize * _SmoothEdges * 3.0;
                            float weight = 1.0 / (abs(x) + abs(y) + 1.0);
                            maskValue += tex2D(_MaskTex, i.uv + offset).r * weight;
                            totalWeight += weight;
                        }
                    }
                    
                    maskValue /= totalWeight;
                }
                else
                {
                    // Без сглаживания просто берем значение маски
                    maskValue = tex2D(_MaskTex, i.uv).r;
                }
                
                // Вычисляем яркость оригинального цвета для сохранения теней
                float luminance = dot(origColor.rgb, fixed3(0.299, 0.587, 0.114));
                
                // Смешиваем цвет стены с цветом краски, сохраняя тени
                fixed3 colorWithShadows = _PaintColor.rgb * lerp(1.0, luminance, _PreserveShadows);
                
                // Применяем окрашивание с учетом маски и прозрачности
                fixed3 finalColor = lerp(origColor.rgb, colorWithShadows, maskValue * _PaintOpacity);
                
                // Если включен режим отладки, показываем маску
                if (_DebugView > 0.5)
                {
                    // В режиме отладки показываем маску с оттенком цвета краски
                    finalColor = lerp(fixed3(0,0,0), _PaintColor.rgb, maskValue);
                }
                
                return fixed4(finalColor, 1.0);
            }
            ENDCG
        }
    }
} 