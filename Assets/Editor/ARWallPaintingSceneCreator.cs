#if ENABLE_INPUT_SYSTEM
#define USING_INPUT_SYSTEM
#endif

using UnityEngine;
using UnityEditor;
using UnityEngine.XR.ARFoundation;
using Unity.XR.CoreUtils;
using UnityEngine.UI;
using System.IO;
using System.Collections.Generic;
using UnityEditor.SceneManagement;

/// <summary>
/// Редакторный скрипт для создания полностью настроенной AR Wall Painting сцены
/// </summary>
public class ARWallPaintingSceneCreator : Editor
{
    [MenuItem("Tools/AR/Create AR Wall Painting Scene")]
    public static void CreateARWallPaintingScene()
    {
        // Создаем новую сцену
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        
        // 1. Создаем основные префабы, если их еще нет
        CreateARPrefabs();
        
        // 2. Создаем базовую AR структуру
        GameObject arRootObject = CreateARRootStructure();
        
        // 3. Настраиваем AR компоненты
        SetupARComponents(arRootObject);
        
        // 4. Создаем UI для приложения и получаем ссылку на Canvas
        Canvas mainCanvas = CreateARUI(arRootObject);
        
        // 5. Добавляем систему сегментации стен
        CreateWallSegmentation(arRootObject);
        
        // 6. Добавляем систему окраски стен
        CreateWallPainting(arRootObject);
        
        // 7. Создаем систему симуляции окружения
        GameObject simEnvironmentObj = new GameObject("Simulated Environment Scene");
        SimulatedEnvironmentScene simEnvironment = simEnvironmentObj.AddComponent<SimulatedEnvironmentScene>();
        
        // 8. Создаем дополнительные вспомогательные объекты
        CreateHelperObjects(arRootObject);
        
        // 9. Добавляем контроллер AR плоскостей
        GameObject planeControllerObj = new GameObject("ARPlaneController");
        ARPlaneController planeController = planeControllerObj.AddComponent<ARPlaneController>();
        
        // Настраиваем ссылки для контроллера плоскостей
        SerializedObject serializedPlaneController = new SerializedObject(planeController);
        serializedPlaneController.FindProperty("planeManager").objectReferenceValue = arRootObject.GetComponentInChildren<ARPlaneManager>();
        serializedPlaneController.ApplyModifiedProperties();
        
        Debug.Log("Добавлен ARPlaneController для управления визуализацией AR плоскостей");
        
        // 10. Добавляем компонент для управления визуализацией стен
        GameObject wallVisualizationUIObj = new GameObject("WallVisualizationUI");
        ARWallVisualizationUI wallVisualizationUI = wallVisualizationUIObj.AddComponent<ARWallVisualizationUI>();
        
        // Находим необходимые компоненты
        XROrigin xrOrigin = arRootObject.GetComponentInChildren<XROrigin>();
        ARPlaneManager planeManager = xrOrigin?.GetComponent<ARPlaneManager>();
        
        // Настраиваем ссылки
        SerializedObject serializedWallUI = new SerializedObject(wallVisualizationUI);
        serializedWallUI.FindProperty("planeManager").objectReferenceValue = planeManager;
        serializedWallUI.FindProperty("uiCanvas").objectReferenceValue = mainCanvas;
        serializedWallUI.ApplyModifiedProperties();
        
        Debug.Log("Добавлен компонент ARWallVisualizationUI для управления визуализацией стен");
        
        // 11. Настраиваем кнопку обновления сегментации
        GameObject updateSegmentationButtonObj = GameObject.Find("UpdateSegmentationButton");
        Button updateSegmentationButton = updateSegmentationButtonObj?.GetComponent<Button>();
        WallSegmentation wallSegmentation = arRootObject.GetComponentInChildren<WallSegmentation>();

        if (updateSegmentationButton != null && wallSegmentation != null)
        {
            // Создаем новый GameObject для хранения EventSystem, если его нет
            if (GameObject.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject eventSystemObj = new GameObject("EventSystem");
                eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystemObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                Debug.Log("Создан EventSystem для обработки UI событий");
            }

            // Создаем компонент для обработки нажатия на кнопку
            GameObject segmentationUpdaterObj = new GameObject("SegmentationButtonHandler");
            SegmentationButtonHandler handler = segmentationUpdaterObj.AddComponent<SegmentationButtonHandler>();
            
            // Настраиваем ссылки
            SerializedObject serializedButtonHandler = new SerializedObject(handler);
            serializedButtonHandler.FindProperty("wallSegmentation").objectReferenceValue = wallSegmentation;
            serializedButtonHandler.ApplyModifiedProperties();
            
            // Привязываем обработчик к кнопке 
            updateSegmentationButton.onClick.AddListener(delegate { handler.UpdateSegmentation(); });
            
            Debug.Log("Кнопка обновления сегментации настроена");
        }
        
        // 12. Проверяем все настройки компонентов AR
        ValidateARSetup(arRootObject);
        
        // Сохраняем сцену если нужно
        bool saveScene = EditorUtility.DisplayDialog(
            "Сохранить сцену?",
            "Хотите сохранить созданную AR Wall Painting сцену?",
            "Да", "Нет");
            
        if (saveScene)
        {
            string path = EditorUtility.SaveFilePanel(
                "Сохранить AR Wall Painting сцену",
                Application.dataPath,
                "ARWallPainting",
                "unity");
                
            if (!string.IsNullOrEmpty(path))
            {
                // Преобразуем путь к относительному для Unity
                if (path.StartsWith(Application.dataPath))
                {
                    path = "Assets" + path.Substring(Application.dataPath.Length);
                }
                
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), path);
                Debug.Log("Сцена успешно сохранена в: " + path);
            }
        }
        
        Debug.Log("AR Wall Painting сцена успешно создана и настроена!");
    }
    
    private static void CreateARPrefabs()
    {
        // Создаем директорию для префабов
        if (!Directory.Exists("Assets/Prefabs"))
        {
            Directory.CreateDirectory("Assets/Prefabs");
        }
        
        // Создаем префаб AR плоскости
        string planePrefabPath = "Assets/Prefabs/ARPlaneVisualizer.prefab";
        if (!File.Exists(planePrefabPath))
        {
            // Создаем базовый объект для плоскости
            GameObject planeObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            planeObject.name = "ARPlaneVisualizer";
            
            // Устанавливаем правильную ориентацию для AR плоскости
            planeObject.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            
            // Добавляем компонент ARPlaneVisualizer
            var visualizer = planeObject.AddComponent<ARPlaneVisualizer>();
            
            // Настраиваем материал
            MeshRenderer renderer = planeObject.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                // Создаем новый материал с полупрозрачностью
                Material planeMaterial = new Material(Shader.Find("Unlit/Color"));
                planeMaterial.color = new Color(0.5f, 0.5f, 0.5f, 0.3f);
                renderer.sharedMaterial = planeMaterial;
                
                // Настраиваем свойства материала для полупрозрачности
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            
            // Сохраняем объект как префаб
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(planeObject, planePrefabPath);
            
            // Удаляем временный объект сцены
            DestroyImmediate(planeObject);
            
            Debug.Log($"AR Plane Prefab создан по пути: {planePrefabPath}");
        }
        
        // Создаем префаб для окрашенной стены
        string wallPrefabPath = "Assets/Prefabs/PaintedWall.prefab";
        if (!File.Exists(wallPrefabPath))
        {
            // Создаем базовый объект для окрашенной стены
            GameObject wallObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            wallObject.name = "PaintedWall";
            
            // Настраиваем материал
            MeshRenderer renderer = wallObject.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                // Создаем новый материал для стены
                Material wallMaterial = new Material(Shader.Find("Unlit/Color"));
                wallMaterial.color = Color.white;
                renderer.sharedMaterial = wallMaterial;
            }
            
            // Сохраняем объект как префаб
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(wallObject, wallPrefabPath);
            
            // Удаляем временный объект сцены
            DestroyImmediate(wallObject);
            
            Debug.Log($"Painted Wall Prefab создан по пути: {wallPrefabPath}");
        }
        
        // Создаем другие необходимые префабы...
    }
    
    private static GameObject CreateARRootStructure()
    {
        // Создаем корневой объект для AR сцены
        GameObject arRoot = new GameObject("ARWallPainting*");
        
        // Создаем AR Session
        GameObject arSessionObj = new GameObject("AR Session");
        arSessionObj.transform.SetParent(arRoot.transform);
        ARSession arSession = arSessionObj.AddComponent<ARSession>();
        arSessionObj.AddComponent<ARInputManager>();
        
        // Создаем XR Origin
        GameObject xrOriginObj = new GameObject("XR Origin");
        xrOriginObj.transform.SetParent(arRoot.transform);
        XROrigin xrOrigin = xrOriginObj.AddComponent<XROrigin>();
        
        // Добавляем AR-компоненты к XR Origin
        ARCameraManager cameraManager = xrOriginObj.AddComponent<ARCameraManager>();
        ARPlaneManager planeManager = xrOriginObj.AddComponent<ARPlaneManager>();
        ARRaycastManager raycastManager = xrOriginObj.AddComponent<ARRaycastManager>();
        
        // Создаем AR камеру
        GameObject cameraObj = new GameObject("AR Camera");
        cameraObj.transform.SetParent(xrOriginObj.transform);
        Camera camera = cameraObj.AddComponent<Camera>();
        camera.tag = "MainCamera";
        
        // Добавляем компоненты к камере
        cameraObj.AddComponent<ARCameraBackground>();
        
        // Добавляем TrackedPoseDriver для обновления позиции камеры через XR устройство
        AddAppropriateTrackedPoseDriver(cameraObj);
        
        // Назначаем камеру для XR Origin
        xrOrigin.Camera = camera;
        
        // Создаем Camera Floor Offset и назначаем его для XR Origin
        GameObject offsetObj = new GameObject("Camera Floor Offset");
        offsetObj.transform.SetParent(xrOriginObj.transform);
        xrOrigin.CameraFloorOffsetObject = offsetObj;
        
        // Правильно настраиваем иерархию - перемещаем AR Camera под Camera Floor Offset
        cameraObj.transform.SetParent(offsetObj.transform);
        
        // Создаем Trackables контейнер
        GameObject trackablesObj = new GameObject("Trackables");
        trackablesObj.transform.SetParent(xrOriginObj.transform);
        
        // Добавляем управление камерами
        arRoot.AddComponent<CustomARCameraSetup>();
        
        return arRoot;
    }
    
    private static void SetupARComponents(GameObject arRoot)
    {
        // Получаем компоненты
        XROrigin xrOrigin = arRoot.GetComponentInChildren<XROrigin>();
        if (xrOrigin == null) return;
        
        // Настраиваем ARPlaneManager
        ARPlaneManager planeManager = xrOrigin.GetComponent<ARPlaneManager>();
        if (planeManager != null)
        {
            // Назначаем префаб плоскости
            string planePrefabPath = "Assets/Prefabs/ARPlaneVisualizer.prefab";
            GameObject planePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(planePrefabPath);
            if (planePrefab != null)
            {
                planeManager.planePrefab = planePrefab;
                Debug.Log("Префаб плоскости назначен для ARPlaneManager");
            }
        }
        
        // Добавляем ARRaycastManager, если его нет
        ARRaycastManager raycastManager = xrOrigin.GetComponent<ARRaycastManager>();
        if (raycastManager == null)
        {
            raycastManager = xrOrigin.gameObject.AddComponent<ARRaycastManager>();
            Debug.Log("ARRaycastManager добавлен на XROrigin");
        }
        
        // Добавляем ARMeshManager, если его нет
        ARMeshManager meshManager = xrOrigin.GetComponent<ARMeshManager>();
        if (meshManager == null)
        {
            // Создаем отдельный объект для ARMeshManager как дочерний для XROrigin
            GameObject meshManagerObj = new GameObject("AR Mesh Manager");
            meshManagerObj.transform.SetParent(xrOrigin.transform);
            meshManager = meshManagerObj.AddComponent<ARMeshManager>();
            
            // Создаем простой MeshFilter, который будет использоваться как meshPrefab
            GameObject tempMeshObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tempMeshObj.name = "TempMeshObject";
            tempMeshObj.AddComponent<ARMeshVisualizer>();
            
            // Получаем MeshFilter из временного объекта для назначения meshPrefab
            MeshFilter meshFilter = tempMeshObj.GetComponent<MeshFilter>();
            if (meshFilter != null)
            {
                // Назначаем meshPrefab - используем компонент MeshFilter
                meshManager.meshPrefab = meshFilter;
                Debug.Log("MeshFilter назначен для ARMeshManager");
            }
            else
            {
                Debug.LogError("Не удалось получить MeshFilter из временного объекта");
            }
            
            // Удаляем временный объект
            DestroyImmediate(tempMeshObj);
        }
        
        // Настраиваем AR Camera
        Camera arCamera = xrOrigin.Camera;
        if (arCamera != null)
        {
            // Убеждаемся, что камера имеет правильные компоненты
            ARCameraManager cameraManager = arCamera.gameObject.GetComponent<ARCameraManager>();
            if (cameraManager == null)
            {
                cameraManager = arCamera.gameObject.AddComponent<ARCameraManager>();
            }
            
            ARCameraBackground cameraBackground = arCamera.gameObject.GetComponent<ARCameraBackground>();
            if (cameraBackground == null)
            {
                cameraBackground = arCamera.gameObject.AddComponent<ARCameraBackground>();
            }
        }
    }
    
    private static Canvas CreateARUI(GameObject arRoot)
    {
        // Создаем Canvas
        GameObject canvasObj = new GameObject("Canvas");
        canvasObj.transform.SetParent(arRoot.transform);
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();
        
        // Создаем панель управления
        GameObject controlPanelObj = new GameObject("ControlPanel");
        controlPanelObj.transform.SetParent(canvasObj.transform);
        RectTransform controlPanelRect = controlPanelObj.AddComponent<RectTransform>();
        controlPanelRect.anchorMin = new Vector2(0, 0);
        controlPanelRect.anchorMax = new Vector2(1, 0.2f);
        controlPanelRect.offsetMin = Vector2.zero;
        controlPanelRect.offsetMax = Vector2.zero;
        
        // Добавляем кнопки для выбора цвета
        CreateColorButtons(controlPanelObj);
        
        // Создаем панель снимков
        GameObject snapshotPanelObj = new GameObject("SnapshotPanel");
        snapshotPanelObj.transform.SetParent(canvasObj.transform);
        RectTransform snapshotPanelRect = snapshotPanelObj.AddComponent<RectTransform>();
        snapshotPanelRect.anchorMin = new Vector2(0.7f, 0.2f);
        snapshotPanelRect.anchorMax = new Vector2(1, 0.8f);
        snapshotPanelRect.offsetMin = Vector2.zero;
        snapshotPanelRect.offsetMax = Vector2.zero;
        
        // Добавляем кнопку для создания снимков
        GameObject snapshotButtonObj = new GameObject("СнимкиButton");
        snapshotButtonObj.transform.SetParent(canvasObj.transform);
        RectTransform snapshotButtonRect = snapshotButtonObj.AddComponent<RectTransform>();
        snapshotButtonRect.anchorMin = new Vector2(0.8f, 0.9f);
        snapshotButtonRect.anchorMax = new Vector2(0.95f, 0.98f);
        snapshotButtonRect.offsetMin = Vector2.zero;
        snapshotButtonRect.offsetMax = Vector2.zero;
        
        Button snapshotButton = snapshotButtonObj.AddComponent<Button>();
        
        // Добавляем текст к кнопке снимков
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(snapshotButtonObj.transform);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        Text text = textObj.AddComponent<Text>();
        text.text = "Снимки";
        text.color = Color.black;
        text.alignment = TextAnchor.MiddleCenter;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        
        // Создаем кнопку для обновления сегментации
        GameObject updateSegmentationButtonObj = new GameObject("UpdateSegmentationButton");
        updateSegmentationButtonObj.transform.SetParent(canvasObj.transform);
        RectTransform updateSegmentationButtonRect = updateSegmentationButtonObj.AddComponent<RectTransform>();
        updateSegmentationButtonRect.anchorMin = new Vector2(0.6f, 0.9f);
        updateSegmentationButtonRect.anchorMax = new Vector2(0.78f, 0.98f);
        updateSegmentationButtonRect.offsetMin = Vector2.zero;
        updateSegmentationButtonRect.offsetMax = Vector2.zero;
        
        Button updateSegmentationButton = updateSegmentationButtonObj.AddComponent<Button>();
        Image buttonImage = updateSegmentationButtonObj.AddComponent<Image>();
        buttonImage.color = new Color(0.3f, 0.6f, 0.9f, 1.0f);
        updateSegmentationButton.targetGraphic = buttonImage;
        
        // Добавляем текст к кнопке
        GameObject updateButtonTextObj = new GameObject("Text");
        updateButtonTextObj.transform.SetParent(updateSegmentationButtonObj.transform);
        RectTransform updateButtonTextRect = updateButtonTextObj.AddComponent<RectTransform>();
        updateButtonTextRect.anchorMin = Vector2.zero;
        updateButtonTextRect.anchorMax = Vector2.one;
        updateButtonTextRect.offsetMin = Vector2.zero;
        updateButtonTextRect.offsetMax = Vector2.zero;
        
        Text updateButtonText = updateButtonTextObj.AddComponent<Text>();
        updateButtonText.text = "Обновить сегм.";
        updateButtonText.color = Color.white;
        updateButtonText.alignment = TextAnchor.MiddleCenter;
        updateButtonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        updateButtonText.fontSize = 14;
        
        // Создаем RawImage для отображения сегментации стен
        GameObject rawImageObj = new GameObject("RawImage");
        rawImageObj.transform.SetParent(canvasObj.transform);
        RectTransform rawImageRect = rawImageObj.AddComponent<RectTransform>();
        rawImageRect.anchorMin = new Vector2(0.05f, 0.7f);
        rawImageRect.anchorMax = new Vector2(0.35f, 0.95f);
        rawImageRect.offsetMin = Vector2.zero;
        rawImageRect.offsetMax = Vector2.zero;
        
        RawImage rawImage = rawImageObj.AddComponent<RawImage>();
        
        return canvas;
    }
    
    private static void CreateColorButtons(GameObject parent)
    {
        // Цвета для кнопок
        Color[] colors = new Color[]
        {
            Color.red,
            Color.green,
            Color.blue,
            Color.yellow,
            Color.white
        };
        
        string[] buttonNames = new string[]
        {
            "RedButton",
            "GreenButton",
            "BlueButton",
            "YellowButton",
            "WhiteButton"
        };
        
        // Создаем кнопки для выбора цвета
        for (int i = 0; i < colors.Length; i++)
        {
            GameObject buttonObj = new GameObject(buttonNames[i]);
            buttonObj.transform.SetParent(parent.transform);
            
            RectTransform buttonRect = buttonObj.AddComponent<RectTransform>();
            float width = 1.0f / colors.Length;
            buttonRect.anchorMin = new Vector2(i * width, 0.2f);
            buttonRect.anchorMax = new Vector2((i + 1) * width, 0.9f);
            buttonRect.offsetMin = new Vector2(5, 5);
            buttonRect.offsetMax = new Vector2(-5, -5);
            
            Button button = buttonObj.AddComponent<Button>();
            
            // Добавляем Image для отображения цвета
            GameObject imageObj = new GameObject("Image");
            imageObj.transform.SetParent(buttonObj.transform);
            RectTransform imageRect = imageObj.AddComponent<RectTransform>();
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;
            
            Image image = imageObj.AddComponent<Image>();
            image.color = colors[i];
            
            // Используем этот Image как target graphic для кнопки
            button.targetGraphic = image;
        }
    }
    
    private static void CreateWallSegmentation(GameObject arRoot)
    {
        // Создаем объект для WallSegmentationManager
        GameObject segmentationManagerObj = new GameObject("WallSegmentationManager");
        segmentationManagerObj.transform.SetParent(arRoot.transform);
        
        // Добавляем компонент WallSegmentation
        WallSegmentation wallSegmentation = segmentationManagerObj.AddComponent<WallSegmentation>();
        
        // Настраиваем параметры для новой модели SegFormer
        SerializedObject serializedWallSegmentation = new SerializedObject(wallSegmentation);

        // Устанавливаем режим сегментации на ExternalModel
        serializedWallSegmentation.FindProperty("currentMode").enumValueIndex = (int)WallSegmentation.SegmentationMode.ExternalModel;

        // Устанавливаем путь к модели
        serializedWallSegmentation.FindProperty("externalModelPath").stringValue = "Models/model.onnx";

        // Задаем имена входного и выходного тензоров для SegFormer
        serializedWallSegmentation.FindProperty("inputName").stringValue = "input";
        serializedWallSegmentation.FindProperty("outputName").stringValue = "logits";

        // Устанавливаем размеры входного изображения для SegFormer
        serializedWallSegmentation.FindProperty("inputWidth").intValue = 512;
        serializedWallSegmentation.FindProperty("inputHeight").intValue = 512;

        // Устанавливаем индекс класса стены для ADE20K датасета
        serializedWallSegmentation.FindProperty("wallClassIndex").intValue = 1;

        // Применяем настройки
        serializedWallSegmentation.ApplyModifiedProperties();
        
        // Находим AR камеру и назначаем ее
        XROrigin xrOrigin = arRoot.GetComponentInChildren<XROrigin>();
        if (xrOrigin != null && xrOrigin.Camera != null)
        {
            ARCameraManager cameraManager = xrOrigin.Camera.GetComponent<ARCameraManager>();
            if (cameraManager != null)
            {
                SerializedObject serializedCameraRef = new SerializedObject(wallSegmentation);
                SerializedProperty cameraManagerProp = serializedCameraRef.FindProperty("cameraManager");
                if (cameraManagerProp != null)
                {
                    cameraManagerProp.objectReferenceValue = cameraManager;
                    serializedCameraRef.ApplyModifiedProperties();
                }
                
                SerializedProperty arCameraProp = serializedCameraRef.FindProperty("arCamera");
                if (arCameraProp != null)
                {
                    arCameraProp.objectReferenceValue = xrOrigin.Camera;
                    serializedCameraRef.ApplyModifiedProperties();
                }
            }
        }
        
        // Находим RawImage для отображения отладки
        RawImage debugImage = GameObject.Find("RawImage")?.GetComponent<RawImage>();
        if (debugImage != null)
        {
            SerializedObject serializedDebugRef = new SerializedObject(wallSegmentation);
            SerializedProperty debugImageProp = serializedDebugRef.FindProperty("debugImage");
            if (debugImageProp != null)
            {
                debugImageProp.objectReferenceValue = debugImage;
                serializedDebugRef.ApplyModifiedProperties();
            }
            
            // Включаем отладочную визуализацию
            SerializedProperty showDebugProp = serializedDebugRef.FindProperty("showDebugVisualisation");
            if (showDebugProp != null)
            {
                showDebugProp.boolValue = true;
                serializedDebugRef.ApplyModifiedProperties();
                Debug.Log("Включена отладочная визуализация в WallSegmentation");
            }
        }
        else
        {
            Debug.LogWarning("RawImage не найден для WallSegmentation. Отладочная визуализация не будет работать.");
        }
        
        // Добавляем также компонент DemoWallSegmentation
        DemoWallSegmentation demoWallSegmentation = segmentationManagerObj.AddComponent<DemoWallSegmentation>();
        
        // Добавляем компонент OpenCVProcessor для постобработки сегментации
        OpenCVProcessor openCVProcessor = segmentationManagerObj.AddComponent<OpenCVProcessor>();
        Debug.Log("Добавлен OpenCVProcessor для улучшения результатов сегментации");

        // Настраиваем параметры OpenCVProcessor через SerializedObject
        SerializedObject serializedOpenCV = new SerializedObject(openCVProcessor);
        if (debugImage != null)
        {
            serializedOpenCV.FindProperty("debugOutputImage").objectReferenceValue = debugImage;
            serializedOpenCV.ApplyModifiedProperties();
        }
        
        // Назначаем AR плоскости и камеру для демо-сегментации
        ARPlaneManager planeManager = arRoot.GetComponentInChildren<ARPlaneManager>();
        if (planeManager != null)
        {
            SerializedObject serializedDemoPlaneRef = new SerializedObject(demoWallSegmentation);
            SerializedProperty planeManagerProp = serializedDemoPlaneRef.FindProperty("planeManager");
            if (planeManagerProp != null)
            {
                planeManagerProp.objectReferenceValue = planeManager;
                serializedDemoPlaneRef.ApplyModifiedProperties();
            }
        }
        
        // Назначаем ARCameraManager для демо-сегментации
        if (xrOrigin != null && xrOrigin.Camera != null)
        {
            ARCameraManager cameraManager = xrOrigin.Camera.GetComponent<ARCameraManager>();
            if (cameraManager != null)
            {
                SerializedObject serializedDemoCamera = new SerializedObject(demoWallSegmentation);
                SerializedProperty cameraManagerProp = serializedDemoCamera.FindProperty("cameraManager");
                if (cameraManagerProp != null)
                {
                    cameraManagerProp.objectReferenceValue = cameraManager;
                    serializedDemoCamera.ApplyModifiedProperties();
                }
            }
        }
        
        // Назначаем RawImage для отображения демо-сегментации
        if (debugImage != null)
        {
            SerializedObject serializedDemoDebug = new SerializedObject(demoWallSegmentation);
            SerializedProperty debugImageProp = serializedDemoDebug.FindProperty("debugImage");
            if (debugImageProp != null)
            {
                debugImageProp.objectReferenceValue = debugImage;
                serializedDemoDebug.ApplyModifiedProperties();
            }
        }
        
        // Добавляем компонент для принудительного включения демо-режима
        ForceDemoMode forceDemoMode = segmentationManagerObj.AddComponent<ForceDemoMode>();
        
        SerializedObject serializedForceDemo = new SerializedObject(forceDemoMode);
        SerializedProperty wallSegmentationProp = serializedForceDemo.FindProperty("wallSegmentation");
        if (wallSegmentationProp != null)
        {
            wallSegmentationProp.objectReferenceValue = wallSegmentation;
            serializedForceDemo.ApplyModifiedProperties();
        }
    }
    
    private static void CreateWallPainting(GameObject arRoot)
    {
        // Создаем материал для окраски стен
        string materialPath = "Assets/Materials/WallPaintMaterial.mat";
        Material wallPaintMaterial = null;
        
        if (!Directory.Exists("Assets/Materials"))
        {
            Directory.CreateDirectory("Assets/Materials");
        }
        
        if (!File.Exists(materialPath))
        {
            // Создаем новый материал
            Material material = new Material(Shader.Find("Unlit/Color"));
            material.color = new Color(1, 0, 1, 0.7f); // Розовый полупрозрачный
            AssetDatabase.CreateAsset(material, materialPath);
            AssetDatabase.SaveAssets();
            wallPaintMaterial = material;
        }
        else
        {
            wallPaintMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        }
        
        // Создаем объект для WallPainterManager
        GameObject painterManagerObj = new GameObject("WallPainterManager");
        painterManagerObj.transform.SetParent(arRoot.transform);
        
        // Добавляем компонент WallPainter
        WallPainter wallPainter = painterManagerObj.AddComponent<WallPainter>();
        
        // Находим необходимые компоненты AR и назначаем их
        XROrigin xrOrigin = arRoot.GetComponentInChildren<XROrigin>();
        ARRaycastManager raycastManager = xrOrigin?.GetComponent<ARRaycastManager>();
        WallSegmentation wallSegmentation = arRoot.GetComponentInChildren<WallSegmentation>();
        
        if (raycastManager == null && xrOrigin != null)
        {
            // Если ARRaycastManager не найден, добавляем его
            raycastManager = xrOrigin.gameObject.AddComponent<ARRaycastManager>();
            Debug.Log("Добавлен ARRaycastManager для WallPainter");
        }
        
        SerializedObject serializedObj = new SerializedObject(wallPainter);
        
        // Назначаем ARRaycastManager
        if (raycastManager != null)
        {
            SerializedProperty raycastManagerProp = serializedObj.FindProperty("raycastManager");
            if (raycastManagerProp != null)
            {
                raycastManagerProp.objectReferenceValue = raycastManager;
                Debug.Log("ARRaycastManager назначен для WallPainter");
            }
        }
        else
        {
            Debug.LogError("ARRaycastManager не найден и не может быть создан. Покраска стен не будет работать!");
        }
        
        // Назначаем XROrigin
        if (xrOrigin != null)
        {
            SerializedProperty xrOriginProp = serializedObj.FindProperty("xrOrigin");
            if (xrOriginProp != null)
            {
                xrOriginProp.objectReferenceValue = xrOrigin;
            }
        }
        
        // Назначаем AR Camera
        if (xrOrigin != null && xrOrigin.Camera != null)
        {
            SerializedProperty arCameraProp = serializedObj.FindProperty("arCamera");
            if (arCameraProp != null)
            {
                arCameraProp.objectReferenceValue = xrOrigin.Camera;
            }
        }
        
        // Назначаем WallSegmentation
        if (wallSegmentation != null)
        {
            SerializedProperty wallSegmentationProp = serializedObj.FindProperty("wallSegmentation");
            if (wallSegmentationProp != null)
            {
                wallSegmentationProp.objectReferenceValue = wallSegmentation;
            }
        }
        
        // Находим или создаем префаб для стены
        GameObject wallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PaintedWall.prefab");
        if (wallPrefab != null)
        {
            SerializedProperty wallPrefabProp = serializedObj.FindProperty("wallPrefab");
            if (wallPrefabProp != null)
            {
                wallPrefabProp.objectReferenceValue = wallPrefab;
            }
        }
        
        // Назначаем материал для окраски стен
        if (wallPaintMaterial != null)
        {
            SerializedProperty wallMaterialProp = serializedObj.FindProperty("wallMaterial");
            if (wallMaterialProp != null)
            {
                wallMaterialProp.objectReferenceValue = wallPaintMaterial;
            }
        }
        
        // Применяем изменения
        serializedObj.ApplyModifiedProperties();
    }
    
    private static void CreateHelperObjects(GameObject arRoot)
    {
        // Создаем объект, который не будет уничтожен при загрузке новой сцены
        // Важно: должен быть в корне сцены для работы DontDestroyOnLoad
        GameObject dontDestroyObj = new GameObject("DontDestroyOnLoad");
        // НЕ делаем его дочерним объектом, так как DontDestroyOnLoad работает только с объектами в корне сцены
        // Скрипт PersistentObject сам переместит объект в корень, если он не там
        dontDestroyObj.AddComponent<PersistentObject>();
        
        // Создаем симуляционную камеру для редактора
        GameObject simulationCameraObj = new GameObject("SimulationCamera");
        simulationCameraObj.transform.SetParent(arRoot.transform);
        Camera simulationCamera = simulationCameraObj.AddComponent<Camera>();
        
        // Добавляем контроллер для управления симуляционной камерой в редакторе
        simulationCameraObj.AddComponent<EditorCameraController>();
        
        // Назначаем начальную позицию и поворот камеры
        simulationCameraObj.transform.position = new Vector3(0, 1.6f, 0);
        simulationCameraObj.transform.rotation = Quaternion.identity;
        
        // Назначаем настройки симуляционной камеры
        simulationCamera.clearFlags = CameraClearFlags.SolidColor;
        simulationCamera.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1.0f);
        simulationCamera.depth = 0; // Рендерим поверх AR камеры
        
        // Находим объект управления камерами и назначаем симуляционную камеру
        CustomARCameraSetup cameraSetup = arRoot.GetComponent<CustomARCameraSetup>();
        if (cameraSetup != null)
        {
            SerializedObject serializedObj = new SerializedObject(cameraSetup);
            SerializedProperty simulationCameraProp = serializedObj.FindProperty("simulationCamera");
            if (simulationCameraProp != null)
            {
                simulationCameraProp.objectReferenceValue = simulationCamera;
                serializedObj.ApplyModifiedProperties();
            }
        }
        
        // Добавляем компонент для отображения диагностики AR
        ARDebugManager debugManager = arRoot.AddComponent<ARDebugManager>();
        
        // Находим все необходимые компоненты и назначаем их для дебаг-менеджера
        ARSession arSession = arRoot.GetComponentInChildren<ARSession>();
        XROrigin xrOrigin = arRoot.GetComponentInChildren<Unity.XR.CoreUtils.XROrigin>();
        ARPlaneManager planeManager = xrOrigin?.GetComponent<ARPlaneManager>();
        
        SerializedObject serializedDebug = new SerializedObject(debugManager);
        
        // Назначаем ARSession
        if (arSession != null)
        {
            SerializedProperty arSessionProp = serializedDebug.FindProperty("arSession");
            if (arSessionProp != null)
            {
                arSessionProp.objectReferenceValue = arSession;
            }
        }
        
        // Назначаем XROrigin
        if (xrOrigin != null)
        {
            SerializedProperty xrOriginProp = serializedDebug.FindProperty("xrOrigin");
            if (xrOriginProp != null)
            {
                xrOriginProp.objectReferenceValue = xrOrigin;
            }
        }
        
        // Назначаем ARPlaneManager
        if (planeManager != null)
        {
            SerializedProperty planeManagerProp = serializedDebug.FindProperty("planeManager");
            if (planeManagerProp != null)
            {
                planeManagerProp.objectReferenceValue = planeManager;
            }
        }
        
        serializedDebug.ApplyModifiedProperties();
        
        // Создаем объект для симуляции окружения (например, для AR Simulation)
        GameObject simulatedEnvObj = new GameObject("Simulated Environment Scene");
        simulatedEnvObj.transform.SetParent(arRoot.transform);
    }

    // Вспомогательный метод для добавления подходящего TrackedPoseDriver
    private static void AddAppropriateTrackedPoseDriver(GameObject targetObject)
    {
        // Сначала попробуем добавить новый Input System TrackedPoseDriver
        var newInputSystemType = System.Type.GetType("UnityEngine.InputSystem.XR.TrackedPoseDriver, Unity.InputSystem");
        if (newInputSystemType != null)
        {
            // Новый Input System доступен
            targetObject.AddComponent(newInputSystemType);
            Debug.Log("Добавлен TrackedPoseDriver (Input System)");
            return;
        }
        
        // Если новый Input System недоступен, пробуем добавить старый TrackedPoseDriver
        var oldInputSystemType = System.Type.GetType("UnityEngine.SpatialTracking.TrackedPoseDriver, UnityEngine.SpatialTracking");
        if (oldInputSystemType != null)
        {
            // Старый Input System доступен
            var driver = targetObject.AddComponent(oldInputSystemType);
            
            // Настраиваем через reflection, чтобы избежать ошибок компиляции
            try
            {
                var setPoseSourceMethod = oldInputSystemType.GetMethod("SetPoseSource", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                var deviceTypeEnum = System.Type.GetType("UnityEngine.SpatialTracking.TrackedPoseDriver+DeviceType, UnityEngine.SpatialTracking");
                var trackedPoseEnum = System.Type.GetType("UnityEngine.SpatialTracking.TrackedPoseDriver+TrackedPose, UnityEngine.SpatialTracking");
                
                if (setPoseSourceMethod != null && deviceTypeEnum != null && trackedPoseEnum != null)
                {
                    var genericXRDevice = System.Enum.Parse(deviceTypeEnum, "GenericXRDevice");
                    var centerEye = System.Enum.Parse(trackedPoseEnum, "Center");
                    setPoseSourceMethod.Invoke(driver, new object[] { genericXRDevice, centerEye });
                    
                    // Установка trackingType и updateType
                    var trackingTypeProperty = oldInputSystemType.GetProperty("trackingType");
                    var updateTypeProperty = oldInputSystemType.GetProperty("updateType");
                    var trackingTypeEnum = System.Type.GetType("UnityEngine.SpatialTracking.TrackedPoseDriver+TrackingType, UnityEngine.SpatialTracking");
                    var updateTypeEnum = System.Type.GetType("UnityEngine.SpatialTracking.TrackedPoseDriver+UpdateType, UnityEngine.SpatialTracking");
                    
                    if (trackingTypeProperty != null && trackingTypeEnum != null)
                    {
                        var rotationAndPosition = System.Enum.Parse(trackingTypeEnum, "RotationAndPosition");
                        trackingTypeProperty.SetValue(driver, rotationAndPosition);
                    }
                    
                    if (updateTypeProperty != null && updateTypeEnum != null)
                    {
                        var updateAndBeforeRender = System.Enum.Parse(updateTypeEnum, "UpdateAndBeforeRender");
                        updateTypeProperty.SetValue(driver, updateAndBeforeRender);
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Не удалось настроить TrackedPoseDriver: {e.Message}");
            }
            
            Debug.Log("Добавлен TrackedPoseDriver (Legacy)");
            return;
        }
        
        // Если оба варианта недоступны, выдаем предупреждение
        Debug.LogWarning("Не удалось добавить TrackedPoseDriver. Убедитесь, что установлен пакет Input System или XR Legacy Input Helpers.");
    }

    // Метод для проверки настройки компонентов после создания сцены
    private static void ValidateARSetup(GameObject arRoot)
    {
        Debug.Log("Проверка настройки AR компонентов:");
        
        // Проверяем наличие всех важных компонентов
        XROrigin xrOrigin = arRoot.GetComponentInChildren<XROrigin>();
        ARSession arSession = arRoot.GetComponentInChildren<ARSession>();
        ARPlaneManager planeManager = xrOrigin?.GetComponent<ARPlaneManager>();
        ARRaycastManager raycastManager = xrOrigin?.GetComponent<ARRaycastManager>();
        WallSegmentation wallSegmentation = arRoot.GetComponentInChildren<WallSegmentation>();
        WallPainter wallPainter = arRoot.GetComponentInChildren<WallPainter>();
        
        // Проверяем XROrigin и Camera
        if (xrOrigin != null)
        {
            Debug.Log("✓ XROrigin найден");
            
            if (xrOrigin.Camera != null)
            {
                Debug.Log("✓ AR Camera найдена");
                
                // Проверяем TrackedPoseDriver
                var trackedPoseDriver1 = xrOrigin.Camera.GetComponent("UnityEngine.InputSystem.XR.TrackedPoseDriver");
                var trackedPoseDriver2 = xrOrigin.Camera.GetComponent("UnityEngine.SpatialTracking.TrackedPoseDriver");
                
                if (trackedPoseDriver1 != null || trackedPoseDriver2 != null)
                {
                    Debug.Log("✓ TrackedPoseDriver найден на AR Camera");
                }
                else
                {
                    Debug.LogError("✗ TrackedPoseDriver НЕ найден на AR Camera! Позиция камеры не будет обновляться");
                }
            }
            else
            {
                Debug.LogError("✗ AR Camera НЕ назначена для XROrigin!");
            }
            
            // Проверяем CameraFloorOffsetObject
            if (xrOrigin.CameraFloorOffsetObject != null)
            {
                Debug.Log("✓ Camera Floor Offset найден и назначен");
            }
            else
            {
                Debug.LogWarning("⚠ Camera Floor Offset не назначен для XROrigin");
            }
        }
        else
        {
            Debug.LogError("✗ XROrigin не найден в сцене!");
        }
        
        // Проверяем AR Session
        if (arSession != null)
        {
            Debug.Log("✓ AR Session найден");
        }
        else
        {
            Debug.LogError("✗ AR Session не найден в сцене!");
        }
        
        // Проверяем ARPlaneManager
        if (planeManager != null)
        {
            Debug.Log("✓ ARPlaneManager найден");
            
            if (planeManager.planePrefab != null)
            {
                Debug.Log("✓ Префаб плоскости назначен для ARPlaneManager");
            }
            else
            {
                Debug.LogError("✗ Префаб плоскости НЕ назначен для ARPlaneManager! Плоскости не будут отображаться");
            }
        }
        else
        {
            Debug.LogError("✗ ARPlaneManager не найден на XROrigin!");
        }
        
        // Проверяем ARRaycastManager
        if (raycastManager != null)
        {
            Debug.Log("✓ ARRaycastManager найден");
        }
        else
        {
            Debug.LogError("✗ ARRaycastManager не найден на XROrigin! Покраска стен не будет работать");
        }
        
        // Проверяем Wall Segmentation
        if (wallSegmentation != null)
        {
            Debug.Log("✓ WallSegmentation найден");
            
            SerializedObject serializedObj = new SerializedObject(wallSegmentation);
            SerializedProperty cameraManagerProp = serializedObj.FindProperty("cameraManager");
            SerializedProperty debugImageProp = serializedObj.FindProperty("debugImage");
            SerializedProperty showDebugProp = serializedObj.FindProperty("showDebugVisualisation");
            
            if (cameraManagerProp != null && cameraManagerProp.objectReferenceValue != null)
            {
                Debug.Log("✓ ARCameraManager назначен для WallSegmentation");
            }
            else
            {
                Debug.LogError("✗ ARCameraManager НЕ назначен для WallSegmentation! Сегментация не будет работать");
            }
            
            if (debugImageProp != null && debugImageProp.objectReferenceValue != null)
            {
                Debug.Log("✓ Debug Image (RawImage) назначен для WallSegmentation");
            }
            else
            {
                Debug.LogWarning("⚠ Debug Image (RawImage) НЕ назначен для WallSegmentation! Отладочная визуализация не будет отображаться");
            }
            
            if (showDebugProp != null && showDebugProp.boolValue)
            {
                Debug.Log("✓ Отладочная визуализация включена в WallSegmentation");
            }
            else
            {
                Debug.LogWarning("⚠ Отладочная визуализация выключена в WallSegmentation");
            }
        }
        else
        {
            Debug.LogError("✗ WallSegmentation не найден в сцене!");
        }
        
        // Проверяем Wall Painter
        if (wallPainter != null)
        {
            Debug.Log("✓ WallPainter найден");
            
            SerializedObject serializedObj = new SerializedObject(wallPainter);
            SerializedProperty raycastManagerProp = serializedObj.FindProperty("raycastManager");
            SerializedProperty wallSegmentationProp = serializedObj.FindProperty("wallSegmentation");
            SerializedProperty wallPrefabProp = serializedObj.FindProperty("wallPrefab");
            SerializedProperty wallMaterialProp = serializedObj.FindProperty("wallMaterial");
            
            if (raycastManagerProp != null && raycastManagerProp.objectReferenceValue != null)
            {
                Debug.Log("✓ ARRaycastManager назначен для WallPainter");
            }
            else
            {
                Debug.LogError("✗ ARRaycastManager НЕ назначен для WallPainter! Покраска стен не будет работать");
            }
            
            if (wallSegmentationProp != null && wallSegmentationProp.objectReferenceValue != null)
            {
                Debug.Log("✓ WallSegmentation назначен для WallPainter");
            }
            else
            {
                Debug.LogError("✗ WallSegmentation НЕ назначен для WallPainter! Покраска стен не будет работать");
            }
            
            if (wallPrefabProp != null && wallPrefabProp.objectReferenceValue != null)
            {
                Debug.Log("✓ Префаб стены назначен для WallPainter");
            }
            else
            {
                Debug.LogError("✗ Префаб стены НЕ назначен для WallPainter! Покраска стен не будет работать");
            }
            
            if (wallMaterialProp != null && wallMaterialProp.objectReferenceValue != null)
            {
                Debug.Log("✓ Материал стены назначен для WallPainter");
            }
            else
            {
                Debug.LogError("✗ Материал стены НЕ назначен для WallPainter! Стены будут пурпурными");
            }
        }
        else
        {
            Debug.LogError("✗ WallPainter не найден в сцене!");
        }
    }
} 