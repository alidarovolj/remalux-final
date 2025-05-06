using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.UI;

/// <summary>
/// Скрипт для инициализации приложения и обеспечения правильного порядка загрузки компонентов.
/// Этот скрипт должен быть добавлен в сцену первым.
/// </summary>
public class AppBootstrapper : MonoBehaviour
{
      [Header("AR Components")]
      [SerializeField] private ARSession arSession;
      [SerializeField] private ARCameraManager arCameraManager;

      [Header("UI Components")]
      [SerializeField] private bool autoInitializeCanvas = true;

      [Header("Wall Painting")]
      [SerializeField] private bool autoSetupWallPainting = true;

      private void Awake()
      {
            // Находим необходимые компоненты, если они не указаны
            FindARComponents();

            // Инициализируем Canvas через CanvasManager
            if (autoInitializeCanvas)
            {
                  InitializeCanvas();
            }
      }

      private void Start()
      {
            // Исправляем все Canvas в сцене для обеспечения правильного отображения
            FixCanvases();

            // Настраиваем систему окрашивания стен
            if (autoSetupWallPainting)
            {
                  SetupWallPainting();
            }

            Debug.Log("AppBootstrapper: Инициализация приложения завершена");
      }

      /// <summary>
      /// Находит компоненты AR, если они не были назначены вручную
      /// </summary>
      private void FindARComponents()
      {
            // Получаем AR Session
            if (arSession == null)
            {
                  arSession = FindObjectOfType<ARSession>();
                  if (arSession == null)
                  {
                        GameObject sessionObj = new GameObject("AR Session");
                        arSession = sessionObj.AddComponent<ARSession>();
                        sessionObj.AddComponent<ARSessionOrigin>();
                        Debug.Log("AppBootstrapper: Создан AR Session");
                  }
            }

            // Получаем AR Camera Manager
            if (arCameraManager == null)
            {
                  arCameraManager = FindObjectOfType<ARCameraManager>();
                  // Не создаем его автоматически, так как требуется правильная настройка иерархии
            }
      }

      /// <summary>
      /// Инициализирует Canvas через CanvasManager
      /// </summary>
      private void InitializeCanvas()
      {
            // Проверяем наличие CanvasManager
            CanvasManager canvasManager = FindObjectOfType<CanvasManager>();

            if (canvasManager == null)
            {
                  // Создаем новый CanvasManager
                  GameObject canvasManagerObj = new GameObject("CanvasManager");
                  canvasManager = canvasManagerObj.AddComponent<CanvasManager>();
                  Debug.Log("AppBootstrapper: Создан CanvasManager");
            }

            // Дополнительная проверка Canvas не требуется, так как CanvasManager сам его инициализирует
      }

      /// <summary>
      /// Настраивает систему окрашивания стен
      /// </summary>
      private void SetupWallPainting()
      {
            // Проверяем наличие WallPaintingSetup
            WallPaintingSetup wallPaintingSetup = FindObjectOfType<WallPaintingSetup>();

            if (wallPaintingSetup == null)
            {
                  // Создаем новый WallPaintingSetup
                  GameObject setupObj = new GameObject("WallPaintingSetup");
                  wallPaintingSetup = setupObj.AddComponent<WallPaintingSetup>();
                  Debug.Log("AppBootstrapper: Создан WallPaintingSetup");
            }

            // Вызываем настройку окрашивания стен
            wallPaintingSetup.SetupWallPainting();
      }

      /// <summary>
      /// Исправляет все Canvas в сцене для правильного отображения
      /// </summary>
      private void FixCanvases()
      {
            // Используем статический метод из CanvasManager
            CanvasManager.FixAllCanvasesInScene();

            // Дополнительная проверка иерархии Canvas
            Canvas[] canvases = FindObjectsOfType<Canvas>();

            foreach (Canvas canvas in canvases)
            {
                  // Проверяем имя Canvas
                  if (canvas.name == "Canvas" || canvas.name == "MainCanvas")
                  {
                        // Перебираем дочерние элементы Canvas и исправляем их настройки
                        foreach (Transform child in canvas.transform)
                        {
                              RawImage rawImage = child.GetComponent<RawImage>();
                              if (rawImage != null)
                              {
                                    // Убедимся, что RawImage имеет правильную прозрачность
                                    rawImage.color = new Color(1f, 1f, 1f, 0f);

                                    // Проверим наличие компонента WallPaintingTextureUpdater
                                    WallPaintingTextureUpdater updater = child.GetComponent<WallPaintingTextureUpdater>();
                                    if (updater != null)
                                    {
                                          // Настраиваем обновление текстуры
                                          updater.useTemporaryMask = true;
                                          Debug.Log($"AppBootstrapper: Настроен WallPaintingTextureUpdater на {child.name}");
                                    }
                              }
                        }
                  }
            }

            Debug.Log("AppBootstrapper: Все Canvas исправлены");
      }
}