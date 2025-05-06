Shader "Hidden/ImprovedWallPaint"
{
    Properties
    {
        _MainTex ("Camera Texture", 2D) = "white" {}
        _MaskTex ("Segmentation Mask", 2D) = "black" {}
        _PaintColor ("Paint Color", Color) = (1,0,0,1)
        _PaintOpacity ("Paint Opacity", Range(0,1)) = 0.7
        _PreserveShadows ("Preserve Shadows", Range(0,1)) = 0.8
        _SmoothEdges ("Smooth Edges", Range(0,1)) = 0.1
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
            float4 _MaskTex_TexelSize;
            float4 _PaintColor;
            float _PaintOpacity;
            float _PreserveShadows;
            float _SmoothEdges;
            float _DebugView;

            // Функция для получения сглаженного значения маски
            float GetSmoothedMask(float2 uv)
            {
                if (_SmoothEdges <= 0.001f)
                {
                    // Если сглаживание отключено, возвращаем оригинальное значение
                    return tex2D(_MaskTex, uv).r;
                }
                
                // Размер ядра для сглаживания (от 1 до 5 пикселей)
                float kernelSize = 1.0 + _SmoothEdges * 4.0;
                float stepSize = kernelSize * _MaskTex_TexelSize.x;
                
                // Применяем ядро свертки для сглаживания
                float smoothedValue = 0.0;
                float totalWeight = 0.0;
                
                // Сэмплируем 9 точек вокруг текущего пикселя
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 offset = float2(x, y) * stepSize;
                        float dist = length(float2(x, y));
                        
                        // Используем гауссову функцию для весов
                        float weight = exp(-dist * dist / 2.0);
                        smoothedValue += tex2D(_MaskTex, uv + offset).r * weight;
                        totalWeight += weight;
                    }
                }
                
                // Нормализуем результат
                return smoothedValue / totalWeight;
            }
            
            // Функция для улучшенного сохранения теней
            float3 PreserveShadows(float3 originalColor, float3 paintColor)
            {
                // Получаем яркость исходного цвета
                float luminance = dot(originalColor, float3(0.299, 0.587, 0.114));
                
                // Применяем нелинейное сохранение теней
                float3 shadedPaintColor;
                
                if (_PreserveShadows <= 0.001f)
                {
                    // Если сохранение теней отключено, просто возвращаем цвет краски
                    shadedPaintColor = paintColor;
                }
                else if (_PreserveShadows >= 0.999f)
                {
                    // Если сохранение теней максимальное, умножаем цвет краски на яркость
                    shadedPaintColor = paintColor * luminance;
                }
                else
                {
                    // Улучшенное сохранение теней с нелинейностью
                    float amount = _PreserveShadows;
                    
                    // Сохраняем больше деталей в темных областях
                    float adjustedLuminance = pow(luminance, 1.0 - amount * 0.5);
                    
                    // Вычисляем цвет с сохранением теней
                    shadedPaintColor = lerp(paintColor, paintColor * adjustedLuminance, amount);
                    
                    // Усиливаем контраст для более естественного вида
                    float contrastBoost = 1.0 + 0.2 * amount;
                    shadedPaintColor = (shadedPaintColor - 0.5) * contrastBoost + 0.5;
                }
                
                return max(float3(0,0,0), shadedPaintColor);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Получаем оригинальный цвет из текстуры камеры
                fixed4 originalColor = tex2D(_MainTex, i.uv);
                
                // Получаем сглаженное значение маски
                float maskValue = GetSmoothedMask(i.uv);
                
                // Если маска почти прозрачная, возвращаем оригинальный цвет без обработки
                if (maskValue < 0.01f)
                    return originalColor;
                
                // Применяем сохранение теней к цвету краски
                float3 shadowedPaintColor = PreserveShadows(originalColor.rgb, _PaintColor.rgb);
                
                // Смешиваем оригинальный цвет с цветом краски, учитывая маску и прозрачность
                float3 finalColor = lerp(originalColor.rgb, shadowedPaintColor, maskValue * _PaintOpacity);
                
                // Отображение в режиме отладки
                if (_DebugView > 0.5)
                {
                    // В режиме отладки показываем маску с оттенком цвета краски
                    return float4(maskValue * _PaintColor.rgb, 1.0);
                }
                
                return float4(finalColor, 1.0);
            }
            ENDCG
        }
    }
} 