Shader "Hidden/EnhancedWallPaint"
{
    Properties
    {
        _MainTex ("Camera Texture", 2D) = "white" {}
        _MaskTex ("Segmentation Mask", 2D) = "black" {}
        _PaintColor ("Paint Color", Color) = (1,0,0,1)
        _PaintOpacity ("Paint Opacity", Range(0,1)) = 0.7
        _PreserveShadows ("Preserve Shadows", Range(0,1)) = 0.8
        _PreserveHighlights ("Preserve Highlights", Range(0,1)) = 0.5
        _PreserveTexture ("Preserve Texture", Range(0,1)) = 0.7
        _SmoothEdges ("Smooth Edges", Range(0,1)) = 0.1
        _GammaCorrection ("Gamma Correction", Range(0.5, 2.0)) = 1.0
        _DetailPreservation ("Detail Preservation", Range(0, 1)) = 0.5
        [Toggle] _AdaptToLighting ("Adapt To Lighting", Float) = 1
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
            float _PreserveHighlights;
            float _PreserveTexture;
            float _SmoothEdges;
            float _GammaCorrection;
            float _DetailPreservation;
            float _AdaptToLighting;
            float _DebugView;

            // Преобразование RGB в пространство YIQ для лучшего сохранения текстуры
            float3 rgb_to_yiq(float3 rgb)
            {
                float3x3 mat = float3x3(
                    0.299, 0.587, 0.114,
                    0.596, -0.274, -0.322,
                    0.212, -0.523, 0.311
                );
                return mul(mat, rgb);
            }

            // Преобразование YIQ обратно в RGB
            float3 yiq_to_rgb(float3 yiq)
            {
                float3x3 mat = float3x3(
                    1.0, 0.956, 0.621,
                    1.0, -0.272, -0.647,
                    1.0, -1.105, 1.702
                );
                return mul(mat, yiq);
            }
            
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
            
            // Функция для извлечения высокочастотных деталей (текстуры)
            float3 ExtractDetails(float3 color)
            {
                // Используем простой фильтр Гаусса для размытия
                float3 blurred = color;
                float totalWeight = 1.0;
                
                // Размер ядра для размытия зависит от степени сохранения деталей
                float kernelSize = 0.001 + 0.004 * _DetailPreservation;
                
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        if (x == 0 && y == 0) continue;
                        
                        float2 offset = float2(x, y) * kernelSize;
                        float weight = 1.0 / (abs(x) + abs(y) + 1.0);
                        
                        blurred += tex2D(_MainTex, offset).rgb * weight;
                        totalWeight += weight;
                    }
                }
                
                blurred /= totalWeight;
                
                // Высокочастотные детали = оригинал - размытый
                return color - blurred;
            }
            
            // Расширенная функция для сохранения теней и текстуры
            float3 EnhancedPreserveShadows(float3 originalColor, float3 paintColor)
            {
                // Преобразуем в YIQ для отделения яркости от цвета
                float3 yiqOrig = rgb_to_yiq(originalColor);
                float3 yiqPaint = rgb_to_yiq(paintColor);
                
                // Извлекаем яркость (Y) из оригинального изображения
                float luminance = yiqOrig.x;
                
                // Вычисляем нормализованную яркость (подчеркивает средние тона)
                float normalizedLuma = saturate((luminance - 0.2) / 0.6);
                
                // Коэффициент сохранения теней зависит от яркости
                float shadowFactor = _PreserveShadows;
                if (_AdaptToLighting > 0.5)
                {
                    // Адаптируем сохранение теней к общему освещению сцены
                    float avgBrightness = luminance;
                    shadowFactor = lerp(shadowFactor, shadowFactor * 1.5, 1.0 - avgBrightness);
                }
                
                // Сохраняем больше деталей в тенях
                float darkAreas = 1.0 - smoothstep(0.0, 0.2, luminance);
                float brightAreas = smoothstep(0.8, 1.0, luminance);
                
                // Применяем нелинейное сохранение теней и бликов
                float shadowPreservation = lerp(1.0, shadowFactor * 2.0, darkAreas);
                float highlightPreservation = lerp(1.0, _PreserveHighlights * 2.0, brightAreas);
                
                // Комбинированный фактор
                float combinedFactor = max(shadowPreservation, highlightPreservation);
                
                // Модифицированная яркость для краски
                float modifiedLuma = lerp(1.0, luminance, combinedFactor);
                
                // Сохраняем детали текстуры
                float3 details = ExtractDetails(originalColor);
                float detailIntensity = length(details) * 5.0;
                float detailFactor = saturate(detailIntensity * _PreserveTexture);
                
                // Создаем результирующую YIQ
                float3 resultYIQ = float3(
                    modifiedLuma * yiqPaint.x, // Модифицированная яркость новой краски
                    lerp(yiqPaint.y, yiqOrig.y, detailFactor * _PreserveTexture), // Сохраняем детали цветности
                    lerp(yiqPaint.z, yiqOrig.z, detailFactor * _PreserveTexture)
                );
                
                // Преобразуем обратно в RGB
                float3 result = yiq_to_rgb(resultYIQ);
                
                // Добавляем сохраненные высокочастотные детали
                result += details * detailFactor * _PreserveTexture * 0.5;
                
                // Гамма-коррекция для улучшения визуального восприятия
                result = pow(max(0, result), _GammaCorrection);
                
                return result;
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
                
                // Применяем расширенное сохранение теней и текстуры к цвету краски
                float3 enhancedColor = EnhancedPreserveShadows(originalColor.rgb, _PaintColor.rgb);
                
                // Смешиваем оригинальный цвет с цветом краски, учитывая маску и прозрачность
                float blendFactor = maskValue * _PaintOpacity;
                float3 finalColor = lerp(originalColor.rgb, enhancedColor, blendFactor);
                
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