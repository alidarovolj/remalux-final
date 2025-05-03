using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using Unity.XR.CoreUtils;
using UnityEngine.InputSystem;
using System.IO;
using UnityEngine.XR.ARSubsystems;
using System;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Комбинированная утилита для создания полной AR сцены для покраски стен
/// </summary>
public static class ARSceneCreator
{
    // Метод для создания AR сцены для покраски стен
    public static void CreateARWallPaintingScene()
    {
        // Создаем новую сцену
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        
        EditorUtility.DisplayProgressBar("Creating AR Scene", "Setting up AR Foundation components...", 0.1f);
        
        // 1. Создаем базовые AR объекты
        var (arSession, xrOrigin, arCamera) = SetupARComponents();
        
        EditorUtility.DisplayProgressBar("Creating AR Scene", "Setting up application controllers...", 0.3f);
        
        // 2. Создаем основные контроллеры приложения
        var (appController, wallSegManager, wallPainterManager) = SetupApplicationControllers();
        
        EditorUtility.DisplayProgressBar("Creating AR Scene", "Creating UI elements...", 0.5f);
        
        // 3. Создаем UI
        var (canvasObj, controlPanel, colorButtons) = SetupUI();
        
        EditorUtility.DisplayProgressBar("Creating AR Scene", "Creating materials and prefabs...", 0.7f);
        
        // 4. Создаем необходимые ассеты
        var (wallPaintMaterial, paintedWallPrefab) = CreateRequiredAssets();
        
        EditorUtility.DisplayProgressBar("Creating AR Scene", "Connecting components...", 0.9f);
        
        // 5. Соединяем компоненты и устанавливаем ссылки
        ConnectComponents(
            appController, 
            wallSegManager, 
            wallPainterManager, 
            arCamera, 
            colorButtons, 
            controlPanel, 
            paintedWallPrefab, 
            wallPaintMaterial
        );
        
        // 6. Добавляем UI для снимков и SnapshotManager
        EditorUtility.DisplayProgressBar("Creating AR Scene", "Adding snapshots functionality...", 0.95f);
        
        // Получаем компонент WallPainter
        WallPainter wallPainter = wallPainterManager.GetComponent<WallPainter>();
        
        // Создаем префаб кнопки снимка
        GameObject snapshotButtonPrefab = CreateSnapshotButtonPrefab();
        
        // Обновляем ссылки для работы со снимками
        ConnectComponents(
            appController, 
            wallSegManager, 
            wallPainterManager, 
            arCamera, 
            colorButtons, 
            controlPanel, 
            paintedWallPrefab, 
            wallPaintMaterial,
            snapshotButtonPrefab
        );
        
        // Создаем UI для снимков
        CreateSnapshotUI(canvasObj, wallPainter);
        
        // Добавляем SnapshotManager
        GameObject snapshotManagerObj = new GameObject("SnapshotManager");
        SnapshotManager snapshotManager = snapshotManagerObj.AddComponent<SnapshotManager>();
        snapshotManager.wallPainter = wallPainter;
        
        EditorUtility.ClearProgressBar();
        
        // 7. Сохраняем сцену
        string scenePath = "Assets/Scenes/ARWallPainting.unity";
        
        // Создаем директорию Scenes, если она не существует
        if (!Directory.Exists("Assets/Scenes"))
            Directory.CreateDirectory("Assets/Scenes");
            
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), scenePath);
        
        // 8. Обновляем список сцен для сборки
        AddSceneToBuildSettings(scenePath);
        
        Debug.Log("AR Wall Painting scene created at: " + scenePath);
        
        EditorUtility.DisplayDialog(
            "AR Scene Created Successfully",
            "AR Wall Painting scene has been created with all necessary components and connections.\n\n" +
            "The scene is ready to use and has been added to the Build Settings.",
            "OK"
        );
    }
    
    private static (GameObject arSession, GameObject xrOrigin, GameObject arCamera) SetupARComponents()
    {
        // Создаем AR Session
        GameObject arSessionObj = new GameObject("AR Session");
        ARSession arSession = arSessionObj.AddComponent<ARSession>();
        arSessionObj.AddComponent<ARInputManager>();
        
        // Создаем XR Origin
        GameObject xrOriginObj = new GameObject("XR Origin");
        XROrigin xrOrigin = xrOriginObj.AddComponent<XROrigin>();
        
        // Добавляем AR компоненты на XR Origin
        ARPlaneManager planeManager = xrOriginObj.AddComponent<ARPlaneManager>();
        ARRaycastManager raycastManager = xrOriginObj.AddComponent<ARRaycastManager>();
        
        // Настраиваем Plane Manager с учетом версии AR Foundation
        try 
        {
            // Для AR Foundation 4.0+ используем requestedDetectionMode
            if (ARVersionHelper.SupportsRequestedDetectionMode())
            {
                planeManager.requestedDetectionMode = PlaneDetectionMode.Vertical;
            }
            // Для более старых версий используем reflection для установки detectionMode
            else
            {
                var property = planeManager.GetType().GetProperty("detectionMode");
                if (property != null)
                {
                    property.SetValue(planeManager, 2); // 2 = Vertical в старых версиях enum
                    Debug.Log("Set detectionMode to Vertical using reflection");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to set detection mode: {ex.Message}. Plane detection may not work properly.");
        }
        
        // Создаем AR камеру
        GameObject cameraObj = new GameObject("AR Camera");
        Camera cam = cameraObj.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 20f;
        
        // Позиционируем камеру
        cameraObj.transform.SetParent(xrOriginObj.transform);
        cameraObj.transform.localPosition = Vector3.zero;
        cameraObj.transform.localRotation = Quaternion.identity;
        
        // Добавляем компоненты камеры
        ARCameraManager cameraManager = cameraObj.AddComponent<ARCameraManager>();
        ARCameraBackground cameraBackground = cameraObj.AddComponent<ARCameraBackground>();
        
        // Добавляем Tracked Pose Driver (Input System) для правильного отслеживания позиции
        var trackedPoseDriver = cameraObj.AddComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
        trackedPoseDriver.positionInput = new InputActionProperty(new InputAction("Position", binding: "<XRHMD>/centerEyePosition"));
        trackedPoseDriver.rotationInput = new InputActionProperty(new InputAction("Rotation", binding: "<XRHMD>/centerEyeRotation"));
        
        // Связываем камеру с XROrigin
        xrOrigin.Camera = cam;
        
        // Создаем объект для Floor Offset
        GameObject floorOffset = new GameObject("Camera Floor Offset");
        floorOffset.transform.SetParent(xrOriginObj.transform);
        floorOffset.transform.localPosition = new Vector3(0, 1.6f, 0); // Средний рост человека
        xrOrigin.CameraFloorOffsetObject = floorOffset;
        
        return (arSessionObj, xrOriginObj, cameraObj);
    }
    
    private static (GameObject appController, GameObject wallSegManager, GameObject wallPainter) SetupApplicationControllers()
    {
        // Создаем AppController
        GameObject appControllerObj = new GameObject("AppController");
        ARWallPaintingApp appController = appControllerObj.AddComponent<ARWallPaintingApp>();
        
        // Создаем WallSegmentationManager
        GameObject wallSegObj = new GameObject("WallSegmentationManager");
        
        // Спрашиваем пользователя, какой тип сегментации использовать
        bool useDemoSegmentation = EditorUtility.DisplayDialog(
            "Segmentation Type",
            "Which wall segmentation implementation would you like to use?",
            "Demo (No ML model)",
            "Real (Requires ONNX model)"
        );
        
        if (useDemoSegmentation)
        {
            wallSegObj.AddComponent<DemoWallSegmentation>();
        }
        else
        {
            var segmentation = wallSegObj.AddComponent<WallSegmentation>();
            
            // Проверяем наличие модели ML в проекте и настраиваем, если она есть
            var nnModel = AssetDatabase.FindAssets("t:NNModel");
            if (nnModel.Length > 0)
            {
                var modelPath = AssetDatabase.GUIDToAssetPath(nnModel[0]);
                // Безопасно загружаем модель без прямого использования Barracuda
                var model = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(modelPath);
                if (model != null)
                {
                    // Устанавливаем модель через SerializedObject
                    var serializedObject = new SerializedObject(segmentation);
                    var modelProperty = serializedObject.FindProperty("modelAsset");
                    if (modelProperty != null)
                    {
                        modelProperty.objectReferenceValue = model;
                        serializedObject.ApplyModifiedProperties();
                        Debug.Log($"ML model assigned: {modelPath}");
                    }
                }
            }
            else
            {
                Debug.LogWarning("No ML model found in project. Wall segmentation may not work properly.");
            }
        }
        
        // Создаем WallPainterManager
        GameObject wallPainterObj = new GameObject("WallPainterManager");
        wallPainterObj.AddComponent<WallPainter>();
        
        return (appControllerObj, wallSegObj, wallPainterObj);
    }
    
    private static (GameObject canvas, GameObject controlPanel, UnityEngine.UI.Button[] colorButtons) SetupUI()
    {
        // Создаем Canvas
        GameObject canvasObj = new GameObject("Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        
        // Добавляем UIManager
        UIManager uiManager = canvasObj.AddComponent<UIManager>();
        
        // Создаем панель для элементов управления
        GameObject panel = new GameObject("ControlPanel");
        panel.transform.SetParent(canvasObj.transform, false);
        
        // Добавляем Image компонент
        UnityEngine.UI.Image panelImage = panel.AddComponent<UnityEngine.UI.Image>();
        panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.7f);
        
        // Настраиваем RectTransform
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0, 0);
        panelRect.anchorMax = new Vector2(1, 0.15f);
        panelRect.offsetMin = new Vector2(10, 10);
        panelRect.offsetMax = new Vector2(-10, 0);
        
        // Добавляем ColorButtons и сохраняем ссылки
        UnityEngine.UI.Button[] colorButtons = new UnityEngine.UI.Button[5];
        
        colorButtons[0] = CreateColorButton(panel.transform, "RedButton", Color.red, new Vector2(0.1f, 0.5f));
        colorButtons[1] = CreateColorButton(panel.transform, "GreenButton", Color.green, new Vector2(0.3f, 0.5f));
        colorButtons[2] = CreateColorButton(panel.transform, "BlueButton", Color.blue, new Vector2(0.5f, 0.5f));
        colorButtons[3] = CreateColorButton(panel.transform, "YellowButton", Color.yellow, new Vector2(0.7f, 0.5f));
        colorButtons[4] = CreateColorButton(panel.transform, "WhiteButton", Color.white, new Vector2(0.9f, 0.5f));
        
        return (canvasObj, panel, colorButtons);
    }
    
    private static UnityEngine.UI.Button CreateColorButton(Transform parent, string name, Color color, Vector2 anchorPosition)
    {
        GameObject buttonObj = new GameObject(name);
        buttonObj.transform.SetParent(parent, false);
        
        // Добавляем Image компонент
        UnityEngine.UI.Image buttonImage = buttonObj.AddComponent<UnityEngine.UI.Image>();
        buttonImage.color = color;
        
        // Добавляем Button компонент
        UnityEngine.UI.Button button = buttonObj.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = buttonImage;
        
        // Настраиваем RectTransform
        RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
        buttonRect.anchorMin = anchorPosition - new Vector2(0.05f, 0.3f);
        buttonRect.anchorMax = anchorPosition + new Vector2(0.05f, 0.3f);
        buttonRect.anchoredPosition = Vector2.zero;
        buttonRect.sizeDelta = Vector2.zero;
        
        return button;
    }
    
    private static (Material wallPaintMaterial, GameObject paintedWallPrefab) CreateRequiredAssets()
    {
        // Проверяем и создаем папки, если их нет
        if (!Directory.Exists("Assets/Materials"))
            Directory.CreateDirectory("Assets/Materials");
            
        if (!Directory.Exists("Assets/Prefabs"))
            Directory.CreateDirectory("Assets/Prefabs");
        
        // Создаем или находим материал для покраски стен
        Material wallPaintMaterial;
        string materialPath = "Assets/Materials/WallPaintMaterial.mat";
        
        if (File.Exists(materialPath))
        {
            wallPaintMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        }
        else
        {
            // Создаем материал для покраски стен
            Shader wallPaintShader = Shader.Find("Custom/WallPaint");
            
            // Если шейдер не найден, используем стандартный
            if (wallPaintShader == null)
            {
                wallPaintShader = Shader.Find("Universal Render Pipeline/Lit");
                if (wallPaintShader == null)
                    wallPaintShader = Shader.Find("Standard");
                    
                Debug.LogWarning("Custom/WallPaint shader not found. Using default shader instead.");
            }
            
            // Создаем материал
            wallPaintMaterial = new Material(wallPaintShader);
            wallPaintMaterial.color = new Color(1f, 1f, 1f, 0.7f);
            
            // Сохраняем материал
            AssetDatabase.CreateAsset(wallPaintMaterial, materialPath);
        }
        
        // Создаем или находим префаб стены
        GameObject paintedWallPrefab;
        string prefabPath = "Assets/Prefabs/PaintedWall.prefab";
        
        if (File.Exists(prefabPath))
        {
            paintedWallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }
        else
        {
            // Создаем префаб стены
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "PaintedWall";
            
            // Настраиваем MeshRenderer
            MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = wallPaintMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            
            // Сохраняем как префаб
            PrefabUtility.SaveAsPrefabAsset(quad, prefabPath);
            paintedWallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            
            // Удаляем временный объект сцены
            UnityEngine.Object.DestroyImmediate(quad);
        }
        
        AssetDatabase.Refresh();
        
        return (wallPaintMaterial, paintedWallPrefab);
    }
    
    private static void ConnectComponents(
        GameObject appController, 
        GameObject wallSegManager, 
        GameObject wallPainterManager,
        GameObject arCamera,
        UnityEngine.UI.Button[] colorButtons,
        GameObject controlPanel,
        GameObject paintedWallPrefab,
        Material wallPaintMaterial,
        GameObject snapshotButtonPrefab = null)
    {
        // Получаем компоненты - используем новый API в Unity 2022+
        ARWallPaintingApp appControllerComponent = appController.GetComponent<ARWallPaintingApp>();
        
        // Используем FindFirstObjectByType для более новых версий Unity, или FindObjectOfType для старых
        UIManager uiManager;
        try
        {
            var findMethod = typeof(UnityEngine.Object).GetMethod("FindFirstObjectByType", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, 
                null, new Type[] { }, null);
            
            if (findMethod != null)
            {
                // Для Unity 2022.2+ используем FindFirstObjectByType
                var genericMethod = findMethod.MakeGenericMethod(typeof(UIManager));
                uiManager = (UIManager)genericMethod.Invoke(null, null);
            }
            else
            {
                // Для более старых версий используем FindObjectOfType
#pragma warning disable CS0618 // Подавляем предупреждение об устаревшем методе
                uiManager = UnityEngine.Object.FindObjectOfType<UIManager>();
#pragma warning restore CS0618
            }
        }
        catch
        {
            // В случае ошибки, используем FindObjectOfType напрямую
#pragma warning disable CS0618
            uiManager = UnityEngine.Object.FindObjectOfType<UIManager>();
#pragma warning restore CS0618
        }
        
        WallPainter wallPainter = wallPainterManager.GetComponent<WallPainter>();
        
        // 1. Настраиваем UIManager
        if (uiManager != null)
        {
            // Назначаем ссылки на кнопки через SerializedObject
            var serializedUIManager = new SerializedObject(uiManager);
            
            // Находим свойство colorPalette и устанавливаем его равным controlPanel
            var colorPaletteProperty = serializedUIManager.FindProperty("colorPalette");
            if (colorPaletteProperty != null) 
                colorPaletteProperty.objectReferenceValue = controlPanel;
                
            // Находим свойства для кнопок и устанавливаем их
            string[] buttonNames = { "redButton", "greenButton", "blueButton", "yellowButton", "whiteButton" };
            for (int i = 0; i < buttonNames.Length && i < colorButtons.Length; i++)
            {
                var buttonProperty = serializedUIManager.FindProperty(buttonNames[i]);
                if (buttonProperty != null)
                    buttonProperty.objectReferenceValue = colorButtons[i];
            }
            
            // Находим свойство wallPainter и устанавливаем его
            var wallPainterProperty = serializedUIManager.FindProperty("wallPainter");
            if (wallPainterProperty != null)
                wallPainterProperty.objectReferenceValue = wallPainter;
                
            // Устанавливаем ссылку на префаб кнопки снимка, если он предоставлен
            if (snapshotButtonPrefab != null)
            {
                var snapshotButtonPrefabProperty = serializedUIManager.FindProperty("snapshotButtonPrefab");
                if (snapshotButtonPrefabProperty != null)
                    snapshotButtonPrefabProperty.objectReferenceValue = snapshotButtonPrefab;
            }
                
            // Применяем изменения
            serializedUIManager.ApplyModifiedProperties();
        }
        
        // 2. Настраиваем WallPainter
        if (wallPainter != null)
        {
            // Назначаем ссылки через SerializedObject
            var serializedWallPainter = new SerializedObject(wallPainter);
            
            // Устанавливаем префаб стены
            var wallPrefabProperty = serializedWallPainter.FindProperty("wallPrefab");
            if (wallPrefabProperty != null)
                wallPrefabProperty.objectReferenceValue = paintedWallPrefab;
                
            // Устанавливаем материал для стены
            var wallMaterialProperty = serializedWallPainter.FindProperty("wallMaterial");
            if (wallMaterialProperty != null)
                wallMaterialProperty.objectReferenceValue = wallPaintMaterial;
                
            // Устанавливаем ссылку на WallSegmentation
            var wallSegmentationProperty = serializedWallPainter.FindProperty("wallSegmentation");
            var segmentation = wallSegManager.GetComponent<WallSegmentation>() ?? 
                               (Component)wallSegManager.GetComponent<DemoWallSegmentation>();
                               
            if (wallSegmentationProperty != null && segmentation != null)
                wallSegmentationProperty.objectReferenceValue = segmentation;
                
            // Применяем изменения
            serializedWallPainter.ApplyModifiedProperties();
        }
        
        // 3. Настраиваем AppController
        if (appControllerComponent != null)
        {
            // Назначаем ссылки через SerializedObject
            var serializedAppController = new SerializedObject(appControllerComponent);
            
            // Устанавливаем ссылку на WallSegmentation
            var wallSegmentationProperty = serializedAppController.FindProperty("wallSegmentation");
            var segmentation = wallSegManager.GetComponent<WallSegmentation>() ?? 
                               (Component)wallSegManager.GetComponent<DemoWallSegmentation>();
                               
            if (wallSegmentationProperty != null && segmentation != null)
                wallSegmentationProperty.objectReferenceValue = segmentation;
                
            // Устанавливаем ссылку на WallPainter
            var wallPainterProperty = serializedAppController.FindProperty("wallPainter");
            if (wallPainterProperty != null)
                wallPainterProperty.objectReferenceValue = wallPainter;
                
            // Устанавливаем ссылку на UIManager
            var uiManagerProperty = serializedAppController.FindProperty("uiManager");
            if (uiManagerProperty != null)
                uiManagerProperty.objectReferenceValue = uiManager;
                
            // Применяем изменения
            serializedAppController.ApplyModifiedProperties();
        }
        
        // 4. Настраиваем WallSegmentation
        Component segmentationComponent = wallSegManager.GetComponent<WallSegmentation>() ?? 
                                         (Component)wallSegManager.GetComponent<DemoWallSegmentation>();
        if (segmentationComponent != null)
        {
            // Назначаем ссылки через SerializedObject
            var serializedSegmentation = new SerializedObject(segmentationComponent);
            
            // Устанавливаем ссылку на AR Camera
            var arCameraProperty = serializedSegmentation.FindProperty("arCamera");
            if (arCameraProperty != null)
                arCameraProperty.objectReferenceValue = arCamera.GetComponent<Camera>();
                
            // Применяем изменения
            serializedSegmentation.ApplyModifiedProperties();
        }
    }
    
    private static void AddSceneToBuildSettings(string scenePath)
    {
        // Получаем текущие сцены из настроек сборки
        var scenes = EditorBuildSettings.scenes;
        
        // Проверяем, есть ли наша сцена уже в настройках
        bool sceneExists = false;
        foreach (var scene in scenes)
        {
            if (scene.path == scenePath)
            {
                sceneExists = true;
                break;
            }
        }
        
        // Если сцены нет в настройках, добавляем её
        if (!sceneExists)
        {
            // Создаем новый массив сцен с размером на 1 больше
            var newScenes = new EditorBuildSettingsScene[scenes.Length + 1];
            
            // Копируем существующие сцены
            for (int i = 0; i < scenes.Length; i++)
            {
                newScenes[i] = scenes[i];
            }
            
            // Добавляем новую сцену
            newScenes[scenes.Length] = new EditorBuildSettingsScene(scenePath, true);
            
            // Устанавливаем новый массив сцен в настройки сборки
            EditorBuildSettings.scenes = newScenes;
        }
    }

    // Создание UI для снимков
    public static void CreateSnapshotUI(GameObject mainCanvas, WallPainter wallPainter)
    {
        // Создаем панель снимков
        GameObject snapshotPanel = new GameObject("SnapshotPanel");
        snapshotPanel.transform.SetParent(mainCanvas.transform, false);
        
        RectTransform snapshotPanelRect = snapshotPanel.AddComponent<RectTransform>();
        snapshotPanelRect.anchorMin = new Vector2(0, 0);
        snapshotPanelRect.anchorMax = new Vector2(1, 0.3f);
        snapshotPanelRect.offsetMin = new Vector2(10, 10);
        snapshotPanelRect.offsetMax = new Vector2(-10, 10);
        
        Image snapshotPanelImage = snapshotPanel.AddComponent<Image>();
        snapshotPanelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
        
        // Создаем контейнер для кнопок снимков
        GameObject snapshotContainer = new GameObject("SnapshotContainer");
        snapshotContainer.transform.SetParent(snapshotPanel.transform, false);
        
        RectTransform containerRect = snapshotContainer.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0, 0);
        containerRect.anchorMax = new Vector2(1, 0.8f);
        containerRect.offsetMin = new Vector2(10, 10);
        containerRect.offsetMax = new Vector2(-10, 0);
        
        // Добавляем горизонтальный layout для размещения кнопок
        HorizontalLayoutGroup layout = snapshotContainer.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 10;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        
        // Создаем панель управления снимками
        GameObject controlsPanel = new GameObject("ControlsPanel");
        controlsPanel.transform.SetParent(snapshotPanel.transform, false);
        
        RectTransform controlsRect = controlsPanel.AddComponent<RectTransform>();
        controlsRect.anchorMin = new Vector2(0, 0.8f);
        controlsRect.anchorMax = new Vector2(1, 1);
        controlsRect.offsetMin = new Vector2(10, 0);
        controlsRect.offsetMax = new Vector2(-10, -10);
        
        // Горизонтальный layout для элементов управления
        HorizontalLayoutGroup controlsLayout = controlsPanel.AddComponent<HorizontalLayoutGroup>();
        controlsLayout.spacing = 10;
        controlsLayout.childAlignment = TextAnchor.MiddleLeft;
        controlsLayout.childForceExpandWidth = false;
        controlsLayout.childForceExpandHeight = true;
        
        // Поле ввода имени снимка
        GameObject inputFieldObj = new GameObject("SnapshotNameInput");
        inputFieldObj.transform.SetParent(controlsPanel.transform, false);
        
        RectTransform inputRect = inputFieldObj.AddComponent<RectTransform>();
        inputRect.sizeDelta = new Vector2(200, 40);
        
        Image inputBg = inputFieldObj.AddComponent<Image>();
        inputBg.color = new Color(0.2f, 0.2f, 0.2f, 1);
        
        // Текстовое поле внутри поля ввода
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(inputFieldObj.transform, false);
        
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10, 0);
        textRect.offsetMax = new Vector2(-10, 0);
        
        // Создаем компонент текста
        var tmp = CreateTMPText(textObj, "Название снимка", TextAlignmentOptions.Left, 16);
        
        // Создаем компонент поля ввода
        TMP_InputField inputField = inputFieldObj.AddComponent<TMP_InputField>();
        inputField.textComponent = tmp;
        inputField.textViewport = textRect;
        inputField.text = "Вариант";
        
        // Кнопка создания снимка
        GameObject createButtonObj = CreateButton(controlsPanel, "Создать снимок", new Vector2(150, 40));
        
        // Кнопка переключения видимости панели снимков
        GameObject toggleButtonObj = CreateButton(mainCanvas, "Снимки", new Vector2(80, 40));
        RectTransform toggleRect = toggleButtonObj.GetComponent<RectTransform>();
        toggleRect.anchorMin = new Vector2(1, 0);
        toggleRect.anchorMax = new Vector2(1, 0);
        toggleRect.anchoredPosition = new Vector2(-150, 100);
        
        // Устанавливаем ссылки на UI компоненты в UIManager
        UIManager uiManager = mainCanvas.GetComponentInChildren<UIManager>();
        if (uiManager != null)
        {
            // Находим соответствующие поля через рефлексию
            var snapshotPanelField = uiManager.GetType().GetField("snapshotPanel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var createSnapshotButtonField = uiManager.GetType().GetField("createSnapshotButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var toggleSnapshotPanelButtonField = uiManager.GetType().GetField("toggleSnapshotPanelButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var snapshotContainerField = uiManager.GetType().GetField("snapshotContainer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var snapshotNameInputField = uiManager.GetType().GetField("snapshotNameInput", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (snapshotPanelField != null) snapshotPanelField.SetValue(uiManager, snapshotPanel);
            if (createSnapshotButtonField != null) createSnapshotButtonField.SetValue(uiManager, createButtonObj.GetComponent<Button>());
            if (toggleSnapshotPanelButtonField != null) toggleSnapshotPanelButtonField.SetValue(uiManager, toggleButtonObj.GetComponent<Button>());
            if (snapshotContainerField != null) snapshotContainerField.SetValue(uiManager, snapshotContainer.transform);
            if (snapshotNameInputField != null) snapshotNameInputField.SetValue(uiManager, inputField);
        }
        
        // По умолчанию скрываем панель снимков
        snapshotPanel.SetActive(false);
    }

    // Хелпер для создания кнопки
    private static GameObject CreateButton(GameObject parent, string text, Vector2 size)
    {
        GameObject buttonObj = new GameObject(text + "Button");
        buttonObj.transform.SetParent(parent.transform, false);
        
        RectTransform buttonRect = buttonObj.AddComponent<RectTransform>();
        buttonRect.sizeDelta = size;
        
        Image buttonImage = buttonObj.AddComponent<Image>();
        buttonImage.color = new Color(0.2f, 0.2f, 0.2f, 1);
        
        Button button = buttonObj.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        
        // Создаем объект для текста
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);
        
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        CreateTMPText(textObj, text, TextAlignmentOptions.Center, 16);
        
        return buttonObj;
    }

    // Хелпер для создания TextMeshPro текста
    private static TextMeshProUGUI CreateTMPText(GameObject parent, string text, TextAlignmentOptions alignment, float fontSize)
    {
        TextMeshProUGUI tmp = parent.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.color = Color.white;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        
        return tmp;
    }

    // Создает префаб кнопки снимка
    public static GameObject CreateSnapshotButtonPrefab()
    {
        // Проверяем существование префаба
        string prefabPath = "Assets/Prefabs/SnapshotButton.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        
        if (prefab != null)
        {
            return prefab;
        }
        
        // Создаем временный объект кнопки
        GameObject buttonObj = new GameObject("SnapshotButton");
        
        // Добавляем RectTransform
        RectTransform rect = buttonObj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(160, 40);
        
        // Добавляем изображение
        Image image = buttonObj.AddComponent<Image>();
        image.color = new Color(0.2f, 0.2f, 0.2f, 1);
        
        // Добавляем компонент кнопки
        Button button = buttonObj.AddComponent<Button>();
        button.targetGraphic = image;
        
        // Добавляем текст
        GameObject textObj = new GameObject("Text (TMP)");
        textObj.transform.SetParent(buttonObj.transform, false);
        
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        // Создаем компонент текста
        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = "Вариант";
        tmp.color = Color.white;
        tmp.fontSize = 16;
        tmp.alignment = TextAlignmentOptions.Center;
        
        // Создаем директорию для префабов, если она не существует
        if (!Directory.Exists("Assets/Prefabs"))
        {
            Directory.CreateDirectory("Assets/Prefabs");
        }
        
        // Сохраняем как префаб
        prefab = PrefabUtility.SaveAsPrefabAsset(buttonObj, prefabPath);
        
        // Уничтожаем временный объект
        UnityEngine.Object.DestroyImmediate(buttonObj);
        
        AssetDatabase.Refresh();
        
        return prefab;
    }
} 