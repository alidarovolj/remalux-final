using UnityEngine;

/// <summary>
/// Applies improved paint effect to walls using segmentation mask
/// with better texture preservation and lighting interaction
/// </summary>
public class ImprovedWallPaintBlit : MonoBehaviour
{
      // Shader properties
      private Shader wallPaintShader;
      private Material wallPaintMaterial;

      // Segmentation mask texture
      [SerializeField] private Texture _maskTexture;

      // Paint color and opacity
      [SerializeField] private Color _paintColor = Color.red;
      [SerializeField, Range(0, 1)] private float _paintOpacity = 0.7f;

      // Additional visual options
      [SerializeField, Range(0, 1)] private float _preserveShadows = 0.8f;
      [SerializeField, Range(0, 1)] private float _smoothEdges = 0.1f;
      [SerializeField] private bool _debugView = false;

      // Advanced options
      [Header("Advanced Options")]
      [SerializeField] private bool _useColorCorrection = true;
      [SerializeField] private bool _adaptToLighting = true;
      [SerializeField, Range(0.5f, 2.0f)] private float _gammaCorrection = 1.0f;
      [SerializeField, Range(0.0f, 1.0f)] private float _detailPreservation = 0.5f;

      // Run-time texture storage
      private RenderTexture _tempTexture;

      // Property accessors
      public Texture maskTexture
      {
            get { return _maskTexture; }
            set { _maskTexture = value; }
      }

      public Color paintColor
      {
            get { return _paintColor; }
            set { _paintColor = value; }
      }

      public float opacity
      {
            get { return _paintOpacity; }
            set { _paintOpacity = value; }
      }

      public float preserveShadows
      {
            get { return _preserveShadows; }
            set { _preserveShadows = value; }
      }

      public float smoothEdges
      {
            get { return _smoothEdges; }
            set { _smoothEdges = value; }
      }

      public bool debugView
      {
            get { return _debugView; }
            set { _debugView = value; }
      }

      private void Start()
      {
            // Load improved shader
            wallPaintShader = Shader.Find("Hidden/ImprovedWallPaint");

            // Fallback to standard shader if improved not found
            if (wallPaintShader == null)
            {
                  Debug.LogWarning("ImprovedWallPaint shader not found! Falling back to standard WallPaint shader.");
                  wallPaintShader = Shader.Find("Hidden/WallPaint");
            }

            if (wallPaintShader == null)
            {
                  Debug.LogError("No wall paint shader found! Make sure either ImprovedWallPaint.shader or WallPaint.shader exists in the Shaders folder and is properly compiled.");
                  enabled = false;
                  return;
            }

            // Create material from shader
            wallPaintMaterial = new Material(wallPaintShader);
      }

      private void OnRenderImage(RenderTexture source, RenderTexture destination)
      {
            // Make sure shader and mask texture are available
            if (wallPaintMaterial == null || _maskTexture == null)
            {
                  Graphics.Blit(source, destination);
                  return;
            }

            // Create temp texture if needed for multi-pass effects
            if (_useColorCorrection && _tempTexture == null)
            {
                  _tempTexture = new RenderTexture(source.width, source.height, 0, source.format);
            }

            // Set up shader parameters
            wallPaintMaterial.SetTexture("_MainTex", source);
            wallPaintMaterial.SetTexture("_MaskTex", _maskTexture);
            wallPaintMaterial.SetColor("_PaintColor", _paintColor);
            wallPaintMaterial.SetFloat("_PaintOpacity", _paintOpacity);
            wallPaintMaterial.SetFloat("_PreserveShadows", _preserveShadows);
            wallPaintMaterial.SetFloat("_SmoothEdges", _smoothEdges);
            wallPaintMaterial.SetFloat("_DebugView", _debugView ? 1.0f : 0.0f);

            // Set advanced parameters if shader supports them
            if (_useColorCorrection)
            {
                  if (wallPaintMaterial.HasProperty("_GammaCorrection"))
                        wallPaintMaterial.SetFloat("_GammaCorrection", _gammaCorrection);

                  if (wallPaintMaterial.HasProperty("_DetailPreservation"))
                        wallPaintMaterial.SetFloat("_DetailPreservation", _detailPreservation);

                  if (wallPaintMaterial.HasProperty("_AdaptToLighting"))
                        wallPaintMaterial.SetFloat("_AdaptToLighting", _adaptToLighting ? 1.0f : 0.0f);
            }

            // Apply the effect
            if (_useColorCorrection && wallPaintMaterial.HasProperty("_GammaCorrection"))
            {
                  // Two-pass rendering for color correction
                  Graphics.Blit(source, _tempTexture, wallPaintMaterial);
                  ApplyColorCorrection(_tempTexture, destination);
            }
            else
            {
                  // Single-pass rendering
                  Graphics.Blit(source, destination, wallPaintMaterial);
            }
      }

      // Apply color correction as a post-process
      private void ApplyColorCorrection(RenderTexture source, RenderTexture destination)
      {
            // If we have a color correction shader, use it
            Shader colorCorrectionShader = Shader.Find("Hidden/ColorCorrection");
            if (colorCorrectionShader != null)
            {
                  Material colorCorrectionMaterial = new Material(colorCorrectionShader);
                  colorCorrectionMaterial.SetTexture("_MainTex", source);
                  colorCorrectionMaterial.SetFloat("_Gamma", _gammaCorrection);

                  Graphics.Blit(source, destination, colorCorrectionMaterial);

                  // Cleanup
                  Destroy(colorCorrectionMaterial);
            }
            else
            {
                  // Fallback to direct blit
                  Graphics.Blit(source, destination);
            }
      }

      private void OnDestroy()
      {
            // Clean up material
            if (wallPaintMaterial != null)
            {
                  if (Application.isPlaying)
                        Destroy(wallPaintMaterial);
                  else
                        DestroyImmediate(wallPaintMaterial);
            }

            // Clean up render texture
            if (_tempTexture != null)
            {
                  if (Application.isPlaying)
                        Destroy(_tempTexture);
                  else
                        DestroyImmediate(_tempTexture);
            }
      }

      // Адаптирует цвет к текущим условиям освещения
      public void AdaptToSceneLighting()
      {
            if (_adaptToLighting && Camera.main != null)
            {
                  // Получаем информацию об освещении сцены
                  var renderSettings = RenderSettings.ambientLight;
                  var mainLight = RenderSettings.sun;

                  // Адаптируем значения сохранения теней в зависимости от яркости освещения
                  if (mainLight != null)
                  {
                        float lightIntensity = mainLight.intensity;
                        // Увеличиваем сохранение теней при ярком освещении
                        _preserveShadows = Mathf.Clamp(_preserveShadows + (lightIntensity - 1.0f) * 0.1f, 0.0f, 1.0f);
                  }

                  // Адаптируем гамма-коррекцию в зависимости от общей яркости сцены
                  float ambientIntensity = (renderSettings.r + renderSettings.g + renderSettings.b) / 3.0f;
                  _gammaCorrection = Mathf.Lerp(0.8f, 1.2f, ambientIntensity);

                  Debug.Log($"Adapted to lighting conditions: PreserveShadows={_preserveShadows}, Gamma={_gammaCorrection}");
            }
      }
}