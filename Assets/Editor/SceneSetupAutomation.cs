using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Редакторский скрипт для автоматической настройки сцены при загрузке
/// </summary>
[InitializeOnLoad]
public class SceneSetupAutomation
{
      static SceneSetupAutomation()
      {
            // Подписываемся на событие загрузки сцены
            EditorSceneManager.sceneOpened += OnSceneOpened;
            Debug.Log("SceneSetupAutomation: Инициализация завершена");
      }

      private static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
      {
            Debug.Log($"SceneSetupAutomation: Открыта сцена {scene.name}");

            // Проверяем наличие AppBootstrapper
            AppBootstrapper bootstrapper = Object.FindObjectOfType<AppBootstrapper>();
            if (bootstrapper == null)
            {
                  // Создаем объект AppBootstrapper
                  GameObject bootstrapperObj = new GameObject("AppBootstrapper");
                  bootstrapper = bootstrapperObj.AddComponent<AppBootstrapper>();
                  Debug.Log("SceneSetupAutomation: Создан AppBootstrapper в сцене");

                  // Помечаем сцену как измененную
                  EditorSceneManager.MarkSceneDirty(scene);
            }

            // Проверяем наличие CanvasManager
            CanvasManager canvasManager = Object.FindObjectOfType<CanvasManager>();
            if (canvasManager == null)
            {
                  // AppBootstrapper должен создать CanvasManager при старте
                  Debug.Log("SceneSetupAutomation: CanvasManager будет создан AppBootstrapper при запуске");
            }

            // Проверяем наличие WallPaintingSetup
            WallPaintingSetup wallPaintingSetup = Object.FindObjectOfType<WallPaintingSetup>();
            if (wallPaintingSetup == null)
            {
                  // Создаем объект WallPaintingSetup
                  GameObject setupObj = new GameObject("WallPaintingSetup");
                  wallPaintingSetup = setupObj.AddComponent<WallPaintingSetup>();
                  Debug.Log("SceneSetupAutomation: Создан WallPaintingSetup в сцене");

                  // Помечаем сцену как измененную
                  EditorSceneManager.MarkSceneDirty(scene);
            }

            // Исправляем объект WallVisualization, если он существует
            FixWallVisualization(scene);
      }

      /// <summary>
      /// Исправляет объект WallVisualization для предотвращения проблемы белого экрана
      /// </summary>
      private static void FixWallVisualization(UnityEngine.SceneManagement.Scene scene)
      {
            // Находим объект WallVisualization
            GameObject wallVisObj = GameObject.Find("WallVisualization");
            bool objectFound = false;

            if (wallVisObj == null)
            {
                  // Если не нашли точно по имени, ищем объект, содержащий "WallVisualization" в имени
                  foreach (GameObject obj in Resources.FindObjectsOfTypeAll<GameObject>())
                  {
                        if (obj.name.Contains("WallVisualization") || obj.name.Contains("WallPaintingVisualization"))
                        {
                              wallVisObj = obj;
                              objectFound = true;
                              break;
                        }
                  }

                  // Если все еще не нашли, ищем по компоненту RawImage под Canvas
                  if (wallVisObj == null)
                  {
                        Canvas[] canvases = Object.FindObjectsOfType<Canvas>();
                        foreach (Canvas canvas in canvases)
                        {
                              foreach (Transform child in canvas.transform)
                              {
                                    if (child.GetComponent<RawImage>() != null)
                                    {
                                          wallVisObj = child.gameObject;
                                          objectFound = true;
                                          Debug.Log($"SceneSetupAutomation: Найден объект {wallVisObj.name} с RawImage под Canvas");
                                          break;
                                    }
                              }
                              if (objectFound) break;
                        }
                  }
            }
            else
            {
                  objectFound = true;
            }

            if (objectFound && wallVisObj != null)
            {
                  Debug.Log($"SceneSetupAutomation: Исправление объекта {wallVisObj.name}");

                  // Получаем компонент RawImage
                  RawImage rawImage = wallVisObj.GetComponent<RawImage>();
                  if (rawImage != null)
                  {
                        // Устанавливаем прозрачность
                        if (rawImage.color.a > 0)
                        {
                              rawImage.color = new Color(1f, 1f, 1f, 0f);
                              Debug.Log("SceneSetupAutomation: Исправлена прозрачность RawImage (установлена в 0)");

                              // Помечаем сцену как измененную
                              EditorSceneManager.MarkSceneDirty(scene);
                        }

                        // Проверяем компонент WallVisualizationManager
                        WallVisualizationManager manager = wallVisObj.GetComponent<WallVisualizationManager>();
                        if (manager == null)
                        {
                              // Добавляем компонент WallVisualizationManager
                              manager = wallVisObj.AddComponent<WallVisualizationManager>();
                              Debug.Log("SceneSetupAutomation: Добавлен компонент WallVisualizationManager");

                              // Помечаем сцену как измененную
                              EditorSceneManager.MarkSceneDirty(scene);
                        }
                  }
            }
      }

      [MenuItem("Remalux/Setup Current Scene")]
      public static void SetupCurrentScene()
      {
            Debug.Log("SceneSetupAutomation: Ручная настройка текущей сцены");
            OnSceneOpened(EditorSceneManager.GetActiveScene(), OpenSceneMode.Single);
      }
}