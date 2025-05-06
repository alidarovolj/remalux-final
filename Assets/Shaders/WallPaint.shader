Shader "Custom/WallPaint"
{
    Properties
    {
        _MainTex ("Camera Texture", 2D) = "white" {}
        _MaskTex ("Segmentation Mask", 2D) = "black" {}
        _PaintColor ("Paint Color", Color) = (1,0,0,1)
        _PaintOpacity ("Paint Opacity", Range(0,1)) = 0.7
        _PreserveShadows ("Preserve Shadows", Range(0,1)) = 0.8
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 200
        
        // Настройка для прозрачности
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        CGPROGRAM
        // Physically based Standard lighting model
        #pragma surface surf Standard fullforwardshadows alpha:fade

        // Use shader model 3.0 target
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _MaskTex;
        fixed4 _PaintColor;
        float _PaintOpacity;
        float _PreserveShadows;

        struct Input
        {
            float2 uv_MainTex;
            float2 uv_MaskTex;
        };

        half _Glossiness;
        half _Metallic;

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Получаем оригинальный цвет и маску
            fixed4 origColor = tex2D(_MainTex, IN.uv_MainTex);
            float maskValue = tex2D(_MaskTex, IN.uv_MaskTex).r;
            
            // Вычисляем яркость оригинального цвета для сохранения теней
            float luminance = dot(origColor.rgb, fixed3(0.299, 0.587, 0.114));
            
            // Смешиваем цвет стены с цветом краски, сохраняя тени
            fixed3 colorWithShadows = _PaintColor.rgb * lerp(1.0, luminance, _PreserveShadows);
            
            // Применяем окрашивание с учетом маски и прозрачности
            fixed3 finalColor = lerp(origColor.rgb, colorWithShadows, maskValue * _PaintOpacity);
            
            o.Albedo = finalColor;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = 1.0;
        }
        ENDCG
    }
    FallBack "Diffuse"
} 