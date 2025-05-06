using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

/// <summary>
/// Редакторские инструменты для исправления Canvas в сцене
/// </summary>
public static class CanvasFixTools
{
      [MenuItem("Remalux/Fix Canvas Issues")]
      public static void FixCanvasIssues()
      {
            // Использование статического метода из CanvasManager
            CanvasManager.FixAllCanvasesInScene();

            Debug.Log("CanvasFixTools: Выполнена автоматическая фиксация Canvas");
      }

      [MenuItem("Remalux/Fix White Background")]
      public static void FixWhiteBackground()
      {
            // Находим все RawImage в сцене
            RawImage[] rawImages = Object.FindObjectsOfType<RawImage>();
            int count = 0;

            foreach (RawImage rawImage in rawImages)
            {
                  // Для каждого RawImage настраиваем прозрачность
                  if (rawImage.color.a > 0)
                  {
                        rawImage.color = new Color(1f, 1f, 1f, 0f);

                        // Проверяем наличие WallPaintingTextureUpdater
                        WallPaintingTextureUpdater updater = rawImage.GetComponent<WallPaintingTextureUpdater>();
                        if (updater != null)
                        {
                              updater.useTemporaryMask = true;

                              // Проверяем материал
                              if (rawImage.material != null)
                              {
                                    // Настраиваем основные параметры материала
                                    if (rawImage.material.HasProperty("_PaintColor"))
                                          rawImage.material.SetColor("_PaintColor", new Color(0.85f, 0.1f, 0.1f, 1.0f));

                                    if (rawImage.material.HasProperty("_PaintOpacity"))
                                          rawImage.material.SetFloat("_PaintOpacity", 0.7f);

                                    if (rawImage.material.HasProperty("_PreserveShadows"))
                                          rawImage.material.SetFloat("_PreserveShadows", 0.8f);
                              }
                        }

                        count++;
                  }
            }

            Debug.Log($"CanvasFixTools: Исправлено {count} RawImage компонентов");

            // Проверяем наличие WallPaintingSetup
            WallPaintingSetup[] setups = Object.FindObjectsOfType<WallPaintingSetup>();
            foreach (WallPaintingSetup setup in setups)
            {
                  setup.SetupWallPainting();
                  Debug.Log("CanvasFixTools: Переинициализирован WallPaintingSetup");
            }

            // Если WallPaintingSetup не найден, создаем новый
            if (setups.Length == 0)
            {
                  GameObject setupObj = new GameObject("WallPaintingSetup");
                  WallPaintingSetup newSetup = setupObj.AddComponent<WallPaintingSetup>();
                  newSetup.SetupWallPainting();
                  Debug.Log("CanvasFixTools: Создан и инициализирован WallPaintingSetup");
            }
      }

      [MenuItem("Remalux/Reset Scene UI")]
      public static void ResetSceneUI()
      {
            // Сначала находим все Canvas в сцене
            Canvas[] canvases = Object.FindObjectsOfType<Canvas>();

            // Удаляем все существующие Canvas
            foreach (Canvas canvas in canvases)
            {
                  Object.DestroyImmediate(canvas.gameObject);
            }

            // Удаляем существующие EventSystem
            UnityEngine.EventSystems.EventSystem[] eventSystems = Object.FindObjectsOfType<UnityEngine.EventSystems.EventSystem>();
            foreach (var es in eventSystems)
            {
                  Object.DestroyImmediate(es.gameObject);
            }

            // Удаляем все WallPaintingSetup
            WallPaintingSetup[] setups = Object.FindObjectsOfType<WallPaintingSetup>();
            foreach (var setup in setups)
            {
                  Object.DestroyImmediate(setup.gameObject);
            }

            // Создаем новый CanvasManager
            GameObject canvasManagerObj = new GameObject("CanvasManager");
            CanvasManager canvasManager = canvasManagerObj.AddComponent<CanvasManager>();

            // Создаем новый WallPaintingSetup
            GameObject setupObj = new GameObject("WallPaintingSetup");
            WallPaintingSetup newSetup = setupObj.AddComponent<WallPaintingSetup>();

            // Запускаем инициализацию
            newSetup.SetupWallPainting();

            Debug.Log("CanvasFixTools: UI сцены полностью сброшен и переинициализирован");
      }

      [MenuItem("Remalux/Fix WallVisualization")]
      public static void FixWallVisualization()
      {
            // Находим объект WallVisualization по имени
            GameObject wallVisObj = GameObject.Find("WallVisualization");
            if (wallVisObj == null)
            {
                  // Если не нашли точно по имени, ищем объект, содержащий "WallVisualization" в имени
                  foreach (GameObject obj in Resources.FindObjectsOfTypeAll<GameObject>())
                  {
                        if (obj.name.Contains("WallVisualization"))
                        {
                              wallVisObj = obj;
                              break;
                        }
                  }
            }

            if (wallVisObj == null)
            {
                  Debug.LogError("CanvasFixTools: WallVisualization не найден в сцене!");
                  return;
            }

            // Получаем компоненты RawImage и исправляем их
            RawImage rawImage = wallVisObj.GetComponent<RawImage>();
            if (rawImage != null)
            {
                  // Устанавливаем прозрачность
                  rawImage.color = new Color(1f, 1f, 1f, 0f);
                  Debug.Log("CanvasFixTools: Исправлена прозрачность RawImage на WallVisualization");

                  // Проверяем и настраиваем материал
                  if (rawImage.material == null)
                  {
                        // Создаем новый материал с шейдером WallPaint
                        Shader wallPaintShader = Shader.Find("Custom/WallPaint");
                        if (wallPaintShader != null)
                        {
                              Material material = new Material(wallPaintShader);
                              rawImage.material = material;
                              Debug.Log("CanvasFixTools: Создан новый материал с шейдером WallPaint");
                        }
                        else
                        {
                              // Если не нашли нужный шейдер, используем стандартный UI
                              Material material = new Material(Shader.Find("UI/Default"));
                              rawImage.material = material;
                              Debug.LogWarning("CanvasFixTools: Шейдер WallPaint не найден, используется UI/Default");
                        }
                  }
            }
            else
            {
                  Debug.LogError("CanvasFixTools: WallVisualization не содержит компонент RawImage!");
            }

            // Проверяем и исправляем WallPaintingTextureUpdater
            WallPaintingTextureUpdater updater = wallVisObj.GetComponent<WallPaintingTextureUpdater>();
            if (updater == null)
            {
                  // Если компонента нет, добавляем его
                  updater = wallVisObj.AddComponent<WallPaintingTextureUpdater>();
                  Debug.Log("CanvasFixTools: Добавлен WallPaintingTextureUpdater на WallVisualization");
            }

            // Настраиваем WallPaintingTextureUpdater
            updater.useTemporaryMask = true;
            updater.paintColor = new Color(0.85f, 0.1f, 0.1f, 1.0f);
            updater.paintOpacity = 0.7f;
            updater.preserveShadows = 0.8f;

            // Запускаем Start метод вручную для инициализации
            System.Reflection.MethodInfo startMethod = typeof(WallPaintingTextureUpdater).GetMethod("Start",
                  System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (startMethod != null)
            {
                  startMethod.Invoke(updater, null);
                  Debug.Log("CanvasFixTools: Вызван Start метод WallPaintingTextureUpdater");
            }

            // Обновляем и перезагружаем родительский Canvas
            Transform parent = wallVisObj.transform.parent;
            if (parent != null)
            {
                  Canvas canvas = parent.GetComponent<Canvas>();
                  if (canvas != null)
                  {
                        // Настраиваем Canvas на overlay режим
                        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                        canvas.sortingOrder = 100;

                        // Убеждаемся, что есть GraphicRaycaster
                        if (canvas.GetComponent<GraphicRaycaster>() == null)
                        {
                              canvas.gameObject.AddComponent<GraphicRaycaster>();
                        }

                        // Обновляем Canvas
                        canvas.enabled = false;
                        canvas.enabled = true;

                        Debug.Log("CanvasFixTools: Обновлен Canvas, содержащий WallVisualization");
                  }
            }

            Debug.Log("CanvasFixTools: WallVisualization исправлен");
      }

      [MenuItem("Remalux/Add WallVisualizationManager")]
      public static void AddWallVisualizationManager()
      {
            // Находим объект WallVisualization
            GameObject wallVisObj = GameObject.Find("WallVisualization");
            if (wallVisObj == null)
            {
                  // Если не нашли точно по имени, ищем объект, содержащий "WallVisualization" в имени
                  foreach (GameObject obj in Resources.FindObjectsOfTypeAll<GameObject>())
                  {
                        if (obj.name.Contains("WallVisualization"))
                        {
                              wallVisObj = obj;
                              break;
                        }
                  }
            }

            if (wallVisObj == null)
            {
                  Debug.LogError("CanvasFixTools: WallVisualization не найден в сцене!");
                  return;
            }

            // Проверяем наличие WallVisualizationManager
            WallVisualizationManager manager = wallVisObj.GetComponent<WallVisualizationManager>();
            if (manager == null)
            {
                  // Добавляем компонент
                  manager = wallVisObj.AddComponent<WallVisualizationManager>();
                  Debug.Log("CanvasFixTools: WallVisualizationManager добавлен на объект " + wallVisObj.name);

                  // Вызываем метод исправления
                  manager.FixVisualization();

                  // Помечаем сцену как измененную
                  UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(wallVisObj.scene);
            }
            else
            {
                  Debug.Log("CanvasFixTools: WallVisualizationManager уже существует на объекте " + wallVisObj.name);

                  // Вызываем метод исправления
                  manager.FixVisualization();
            }
      }

      [MenuItem("Remalux/Create New WallVisualization")]
      public static void CreateNewWallVisualization()
      {
            // Проверяем наличие Canvas
            Canvas canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                  // Создаем новый Canvas
                  GameObject canvasObj = new GameObject("Canvas");
                  canvas = canvasObj.AddComponent<Canvas>();
                  canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                  canvas.sortingOrder = 100;

                  // Добавляем необходимые компоненты
                  canvasObj.AddComponent<CanvasScaler>();
                  canvasObj.AddComponent<GraphicRaycaster>();

                  Debug.Log("CanvasFixTools: Создан новый Canvas");
            }

            // Удаляем существующий WallVisualization, если он есть
            GameObject existingObj = GameObject.Find("WallVisualization");
            if (existingObj != null)
            {
                  Object.DestroyImmediate(existingObj);
                  Debug.Log("CanvasFixTools: Удален существующий WallVisualization");
            }

            // Создаем новый объект WallVisualization
            GameObject wallVisObj = new GameObject("WallVisualization");
            wallVisObj.transform.SetParent(canvas.transform, false);

            // Настраиваем RectTransform для заполнения всего экрана
            RectTransform rectTransform = wallVisObj.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);

            // Добавляем RawImage и настраиваем его
            RawImage rawImage = wallVisObj.AddComponent<RawImage>();
            rawImage.color = new Color(1f, 1f, 1f, 0f); // Полная прозрачность

            // Создаем материал
            Shader shader = Shader.Find("Custom/WallPaint");
            if (shader == null)
                  shader = Shader.Find("Custom/WallPainting");

            if (shader != null)
            {
                  Material material = new Material(shader);
                  rawImage.material = material;

                  // Настраиваем параметры
                  if (material.HasProperty("_PaintColor"))
                        material.SetColor("_PaintColor", new Color(0.85f, 0.1f, 0.1f, 1.0f));

                  if (material.HasProperty("_PaintOpacity"))
                        material.SetFloat("_PaintOpacity", 0.7f);

                  if (material.HasProperty("_PreserveShadows"))
                        material.SetFloat("_PreserveShadows", 0.8f);

                  Debug.Log($"CanvasFixTools: Создан материал с шейдером {shader.name}");
            }
            else
            {
                  Debug.LogWarning("CanvasFixTools: Не удалось найти шейдер WallPaint, использутся стандартный");
            }

            // Добавляем компоненты
            WallPaintingTextureUpdater updater = wallVisObj.AddComponent<WallPaintingTextureUpdater>();
            updater.useTemporaryMask = true;
            updater.paintColor = new Color(0.85f, 0.1f, 0.1f, 1.0f);
            updater.paintOpacity = 0.7f;
            updater.preserveShadows = 0.8f;

            // Добавляем менеджер
            WallVisualizationManager manager = wallVisObj.AddComponent<WallVisualizationManager>();

            Debug.Log("CanvasFixTools: Создан новый WallVisualization с правильными настройками");

            // Помечаем сцену как измененную
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(wallVisObj.scene);
      }
}