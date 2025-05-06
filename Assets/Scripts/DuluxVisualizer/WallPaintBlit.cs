using UnityEngine;

/// <summary>
/// Applies paint effect to walls using segmentation mask
/// </summary>
public class WallPaintBlit : MonoBehaviour
{
      // Shader properties
      private Shader wallPaintShader;
      private Material wallPaintMaterial;

      // Segmentation mask texture
      [SerializeField] private Texture _maskTexture;

      // Paint color and opacity
      [SerializeField] private Color _paintColor = Color.red;
      [SerializeField, Range(0, 1)] private float _paintOpacity = 0.7f;

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

      private void Start()
      {
            // Load shader
            wallPaintShader = Shader.Find("Hidden/WallPaint");

            if (wallPaintShader == null)
            {
                  Debug.LogError("WallPaint shader not found! Make sure it's in the Shaders folder and properly compiled.");
                  enabled = false;
                  return;
            }

            // Create material from shader
            wallPaintMaterial = new Material(wallPaintShader);
      }

      private void OnRenderImage(RenderTexture source, RenderTexture destination)
      {
            // Make sure shader and mask texture are available
            if (wallPaintShader == null || _maskTexture == null)
            {
                  Graphics.Blit(source, destination);
                  return;
            }

            // Set up shader parameters
            wallPaintMaterial.SetTexture("_MainTex", source);
            wallPaintMaterial.SetTexture("_MaskTex", _maskTexture);
            wallPaintMaterial.SetColor("_PaintColor", _paintColor);
            wallPaintMaterial.SetFloat("_PaintOpacity", _paintOpacity);

            // Apply effect
            Graphics.Blit(source, destination, wallPaintMaterial);
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
      }
}