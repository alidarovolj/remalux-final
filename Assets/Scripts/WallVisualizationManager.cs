using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Компонент для управления WallVisualization и решения проблемы белого экрана
/// </summary>
public class WallVisualizationManager : MonoBehaviour
{
      [Header("References")]
      [SerializeField] private RawImage rawImage;
      [SerializeField] private WallPaintingTextureUpdater textureUpdater;

      [Header("Settings")]
      [SerializeField] private bool useTemporaryMask = true;
      [SerializeField] private Color paintColor = new Color(0.85f, 0.1f, 0.1f, 1.0f);
      [SerializeField] private float paintOpacity = 0.7f;
      [SerializeField] private float preserveShadows = 0.8f;

      [Header("Runtime")]
      [SerializeField] private bool fixOnStart = true;
      [SerializeField] private bool fixPeriodically = true;
      [SerializeField] private float checkInterval = 1.0f;

      private float lastCheckTime = 0f;

      private void Awake()
      {
            // Находим компоненты, если они не назначены
            if (rawImage == null)
                  rawImage = GetComponent<RawImage>();

            if (textureUpdater == null)
                  textureUpdater = GetComponent<WallPaintingTextureUpdater>();
      }

      private void Start()
      {
            if (fixOnStart)
                  FixVisualization();
      }

      private void Update()
      {
            if (fixPeriodically && Time.time > lastCheckTime + checkInterval)
            {
                  lastCheckTime = Time.time;

                  // Проверяем состояние RawImage
                  if (rawImage != null && rawImage.color.a > 0.1f)
                  {
                        Debug.Log("WallVisualizationManager: Обнаружен непрозрачный RawImage, исправляем");
                        FixVisualization();
                  }

                  // Проверяем наличие материала
                  if (rawImage != null && rawImage.material == null)
                  {
                        Debug.Log("WallVisualizationManager: Отсутствует материал, исправляем");
                        FixVisualization();
                  }
            }
      }

      /// <summary>
      /// Исправляет настройки визуализации
      /// </summary>
      public void FixVisualization()
      {
            // Проверяем наличие необходимых компонентов
            if (rawImage == null)
            {
                  rawImage = GetComponent<RawImage>();
                  if (rawImage == null)
                  {
                        Debug.LogError("WallVisualizationManager: RawImage компонент не найден!");
                        return;
                  }
            }

            // Устанавливаем прозрачность для RawImage
            rawImage.color = new Color(1f, 1f, 1f, 0f);

            // Проверяем наличие WallPaintingTextureUpdater
            if (textureUpdater == null)
            {
                  textureUpdater = GetComponent<WallPaintingTextureUpdater>();
                  if (textureUpdater == null)
                  {
                        textureUpdater = gameObject.AddComponent<WallPaintingTextureUpdater>();
                        Debug.Log("WallVisualizationManager: Добавлен WallPaintingTextureUpdater");
                  }
            }

            // Настраиваем WallPaintingTextureUpdater
            textureUpdater.useTemporaryMask = useTemporaryMask;
            textureUpdater.paintColor = paintColor;
            textureUpdater.paintOpacity = paintOpacity;
            textureUpdater.preserveShadows = preserveShadows;

            // Проверяем материал
            if (rawImage.material == null)
            {
                  // Пытаемся найти подходящий шейдер
                  Shader shader = Shader.Find("Custom/WallPaint");
                  if (shader == null)
                        shader = Shader.Find("Custom/WallPainting");
                  if (shader == null)
                        shader = Shader.Find("UI/Default");

                  if (shader != null)
                  {
                        Material material = new Material(shader);
                        rawImage.material = material;
                        Debug.Log($"WallVisualizationManager: Создан материал с шейдером {shader.name}");

                        // Настраиваем параметры материала
                        if (material.HasProperty("_PaintColor"))
                              material.SetColor("_PaintColor", paintColor);

                        if (material.HasProperty("_PaintOpacity"))
                              material.SetFloat("_PaintOpacity", paintOpacity);

                        if (material.HasProperty("_PreserveShadows"))
                              material.SetFloat("_PreserveShadows", preserveShadows);
                  }
                  else
                  {
                        Debug.LogError("WallVisualizationManager: Не удалось найти подходящий шейдер!");
                  }
            }

            // Перезапускаем компонент WallPaintingTextureUpdater
            if (textureUpdater != null)
            {
                  textureUpdater.enabled = false;
                  textureUpdater.enabled = true;
            }

            Debug.Log("WallVisualizationManager: Визуализация исправлена");
      }
}