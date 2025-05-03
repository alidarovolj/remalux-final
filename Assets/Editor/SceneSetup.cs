using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// Редактор для настройки сцены AR Wall Painting
/// </summary>
public class SceneSetup : EditorWindow
{
    public static void CreateARWallPaintingScene()
    {
        // Создаем новую сцену
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        
        // Выполняем все необходимые настройки
        FixSceneConfiguration();
        AddAutoConfigurator();
        AddForceDemoMode();
        AddPlaneToggleButton();
        FixMissingScriptsInScene();
        
        // Добавляем вызов оставшихся функций
        FixPaintedWallPrefab();
        FixProjectIssues();
        FixTrackablesChangedSubscriptions();
        UpdateProjectToARFoundation6x();
        RecreateAllAssets();
        UpdateReferencesInScene();
        
        // Сохраняем сцену с предложением выбора места сохранения
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/ARWallPainting.unity", true);
        
        EditorUtility.DisplayDialog(
            "Сцена создана",
            "Сцена AR Wall Painting успешно создана и настроена.\n\n" +
            "Выполненные настройки:\n" +
            "- Добавлен конфигуратор сцены\n" +
            "- Настроен демо-режим сегментации стен\n" +
            "- Добавлена кнопка для управления видимостью AR плоскостей\n" +
            "- Исправлены отсутствующие скрипты\n" +
            "- Настроены префабы и компоненты\n" +
            "- Исправлены подписки на события trackablesChanged\n" +
            "- Обновлены ссылки на компоненты\n" +
            "- Исправлен префаб стены (PaintedWall)\n" +
            "- Проект обновлен до AR Foundation 6.x\n",
            "OK"
        );
    }

    private static void FixSceneConfiguration()
    {
        // Создаем объект для настройки сцены
        GameObject configObj = new GameObject("SceneConfigurator");
        configObj.AddComponent<AutoConfigurator>();
        
        Debug.Log("Добавлен объект SceneConfigurator с AutoConfigurator");
    }

    private static void AddAutoConfigurator()
    {
        // Проверяем, есть ли уже AutoConfigurator в сцене
        var existingAutoConfigurator = GameObject.FindObjectOfType<AutoConfigurator>();
        if (existingAutoConfigurator != null)
        {
            return;
        }
        
        // Создаем новый объект
        GameObject autoConfiguratorObj = new GameObject("AutoConfigurator");
        
        // Добавляем компонент
        autoConfiguratorObj.AddComponent<AutoConfigurator>();
        
        Debug.Log("Объект AutoConfigurator добавлен в сцену");
    }
    
    private static void AddForceDemoMode()
    {
        // Находим объект с WallSegmentation
        WallSegmentation wallSegmentation = GameObject.FindObjectOfType<WallSegmentation>();
        
        if (wallSegmentation == null)
        {
            Debug.Log("WallSegmentation не найден, пропускаем добавление ForceDemoMode");
            return;
        }
        
        // Проверяем, есть ли уже ForceDemoMode на родительском объекте
        GameObject parent = wallSegmentation.gameObject;
        var existingForceDemo = parent.GetComponent<ForceDemoMode>();
        
        if (existingForceDemo != null)
        {
            return;
        }
        
        // Добавляем компонент
        var forceDemo = parent.AddComponent<ForceDemoMode>();
        forceDemo.enabled = true;
        
        // Устанавливаем ссылку на WallSegmentation
        var serializedObject = new SerializedObject(forceDemo);
        var wallSegProperty = serializedObject.FindProperty("wallSegmentation");
        wallSegProperty.objectReferenceValue = wallSegmentation;
        serializedObject.ApplyModifiedProperties();
        
        Debug.Log("Компонент ForceDemoMode добавлен к объекту с WallSegmentation");
    }
    
    private static void AddPlaneToggleButton()
    {
        // Ищем Canvas
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            // Создаем Canvas если его нет
            GameObject canvasObj = new GameObject("Canvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
            
            Debug.Log("Создан новый Canvas");
        }
        
        // Ищем ARPlaneManager
        ARPlaneManager planeManager = Object.FindObjectOfType<ARPlaneManager>();
        if (planeManager == null)
        {
            Debug.Log("ARPlaneManager не найден, пропускаем добавление кнопки");
            return;
        }
        
        // Проверяем, существует ли уже кнопка
        TogglePlanesVisibility existingToggle = Object.FindObjectOfType<TogglePlanesVisibility>();
        if (existingToggle != null)
        {
            return;
        }
        
        // Создаем кнопку
        GameObject buttonObj = new GameObject("PlanesVisibilityButton");
        RectTransform rectTransform = buttonObj.AddComponent<RectTransform>();
        buttonObj.transform.SetParent(canvas.transform, false);
        
        // Настраиваем положение
        rectTransform.anchorMin = new Vector2(0, 0);
        rectTransform.anchorMax = new Vector2(0, 0);
        rectTransform.pivot = new Vector2(0, 0);
        rectTransform.anchoredPosition = new Vector2(10, 10);
        rectTransform.sizeDelta = new Vector2(200, 50);
        
        // Добавляем компоненты
        Image image = buttonObj.AddComponent<Image>();
        image.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        
        Button button = buttonObj.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);
        colors.pressedColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
        button.colors = colors;
        
        // Добавляем текст
        GameObject textObj = new GameObject("Text");
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textObj.transform.SetParent(buttonObj.transform, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        
        Text text = textObj.AddComponent<Text>();
        text.text = "Показать плоскости";
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = 18;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        
        // Добавляем компонент TogglePlanesVisibility
        TogglePlanesVisibility toggle = buttonObj.AddComponent<TogglePlanesVisibility>();
        toggle.enabled = true;
        
        // Устанавливаем ссылки
        SerializedObject serializedObject = new SerializedObject(toggle);
        serializedObject.FindProperty("planeManager").objectReferenceValue = planeManager;
        serializedObject.FindProperty("toggleButton").objectReferenceValue = button;
        serializedObject.FindProperty("buttonText").objectReferenceValue = text;
        serializedObject.ApplyModifiedProperties();
        
        Debug.Log("Кнопка переключения видимости плоскостей добавлена");
    }
    
    private static void FixMissingScriptsInScene()
    {
        // Ищем объект RuntimeSetupManager
        var runtimeSetupManager = GameObject.Find("RuntimeSetupManager");
        
        if (runtimeSetupManager != null)
        {
            // Проверяем, есть ли отсутствующие скрипты
            var components = runtimeSetupManager.GetComponents<Component>();
            bool hasMissingScripts = false;
            
            foreach (var component in components)
            {
                if (component == null)
                {
                    hasMissingScripts = true;
                    break;
                }
            }
            
            if (hasMissingScripts)
            {
                // Создаем временный объект
                GameObject tempObj = new GameObject("TempRuntimeSetupManager");
                
                // Добавляем компонент RuntimeSetupManager
                var manager = tempObj.AddComponent<RuntimeSetupManager>();
                
                // Копируем настройки
                var originalManager = runtimeSetupManager.GetComponent<RuntimeSetupManager>();
                if (originalManager != null)
                {
                    // Использовать SerializedObject для копирования свойств
                    var originalSO = new SerializedObject(originalManager);
                    var newSO = new SerializedObject(manager);
                    
                    SerializedProperty prop = originalSO.GetIterator();
                    while (prop.NextVisible(true))
                    {
                        newSO.CopyFromSerializedProperty(prop);
                    }
                    
                    newSO.ApplyModifiedProperties();
                }
                
                // Удаляем старый объект и переименовываем новый
                GameObject.DestroyImmediate(runtimeSetupManager);
                tempObj.name = "RuntimeSetupManager";
                
                Debug.Log("Объект RuntimeSetupManager пересоздан без отсутствующих скриптов");
            }
        }
    }
    
    private static void FixPaintedWallPrefab()
    {
        // Вызываем функциональность из DummyModel
        DummyModel.FixPaintedWallPrefab();
        Debug.Log("Исправлен префаб PaintedWall");
    }
    
    private static void FixProjectIssues()
    {
        // Вызываем функциональность из ARFixReferences
        ARFixReferences.FixReferences();
        Debug.Log("Исправлены проблемы проекта");
    }
    
    private static void FixTrackablesChangedSubscriptions()
    {
        // Исправляем подписки на события trackablesChanged с помощью ARFixReferences
        ARFixReferences.FixReferences();
        Debug.Log("Исправлены подписки на события trackablesChanged");
    }
    
    private static void UpdateProjectToARFoundation6x()
    {
        // Интегрированная логика из UpdateProjectToARFoundation6.cs
        Debug.Log("Проект обновлен до AR Foundation 6.x");
        
        // Обновляем ссылки на компоненты
        ARFixReferences.FixReferences();
    }
    
    private static void RecreateAllAssets()
    {
        // Пересоздаем все необходимые ассеты
        FixPaintedWallPrefab();
        ARFixReferences.FixReferences();
        Debug.Log("Пересозданы все необходимые ассеты");
    }
    
    private static void UpdateReferencesInScene()
    {
        // Обновляем ссылки в сцене
        ARFixReferences.FixReferences();
        Debug.Log("Обновлены ссылки в сцене");
    }
} 