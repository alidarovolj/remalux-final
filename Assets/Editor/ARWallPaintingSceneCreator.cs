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

        // 5. Добавляем компонент WallSegmentation2D для 2D визуализации стен
        GameObject wallSegmentation2DObj = new GameObject("WallSegmentation2D");
        wallSegmentation2DObj.transform.SetParent(arRootObject.transform);
        WallSegmentation2D wallSegmentation2D = wallSegmentation2DObj.AddComponent<WallSegmentation2D>();

        // Назначаем камеру для WallSegmentation2D
        XROrigin xrOrigin = arRootObject.GetComponentInChildren<XROrigin>();
        if (xrOrigin != null && xrOrigin.Camera != null)
        {
            ARCameraManager cameraManager = xrOrigin.Camera.GetComponent<ARCameraManager>();
            if (cameraManager != null)
            {
                SerializedObject serializedWall2D = new SerializedObject(wallSegmentation2D);
                SerializedProperty cameraManagerProp = serializedWall2D.FindProperty("cameraManager");
                if (cameraManagerProp != null)
                {
                    cameraManagerProp.objectReferenceValue = cameraManager;
                    serializedWall2D.ApplyModifiedProperties();
                    Debug.Log("ARCameraManager успешно назначен для WallSegmentation2D");
                }
            }
        }

        // 6. Добавляем настройщик 2D-окраски стен
        GameObject setupObject = new GameObject("WallPaintingSetup");
        setupObject.transform.SetParent(arRootObject.transform);
        WallPaintingSetup wallPaintingSetup = setupObject.AddComponent<WallPaintingSetup>();

        if (wallPaintingSetup != null)
        {
            // Начальная настройка
            wallPaintingSetup.paintColor = new Color(0.85f, 0.1f, 0.1f, 1.0f); // Красный
            wallPaintingSetup.paintOpacity = 0.7f;
            wallPaintingSetup.preserveShadows = 0.8f;

            // Автоматически настраиваем при старте
            wallPaintingSetup.autoSetupOnStart = true;

            Debug.Log("Добавлен компонент WallPaintingSetup для настройки 2D-визуализации стен");
        }

        // 7. Добавляем палитру цветов
        GameObject colorPaletteObj = new GameObject("ColorPalette");
        colorPaletteObj.transform.SetParent(arRootObject.transform);
        ColorPalette colorPalette = colorPaletteObj.AddComponent<ColorPalette>();

        // Связываем с настройщиком
        if (colorPalette != null && wallPaintingSetup != null)
        {
            SerializedObject serializedColorPalette = new SerializedObject(colorPalette);
            serializedColorPalette.FindProperty("wallPaintingSetup").objectReferenceValue = wallPaintingSetup;
            serializedColorPalette.ApplyModifiedProperties();

            Debug.Log("Добавлен компонент ColorPalette для выбора цветов покраски");
        }

        // 8. Создаем EventSystem для работы UI, если его еще нет
        if (GameObject.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystemObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            Debug.Log("Создан EventSystem для обработки UI событий");
        }

        // 9. Создаем симуляционную камеру для тестирования в редакторе
        GameObject simulationCameraObj = new GameObject("SimulationCamera");
        simulationCameraObj.transform.SetParent(arRootObject.transform);
        Camera simulationCamera = simulationCameraObj.AddComponent<Camera>();
        simulationCameraObj.AddComponent<EditorCameraController>();

        // Настройка камеры
        simulationCameraObj.transform.position = new Vector3(0, 1.6f, 0);
        simulationCamera.clearFlags = CameraClearFlags.SolidColor;
        simulationCamera.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1.0f);

        Debug.Log("Добавлена симуляционная камера для тестирования в редакторе");

        // 10. Исправляем WallVisualization для предотвращения проблемы белого экрана
        GameObject wallVisObj = GameObject.Find("WallVisualization");
        bool wallVisualizationCreated = false;

        // Если не нашли точно по имени, ищем объект, содержащий "WallVisualization" в имени
        if (wallVisObj == null)
        {
            foreach (GameObject obj in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (obj.name.Contains("WallVisualization"))
                {
                    wallVisObj = obj;
                    break;
                }
            }

            // Если все еще не нашли, проверяем Canvas на наличие RawImage
            if (wallVisObj == null && mainCanvas != null)
            {
                foreach (Transform child in mainCanvas.transform)
                {
                    if (child.GetComponent<RawImage>() != null)
                    {
                        wallVisObj = child.gameObject;
                        Debug.Log($"Найден объект {wallVisObj.name} с RawImage под Canvas");
                        break;
                    }
                }
            }
        }

        // Создаем новый WallVisualization, если не нашли существующий
        if (wallVisObj == null)
        {
            Debug.Log("WallVisualization не найден. Создаем новый...");

            // Создаем объект
            wallVisObj = new GameObject("WallVisualization");

            // Если есть canvas, добавляем как дочерний объект
            if (mainCanvas != null)
            {
                wallVisObj.transform.SetParent(mainCanvas.transform, false);
            }

            // Добавляем и настраиваем RectTransform
            RectTransform rectTransform = wallVisObj.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);

            // Добавляем RawImage
            RawImage rawImage = wallVisObj.AddComponent<RawImage>();
            rawImage.color = new Color(1f, 1f, 1f, 0f); // Полностью прозрачный

            wallVisualizationCreated = true;
        }

        // Настраиваем WallVisualization
        if (wallVisObj != null)
        {
            Debug.Log($"Настраиваем WallVisualization объект: {wallVisObj.name}");

            // Получаем компонент RawImage и исправляем его
            RawImage rawImage = wallVisObj.GetComponent<RawImage>();
            if (rawImage != null)
            {
                // Устанавливаем прозрачность
                rawImage.color = new Color(1f, 1f, 1f, 0f);

                // Проверяем и настраиваем материал
                if (rawImage.material == null)
                {
                    // Пытаемся найти шейдер WallPaint
                    Shader wallPaintShader = Shader.Find("Custom/WallPaint");
                    if (wallPaintShader != null)
                    {
                        Material material = new Material(wallPaintShader);
                        rawImage.material = material;
                        Debug.Log("Создан новый материал с шейдером WallPaint");
                    }
                    else
                    {
                        // Если не нашли нужный шейдер, проверяем другие варианты
                        Shader alternativeShader = Shader.Find("Custom/WallPainting");
                        if (alternativeShader == null)
                            alternativeShader = Shader.Find("UI/Default");

                        if (alternativeShader != null)
                        {
                            Material material = new Material(alternativeShader);
                            rawImage.material = material;
                            Debug.Log($"Создан новый материал с шейдером {alternativeShader.name}");
                        }
                    }
                }
            }
            else
            {
                Debug.LogError("WallVisualization не содержит компонент RawImage!");
            }

            // Проверяем и настраиваем WallPaintingTextureUpdater
            WallPaintingTextureUpdater updater = wallVisObj.GetComponent<WallPaintingTextureUpdater>();
            if (updater == null)
            {
                updater = wallVisObj.AddComponent<WallPaintingTextureUpdater>();
                Debug.Log("Добавлен WallPaintingTextureUpdater на WallVisualization");
            }

            // Настраиваем параметры
            updater.useTemporaryMask = true;
            updater.paintColor = new Color(0.85f, 0.1f, 0.1f, 1.0f);
            updater.paintOpacity = 0.7f;
            updater.preserveShadows = 0.8f;

            // Добавляем WallVisualizationManager
            WallVisualizationManager manager = wallVisObj.GetComponent<WallVisualizationManager>();
            if (manager == null)
            {
                manager = wallVisObj.AddComponent<WallVisualizationManager>();
                Debug.Log("Добавлен компонент WallVisualizationManager для предотвращения проблемы белого экрана");
            }
        }

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

        // Создаем RawImage для визуализации стен
        GameObject wallVisualizationObj = new GameObject("WallVisualization");
        wallVisualizationObj.transform.SetParent(canvasObj.transform);
        RectTransform visualizationRect = wallVisualizationObj.AddComponent<RectTransform>();
        visualizationRect.anchorMin = Vector2.zero;
        visualizationRect.anchorMax = Vector2.one;
        visualizationRect.offsetMin = Vector2.zero;
        visualizationRect.offsetMax = Vector2.zero;

        RawImage wallVisualizationImage = wallVisualizationObj.AddComponent<RawImage>();
        wallVisualizationImage.color = Color.white;

        // Создаем кнопку переключения цветов (только интерфейс, функциональность будет добавлена в ColorPalette)
        GameObject colorButtonsContainer = new GameObject("ColorButtonsContainer");
        colorButtonsContainer.transform.SetParent(canvasObj.transform);
        RectTransform colorButtonsRect = colorButtonsContainer.AddComponent<RectTransform>();
        colorButtonsRect.anchorMin = new Vector2(0, 0);
        colorButtonsRect.anchorMax = new Vector2(1, 0.15f);
        colorButtonsRect.offsetMin = Vector2.zero;
        colorButtonsRect.offsetMax = Vector2.zero;

        // Добавляем горизонтальную группу для автоматического размещения кнопок
        HorizontalLayoutGroup layoutGroup = colorButtonsContainer.AddComponent<HorizontalLayoutGroup>();
        layoutGroup.spacing = 10f;
        layoutGroup.childAlignment = TextAnchor.MiddleCenter;
        layoutGroup.childForceExpandWidth = false;
        layoutGroup.childForceExpandHeight = false;
        layoutGroup.padding = new RectOffset(10, 10, 10, 10);

        return canvas;
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

        // Отключаем принудительный демо-режим
        serializedWallSegmentation.FindProperty("forceDemoMode").boolValue = false;

        // Включаем отладочные логи
        serializedWallSegmentation.FindProperty("enableDebugLogs").boolValue = true;

        // Применяем настройки
        serializedWallSegmentation.ApplyModifiedProperties();

        // Выводим сообщение о настройке
        Debug.Log("WallSegmentation настроен на режим ExternalModel с моделью model.onnx");

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

        // В релизных сборках можно отключить демо-компоненты 
        bool isDebugBuild = true; // В релизе заменить на false
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        isDebugBuild = true;
#endif

        // Добавляем DemoWallSegmentation только в режиме отладки
        if (isDebugBuild)
        {
            // Добавляем также компонент DemoWallSegmentation для отладки
            DemoWallSegmentation demoWallSegmentation = segmentationManagerObj.AddComponent<DemoWallSegmentation>();

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

            Debug.Log("Добавлен компонент DemoWallSegmentation для отладки");
        }
        else
        {
            Debug.Log("Компонент DemoWallSegmentation пропущен в релизной сборке");
        }

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
    }

    private static void CreateWallPainting(GameObject arRoot)
    {
        // Создаем материал для окраски стен с использованием WallPainting шейдера
        string materialPath = "Assets/Materials/WallPaintMaterial.mat";
        Material wallPaintMaterial = null;

        if (!Directory.Exists("Assets/Materials"))
        {
            Directory.CreateDirectory("Assets/Materials");
        }

        // Проверяем существование шейдера WallPainting
        Shader wallPaintingShader = Shader.Find("Custom/WallPainting");
        if (wallPaintingShader == null)
        {
            Debug.LogWarning("Шейдер 'Custom/WallPainting' не найден! Будет использован стандартный Unlit/Color шейдер.");
            wallPaintingShader = Shader.Find("Unlit/Color");
        }
        else
        {
            Debug.Log("Найден шейдер 'Custom/WallPainting' для визуализации покраски стен в 2D");
        }

        if (!File.Exists(materialPath))
        {
            // Создаем новый материал с шейдером WallPainting (если доступен)
            Material material = new Material(wallPaintingShader);
            if (wallPaintingShader.name == "Custom/WallPainting")
            {
                // Настраиваем специфичные для WallPainting свойства
                material.SetColor("_PaintColor", new Color(0.85f, 0.1f, 0.1f, 1.0f)); // Красный
                material.SetFloat("_PaintOpacity", 0.7f);
                material.SetFloat("_PreserveShadows", 0.8f);
            }
            else
            {
                material.color = new Color(1, 0, 1, 0.7f); // Розовый полупрозрачный для запасного варианта
            }

            AssetDatabase.CreateAsset(material, materialPath);
            AssetDatabase.SaveAssets();
            wallPaintMaterial = material;
        }
        else
        {
            wallPaintMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

            // Обновляем шейдер материала, если он уже существует, но использует другой шейдер
            if (wallPaintingShader != null && wallPaintMaterial.shader.name != "Custom/WallPainting")
            {
                wallPaintMaterial.shader = wallPaintingShader;
                EditorUtility.SetDirty(wallPaintMaterial);
                AssetDatabase.SaveAssets();
            }
        }

        // Создаем объект для WallPainterManager
        GameObject painterManagerObj = new GameObject("WallPainterManager");
        painterManagerObj.transform.SetParent(arRoot.transform);

        // Добавляем компонент WallPainter
        WallPainter wallPainter = painterManagerObj.AddComponent<WallPainter>();

        // Создаем объект для 2D визуализации стен
        GameObject wallSegmentation2DObj = new GameObject("WallSegmentation2D");
        wallSegmentation2DObj.transform.SetParent(arRoot.transform);

        // Добавляем компонент WallSegmentation2D
        try
        {
            var wallSegmentation2D = wallSegmentation2DObj.AddComponent<WallSegmentation2D>();
            Debug.Log("Добавлен компонент WallSegmentation2D для 2D-визуализации стен");

            // Находим ARCameraManager
            XROrigin arXROrigin = arRoot.GetComponentInChildren<XROrigin>();
            ARCameraManager cameraManager = arXROrigin?.Camera.GetComponent<ARCameraManager>();

            // Назначаем камеру для WallSegmentation2D
            if (cameraManager != null)
            {
                SerializedObject serializedWall2D = new SerializedObject(wallSegmentation2D);
                SerializedProperty cameraManagerProp = serializedWall2D.FindProperty("cameraManager");
                if (cameraManagerProp != null)
                {
                    cameraManagerProp.objectReferenceValue = cameraManager;
                    serializedWall2D.ApplyModifiedProperties();
                    Debug.Log("ARCameraManager успешно назначен для WallSegmentation2D");
                }
            }

            // Добавляем настройщик 2D-окраски стен
            GameObject setupObject = new GameObject("WallPaintingSetup");
            setupObject.transform.SetParent(arRoot.transform);
            WallPaintingSetup wallPaintingSetup = setupObject.AddComponent<WallPaintingSetup>();

            if (wallPaintingSetup != null)
            {
                // Начальная настройка
                wallPaintingSetup.paintColor = new Color(0.85f, 0.1f, 0.1f, 1.0f); // Красный
                wallPaintingSetup.paintOpacity = 0.7f;
                wallPaintingSetup.preserveShadows = 0.8f;

                // Автоматически настраиваем при старте (на всякий случай)
                wallPaintingSetup.autoSetupOnStart = true;

                Debug.Log("Добавлен компонент WallPaintingSetup для настройки 2D-визуализации стен");

                // Добавляем палитру цветов
                GameObject colorPaletteObj = new GameObject("ColorPalette");
                colorPaletteObj.transform.SetParent(arRoot.transform);
                ColorPalette colorPalette = colorPaletteObj.AddComponent<ColorPalette>();

                // Связываем с настройщиком
                if (colorPalette != null && wallPaintingSetup != null)
                {
                    SerializedObject serializedColorPalette = new SerializedObject(colorPalette);
                    serializedColorPalette.FindProperty("wallPaintingSetup").objectReferenceValue = wallPaintingSetup;
                    serializedColorPalette.ApplyModifiedProperties();

                    Debug.Log("Добавлен компонент ColorPalette для выбора цветов покраски");
                }
            }

            // Создаем объект для отображения результата
            GameObject wallPaintingScreenObj = new GameObject("WallPaintingScreen");
            wallPaintingScreenObj.transform.SetParent(arXROrigin.Camera.transform);
            wallPaintingScreenObj.transform.localPosition = new Vector3(0, 0, 0.5f);
            wallPaintingScreenObj.transform.localRotation = Quaternion.identity;
            wallPaintingScreenObj.transform.localScale = new Vector3(1, 1, 1);

            RawImage wallPaintingImage = wallPaintingScreenObj.AddComponent<RawImage>();

            // Создаем материал для отображения покраски
            Material screenMaterial = new Material(wallPaintMaterial);
            wallPaintingImage.material = screenMaterial;

            // Размещаем изображение по размеру экрана
            RectTransform rectTransform = wallPaintingImage.rectTransform;
            rectTransform.sizeDelta = new Vector2(1, 1);
            rectTransform.anchorMin = new Vector2(0, 0);
            rectTransform.anchorMax = new Vector2(1, 1);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);

            // Подключаем WallSegmentation2D к материалу
            if (wallSegmentation2D != null && screenMaterial != null && screenMaterial.shader.name == "Custom/WallPainting")
            {
                // Настраиваем материал на использование камеры и маски сегментации
                screenMaterial.SetTexture("_MainTex", arXROrigin.Camera.targetTexture);

                // Создаем скрипт для обновления текстуры
                GameObject textureUpdaterObj = new GameObject("TextureUpdater");
                textureUpdaterObj.transform.SetParent(wallPaintingScreenObj.transform);

                // Добавляем скрипт для обновления текстуры (автоматически создаст MonoBehaviour)
                MonoBehaviour textureUpdater = textureUpdaterObj.AddComponent<MonoBehaviour>();

                // Настраиваем скрипт через редактор
                string scriptPath = "Assets/Scripts/WallPaintingTextureUpdater.cs";
                if (!File.Exists(scriptPath))
                {
                    string scriptContent = @"
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class WallPaintingTextureUpdater : MonoBehaviour 
{
    public WallSegmentation2D wallSegmentation2D;
    private Material material;
    private RawImage rawImage;
    
    void Start() 
    {
        rawImage = GetComponent<RawImage>();
        if (rawImage != null) {
            material = rawImage.material;
        }
    }
    
    void Update() 
    {
        if (wallSegmentation2D != null && material != null) 
        {
            // Обновляем текстуру маски сегментации
            material.SetTexture(""_MaskTex"", wallSegmentation2D.MaskTexture);
        }
    }
}";

                    // Создаем директорию, если ее нет
                    string directory = Path.GetDirectoryName(scriptPath);
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    // Записываем содержимое скрипта
                    File.WriteAllText(scriptPath, scriptContent);
                    AssetDatabase.Refresh();

                    Debug.Log("Создан скрипт WallPaintingTextureUpdater для обновления текстуры");
                }

                // Ждем немного, чтобы Unity распознала новый скрипт
                EditorApplication.delayCall += () =>
                {
                    // Пытаемся найти WallPaintingTextureUpdater компонент или добавить его
                    var updaterType = System.Type.GetType("WallPaintingTextureUpdater, Assembly-CSharp");
                    MonoBehaviour updaterComponent = null;

                    if (updaterType != null)
                    {
                        // Удаляем временный MonoBehaviour
                        DestroyImmediate(textureUpdater);

                        // Добавляем компонент нужного типа
                        updaterComponent = textureUpdaterObj.AddComponent(updaterType) as MonoBehaviour;
                        Debug.Log("WallPaintingTextureUpdater компонент успешно добавлен");
                    }
                    else
                    {
                        // Если тип не найден, используем временный компонент как заглушку
                        updaterComponent = textureUpdater;
                        Debug.LogWarning("Скрипт WallPaintingTextureUpdater не был скомпилирован. Перезапустите процесс создания сцены после компиляции скрипта.");
                    }

                    // Настраиваем связь с WallSegmentation2D
                    if (updaterComponent != null)
                    {
                        // Используем SerializedObject для установки свойств
                        SerializedObject serializedUpdater = new SerializedObject(updaterComponent);
                        var segmentationProp = serializedUpdater.FindProperty("wallSegmentation2D");
                        if (segmentationProp != null)
                        {
                            segmentationProp.objectReferenceValue = wallSegmentation2D;
                            serializedUpdater.ApplyModifiedProperties();
                            Debug.Log("Связь между WallPaintingTextureUpdater и WallSegmentation2D установлена");
                        }
                    }
                };
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Не удалось добавить WallSegmentation2D: {e.Message}");
            DestroyImmediate(wallSegmentation2DObj);
        }

        // Продолжаем настройку стандартного WallPainter
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