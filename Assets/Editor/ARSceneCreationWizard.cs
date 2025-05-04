using UnityEngine;
using UnityEditor;
using UnityEngine.XR.ARFoundation;
using Unity.XR.CoreUtils;
using System.IO;
using UnityEngine.XR.ARSubsystems;
using System.Linq;

/// <summary>
/// Мастер создания настроенной AR-сцены с оптимальными параметрами
/// </summary>
public class ARSceneCreationWizard : EditorWindow
{
    // Настройки сцены
    private string sceneName = "AR_Scene";
    private bool includeDebugCanvas = true;
    private bool setupWallSegmentation = true;
    private bool useDemoMode = true; // По умолчанию используем демо-режим
    
    // Настройки AR
    private bool setupPlaneDetection = true;
    private bool setupRaycast = true;
    private bool enableRunInBackground = true;
    
    // Ссылки на префабы
    private GameObject arSessionPrefab;
    private GameObject arOriginPrefab;
    private GameObject uiCanvasPrefab;
    
    [MenuItem("AR Tools/Create AR Scene", false, 10)]
    public static void ShowWindow()
    {
        GetWindow<ARSceneCreationWizard>("AR Scene Wizard");
    }
    
    // Добавим дополнительный пункт меню с уникальным путем
    [MenuItem("AR Tools/Wall Painting/Create AR Scene Wizard", false, 100)]
    public static void ShowWallPaintingWindow()
    {
        GetWindow<ARSceneCreationWizard>("AR Wall Painting Scene Wizard");
    }
    
    private void OnGUI()
    {
        GUILayout.Label("AR Wall Painting Scene Setup", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        
        // Базовые настройки
        sceneName = EditorGUILayout.TextField("Scene Name", sceneName);
        
        EditorGUILayout.Space();
        GUILayout.Label("Components", EditorStyles.boldLabel);
        
        setupPlaneDetection = EditorGUILayout.Toggle("Plane Detection", setupPlaneDetection);
        setupRaycast = EditorGUILayout.Toggle("Raycast Manager", setupRaycast);
        includeDebugCanvas = EditorGUILayout.Toggle("Debug Canvas", includeDebugCanvas);
        setupWallSegmentation = EditorGUILayout.Toggle("Wall Segmentation", setupWallSegmentation);
        
        if (setupWallSegmentation)
        {
            EditorGUI.indentLevel++;
            useDemoMode = EditorGUILayout.Toggle("Use Demo Mode", useDemoMode);
            EditorGUI.indentLevel--;
        }
        
        EditorGUILayout.Space();
        GUILayout.Label("AR Session Settings", EditorStyles.boldLabel);
        enableRunInBackground = EditorGUILayout.Toggle("Run In Background", enableRunInBackground);
        
        EditorGUILayout.Space();
        
        // Кнопка создания сцены
        if (GUILayout.Button("Create AR Scene"))
        {
            CreateARScene();
        }
    }
    
    private void CreateARScene()
    {
        try
        {
            // 1. Создаем необходимые объекты
            
            // AR Session
            GameObject arSessionObj = new GameObject("AR Session");
            ARSession arSession = arSessionObj.AddComponent<ARSession>();
            arSessionObj.AddComponent<ARInputManager>();
            
            // Добавляем помощник конфигурации
            ARSession_ConfigHelper configHelper = arSessionObj.AddComponent<ARSession_ConfigHelper>();
            SerializedObject configSerializedObj = new SerializedObject(configHelper);
            
            // Проверяем существование свойств перед их использованием
            var runInBackgroundProp = configSerializedObj.FindProperty("runInBackground");
            if (runInBackgroundProp != null)
            {
                runInBackgroundProp.boolValue = enableRunInBackground;
            }
            
            // Устанавливаем trackingFeatures через int, а не через Feature
            var trackingFeaturesProp = configSerializedObj.FindProperty("trackingFeatures");
            if (trackingFeaturesProp != null)
            {
                // Используем явное приведение типов вместо предварительного создания переменной Feature
                int features = (int)(1 << 0) | (int)(1 << 3); // Примерно соответствует PointCloud | PlaneTracking
                trackingFeaturesProp.intValue = features;
            }
            
            configSerializedObj.ApplyModifiedProperties();
            
            // Устанавливаем глобальный параметр для приложения
            Application.runInBackground = enableRunInBackground;
            
            // XR Origin
            GameObject xrOriginObj = new GameObject("XR Origin");
            XROrigin xrOrigin = xrOriginObj.AddComponent<XROrigin>();
            
            // AR Camera
            GameObject arCameraObj = new GameObject("AR Camera");
            arCameraObj.transform.SetParent(xrOriginObj.transform);
            Camera arCamera = arCameraObj.AddComponent<Camera>();
            arCameraObj.AddComponent<ARCameraManager>();
            arCameraObj.AddComponent<ARCameraBackground>();
            
            // Добавляем TrackedPoseDriver для обновления позиции камеры
            try
            {
                // Пробуем различные варианты драйвера отслеживания позиции
                System.Type trackedPoseDriverType = 
                    FindTypeInProject("UnityEngine.InputSystem.XR.TrackedPoseDriver") ?? 
                    FindTypeInProject("Unity.XR.CoreUtils.TrackedPoseDriver") ?? 
                    FindTypeInProject("UnityEngine.SpatialTracking.TrackedPoseDriver") ??
                    FindTypeInProject("TrackedPoseDriver");
                
                if (trackedPoseDriverType != null)
                {
                    Component driver = arCameraObj.AddComponent(trackedPoseDriverType);
                    Debug.Log($"Добавлен {trackedPoseDriverType.Name} для AR Camera");
                    
                    // Пробуем настроить драйвер через reflection, если это возможно
                    try
                    {
                        // Общие настройки через SerializedObject, если они есть
                        SerializedObject driverSerializedObj = new SerializedObject(driver);
                        
                        // Пробуем настроить tracking type (если свойство существует)
                        var trackingTypeProp = driverSerializedObj.FindProperty("trackingType");
                        if (trackingTypeProp != null)
                            trackingTypeProp.intValue = 0; // 0 обычно означает "RotationAndPosition"
                        
                        driverSerializedObj.ApplyModifiedProperties();
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"Не удалось настроить параметры TrackedPoseDriver: {ex.Message}");
                    }
                }
                else
                {
                    Debug.LogWarning("Не удалось найти ни один компонент для отслеживания позиции AR камеры. " +
                                     "Позиция камеры не будет обновляться корректно. " +
                                     "Убедитесь, что в проекте установлены пакеты Unity XR или AR Foundation.");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Ошибка при добавлении TrackedPoseDriver: {ex.Message}");
            }
            
            // Настраиваем XR Origin
            xrOrigin.Camera = arCamera;
            
            // Добавляем Camera Floor Offset GameObject
            GameObject cameraFloorOffset = new GameObject("Camera Floor Offset");
            cameraFloorOffset.transform.SetParent(xrOriginObj.transform);
            
            // Перемещаем камеру под новый родительский объект
            arCameraObj.transform.SetParent(cameraFloorOffset.transform);
            
            // Устанавливаем Camera Floor Offset в XR Origin
            var xrOriginSerializedObj = new SerializedObject(xrOrigin);
            var cameraFloorOffsetProp = xrOriginSerializedObj.FindProperty("m_CameraFloorOffsetObject");
            if (cameraFloorOffsetProp != null)
            {
                cameraFloorOffsetProp.objectReferenceValue = cameraFloorOffset;
                xrOriginSerializedObj.ApplyModifiedProperties();
                Debug.Log("Настроен Camera Floor Offset для XR Origin");
            }
            
            // Необходимые AR менеджеры
            ARPlaneManager planeManager = null;
            ARRaycastManager raycastManager = null;
            
            if (setupPlaneDetection)
            {
                planeManager = xrOriginObj.AddComponent<ARPlaneManager>();
                
                // Настраиваем обнаружение плоскостей
                var planeManagerSerializedObj = new SerializedObject(planeManager);
                
                // Устанавливаем режим детекции (горизонтальные и вертикальные плоскости)
                var detectionModeProp = planeManagerSerializedObj.FindProperty("m_DetectionMode");
                if (detectionModeProp != null)
                {
                    // Значение 3 соответствует Everything (Horizontal, Vertical, and 45-degree angles)
                    detectionModeProp.intValue = 3;
                }
                
                // Включаем распознавание вертикальных плоскостей (для стен)
                var classificationEnabledProp = planeManagerSerializedObj.FindProperty("m_ClassificationEnabled");
                if (classificationEnabledProp != null)
                {
                    classificationEnabledProp.boolValue = true;
                }
                
                planeManagerSerializedObj.ApplyModifiedProperties();
                Debug.Log("Настроен ARPlaneManager для распознавания вертикальных и горизонтальных плоскостей");
            }
            
            if (setupRaycast)
            {
                raycastManager = xrOriginObj.AddComponent<ARRaycastManager>();
            }
            
            // Debug Canvas
            GameObject canvasObj = null;
            if (includeDebugCanvas)
            {
                canvasObj = new GameObject("Canvas");
                Canvas canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
                canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                
                // Debug UI
                GameObject debugTextObj = new GameObject("ARDebugText");
                debugTextObj.transform.SetParent(canvasObj.transform);
                UnityEngine.UI.Text debugText = debugTextObj.AddComponent<UnityEngine.UI.Text>();
                
                // Пробуем загрузить шрифт с поддержкой нескольких вариантов
                Font font = null;
                
                // Перечень потенциальных встроенных шрифтов в порядке предпочтения
                string[] potentialFonts = new string[] { 
                    "LegacyRuntime.ttf", 
                    "Inter-Regular.ttf", 
                    "Arial.ttf" 
                };
                
                // Пробуем загрузить доступный шрифт
                foreach (string fontName in potentialFonts)
                {
                    font = Resources.GetBuiltinResource<Font>(fontName);
                    if (font != null) break;
                }
                
                // Если ни один из встроенных шрифтов не найден, пробуем найти любой доступный шрифт в проекте
                if (font == null)
                {
                    font = Resources.FindObjectsOfTypeAll<Font>().FirstOrDefault();
                    Debug.LogWarning("Не удалось найти встроенные шрифты. Используется первый доступный шрифт из проекта.");
                }
                
                // Применяем шрифт (если найден)
                if (font != null)
                {
                    debugText.font = font;
                }
                else
                {
                    Debug.LogWarning("Не удалось найти ни одного шрифта! Текст может отображаться некорректно.");
                }
                
                debugText.fontSize = 14;
                debugText.color = Color.white;
                
                RectTransform rectTransform = debugTextObj.GetComponent<RectTransform>();
                rectTransform.anchorMin = new Vector2(0, 1);
                rectTransform.anchorMax = new Vector2(1, 1);
                rectTransform.pivot = new Vector2(0.5f, 1);
                rectTransform.sizeDelta = new Vector2(0, 200);
                rectTransform.anchoredPosition = Vector2.zero;
                
                // Debug Manager - проверяем наличие компонента в проекте
                System.Type debugManagerType = FindTypeInProject("ARDebugManager");
                if (debugManagerType != null)
                {
                    GameObject debugManagerObj = new GameObject("ARDebugManager");
                    Component debugManager = debugManagerObj.AddComponent(debugManagerType);
                    
                    SerializedObject debugManagerSerializedObj = new SerializedObject(debugManager);
                    
                    var arSessionProp = debugManagerSerializedObj.FindProperty("arSession");
                    if (arSessionProp != null) arSessionProp.objectReferenceValue = arSession;
                    
                    var xrOriginProp = debugManagerSerializedObj.FindProperty("xrOrigin");
                    if (xrOriginProp != null) xrOriginProp.objectReferenceValue = xrOrigin;
                    
                    var debugTextProp = debugManagerSerializedObj.FindProperty("debugText");
                    if (debugTextProp != null) debugTextProp.objectReferenceValue = debugText;
                    
                    debugManagerSerializedObj.ApplyModifiedProperties();
                }
            }
            
            // Wall Segmentation
            if (setupWallSegmentation)
            {
                GameObject segmentationObj = new GameObject("WallSegmentationManager");
                
                if (useDemoMode)
                {
                    // Проверяем наличие компонента в проекте
                    System.Type demoSegmentationType = FindTypeInProject("DemoWallSegmentation");
                    if (demoSegmentationType != null)
                    {
                        Component demoSegmentation = segmentationObj.AddComponent(demoSegmentationType);
                        
                        // Настройка компонента
                        SerializedObject demoSegmentationSerializedObj = new SerializedObject(demoSegmentation);
                        
                        var planeManagerProp = demoSegmentationSerializedObj.FindProperty("planeManager");
                        if (planeManagerProp != null)
                            planeManagerProp.objectReferenceValue = setupPlaneDetection ? planeManager : null;
                        
                        var cameraManagerProp = demoSegmentationSerializedObj.FindProperty("cameraManager");
                        var arCameraManager = arCameraObj.GetComponent<ARCameraManager>();
                        if (cameraManagerProp != null && arCameraManager != null)
                            cameraManagerProp.objectReferenceValue = arCameraManager;
                        
                        // Если есть canvas, создаем отладочное изображение
                        if (canvasObj != null)
                        {
                            GameObject debugImageObj = new GameObject("SegmentationDebugImage");
                            debugImageObj.transform.SetParent(canvasObj.transform);
                            UnityEngine.UI.RawImage rawImage = debugImageObj.AddComponent<UnityEngine.UI.RawImage>();
                            
                            RectTransform rectTransform = debugImageObj.GetComponent<RectTransform>();
                            rectTransform.anchorMin = new Vector2(1, 1);
                            rectTransform.anchorMax = new Vector2(1, 1);
                            rectTransform.pivot = new Vector2(1, 1);
                            rectTransform.sizeDelta = new Vector2(200, 150);
                            rectTransform.anchoredPosition = Vector2.zero;
                            
                            var debugImageProp = demoSegmentationSerializedObj.FindProperty("debugImage");
                            if (debugImageProp != null)
                                debugImageProp.objectReferenceValue = rawImage;
                        }
                        
                        demoSegmentationSerializedObj.ApplyModifiedProperties();
                    }
                    else
                    {
                        Debug.LogWarning("Компонент DemoWallSegmentation не найден в проекте.");
                    }
                }
                else
                {
                    // Проверяем наличие компонента в проекте
                    System.Type wallSegmentationType = FindTypeInProject("WallSegmentation");
                    if (wallSegmentationType != null)
                    {
                        Component wallSegmentation = segmentationObj.AddComponent(wallSegmentationType);
                        
                        // Настройка компонента
                        SerializedObject wallSegmentationSerializedObj = new SerializedObject(wallSegmentation);
                        
                        var cameraManagerProp = wallSegmentationSerializedObj.FindProperty("cameraManager");
                        var arCameraManager = arCameraObj.GetComponent<ARCameraManager>();
                        if (cameraManagerProp != null && arCameraManager != null)
                            cameraManagerProp.objectReferenceValue = arCameraManager;
                        
                        var arCameraProp = wallSegmentationSerializedObj.FindProperty("arCamera");
                        if (arCameraProp != null)
                            arCameraProp.objectReferenceValue = arCamera;
                        
                        var currentModeProp = wallSegmentationSerializedObj.FindProperty("currentMode");
                        if (currentModeProp != null)
                            currentModeProp.enumValueIndex = 0; // Demo Mode
                        
                        // Если есть canvas, создаем отладочное изображение
                        if (canvasObj != null)
                        {
                            GameObject debugImageObj = new GameObject("SegmentationDebugImage");
                            debugImageObj.transform.SetParent(canvasObj.transform);
                            UnityEngine.UI.RawImage rawImage = debugImageObj.AddComponent<UnityEngine.UI.RawImage>();
                            
                            RectTransform rectTransform = debugImageObj.GetComponent<RectTransform>();
                            rectTransform.anchorMin = new Vector2(1, 1);
                            rectTransform.anchorMax = new Vector2(1, 1);
                            rectTransform.pivot = new Vector2(1, 1);
                            rectTransform.sizeDelta = new Vector2(200, 150);
                            rectTransform.anchoredPosition = Vector2.zero;
                            
                            var debugImageProp = wallSegmentationSerializedObj.FindProperty("debugImage");
                            if (debugImageProp != null)
                                debugImageProp.objectReferenceValue = rawImage;
                        }
                        
                        wallSegmentationSerializedObj.ApplyModifiedProperties();
                    }
                    else
                    {
                        Debug.LogWarning("Компонент WallSegmentation не найден в проекте.");
                    }
                }
            }
            
            // AR Wall Painting App Controller
            GameObject appControllerObj = new GameObject("ARWallPaintingAppController");
            
            // Проверяем наличие компонента в проекте
            System.Type appControllerType = FindTypeInProject("ARWallPaintingApp");
            if (appControllerType != null)
            {
                Component appController = appControllerObj.AddComponent(appControllerType);
                
                // Настройка контроллера приложения
                SerializedObject appControllerSerializedObj = new SerializedObject(appController);
                
                var arSessionProp = appControllerSerializedObj.FindProperty("arSession");
                if (arSessionProp != null)
                    arSessionProp.objectReferenceValue = arSession;
                
                var xrOriginProp = appControllerSerializedObj.FindProperty("xrOrigin");
                if (xrOriginProp != null)
                    xrOriginProp.objectReferenceValue = xrOrigin;
                
                if (setupPlaneDetection)
                {
                    var planeManagerProp = appControllerSerializedObj.FindProperty("planeManager");
                    if (planeManagerProp != null)
                        planeManagerProp.objectReferenceValue = planeManager;
                }
                
                if (setupRaycast)
                {
                    var raycastManagerProp = appControllerSerializedObj.FindProperty("raycastManager");
                    if (raycastManagerProp != null)
                        raycastManagerProp.objectReferenceValue = raycastManager;
                }
                
                appControllerSerializedObj.ApplyModifiedProperties();
            }
            else
            {
                Debug.LogWarning("Компонент ARWallPaintingApp не найден в проекте.");
            }
            
            // 2. Создаем AR Plane Controller если необходимо
            if (setupPlaneDetection)
            {
                System.Type planeControllerType = FindTypeInProject("ARPlaneController");
                if (planeControllerType != null)
                {
                    GameObject planeControllerObj = new GameObject("ARPlaneController");
                    Component planeController = planeControllerObj.AddComponent(planeControllerType);
                    
                    SerializedObject planeCtrlSerializedObj = new SerializedObject(planeController);
                    var planeManagerProp = planeCtrlSerializedObj.FindProperty("planeManager");
                    if (planeManagerProp != null)
                        planeManagerProp.objectReferenceValue = planeManager;
                    
                    planeCtrlSerializedObj.ApplyModifiedProperties();
                }
            }
            
            // 3. Сообщаем пользователю об успешном создании
            EditorUtility.DisplayDialog("Успех", "AR сцена успешно создана! Не забудьте сохранить её через File > Save Scene As...", "OK");
        }
        catch (System.Exception ex)
        {
            // Обрабатываем ошибки создания сцены
            Debug.LogError($"Ошибка при создании AR сцены: {ex.Message}\n{ex.StackTrace}");
            EditorUtility.DisplayDialog("Ошибка", $"Произошла ошибка при создании AR сцены: {ex.Message}", "OK");
        }
    }

    // Вспомогательный метод для поиска типа в текущем проекте
    private System.Type FindTypeInProject(string typeName)
    {
        // Сначала пробуем обычный поиск типа (работает для системных типов и типов из загруженных сборок)
        System.Type type = System.Type.GetType(typeName);
        
        // Если тип не найден, ищем его во всех загруженных сборках
        if (type == null)
        {
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                // Пробуем найти тип по имени без пространства имен
                type = assembly.GetType(typeName);
                if (type != null)
                    return type;
                
                // Если не нашли, пробуем найти тип по короткому имени внутри всех пространств имен сборки
                foreach (var definedType in assembly.GetTypes())
                {
                    if (definedType.Name == typeName)
                        return definedType;
                }
            }
        }
        
        return type;
    }
} 