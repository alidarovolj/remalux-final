using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEngine.XR.ARFoundation;
using System.Collections.Generic;

#if UNITY_EDITOR
/// <summary>
/// Инструмент для создания AR сцены с исправленным компонентом WallSegmentation
/// </summary>
public class CreateSceneWithFixedSegmentation : Editor
{
      // Ссылка на ARCameraManager для передачи компонентам
      private static ARCameraManager arCameraManager;

      /// <summary>
      /// Создает новую AR сцену с исправленным компонентом WallSegmentation
      /// </summary>
      [MenuItem("Tools/AR Wall Painting/Create Scene With Fixed Segmentation", false, 1)]
      public static void CreateARSceneWithFixedSegmentation()
      {
            // Спрашиваем пользователя, хочет ли он сохранить текущую сцену
            if (EditorSceneManager.GetActiveScene().isDirty)
            {
                  bool save = EditorUtility.DisplayDialog(
                      "Сохранить текущую сцену?",
                      "Текущая сцена содержит несохраненные изменения. Сохранить перед созданием новой сцены?",
                      "Сохранить", "Не сохранять");

                  if (save)
                  {
                        EditorSceneManager.SaveOpenScenes();
                  }
            }

            // Создаем новую сцену
            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SetActiveScene(newScene);

            // Отображаем диалог с инструкциями по установке пакетов
            EditorUtility.DisplayDialog("Проверьте зависимости",
                "Перед созданием сцены убедитесь, что установлены следующие пакеты:\n\n" +
                "1. AR Foundation\n" +
                "2. ARKit XR Plugin (для iOS)\n" +
                "3. ARCore XR Plugin (для Android)\n" +
                "4. Barracuda\n" +
                "5. Unity XR Core Utils\n" +
                "6. Unity UI\n\n" +
                "Установите недостающие пакеты через Package Manager.",
                "Продолжить");

            // Создаем AR сцену
            GameObject sceneRoot = new GameObject("AR Wall Painting Scene");

            // Добавляем компонент creator
            ARWallPaintingCreator creator = sceneRoot.AddComponent<ARWallPaintingCreator>();

            // Выполняем настройку сцены
            Debug.Log("Создание AR сцены для покраски стен...");
            ARWallPaintingCreator.CreateScene();

            // Находим компонент WallSegmentation и настраиваем его
            WallSegmentation[] segmentationComponents = FindObjectsOfType<WallSegmentation>();
            if (segmentationComponents.Length > 0)
            {
                  foreach (WallSegmentation segmentation in segmentationComponents)
                  {
                        // Устанавливаем безопасный демо-режим по умолчанию
                        segmentation.SwitchMode(WallSegmentation.SegmentationMode.Demo);
                        Debug.Log($"Компонент WallSegmentation на объекте {segmentation.gameObject.name} настроен в демо-режиме.");

                        // Получаем ссылку на ARCameraManager из сцены
                        if (arCameraManager == null)
                        {
                              arCameraManager = FindObjectOfType<ARCameraManager>();
                        }

                        // Находим основной Canvas для создания UI элементов
                        Canvas mainCanvas = FindObjectOfType<Canvas>();
                        if (mainCanvas == null)
                        {
                              // Если Canvas не найден, создаем новый
                              GameObject canvasObject = new GameObject("Main Canvas");
                              mainCanvas = canvasObject.AddComponent<Canvas>();
                              mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

                              // Добавляем компоненты для работы Canvas
                              canvasObject.AddComponent<UnityEngine.UI.CanvasScaler>();
                              canvasObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                        }

                        // Настраиваем отладочный дисплей для WallSegmentation, если включена отладка
                        if (segmentation.IsDebugVisualizationEnabled())
                        {
                              // Создаем объект для отображения отладочной информации
                              GameObject debugImageObject = new GameObject("Debug Image");
                              debugImageObject.transform.SetParent(mainCanvas.transform, false);

                              // Добавляем компонент RawImage для отображения отладочной информации
                              UnityEngine.UI.RawImage debugImage = debugImageObject.AddComponent<UnityEngine.UI.RawImage>();

                              // Настраиваем RectTransform для отображения в углу экрана
                              RectTransform debugRect = debugImageObject.GetComponent<RectTransform>();
                              debugRect.anchorMin = new Vector2(0, 0);
                              debugRect.anchorMax = new Vector2(0.3f, 0.3f);
                              debugRect.pivot = new Vector2(0, 0);
                              debugRect.anchoredPosition = Vector2.zero;
                              debugRect.sizeDelta = Vector2.zero;

                              // Включаем отладочную визуализацию и передаем созданный UI элемент
                              segmentation.EnableDebugVisualization(true);
                        }

                        // Назначаем ARCameraManager для получения изображения с камеры
                        segmentation.cameraManager = arCameraManager;
                  }
            }
            else
            {
                  Debug.LogWarning("Не удалось найти компонент WallSegmentation в созданной сцене!");
            }

            // Фокусируемся на корневом объекте в иерархии
            Selection.activeGameObject = sceneRoot;

            // Сохраняем сцену
            string scenePath = "Assets/Scenes/ARWallPaintingFixed.unity";
            EditorSceneManager.SaveScene(newScene, scenePath);

            Debug.Log($"AR сцена для покраски стен успешно создана и сохранена по пути: {scenePath}!");
            EditorUtility.DisplayDialog("Готово",
                $"AR сцена для покраски стен успешно создана!\n\n" +
                "Для использования модели сегментации стен:\n" +
                "1. Установите необходимые пакеты\n" +
                "2. Разместите модель ONNX в папке Resources/Models\n" +
                "3. Переключите режим сегментации на ExternalModel\n\n" +
                "Сцена сохранена как: ARWallPaintingFixed.unity",
                "OK");
      }
}
#endif