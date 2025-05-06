Shader "Custom/WallPainting"
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
        Tags { "RenderType"="Opaque" }
        LOD 100
        
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            
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
            
            sampler2D _MainTex;
            sampler2D _MaskTex;
            float4 _MainTex_ST;
            float4 _PaintColor;
            float _PaintOpacity;
            float _PreserveShadows;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // Получаем оригинальный цвет
                fixed4 origColor = tex2D(_MainTex, i.uv);
                
                // Получаем маску стены (0-1)
                float maskValue = tex2D(_MaskTex, i.uv).r;
                
                // Вычисляем яркость оригинального цвета для сохранения теней
                float luminance = dot(origColor.rgb, float3(0.299, 0.587, 0.114));
                
                // Смешиваем цвет стены с цветом краски, сохраняя тени
                fixed3 colorWithShadows = _PaintColor.rgb * lerp(1.0, luminance, _PreserveShadows);
                
                // Применяем окрашивание с учетом маски и прозрачности
                fixed3 finalColor = lerp(origColor.rgb, colorWithShadows, maskValue * _PaintOpacity);
                
                return fixed4(finalColor, 1.0);
            }
            ENDCGPROGRAM
        }
    }
}