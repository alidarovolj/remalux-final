using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

#if UNITY_EDITOR
/// <summary>
/// Инструменты для создания и настройки AR сцены для покраски стен
/// </summary>
public class ARSceneTools : Editor
{
      /// <summary>
      /// Создает новую AR сцену для покраски стен
      /// </summary>
      [MenuItem("Tools/AR Wall Painting/Create New Scene", false, 10)]
      public static void CreateARWallPaintingScene()
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

            // Создаем AR сцену
            GameObject sceneRoot = new GameObject("AR Wall Painting Scene");

            // Добавляем компонент creator
            ARWallPaintingCreator creator = sceneRoot.AddComponent<ARWallPaintingCreator>();

            // Выполняем настройку сцены
            Debug.Log("Создание AR сцены для покраски стен...");
            ARWallPaintingCreator.CreateScene();

            // Фокусируемся на корневом объекте в иерархии
            Selection.activeGameObject = sceneRoot;

            Debug.Log("AR сцена для покраски стен успешно создана!");
            EditorUtility.DisplayDialog("Готово", "AR сцена для покраски стен успешно создана!", "OK");
      }

      /// <summary>
      /// Открывает окно с инструкциями по установке необходимых пакетов
      /// </summary>
      [MenuItem("Tools/AR Wall Painting/Install Dependencies", false, 11)]
      public static void ShowDependenciesWindow()
      {
            ARDependenciesWindow.ShowWindow();
      }

      /// <summary>
      /// Тестовый запуск демонстрационного режима
      /// </summary>
      [MenuItem("Tools/AR Wall Painting/Run Demo Mode", false, 12)]
      public static void RunDemoMode()
      {
            // Проверяем наличие активной сцены
            if (EditorSceneManager.GetActiveScene().rootCount == 0)
            {
                  EditorUtility.DisplayDialog("Ошибка", "Сначала нужно создать AR сцену!", "OK");
                  return;
            }

            // Ищем WallSegmentation компонент
            WallSegmentation segmentation = FindObjectOfType<WallSegmentation>();
            if (segmentation == null)
            {
                  EditorUtility.DisplayDialog("Ошибка", "Не найден компонент WallSegmentation в сцене!", "OK");
                  return;
            }

            // Переключаем на демо-режим
            segmentation.SwitchMode(WallSegmentation.SegmentationMode.Demo);
            EditorUtility.DisplayDialog("Готово", "Переключено на демо-режим сегментации", "OK");
      }
}

/// <summary>
/// Окно с информацией о зависимостях
/// </summary>
public class ARDependenciesWindow : EditorWindow
{
      private Vector2 scrollPosition;
      private string dependenciesText = "";

      [MenuItem("Tools/AR Wall Painting/Dependencies Info", false, 20)]
      public static void ShowWindow()
      {
            ARDependenciesWindow window = GetWindow<ARDependenciesWindow>("AR Dependencies");
            window.LoadDependenciesText();
      }

      private void LoadDependenciesText()
      {
            // Путь к файлу с информацией о зависимостях
            string filePath = "Assets/Scripts/DuluxVisualizer/PackageDependencies.txt";

            // Проверяем существование файла
            if (System.IO.File.Exists(filePath))
            {
                  dependenciesText = System.IO.File.ReadAllText(filePath);
            }
            else
            {
                  dependenciesText = "Для работы AR Wall Painting требуются следующие пакеты:\n\n" +
                      "1. AR Foundation (com.unity.xr.arfoundation) - версия 4.1.7 или новее\n" +
                      "2. ARKit XR Plugin (com.unity.xr.arkit) - версия 4.1.7 или новее (для iOS)\n" +
                      "3. ARCore XR Plugin (com.unity.xr.arcore) - версия 4.1.7 или новее (для Android)\n" +
                      "4. Barracuda (com.unity.barracuda) - версия 2.0.0 или новее\n" +
                      "5. Unity XR Core Utils (com.unity.xr.core-utils) - версия 2.0.0 или новее\n\n" +
                      "Установите необходимые пакеты через Package Manager.";
            }
      }

      private void OnGUI()
      {
            GUILayout.Label("Необходимые зависимости для AR Wall Painting", EditorStyles.boldLabel);

            // Область с прокруткой для текста
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            GUILayout.TextArea(dependenciesText, GUILayout.ExpandHeight(true));
            GUILayout.EndScrollView();

            GUILayout.Space(10);

            // Кнопка для открытия Package Manager
            if (GUILayout.Button("Открыть Package Manager"))
            {
                  EditorApplication.ExecuteMenuItem("Window/Package Manager");
            }
      }
}
#endif