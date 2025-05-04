using UnityEngine;
using UnityEditor;
using UnityEngine.XR.ARFoundation;
using Unity.XR.CoreUtils;
using UnityEngine.UI;
using System.IO;

/// <summary>
/// Инструмент для создания и настройки AR сцены для покраски стен
/// </summary>
#if UNITY_EDITOR
public class ARSceneCreator : EditorWindow
{
    // Параметры для создания сцены
    private string sceneName = "ARWallPainting";
    private bool createARSession = true;
    private bool createUIControls = true;
    private bool createDebugVisuals = true;
    private WallSegmentation.SegmentationMode segmentationMode = WallSegmentation.SegmentationMode.Demo;
    private Object onnxModelAsset;
    
    // Опции для внешней модели
    private bool useExternalModel = false;
    private string externalModelPath = "model.onnx"; // Путь относительно StreamingAssets
    
    // Открываем окно инструмента
    [MenuItem("AR/Create AR Wall Painting Scene", false, 10)]
    public static void ShowWindow()
    {
        GetWindow<ARSceneCreator>("AR Wall Painting Setup");
    }
    
    [MenuItem("AR/Generate New ARWallPainting Scene", false, 11)]
    public static void GenerateARWallPaintingScene()
    {
        // Создаем новую сцену через ARWallPaintingSceneCreator
        ARWallPaintingSceneCreator.CreateARWallPaintingScene();
    }
    
    // Отрисовка UI инструмента
    private void OnGUI()
    {
        GUILayout.Label("AR Wall Painting Scene Creator", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Этот инструмент создаст сцену для AR приложения покраски стен.", MessageType.Info);
        
        GUILayout.Space(10);
        
        // Базовые настройки
        sceneName = EditorGUILayout.TextField("Название сцены:", sceneName);
        createARSession = EditorGUILayout.Toggle("Создать AR Session", createARSession);
        createUIControls = EditorGUILayout.Toggle("Создать UI элементы", createUIControls);
        createDebugVisuals = EditorGUILayout.Toggle("Создать отладочную визуализацию", createDebugVisuals);
        
        GUILayout.Space(10);
        GUILayout.Label("Настройки сегментации стен", EditorStyles.boldLabel);
        
        // Выбор режима сегментации
        segmentationMode = (WallSegmentation.SegmentationMode)EditorGUILayout.EnumPopup("Режим сегментации:", segmentationMode);
        
        // Показываем дополнительные опции в зависимости от выбранного режима
        if (segmentationMode == WallSegmentation.SegmentationMode.EmbeddedModel)
        {
            onnxModelAsset = EditorGUILayout.ObjectField("ONNX модель:", onnxModelAsset, typeof(Unity.Barracuda.NNModel), false);
        }
        else if (segmentationMode == WallSegmentation.SegmentationMode.ExternalModel)
        {
            EditorGUILayout.HelpBox("Внешняя модель должна находиться в папке StreamingAssets.", MessageType.Info);
            externalModelPath = EditorGUILayout.TextField("Путь к модели:", externalModelPath);
            
            EditorGUILayout.BeginHorizontal();
            
            // Проверяем наличие StreamingAssets и создаем папку при необходимости
            if (GUILayout.Button("Создать папку StreamingAssets"))
            {
                if (!Directory.Exists(Application.streamingAssetsPath))
                {
                    Directory.CreateDirectory(Application.streamingAssetsPath);
                    AssetDatabase.Refresh();
                    EditorUtility.DisplayDialog("Информация", "Папка StreamingAssets создана", "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog("Информация", "Папка StreamingAssets уже существует", "OK");
                }
            }
            
            // Добавляем кнопку для выбора файла ONNX модели
            if (GUILayout.Button("Выбрать ONNX файл"))
            {
                string filepath = EditorUtility.OpenFilePanel("Выберите ONNX модель", "", "onnx");
                if (!string.IsNullOrEmpty(filepath))
                {
                    // Получаем только имя файла (без пути)
                    string filename = Path.GetFileName(filepath);
                    externalModelPath = filename;
                    
                    // Проверяем, нужно ли копировать файл в StreamingAssets
                    bool shouldCopy = EditorUtility.DisplayDialog(
                        "Копировать файл?", 
                        $"Скопировать {filename} в папку StreamingAssets?", 
                        "Да", "Нет");
                    
                    if (shouldCopy)
                    {
                        CopyModelToStreamingAssets(filepath);
                    }
                }
            }
            
            EditorGUILayout.EndHorizontal();
            
            // Проверяем, существует ли файл модели в StreamingAssets
            string fullModelPath = Path.Combine(Application.streamingAssetsPath, externalModelPath);
            bool modelExists = File.Exists(fullModelPath);
            
            EditorGUILayout.BeginHorizontal();
            
            // Показываем статус модели
            if (modelExists)
            {
                EditorGUILayout.HelpBox($"Модель найдена: {fullModelPath}", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox($"Модель не найдена в StreamingAssets!", MessageType.Warning);
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        GUILayout.Space(20);
        
        // Кнопка создания сцены
        if (GUILayout.Button("Создать AR сцену"))
        {
            CreateARScene();
        }
    }
    
    // Создание AR сцены
    private void CreateARScene()
    {
        // Проверяем наличие модели при использовании внешней модели
        if (segmentationMode == WallSegmentation.SegmentationMode.ExternalModel)
        {
            string fullModelPath = Path.Combine(Application.streamingAssetsPath, externalModelPath);
            if (!File.Exists(fullModelPath))
            {
                bool proceed = EditorUtility.DisplayDialog(
                    "Модель не найдена", 
                    $"Внешняя модель не найдена по пути {fullModelPath}. Хотите продолжить с демо-режимом?", 
                    "Продолжить с демо-режимом", "Отмена");
                
                if (!proceed)
                    return;
                
                // Переключаемся на демо-режим, если модель не найдена
                segmentationMode = WallSegmentation.SegmentationMode.Demo;
            }
        }
        
        // Создаем корневой объект для всей сцены
        GameObject root = new GameObject(sceneName);
        
        // Создаем AR Session, если требуется
        if (createARSession)
        {
            CreateARSessionObjects(root);
        }
        
        // Создаем AR компоненты
        GameObject arOrigin = CreateAROrigin(root);
        
        // Создаем менеджеры для работы с AR
        CreateManagers(root, arOrigin);
        
        // Создаем UI, если требуется
        if (createUIControls)
        {
            CreateUI(root);
        }
        
        // Сохраняем сцену
        EditorUtility.DisplayDialog("Готово", "AR сцена создана успешно!", "OK");
    }
    
    // Создание AR Session объектов
    private void CreateARSessionObjects(GameObject root)
    {
        // AR Session
        GameObject sessionObject = new GameObject("AR Session");
        sessionObject.transform.SetParent(root.transform);
        sessionObject.AddComponent<ARSession>();
        
        // Конфигурация сессии
        GameObject sessionOrigin = GameObject.Find("AR Session Origin");
        if (sessionOrigin == null)
        {
            ARSessionOrigin origin = sessionObject.AddComponent<ARSessionOrigin>();
            
            // Добавляем настройки отслеживания человека, если нужно
            // origin.trackablesParent = new GameObject("Trackables").transform;
        }
    }
    
    // Создание AR Origin и камеры
    private GameObject CreateAROrigin(GameObject root)
    {
        // AR Origin
        GameObject originObject = new GameObject("XR Origin");
        originObject.transform.SetParent(root.transform);
        XROrigin origin = originObject.AddComponent<XROrigin>();
        
        // AR Camera
        GameObject cameraObject = new GameObject("AR Camera");
        cameraObject.transform.SetParent(originObject.transform);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 20f;
        
        // AR Camera Manager
        ARCameraManager cameraManager = cameraObject.AddComponent<ARCameraManager>();
        ARCameraBackground cameraBackground = cameraObject.AddComponent<ARCameraBackground>();
        
        // Устанавливаем камеру в Origin
        origin.Camera = camera;
        
        // Добавляем дополнительные компоненты для камеры
        cameraObject.AddComponent<ARCameraBackground>();
        
        // Создаем смещение камеры от пола
        GameObject floorOffset = new GameObject("Camera Floor Offset");
        floorOffset.transform.SetParent(originObject.transform);
        
        // Создаем объект для отслеживаемых плоскостей
        GameObject trackables = new GameObject("Trackables");
        trackables.transform.SetParent(originObject.transform);
        
        return originObject;
    }
    
    // Создание менеджеров для работы с AR
    private void CreateManagers(GameObject root, GameObject arOrigin)
    {
        // Создаем AppController
        GameObject appController = new GameObject("AppController");
        appController.transform.SetParent(root.transform);
        
        // Добавляем WallSegmentationManager
        GameObject segmentationManager = new GameObject("WallSegmentationManager");
        segmentationManager.transform.SetParent(root.transform);
        WallSegmentation segmentation = segmentationManager.AddComponent<WallSegmentation>();
        
        // Настраиваем сегментацию в зависимости от выбранного режима
        segmentation.SwitchMode(segmentationMode);
        
        // Если выбрана встроенная модель, назначаем её
        if (segmentationMode == WallSegmentation.SegmentationMode.EmbeddedModel && onnxModelAsset != null)
        {
            SerializedObject serializedSegmentation = new SerializedObject(segmentation);
            SerializedProperty modelProperty = serializedSegmentation.FindProperty("embeddedModelAsset");
            modelProperty.objectReferenceValue = onnxModelAsset;
            serializedSegmentation.ApplyModifiedProperties();
        }
        // Если выбрана внешняя модель, устанавливаем путь
        else if (segmentationMode == WallSegmentation.SegmentationMode.ExternalModel)
        {
            SerializedObject serializedSegmentation = new SerializedObject(segmentation);
            SerializedProperty pathProperty = serializedSegmentation.FindProperty("externalModelPath");
            pathProperty.stringValue = externalModelPath;
            serializedSegmentation.ApplyModifiedProperties();
        }
        
        // Добавляем WallPainterManager
        GameObject painterManager = new GameObject("WallPainterManager");
        painterManager.transform.SetParent(root.transform);
        painterManager.AddComponent<MonoBehaviour>(); // Заглушка, позже заменим на WallPainter
        
        // Добавляем дополнительные менеджеры (плоскости, точки, изображения)
        // ARPlaneManager, ARPointCloudManager, и т.д.
        
        // Находим камеру и добавляем отладочную визуализацию при необходимости
        Camera arCamera = arOrigin.GetComponentInChildren<Camera>();
        if (arCamera != null && createDebugVisuals)
        {
            // Если требуется, создадим отладочный UI для отображения сегментации
            CreateDebugVisualisation(segmentation, arCamera);
        }
    }
    
    // Создание UI элементов
    private void CreateUI(GameObject root)
    {
        // Canvas для UI
        GameObject canvas = new GameObject("Canvas");
        canvas.transform.SetParent(root.transform);
        Canvas canvasComponent = canvas.AddComponent<Canvas>();
        canvasComponent.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.AddComponent<CanvasScaler>();
        canvas.AddComponent<GraphicRaycaster>();
        
        // Создаем панель управления
        GameObject controlPanel = new GameObject("ControlPanel");
        controlPanel.transform.SetParent(canvas.transform);
        RectTransform controlRect = controlPanel.AddComponent<RectTransform>();
        controlRect.anchorMin = new Vector2(0, 1);
        controlRect.anchorMax = new Vector2(1, 1);
        controlRect.pivot = new Vector2(0.5f, 1);
        controlRect.sizeDelta = new Vector2(0, 100);
        
        // Создаем кнопки для выбора цвета
        CreateColorButton(controlPanel, "RedButton", Color.red, new Vector2(0.1f, 0.5f));
        CreateColorButton(controlPanel, "GreenButton", Color.green, new Vector2(0.3f, 0.5f));
        CreateColorButton(controlPanel, "BlueButton", Color.blue, new Vector2(0.5f, 0.5f));
        CreateColorButton(controlPanel, "YellowButton", Color.yellow, new Vector2(0.7f, 0.5f));
        CreateColorButton(controlPanel, "WhiteButton", Color.white, new Vector2(0.9f, 0.5f));
        
        // Создаем элементы для переключения режимов сегментации
        CreateSegmentationModeSwitcher(canvas);
        
        // Панель для снимков
        GameObject snapshotPanel = new GameObject("SnapshotPanel");
        snapshotPanel.transform.SetParent(canvas.transform);
        RectTransform snapshotRect = snapshotPanel.AddComponent<RectTransform>();
        snapshotRect.anchorMin = new Vector2(1, 0);
        snapshotRect.anchorMax = new Vector2(1, 0);
        snapshotRect.pivot = new Vector2(1, 0);
        snapshotRect.sizeDelta = new Vector2(150, 150);
        snapshotRect.anchoredPosition = new Vector2(-20, 20);
        
        // Кнопка для создания снимков
        GameObject snapshotButton = new GameObject("СнимкиButton");
        snapshotButton.transform.SetParent(canvas.transform);
        RectTransform buttonRect = snapshotButton.AddComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1, 0);
        buttonRect.anchorMax = new Vector2(1, 0);
        buttonRect.pivot = new Vector2(1, 0);
        buttonRect.sizeDelta = new Vector2(150, 60);
        buttonRect.anchoredPosition = new Vector2(-20, 180);
        
        // Добавляем компоненты кнопки
        Button button = snapshotButton.AddComponent<Button>();
        Image buttonImage = snapshotButton.AddComponent<Image>();
        buttonImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        button.targetGraphic = buttonImage;
        
        // Текст кнопки
        GameObject buttonText = new GameObject("Text");
        buttonText.transform.SetParent(snapshotButton.transform);
        RectTransform textRect = buttonText.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        Text text = buttonText.AddComponent<Text>();
        text.text = "Снимки";
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = 24;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        
        // Добавляем SnapshotManager
        GameObject snapshotManager = new GameObject("SnapshotManager");
        snapshotManager.transform.SetParent(root.transform);
        snapshotManager.AddComponent<MonoBehaviour>(); // Заглушка, позже заменим на SnapshotManager
    }
    
    // Создание кнопки выбора цвета
    private void CreateColorButton(GameObject parent, string name, Color color, Vector2 anchorPosition)
    {
        GameObject button = new GameObject(name);
        button.transform.SetParent(parent.transform);
        
        RectTransform rect = button.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(anchorPosition.x - 0.05f, 0.1f);
        rect.anchorMax = new Vector2(anchorPosition.x + 0.05f, 0.9f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        
        Image image = button.AddComponent<Image>();
        image.color = color;
        
        Button buttonComponent = button.AddComponent<Button>();
        buttonComponent.targetGraphic = image;
        
        ColorBlock colors = buttonComponent.colors;
        colors.highlightedColor = new Color(color.r, color.g, color.b, 0.8f);
        colors.pressedColor = new Color(color.r, color.g, color.b, 0.5f);
        buttonComponent.colors = colors;
    }
    
    // Создание отладочной визуализации для сегментации
    private void CreateDebugVisualisation(WallSegmentation segmentation, Camera camera)
    {
        if (segmentation == null || camera == null)
            return;
            
        // Создаем UI для отображения отладочной информации
        GameObject debugCanvas = new GameObject("DebugCanvas");
        debugCanvas.transform.SetParent(camera.transform);
        
        Canvas canvasComponent = debugCanvas.AddComponent<Canvas>();
        canvasComponent.renderMode = RenderMode.ScreenSpaceOverlay;
        debugCanvas.AddComponent<CanvasScaler>();
        debugCanvas.AddComponent<GraphicRaycaster>();
        
        // Создаем изображение для визуализации сегментации
        GameObject debugImage = new GameObject("SegmentationDebugImage");
        debugImage.transform.SetParent(debugCanvas.transform);
        
        RectTransform imageRect = debugImage.AddComponent<RectTransform>();
        imageRect.anchorMin = new Vector2(0, 0);
        imageRect.anchorMax = new Vector2(0.3f, 0.3f);
        imageRect.offsetMin = new Vector2(10, 10);
        imageRect.offsetMax = new Vector2(-10, -10);
        
        RawImage rawImage = debugImage.AddComponent<RawImage>();
        rawImage.color = new Color(1, 1, 1, 0.7f);
        
        // Связываем отладочное изображение с компонентом сегментации
        SerializedObject serializedSegmentation = new SerializedObject(segmentation);
        SerializedProperty debugImageProperty = serializedSegmentation.FindProperty("debugImage");
        debugImageProperty.objectReferenceValue = rawImage;
        
        SerializedProperty showDebugProperty = serializedSegmentation.FindProperty("showDebugVisualisation");
        showDebugProperty.boolValue = true;
        
        serializedSegmentation.ApplyModifiedProperties();
    }

    // Копирование модели в StreamingAssets
    private void CopyModelToStreamingAssets(string sourcePath)
    {
        try
        {
            // Создаем директорию StreamingAssets, если она не существует
            if (!Directory.Exists(Application.streamingAssetsPath))
            {
                Directory.CreateDirectory(Application.streamingAssetsPath);
            }
            
            // Имя файла (без пути)
            string filename = Path.GetFileName(sourcePath);
            
            // Полный путь назначения
            string destinationPath = Path.Combine(Application.streamingAssetsPath, filename);
            
            // Проверяем, существует ли файл с таким именем
            if (File.Exists(destinationPath))
            {
                bool overwrite = EditorUtility.DisplayDialog(
                    "Файл уже существует", 
                    $"Файл {filename} уже существует в StreamingAssets. Перезаписать?", 
                    "Да", "Нет");
                
                if (!overwrite)
                    return;
            }
            
            // Копируем файл
            File.Copy(sourcePath, destinationPath, true);
            
            // Обновляем ассеты
            AssetDatabase.Refresh();
            
            EditorUtility.DisplayDialog(
                "Копирование завершено", 
                $"Модель {filename} успешно скопирована в StreamingAssets", 
                "OK");
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog(
                "Ошибка копирования", 
                $"Не удалось скопировать модель: {e.Message}", 
                "OK");
        }
    }

    // Создание UI-элементов для переключения режимов сегментации
    private void CreateSegmentationModeSwitcher(GameObject parent)
    {
        // Панель для элементов управления сегментацией
        GameObject segmentationPanel = new GameObject("SegmentationPanel");
        segmentationPanel.transform.SetParent(parent.transform);
        RectTransform segmentationRect = segmentationPanel.AddComponent<RectTransform>();
        segmentationRect.anchorMin = new Vector2(0, 0);
        segmentationRect.anchorMax = new Vector2(0, 0);
        segmentationRect.pivot = new Vector2(0, 0);
        segmentationRect.sizeDelta = new Vector2(300, 160);
        segmentationRect.anchoredPosition = new Vector2(20, 20);
        
        // Фон панели
        Image panelImage = segmentationPanel.AddComponent<Image>();
        panelImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        
        // Заголовок панели
        GameObject titleObject = new GameObject("TitleText");
        titleObject.transform.SetParent(segmentationPanel.transform);
        RectTransform titleRect = titleObject.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.sizeDelta = new Vector2(0, 30);
        titleRect.anchoredPosition = new Vector2(0, 0);
        
        Text titleText = titleObject.AddComponent<Text>();
        titleText.text = "Режим сегментации";
        titleText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        titleText.fontSize = 18;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = Color.white;
        
        // Статус текущего режима
        GameObject statusObject = new GameObject("StatusText");
        statusObject.transform.SetParent(segmentationPanel.transform);
        RectTransform statusRect = statusObject.AddComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0, 1);
        statusRect.anchorMax = new Vector2(1, 1);
        statusRect.pivot = new Vector2(0.5f, 1);
        statusRect.sizeDelta = new Vector2(0, 25);
        statusRect.anchoredPosition = new Vector2(0, -30);
        
        Text statusText = statusObject.AddComponent<Text>();
        statusText.text = "Режим: Демо";
        statusText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        statusText.fontSize = 14;
        statusText.alignment = TextAnchor.MiddleCenter;
        statusText.color = Color.white;
        
        // Кнопки переключения режимов
        // 1. Демо-режим
        GameObject demoButton = CreateModeButton(segmentationPanel, "DemoModeButton", "Демо режим", 
                                              new Vector2(0.5f, 0.5f), new Vector2(0, -65));
        
        // 2. Встроенная модель
        GameObject embeddedButton = CreateModeButton(segmentationPanel, "EmbeddedModelButton", "Встроенная модель", 
                                                 new Vector2(0.5f, 0.5f), new Vector2(0, -100));
        
        // 3. Внешняя модель
        GameObject externalButton = CreateModeButton(segmentationPanel, "ExternalModelButton", "Внешняя модель", 
                                                 new Vector2(0.5f, 0.5f), new Vector2(0, -135));
        
        // Добавляем компонент для переключения режимов
        SwitchSegmentationMode switcher = segmentationPanel.AddComponent<SwitchSegmentationMode>();
        
        // Устанавливаем ссылки на UI-элементы
        SerializedObject serializedSwitcher = new SerializedObject(switcher);
        serializedSwitcher.FindProperty("demoModeButton").objectReferenceValue = demoButton.GetComponent<Button>();
        serializedSwitcher.FindProperty("embeddedModelButton").objectReferenceValue = embeddedButton.GetComponent<Button>();
        serializedSwitcher.FindProperty("externalModelButton").objectReferenceValue = externalButton.GetComponent<Button>();
        serializedSwitcher.FindProperty("statusText").objectReferenceValue = statusText;
        serializedSwitcher.ApplyModifiedProperties();
    }
    
    // Создание кнопки переключения режима
    private GameObject CreateModeButton(GameObject parent, string name, string buttonText, 
                                       Vector2 anchorPoint, Vector2 position)
    {
        GameObject button = new GameObject(name);
        button.transform.SetParent(parent.transform);
        
        RectTransform rect = button.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.1f, 0.5f);
        rect.anchorMax = new Vector2(0.9f, 0.5f);
        rect.pivot = anchorPoint;
        rect.sizeDelta = new Vector2(0, 30);
        rect.anchoredPosition = position;
        
        Image image = button.AddComponent<Image>();
        image.color = new Color(0.4f, 0.4f, 0.4f, 1f);
        
        Button buttonComponent = button.AddComponent<Button>();
        buttonComponent.targetGraphic = image;
        
        ColorBlock colors = buttonComponent.colors;
        colors.highlightedColor = new Color(0.6f, 0.6f, 0.6f, 1f);
        colors.pressedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        buttonComponent.colors = colors;
        
        // Текст кнопки
        GameObject textObject = new GameObject("Text");
        textObject.transform.SetParent(button.transform);
        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        Text text = textObject.AddComponent<Text>();
        text.text = buttonText;
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = 14;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        
        return button;
    }
}
#endif 