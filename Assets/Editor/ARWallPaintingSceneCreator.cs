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
        
        // 4. Создаем UI для приложения
        CreateARUI(arRootObject);
        
        // 5. Добавляем систему сегментации стен
        CreateWallSegmentation(arRootObject);
        
        // 6. Добавляем систему окраски стен
        CreateWallPainting(arRootObject);
        
        // 7. Создаем дополнительные вспомогательные объекты
        CreateHelperObjects(arRootObject);
        
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
        
        // Назначаем камеру для XR Origin
        xrOrigin.Camera = camera;
        
        // Создаем Camera Floor Offset
        GameObject offsetObj = new GameObject("Camera Floor Offset");
        offsetObj.transform.SetParent(xrOriginObj.transform);
        
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
            
            // Получаем MeshFilter из временного объекта
            MeshFilter meshFilter = tempMeshObj.GetComponent<MeshFilter>();
            
            // Назначаем meshPrefab
            meshManager.meshPrefab = meshFilter;
            
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
    
    private static void CreateARUI(GameObject arRoot)
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
        
        // Создаем RawImage для отображения сегментации стен
        GameObject rawImageObj = new GameObject("RawImage");
        rawImageObj.transform.SetParent(canvasObj.transform);
        RectTransform rawImageRect = rawImageObj.AddComponent<RectTransform>();
        rawImageRect.anchorMin = new Vector2(0.05f, 0.7f);
        rawImageRect.anchorMax = new Vector2(0.35f, 0.95f);
        rawImageRect.offsetMin = Vector2.zero;
        rawImageRect.offsetMax = Vector2.zero;
        
        RawImage rawImage = rawImageObj.AddComponent<RawImage>();
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
        
        // Находим AR камеру и назначаем ее
        XROrigin xrOrigin = arRoot.GetComponentInChildren<XROrigin>();
        if (xrOrigin != null && xrOrigin.Camera != null)
        {
            ARCameraManager cameraManager = xrOrigin.Camera.GetComponent<ARCameraManager>();
            if (cameraManager != null)
            {
                SerializedObject serializedObj = new SerializedObject(wallSegmentation);
                SerializedProperty cameraManagerProp = serializedObj.FindProperty("cameraManager");
                if (cameraManagerProp != null)
                {
                    cameraManagerProp.objectReferenceValue = cameraManager;
                    serializedObj.ApplyModifiedProperties();
                }
                
                SerializedProperty arCameraProp = serializedObj.FindProperty("arCamera");
                if (arCameraProp != null)
                {
                    arCameraProp.objectReferenceValue = xrOrigin.Camera;
                    serializedObj.ApplyModifiedProperties();
                }
            }
        }
        
        // Находим RawImage для отображения отладки
        RawImage debugImage = GameObject.Find("RawImage")?.GetComponent<RawImage>();
        if (debugImage != null)
        {
            SerializedObject serializedObj = new SerializedObject(wallSegmentation);
            SerializedProperty debugImageProp = serializedObj.FindProperty("debugImage");
            if (debugImageProp != null)
            {
                debugImageProp.objectReferenceValue = debugImage;
                serializedObj.ApplyModifiedProperties();
            }
        }
        
        // Добавляем также компонент DemoWallSegmentation
        DemoWallSegmentation demoWallSegmentation = segmentationManagerObj.AddComponent<DemoWallSegmentation>();
        
        // Назначаем AR плоскости и камеру для демо-сегментации
        ARPlaneManager planeManager = arRoot.GetComponentInChildren<ARPlaneManager>();
        if (planeManager != null)
        {
            SerializedObject serializedObj = new SerializedObject(demoWallSegmentation);
            SerializedProperty planeManagerProp = serializedObj.FindProperty("planeManager");
            if (planeManagerProp != null)
            {
                planeManagerProp.objectReferenceValue = planeManager;
                serializedObj.ApplyModifiedProperties();
            }
        }
        
        // Назначаем ARCameraManager для демо-сегментации
        if (xrOrigin != null && xrOrigin.Camera != null)
        {
            ARCameraManager cameraManager = xrOrigin.Camera.GetComponent<ARCameraManager>();
            if (cameraManager != null)
            {
                SerializedObject serializedObj = new SerializedObject(demoWallSegmentation);
                SerializedProperty cameraManagerProp = serializedObj.FindProperty("cameraManager");
                if (cameraManagerProp != null)
                {
                    cameraManagerProp.objectReferenceValue = cameraManager;
                    serializedObj.ApplyModifiedProperties();
                }
            }
        }
        
        // Назначаем RawImage для отображения демо-сегментации
        if (debugImage != null)
        {
            SerializedObject serializedObj = new SerializedObject(demoWallSegmentation);
            SerializedProperty debugImageProp = serializedObj.FindProperty("debugImage");
            if (debugImageProp != null)
            {
                debugImageProp.objectReferenceValue = debugImage;
                serializedObj.ApplyModifiedProperties();
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
        ARRaycastManager raycastManager = arRoot.GetComponentInChildren<ARRaycastManager>();
        WallSegmentation wallSegmentation = arRoot.GetComponentInChildren<WallSegmentation>();
        
        SerializedObject serializedObj = new SerializedObject(wallPainter);
        
        // Назначаем ARRaycastManager
        if (raycastManager != null)
        {
            SerializedProperty raycastManagerProp = serializedObj.FindProperty("raycastManager");
            if (raycastManagerProp != null)
            {
                raycastManagerProp.objectReferenceValue = raycastManager;
            }
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
        GameObject dontDestroyObj = new GameObject("DontDestroyOnLoad");
        dontDestroyObj.transform.SetParent(arRoot.transform);
        // Добавляем компонент, который вызывает DontDestroyOnLoad в Awake
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
} 