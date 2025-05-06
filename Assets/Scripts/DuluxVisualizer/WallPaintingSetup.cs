using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.UI;

/// <summary>
/// Вспомогательный скрипт для настройки системы 2D окрашивания стен в стиле Dulux
/// </summary>
public class WallPaintingSetup : MonoBehaviour
{
      [Header("Настройка отображения")]
      [Tooltip("Цвет покраски стен")]
      public Color paintColor = new Color(0.85f, 0.1f, 0.1f, 1.0f);

      [Range(0f, 1f)]
      [Tooltip("Непрозрачность покраски (0-1)")]
      public float paintOpacity = 0.7f;

      [Range(0f, 1f)]
      [Tooltip("Степень сохранения теней (0-1)")]
      public float preserveShadows = 0.8f;

      [Header("Дополнительные параметры")]
      [Tooltip("Автоматически искать и настраивать компоненты при старте")]
      public bool autoSetupOnStart = true;

      [Tooltip("Запустить настройку сейчас (в режиме редактирования)")]
      public bool setupNow = false;

      [Header("Диагностика")]
      [SerializeField] private bool showDebug = true;

      private WallSegmentation2D wallSegmentation2D;
      private WallPaintingTextureUpdater textureUpdater;
      private Material wallPaintMaterial;

      private void Start()
      {
            if (autoSetupOnStart)
            {
                  SetupWallPainting();
            }
      }

      private void OnValidate()
      {
            if (setupNow && Application.isEditor)
            {
                  setupNow = false;
                  SetupWallPainting();
                  Debug.Log("WallPaintingSetup: Настройка выполнена");
            }

            // Обновляем настройки, если есть активные компоненты
            UpdateWallPaintingSettings();
      }

      /// <summary>
      /// Полная настройка системы 2D-окрашивания стен
      /// </summary>
      public void SetupWallPainting()
      {
            // 1. Проверяем и создаем необходимые компоненты
            CreateMissingComponents();

            // 2. Настраиваем связи между компонентами
            ConfigureComponents();

            // 3. Обновляем настройки покраски
            UpdateWallPaintingSettings();

            Debug.Log("WallPaintingSetup: Система 2D-окрашивания стен настроена");
      }

      /// <summary>
      /// Создает отсутствующие компоненты, необходимые для системы окрашивания
      /// </summary>
      private void CreateMissingComponents()
      {
            // Создаем объект для WallSegmentation2D, если не найден
            if (wallSegmentation2D == null)
            {
                  wallSegmentation2D = FindObjectOfType<WallSegmentation2D>();

                  if (wallSegmentation2D == null)
                  {
                        GameObject wallSegmentationObj = new GameObject("WallSegmentation2D");
                        wallSegmentation2D = wallSegmentationObj.AddComponent<WallSegmentation2D>();
                        Debug.Log("WallPaintingSetup: Создан объект WallSegmentation2D");

                        // Назначаем camera manager, если доступен
                        ARCameraManager cameraManager = FindObjectOfType<ARCameraManager>();
                        if (cameraManager != null)
                        {
                              SerializedObjectUtility.SetObjectReference(wallSegmentation2D, "cameraManager", cameraManager);
                              Debug.Log("WallPaintingSetup: ARCameraManager назначен для WallSegmentation2D");
                        }
                  }
            }

            // Находим или создаем Canvas для UI
            Canvas canvas = FindCanvasOrCreate();

            // Находим или создаем объект с RawImage для отображения результата
            if (textureUpdater == null)
            {
                  // Ищем существующий WallPaintingTextureUpdater
                  textureUpdater = FindObjectOfType<WallPaintingTextureUpdater>();

                  if (textureUpdater == null && canvas != null)
                  {
                        // Создаем объект с RawImage в Canvas
                        GameObject imageObj = new GameObject("WallPaintingVisualization");
                        imageObj.transform.SetParent(canvas.transform, false);

                        // Настраиваем RectTransform для заполнения всего экрана
                        RectTransform rectTransform = imageObj.AddComponent<RectTransform>();
                        rectTransform.anchorMin = Vector2.zero;
                        rectTransform.anchorMax = Vector2.one;
                        rectTransform.offsetMin = Vector2.zero;
                        rectTransform.offsetMax = Vector2.zero;
                        rectTransform.pivot = new Vector2(0.5f, 0.5f);

                        // Добавляем RawImage
                        RawImage rawImage = imageObj.AddComponent<RawImage>();
                        rawImage.color = Color.clear; // Полностью прозрачный для видимости камеры

                        // Добавляем компонент WallPaintingTextureUpdater
                        textureUpdater = imageObj.AddComponent<WallPaintingTextureUpdater>();

                        Debug.Log("WallPaintingSetup: Создан объект с RawImage и WallPaintingTextureUpdater");
                  }
                  else if (textureUpdater == null)
                  {
                        Debug.LogError("WallPaintingSetup: Не удалось создать WallPaintingTextureUpdater (Canvas не найден)");
                  }
            }

            // Создаем материал, если его нет
            if (textureUpdater != null && (textureUpdater.GetComponent<RawImage>().material == null || wallPaintMaterial == null))
            {
                  CreateWallPaintMaterial();
            }
            else if (textureUpdater != null)
            {
                  wallPaintMaterial = textureUpdater.GetComponent<RawImage>().material;
            }

            // Добавляем обновляемый компонент WallPaintingTextureUpdater, если его еще нет
            if (textureUpdater != null && !textureUpdater.gameObject.GetComponent<WallPaintingTextureUpdater>())
            {
                  textureUpdater.gameObject.AddComponent<WallPaintingTextureUpdater>();
                  Debug.Log("WallPaintingSetup: Добавлен компонент WallPaintingTextureUpdater");
            }
      }

      /// <summary>
      /// Создает материал для покраски стен
      /// </summary>
      private void CreateWallPaintMaterial()
      {
            if (textureUpdater == null) return;

            RawImage rawImage = textureUpdater.GetComponent<RawImage>();
            if (rawImage == null) return;

            // Проверяем наличие шейдера WallPaint
            Shader wallPaintShader = Shader.Find("Custom/WallPaint");

            if (wallPaintShader != null)
            {
                  // Создаем новый материал
                  wallPaintMaterial = new Material(wallPaintShader);
                  rawImage.material = wallPaintMaterial;

                  // Устанавливаем начальные значения
                  wallPaintMaterial.SetColor("_PaintColor", paintColor);
                  wallPaintMaterial.SetFloat("_PaintOpacity", paintOpacity);
                  wallPaintMaterial.SetFloat("_PreserveShadows", preserveShadows);

                  Debug.Log("WallPaintingSetup: Создан материал с шейдером Custom/WallPaint");
                  return;
            }

            // Резервные шейдеры
            string[] shaderNames = new string[] {
                  "Custom/WallPainting",
                  "Unlit/WallPainting",
                  "Hidden/WallPainting",
                  "Unlit/Texture"
            };

            // Пробуем найти подходящий шейдер
            foreach (string shaderName in shaderNames)
            {
                  Shader shader = Shader.Find(shaderName);
                  if (shader != null)
                  {
                        wallPaintMaterial = new Material(shader);
                        rawImage.material = wallPaintMaterial;

                        // Устанавливаем начальные значения, если шейдер поддерживает
                        if (wallPaintMaterial.HasProperty("_PaintColor"))
                              wallPaintMaterial.SetColor("_PaintColor", paintColor);

                        if (wallPaintMaterial.HasProperty("_PaintOpacity"))
                              wallPaintMaterial.SetFloat("_PaintOpacity", paintOpacity);

                        if (wallPaintMaterial.HasProperty("_PreserveShadows"))
                              wallPaintMaterial.SetFloat("_PreserveShadows", preserveShadows);

                        Debug.Log($"WallPaintingSetup: Создан материал с шейдером {shaderName}");
                        return;
                  }
            }

            // Если ничего не нашли, используем стандартный материал
            wallPaintMaterial = new Material(Shader.Find("Unlit/Texture"));
            rawImage.material = wallPaintMaterial;
            Debug.LogWarning("WallPaintingSetup: Не найдены специальные шейдеры. Используем Unlit/Texture");
      }

      /// <summary>
      /// Находит существующий Canvas или создает новый
      /// </summary>
      private Canvas FindCanvasOrCreate()
      {
            Canvas canvas = FindObjectOfType<Canvas>();

            if (canvas == null)
            {
                  // Создаем Canvas
                  GameObject canvasObj = new GameObject("Canvas");
                  canvas = canvasObj.AddComponent<Canvas>();
                  canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                  // Установка sortingOrder, чтобы Canvas был всегда поверх AR контента
                  canvas.sortingOrder = 100;

                  // Добавляем CanvasScaler для правильного масштабирования на разных устройствах
                  CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                  scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                  scaler.referenceResolution = new Vector2(1080, 1920);
                  scaler.matchWidthOrHeight = 0.5f; // Баланс между шириной и высотой

                  canvasObj.AddComponent<GraphicRaycaster>();
                  Debug.Log("WallPaintingSetup: Создан объект Canvas");

                  // Добавляем EventSystem, если его еще нет
                  if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
                  {
                        GameObject eventSystemObj = new GameObject("EventSystem");
                        eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                        eventSystemObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                        Debug.Log("WallPaintingSetup: Создан EventSystem для UI");
                  }
            }
            else
            {
                  // Обновляем настройки существующего Canvas
                  if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                  {
                        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                        Debug.Log("WallPaintingSetup: Обновлен режим Canvas в ScreenSpaceOverlay");
                  }

                  // Убедимся, что Canvas имеет высокий sortingOrder
                  if (canvas.sortingOrder < 100)
                  {
                        canvas.sortingOrder = 100;
                        Debug.Log("WallPaintingSetup: Обновлен sortingOrder Canvas");
                  }

                  // Проверяем наличие GraphicRaycaster
                  GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
                  if (raycaster == null)
                  {
                        raycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();
                        Debug.Log("WallPaintingSetup: Добавлен GraphicRaycaster на существующий Canvas");
                  }
            }

            return canvas;
      }

      private void ConfigureComponents()
      {
            // Связываем компоненты между собой
            if (textureUpdater != null && wallSegmentation2D != null)
            {
                  textureUpdater.wallSegmentation2D = wallSegmentation2D;

                  // Включаем режим временной маски для отладки
                  textureUpdater.useTemporaryMask = true;
            }
      }

      private void UpdateWallPaintingSettings()
      {
            // Обновляем настройки материала и текстурного обновления
            if (textureUpdater != null)
            {
                  textureUpdater.paintColor = paintColor;
                  textureUpdater.paintOpacity = paintOpacity;
                  textureUpdater.preserveShadows = preserveShadows;
            }

            // Обновляем материал напрямую, если доступен
            if (wallPaintMaterial != null)
            {
                  if (wallPaintMaterial.HasProperty("_PaintColor"))
                        wallPaintMaterial.SetColor("_PaintColor", paintColor);

                  if (wallPaintMaterial.HasProperty("_PaintOpacity"))
                        wallPaintMaterial.SetFloat("_PaintOpacity", paintOpacity);

                  if (wallPaintMaterial.HasProperty("_PreserveShadows"))
                        wallPaintMaterial.SetFloat("_PreserveShadows", preserveShadows);
            }
      }
}

/// <summary>
/// Вспомогательный класс для работы с SerializedObject в рантайме
/// </summary>
public static class SerializedObjectUtility
{
      public static void SetObjectReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
      {
#if UNITY_EDITOR
            UnityEditor.SerializedObject serializedObject = new UnityEditor.SerializedObject(target);
            UnityEditor.SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                  property.objectReferenceValue = value;
                  serializedObject.ApplyModifiedProperties();
            }
#endif
            // В рантайме используем Reflection
            System.Reflection.FieldInfo field = target.GetType().GetField(propertyName,
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            if (field != null)
            {
                  field.SetValue(target, value);
            }
      }
}