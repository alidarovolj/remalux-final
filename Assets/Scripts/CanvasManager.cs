using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Менеджер для управления Canvas в приложении AR. 
/// Обеспечивает единую точку доступа к Canvas и его правильную настройку.
/// </summary>
public class CanvasManager : MonoBehaviour
{
      // Синглтон для доступа к менеджеру
      public static CanvasManager Instance { get; private set; }

      // Основной Canvas для UI
      public Canvas MainCanvas { get; private set; }

      [Header("Canvas Settings")]
      [SerializeField] private RenderMode canvasRenderMode = RenderMode.ScreenSpaceOverlay;
      [SerializeField] private int canvasSortOrder = 100;
      [SerializeField] private Vector2 referenceResolution = new Vector2(1080, 1920);
      [SerializeField] private float matchWidthOrHeight = 0.5f;

      private void Awake()
      {
            // Настройка синглтона
            if (Instance == null)
            {
                  Instance = this;
                  DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
                  Destroy(gameObject);
                  return;
            }

            // Инициализация Canvas при запуске
            InitializeCanvas();
      }

      /// <summary>
      /// Инициализирует Canvas или находит существующий
      /// </summary>
      private void InitializeCanvas()
      {
            // Поиск существующего Canvas в сцене
            Canvas[] canvases = FindObjectsOfType<Canvas>();

            // Если Canvas уже существует в сцене
            if (canvases.Length > 0)
            {
                  // Сначала проверяем, есть ли Canvas с именем "Canvas" или "MainCanvas"
                  Canvas mainCanvas = null;
                  foreach (Canvas canvas in canvases)
                  {
                        if (canvas.name == "Canvas" || canvas.name == "MainCanvas")
                        {
                              mainCanvas = canvas;
                              break;
                        }
                  }

                  // Если не нашли Canvas с нужным именем, используем первый найденный
                  if (mainCanvas == null)
                  {
                        mainCanvas = canvases[0];
                  }

                  MainCanvas = mainCanvas;
                  Debug.Log("CanvasManager: Используется существующий Canvas: " + MainCanvas.name);

                  // Проверяем и обновляем настройки Canvas
                  UpdateCanvasSettings(MainCanvas);
            }
            else
            {
                  // Создаем новый Canvas, если не найден существующий
                  GameObject canvasObject = new GameObject("MainCanvas");
                  MainCanvas = canvasObject.AddComponent<Canvas>();
                  MainCanvas.renderMode = canvasRenderMode;
                  MainCanvas.sortingOrder = canvasSortOrder;

                  // Добавляем CanvasScaler
                  CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
                  scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                  scaler.referenceResolution = referenceResolution;
                  scaler.matchWidthOrHeight = matchWidthOrHeight;

                  // Добавляем GraphicRaycaster для обработки событий
                  canvasObject.AddComponent<GraphicRaycaster>();

                  Debug.Log("CanvasManager: Создан новый Canvas");
            }

            // Проверяем наличие EventSystem
            EnsureEventSystemExists();
      }

      /// <summary>
      /// Обновляет настройки существующего Canvas
      /// </summary>
      private void UpdateCanvasSettings(Canvas canvas)
      {
            if (canvas == null) return;

            // Обновляем режим рендеринга, если нужно
            if (canvas.renderMode != canvasRenderMode)
            {
                  canvas.renderMode = canvasRenderMode;
                  Debug.Log("CanvasManager: Обновлен режим рендеринга Canvas");
            }

            // Обновляем порядок сортировки
            if (canvas.sortingOrder < canvasSortOrder)
            {
                  canvas.sortingOrder = canvasSortOrder;
                  Debug.Log("CanvasManager: Обновлен порядок сортировки Canvas");
            }

            // Проверяем наличие GraphicRaycaster
            if (canvas.GetComponent<GraphicRaycaster>() == null)
            {
                  canvas.gameObject.AddComponent<GraphicRaycaster>();
                  Debug.Log("CanvasManager: Добавлен GraphicRaycaster");
            }

            // Проверяем и обновляем CanvasScaler
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                  scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                  scaler.referenceResolution = referenceResolution;
                  scaler.matchWidthOrHeight = matchWidthOrHeight;
            }
            else
            {
                  scaler = canvas.gameObject.AddComponent<CanvasScaler>();
                  scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                  scaler.referenceResolution = referenceResolution;
                  scaler.matchWidthOrHeight = matchWidthOrHeight;
                  Debug.Log("CanvasManager: Добавлен CanvasScaler");
            }
      }

      /// <summary>
      /// Проверяет наличие EventSystem и создает его, если необходимо
      /// </summary>
      private void EnsureEventSystemExists()
      {
            if (FindObjectOfType<EventSystem>() == null)
            {
                  GameObject eventSystemObj = new GameObject("EventSystem");
                  eventSystemObj.AddComponent<EventSystem>();
                  eventSystemObj.AddComponent<StandaloneInputModule>();
                  Debug.Log("CanvasManager: Создан EventSystem");
            }
      }

      /// <summary>
      /// Возвращает текущий Canvas или создает новый, если его нет
      /// </summary>
      public Canvas GetCanvas()
      {
            if (MainCanvas == null)
            {
                  InitializeCanvas();
            }

            return MainCanvas;
      }

      /// <summary>
      /// Создает и возвращает новый UI элемент, прикрепленный к Canvas
      /// </summary>
      public GameObject CreateUIElement(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
      {
            Canvas canvas = GetCanvas();

            GameObject uiElement = new GameObject(name);
            RectTransform rectTransform = uiElement.AddComponent<RectTransform>();
            uiElement.transform.SetParent(canvas.transform, false);

            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = offsetMin;
            rectTransform.offsetMax = offsetMax;

            return uiElement;
      }

      /// <summary>
      /// Статический метод для исправления Canvas в сцене
      /// </summary>
      public static void FixAllCanvasesInScene()
      {
            // Находим все Canvas в сцене
            Canvas[] canvases = FindObjectsOfType<Canvas>();

            if (canvases.Length == 0)
            {
                  Debug.LogWarning("CanvasManager: В сцене не найдены Canvas объекты");
                  return;
            }

            // Исправляем каждый Canvas
            foreach (Canvas canvas in canvases)
            {
                  // Устанавливаем режим рендеринга
                  if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                  {
                        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                        Debug.Log($"CanvasManager: Исправлен режим рендеринга Canvas '{canvas.name}'");
                  }

                  // Устанавливаем порядок сортировки
                  if (canvas.sortingOrder < 100)
                  {
                        canvas.sortingOrder = 100;
                        Debug.Log($"CanvasManager: Исправлен sortingOrder для Canvas '{canvas.name}'");
                  }

                  // Проверяем наличие GraphicRaycaster
                  if (canvas.GetComponent<GraphicRaycaster>() == null)
                  {
                        canvas.gameObject.AddComponent<GraphicRaycaster>();
                        Debug.Log($"CanvasManager: Добавлен GraphicRaycaster на Canvas '{canvas.name}'");
                  }

                  // Проверяем наличие и настраиваем CanvasScaler
                  CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                  if (scaler == null)
                  {
                        scaler = canvas.gameObject.AddComponent<CanvasScaler>();
                        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                        scaler.referenceResolution = new Vector2(1080, 1920);
                        scaler.matchWidthOrHeight = 0.5f;
                        Debug.Log($"CanvasManager: Добавлен CanvasScaler на Canvas '{canvas.name}'");
                  }
                  else if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
                  {
                        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                        scaler.referenceResolution = new Vector2(1080, 1920);
                        scaler.matchWidthOrHeight = 0.5f;
                        Debug.Log($"CanvasManager: Обновлен CanvasScaler на Canvas '{canvas.name}'");
                  }

                  // Перебираем все RawImage в Canvas и исправляем их
                  RawImage[] rawImages = canvas.GetComponentsInChildren<RawImage>(true);
                  foreach (RawImage rawImage in rawImages)
                  {
                        // Проверяем наличие WallPaintingTextureUpdater
                        WallPaintingTextureUpdater updater = rawImage.GetComponent<WallPaintingTextureUpdater>();
                        if (updater != null)
                        {
                              // Если есть WallPaintingTextureUpdater, устанавливаем прозрачность
                              if (rawImage.color.a > 0.1f)
                              {
                                    rawImage.color = new Color(1f, 1f, 1f, 0f);
                                    Debug.Log($"CanvasManager: Исправлена прозрачность RawImage '{rawImage.name}'");
                              }
                        }
                  }
            }

            Debug.Log($"CanvasManager: Исправлено {canvases.Length} Canvas объектов в сцене");

            // Проверяем наличие EventSystem
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                  GameObject eventSystemObj = new GameObject("EventSystem");
                  eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                  eventSystemObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                  Debug.Log("CanvasManager: Создан EventSystem");
            }
      }
}