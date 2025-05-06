using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Обновляет материал с шейдером WallPainting, используя маску сегментации из WallSegmentation2D
/// </summary>
[RequireComponent(typeof(RawImage))]
public class WallPaintingTextureUpdater : MonoBehaviour
{
      [Tooltip("Компонент 2D сегментации стен")]
      public WallSegmentation2D wallSegmentation2D;

      [Tooltip("Цвет покраски")]
      public Color paintColor = new Color(0.85f, 0.1f, 0.1f, 1.0f);

      [Range(0, 1)]
      [Tooltip("Непрозрачность покраски (0-1)")]
      public float paintOpacity = 0.7f;

      [Range(0, 1)]
      [Tooltip("Степень сохранения теней (0-1)")]
      public float preserveShadows = 0.8f;

      [Tooltip("Использовать временную маску, если ML модель недоступна")]
      public bool useTemporaryMask = true;

      private Material material;
      private RawImage rawImage;
      private Texture2D temporaryMaskTexture;
      private bool useFallbackMaterial = false;

      /// <summary>
      /// Публичный метод для получения материала
      /// </summary>
      public Material GetMaterial()
      {
            if (material == null && rawImage != null)
            {
                  material = rawImage.material;
            }

            return material;
      }

      void Start()
      {
            // Находим компоненты
            rawImage = GetComponent<RawImage>();
            if (rawImage != null)
            {
                  // Настройка RawImage для того, чтобы не перекрывать камеру AR
                  rawImage.raycastTarget = false;

                  // Получаем или создаем материал
                  if (rawImage.material != null)
                  {
                        material = rawImage.material;
                        Debug.Log("WallPaintingTextureUpdater: Используем существующий материал");
                  }
                  else
                  {
                        Debug.Log("WallPaintingTextureUpdater: материал отсутствует, создаем новый");
                        EnsureProperShaderIsUsed();
                  }

                  // Если компонент WallSegmentation2D не назначен, ищем его в сцене
                  if (wallSegmentation2D == null)
                  {
                        wallSegmentation2D = FindObjectOfType<WallSegmentation2D>();
                        if (wallSegmentation2D == null)
                        {
                              Debug.LogWarning("WallPaintingTextureUpdater: не найден компонент WallSegmentation2D");

                              if (useTemporaryMask)
                              {
                                    CreateTemporaryMask();
                              }
                        }
                  }

                  // Настройка ректтрансформа для полного заполнения экрана
                  RectTransform rectTransform = GetComponent<RectTransform>();
                  if (rectTransform != null)
                  {
                        rectTransform.anchorMin = Vector2.zero;
                        rectTransform.anchorMax = Vector2.one;
                        rectTransform.offsetMin = Vector2.zero;
                        rectTransform.offsetMax = Vector2.zero;
                        rectTransform.pivot = new Vector2(0.5f, 0.5f);
                  }

                  // Важно: напрямую устанавливаем режим прозрачности на прозрачный, чтобы видеть камеру
                  if (rawImage != null)
                  {
                        rawImage.color = new Color(1f, 1f, 1f, 0.0f); // Полностью прозрачный, чтобы была видна только камера
                        Debug.Log("WallPaintingTextureUpdater: RawImage настроен на прозрачный режим");
                  }

                  // Настраиваем начальные параметры материала
                  if (material != null && (material.shader.name.Contains("WallPaint") || useFallbackMaterial))
                  {
                        if (material.HasProperty("_PaintColor"))
                        {
                              material.SetColor("_PaintColor", paintColor);
                              Debug.Log("WallPaintingTextureUpdater: Установлен цвет покраски: " + paintColor);
                        }

                        if (material.HasProperty("_PaintOpacity"))
                        {
                              material.SetFloat("_PaintOpacity", paintOpacity);
                              Debug.Log("WallPaintingTextureUpdater: Установлена прозрачность: " + paintOpacity);
                        }

                        if (material.HasProperty("_PreserveShadows"))
                        {
                              material.SetFloat("_PreserveShadows", preserveShadows);
                              Debug.Log("WallPaintingTextureUpdater: Установлено сохранение теней: " + preserveShadows);
                        }

                        // Настраиваем текстуру с камеры
                        SetupCameraTexture();

                        // Принудительное создание временной маски, если нужно
                        if (useTemporaryMask && (wallSegmentation2D == null || wallSegmentation2D.MaskTexture == null))
                        {
                              CreateTemporaryMask();
                        }
                  }
            }
      }

      private void EnsureProperShaderIsUsed()
      {
            bool needNewMaterial = false;

            // Проверяем наличие материала
            if (material == null)
            {
                  Debug.Log("WallPaintingTextureUpdater: Материал не назначен, создаем новый");
                  needNewMaterial = true;
            }
            // Проверяем правильность шейдера
            else if (!material.shader.name.Contains("WallPaint"))
            {
                  Debug.LogWarning($"Материал использует шейдер '{material.shader.name}' вместо 'Custom/WallPaint'");
                  needNewMaterial = true;
            }

            if (needNewMaterial)
            {
                  // Ищем шейдер WallPaint (исправленное название)
                  Shader wallPaintingShader = Shader.Find("Custom/WallPaint");

                  if (wallPaintingShader != null)
                  {
                        Debug.Log("WallPaintingTextureUpdater: Найден шейдер Custom/WallPaint, создаем новый материал");
                        material = new Material(wallPaintingShader);
                  }
                  else
                  {
                        // Пробуем найти альтернативные варианты шейдера
                        string[] shaderVariants = { "Custom/WallPainting", "Unlit/WallPainting", "Hidden/WallPainting" };
                        bool shaderFound = false;

                        foreach (string shaderName in shaderVariants)
                        {
                              Shader alternativeShader = Shader.Find(shaderName);
                              if (alternativeShader != null)
                              {
                                    material = new Material(alternativeShader);
                                    Debug.Log($"WallPaintingTextureUpdater: Используем альтернативный шейдер {shaderName}");
                                    shaderFound = true;
                                    break;
                              }
                        }

                        if (!shaderFound)
                        {
                              // Если ни один шейдер не найден, используем Unlit/Texture как аварийный вариант
                              Shader unlit = Shader.Find("Unlit/Texture");
                              if (unlit != null)
                              {
                                    material = new Material(unlit);
                                    useFallbackMaterial = true;
                                    Debug.LogWarning("WallPaintingTextureUpdater: Шейдеры WallPaint не найдены! Используем Unlit/Texture");
                              }
                              else
                              {
                                    Debug.LogError("WallPaintingTextureUpdater: Не удалось найти подходящий шейдер!");
                                    return;
                              }
                        }
                  }

                  // Назначаем созданный материал компоненту RawImage
                  if (rawImage != null && material != null)
                  {
                        rawImage.material = material;
                  }
            }
      }

      private void SetupCameraTexture()
      {
            if (material == null) return;

            // Если текстура камеры не назначена или null, пытаемся исправить
            if (useFallbackMaterial || !material.HasProperty("_MainTex") || material.GetTexture("_MainTex") == null)
            {
                  // Проверяем несколько источников для текстуры камеры
                  Texture cameraTexture = null;

                  // 1. Пытаемся получить текстуру из WallSegmentation2D (она уже должна иметь доступ к камере)
                  if (wallSegmentation2D != null && wallSegmentation2D.CameraTexture != null)
                  {
                        cameraTexture = wallSegmentation2D.CameraTexture;
                        Debug.Log("WallPaintingTextureUpdater: Получена текстура камеры из WallSegmentation2D");
                  }

                  // 2. Пытаемся получить текстуру из targetTexture основной камеры
                  if (cameraTexture == null)
                  {
                        Camera mainCamera = Camera.main;
                        if (mainCamera != null && mainCamera.targetTexture != null)
                        {
                              cameraTexture = mainCamera.targetTexture;
                              Debug.Log("WallPaintingTextureUpdater: Получена текстура камеры из Camera.main.targetTexture");
                        }

                        // Если Camera.main не имеет targetTexture, создаем новую RenderTexture
                        if (cameraTexture == null && mainCamera != null)
                        {
                              RenderTexture renderTexture = new RenderTexture(Screen.width, Screen.height, 24);
                              mainCamera.targetTexture = renderTexture;
                              cameraTexture = renderTexture;
                              Debug.Log("WallPaintingTextureUpdater: Создана новая RenderTexture для камеры");

                              // Не забываем вернуть targetTexture в null после рендеринга
                              mainCamera.targetTexture = null;
                        }
                  }

                  // 3. Проверяем WebCamTexture в rawImage
                  if (cameraTexture == null && rawImage.texture != null)
                  {
                        cameraTexture = rawImage.texture;
                        Debug.Log("WallPaintingTextureUpdater: Используется текстура из RawImage: " + cameraTexture);
                  }

                  // 4. Пытаемся найти текстуру камеры AR
                  if (cameraTexture == null)
                  {
                        UnityEngine.XR.ARFoundation.ARCameraManager arCameraManager = FindObjectOfType<UnityEngine.XR.ARFoundation.ARCameraManager>();
                        if (arCameraManager != null)
                        {
                              Debug.Log("WallPaintingTextureUpdater: Найден ARCameraManager, пытаемся получить текстуру");

                              // Пытаемся получить текстуру через приватное поле
                              System.Reflection.FieldInfo textureField = arCameraManager.GetType().GetField("m_CameraTexture",
                                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

                              if (textureField != null)
                              {
                                    Texture arTexture = textureField.GetValue(arCameraManager) as Texture;
                                    if (arTexture != null)
                                    {
                                          cameraTexture = arTexture;
                                          Debug.Log("WallPaintingTextureUpdater: Получена текстура из ARCameraManager: " + cameraTexture);
                                    }
                              }
                        }
                  }

                  // 5. Если все еще нет текстуры, создаем пустую текстуру для предотвращения ошибок
                  if (cameraTexture == null)
                  {
                        Debug.LogWarning("WallPaintingTextureUpdater: Не удалось найти текстуру камеры, создаем временную");
                        Texture2D tempTexture = new Texture2D(512, 512);
                        Color[] pixels = new Color[512 * 512];
                        for (int i = 0; i < pixels.Length; i++)
                        {
                              pixels[i] = new Color(0.5f, 0.5f, 0.5f, 1.0f); // Серый цвет вместо полностью белого
                        }
                        tempTexture.SetPixels(pixels);
                        tempTexture.Apply();
                        cameraTexture = tempTexture;
                  }

                  // Устанавливаем текстуру в материал
                  if (material.HasProperty("_MainTex"))
                  {
                        material.SetTexture("_MainTex", cameraTexture);
                        Debug.Log("WallPaintingTextureUpdater: Установлена текстура камеры в материал: " + cameraTexture);
                  }

                  // Если это UI/Default материал или другой fallback, также устанавливаем текстуру в rawImage
                  if (useFallbackMaterial || material.shader.name.Contains("Unlit"))
                  {
                        rawImage.texture = cameraTexture;
                        Debug.Log("WallPaintingTextureUpdater: Установлена текстура камеры в RawImage: " + cameraTexture);
                  }
            }
      }

      private void CreateTemporaryMask()
      {
            Debug.Log("WallPaintingTextureUpdater: Создание временной маски для тестирования");

            // Используем размер 32x32 для соответствия с нужным форматом модели
            int resolution = 32;
            temporaryMaskTexture = new Texture2D(resolution, resolution, TextureFormat.R8, false);
            Color[] pixels = new Color[resolution * resolution];

            for (int y = 0; y < resolution; y++)
            {
                  for (int x = 0; x < resolution; x++)
                  {
                        // Создаем простую маску: левая половина изображения - "стена"
                        float value = (x < resolution / 2) ? 1.0f : 0.0f;
                        pixels[y * resolution + x] = new Color(value, value, value, 1.0f);
                  }
            }

            temporaryMaskTexture.SetPixels(pixels);
            temporaryMaskTexture.Apply();

            // Если нет материала или текстуры, не пытаемся обновить
            if (material != null)
            {
                  if (material.HasProperty("_MaskTex"))
                  {
                        material.SetTexture("_MaskTex", temporaryMaskTexture);
                  }
                  else if (useFallbackMaterial)
                  {
                        // Для fallback материала используем основную текстуру
                        material.SetTexture("_MainTex", temporaryMaskTexture);
                  }
            }
      }

      void Update()
      {
            if (material == null) return;

            // Обновляем параметры материала, если они изменились через инспектор
            if (material.HasProperty("_PaintColor"))
                  material.SetColor("_PaintColor", paintColor);

            if (material.HasProperty("_PaintOpacity"))
                  material.SetFloat("_PaintOpacity", paintOpacity);

            if (material.HasProperty("_PreserveShadows"))
                  material.SetFloat("_PreserveShadows", preserveShadows);

            // Обновляем текстуру маски сегментации
            if (material.HasProperty("_MaskTex") && wallSegmentation2D != null && wallSegmentation2D.MaskTexture != null)
            {
                  material.SetTexture("_MaskTex", wallSegmentation2D.MaskTexture);
            }
            else if (temporaryMaskTexture != null && useTemporaryMask)
            {
                  // Если нет маски от WallSegmentation2D, используем временную
                  if (material.HasProperty("_MaskTex"))
                  {
                        material.SetTexture("_MaskTex", temporaryMaskTexture);
                  }
                  else if (useFallbackMaterial)
                  {
                        // Для fallback материала используем основную текстуру
                        material.SetTexture("_MainTex", temporaryMaskTexture);
                  }
            }
      }

      void OnDestroy()
      {
            if (temporaryMaskTexture != null)
            {
                  Destroy(temporaryMaskTexture);
            }
      }
}