using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine.XR.ARFoundation;
using UnityEngine.UI;
using Unity.XR.CoreUtils;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

/// <summary>
/// Утилита для автоматизации настройки проекта AR Wall Painting
/// </summary>
public static class SetupUtility
{
    private const string prefabsPath = "Assets/Prefabs";
    private const string materialsPath = "Assets/Materials";
    private const string shadersPath = "Assets/Shaders";
    
    [MenuItem("Tools/AR/Setup AR Wall Painting Project")]
    public static void SetupProject()
    {
        // Убеждаемся, что используем правильную сцену
        if (!EditorSceneManager.GetActiveScene().name.Contains("ARWallPainting"))
        {
            bool result = EditorUtility.DisplayDialog(
                "Create New Scene?",
                "It's recommended to use the AR Wall Painting scene. Would you like to create a new AR scene now?",
                "Create AR Scene",
                "Use Current Scene"
            );
            
            if (result)
            {
                EditorApplication.ExecuteMenuItem("Tools/AR/Create AR Wall Painting Scene");
                // Ждем немного, чтобы сцена успела создаться
                EditorApplication.delayCall += () => ContinueSetup();
                return;
            }
        }
        
        ContinueSetup();
    }
    
    private static void ContinueSetup()
    {
        // Создаем материал для покраски стен
        CreateWallPaintMaterial();
        
        // Настраиваем основные объекты сцены
        SetupSceneObjects();
        
        // Сохраняем сцену
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        
        EditorUtility.DisplayDialog(
            "Setup Complete",
            "AR Wall Painting project has been set up successfully. You may need to manually adjust UI elements and references.",
            "OK"
        );
    }
    
    private static void CreateWallPaintMaterial()
    {
        // Убеждаемся, что директория существует
        if (!Directory.Exists(materialsPath))
        {
            Directory.CreateDirectory(materialsPath);
        }
        
        // Путь к материалу
        string materialAssetPath = Path.Combine(materialsPath, "WallPaintMaterial.mat");
        
        // Проверяем, существует ли файл материала
        if (File.Exists(materialAssetPath))
        {
            // Удаляем поврежденный материал
            AssetDatabase.DeleteAsset(materialAssetPath);
            AssetDatabase.Refresh();
            Debug.Log("Deleted existing material at: " + materialAssetPath);
        }
        
        // Проверяем, существует ли шейдер
        Shader wallPaintShader = Shader.Find("Custom/WallPaint");
        if (wallPaintShader == null)
        {
            Debug.LogError("Custom/WallPaint shader not found. Make sure it's properly imported.");
            return;
        }
        
        // Создаем новый материал
        Material wallPaintMaterial = new Material(wallPaintShader);
        wallPaintMaterial.color = new Color(1f, 1f, 1f, 0.5f);
        wallPaintMaterial.SetFloat("_Glossiness", 0.2f);
        wallPaintMaterial.SetFloat("_Metallic", 0.0f);
        
        // Сохраняем материал в проекте
        AssetDatabase.CreateAsset(wallPaintMaterial, materialAssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        Debug.Log("Wall Paint Material created at: " + materialAssetPath);
    }
    
    private static void SetupSceneObjects()
    {
        // Убеждаемся, что в сцене есть необходимые AR компоненты
        EnsureARComponents();
        
        // Создаем объект AppController для управления приложением
        GameObject appController = GameObject.Find("AppController");
        if (appController == null)
        {
            appController = new GameObject("AppController");
            ARWallPaintingApp appScript = appController.AddComponent<ARWallPaintingApp>();
            
            // Находим AR компоненты и ссылаемся на них
            ARSession arSession = Object.FindObjectOfType<ARSession>();
            ARSessionOrigin arSessionOrigin = Object.FindObjectOfType<ARSessionOrigin>();
            ARPlaneManager planeManager = Object.FindObjectOfType<ARPlaneManager>();
            ARRaycastManager raycastManager = Object.FindObjectOfType<ARRaycastManager>();
            
            if (arSessionOrigin != null && raycastManager == null)
            {
                raycastManager = arSessionOrigin.gameObject.AddComponent<ARRaycastManager>();
            }
            
            // Настраиваем ссылки
            if (appScript != null)
            {
                SerializedObject serializedObject = new SerializedObject(appScript);
                serializedObject.FindProperty("arSession").objectReferenceValue = arSession;
                serializedObject.FindProperty("arSessionOrigin").objectReferenceValue = arSessionOrigin;
                serializedObject.FindProperty("planeManager").objectReferenceValue = planeManager;
                serializedObject.FindProperty("raycastManager").objectReferenceValue = raycastManager;
                
                // Настраиваем материал
                string materialAssetPath = Path.Combine(materialsPath, "WallPaintMaterial.mat");
                Material wallPaintMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialAssetPath);
                if (wallPaintMaterial != null)
                {
                    serializedObject.FindProperty("wallPaintMaterial").objectReferenceValue = wallPaintMaterial;
                }
                
                serializedObject.ApplyModifiedProperties();
            }
        }
        
        // Создаем объект для сегментации стен
        GameObject wallSegmentationObj = GameObject.Find("WallSegmentationManager");
        if (wallSegmentationObj == null)
        {
            wallSegmentationObj = new GameObject("WallSegmentationManager");
            
            // Решаем, какой тип сегментации использовать
            bool useDummySegmentation = EditorUtility.DisplayDialog(
                "Segmentation Type",
                "Which wall segmentation implementation would you like to use?",
                "Demo (No ML model)",
                "Real (Requires ONNX model)"
            );
            
            if (useDummySegmentation)
            {
                DemoWallSegmentation demoSegmentation = wallSegmentationObj.AddComponent<DemoWallSegmentation>();
                
                // Настраиваем ссылки
                ARPlaneManager planeManager = Object.FindObjectOfType<ARPlaneManager>();
                ARCameraManager cameraManager = Object.FindObjectOfType<ARCameraManager>();
                
                SerializedObject serializedObject = new SerializedObject(demoSegmentation);
                serializedObject.FindProperty("planeManager").objectReferenceValue = planeManager;
                serializedObject.FindProperty("cameraManager").objectReferenceValue = cameraManager;
                serializedObject.ApplyModifiedProperties();
            }
            else
            {
                WallSegmentation segmentation = wallSegmentationObj.AddComponent<WallSegmentation>();
                
                // Настраиваем ссылки
                ARCameraManager cameraManager = Object.FindObjectOfType<ARCameraManager>();
                ARSessionOrigin sessionOrigin = Object.FindObjectOfType<ARSessionOrigin>();
                
                SerializedObject serializedObject = new SerializedObject(segmentation);
                serializedObject.FindProperty("cameraManager").objectReferenceValue = cameraManager;
                serializedObject.FindProperty("sessionOrigin").objectReferenceValue = sessionOrigin;
                serializedObject.ApplyModifiedProperties();
            }
        }
        
        // Создаем объект для покраски стен
        GameObject wallPainterObj = GameObject.Find("WallPainterManager");
        if (wallPainterObj == null)
        {
            wallPainterObj = new GameObject("WallPainterManager");
            WallPainter wallPainter = wallPainterObj.AddComponent<WallPainter>();
            
            // Настраиваем ссылки
            ARRaycastManager raycastManager = Object.FindObjectOfType<ARRaycastManager>();
            ARSessionOrigin sessionOrigin = Object.FindObjectOfType<ARSessionOrigin>();
            Camera arCamera = sessionOrigin?.camera;
            
            // Ищем компонент сегментации (любого типа)
            MonoBehaviour wallSegmentation = Object.FindObjectOfType<WallSegmentation>() as MonoBehaviour;
            if (wallSegmentation == null)
            {
                wallSegmentation = Object.FindObjectOfType<DemoWallSegmentation>() as MonoBehaviour;
            }
            
            // Находим префаб стены
            string prefabPath = Path.Combine(prefabsPath, "PaintedWall.prefab");
            GameObject wallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            
            // Настраиваем ссылки
            SerializedObject serializedObject = new SerializedObject(wallPainter);
            serializedObject.FindProperty("raycastManager").objectReferenceValue = raycastManager;
            serializedObject.FindProperty("sessionOrigin").objectReferenceValue = sessionOrigin;
            serializedObject.FindProperty("arCamera").objectReferenceValue = arCamera;
            serializedObject.FindProperty("wallSegmentation").objectReferenceValue = wallSegmentation;
            
            if (wallPrefab != null)
            {
                serializedObject.FindProperty("wallPrefab").objectReferenceValue = wallPrefab;
            }
            else
            {
                Debug.LogWarning("PaintedWall prefab not found at: " + prefabPath);
            }
            
            serializedObject.ApplyModifiedProperties();
        }
        
        // Настраиваем UI
        SetupUI();
        
        // Связываем все компоненты в AppController
        ConnectComponents();
    }
    
    private static void EnsureARComponents()
    {
        // Проверяем, есть ли AR Session
        ARSession arSession = Object.FindObjectOfType<ARSession>();
        if (arSession == null)
        {
            GameObject arSessionObj = new GameObject("AR Session");
            arSession = arSessionObj.AddComponent<ARSession>();
        }
        
        // Проверяем, есть ли AR Session Origin
        ARSessionOrigin arSessionOrigin = Object.FindObjectOfType<ARSessionOrigin>();
        if (arSessionOrigin == null)
        {
            GameObject arSessionOriginObj = new GameObject("AR Session Origin");
            arSessionOrigin = arSessionOriginObj.AddComponent<ARSessionOrigin>();
            
            // Создаем AR Camera
            GameObject arCameraObj = new GameObject("AR Camera");
            arCameraObj.transform.SetParent(arSessionOrigin.transform);
            Camera cam = arCameraObj.AddComponent<Camera>();
            arCameraObj.AddComponent<ARCameraManager>();
            arCameraObj.AddComponent<ARCameraBackground>();
            arSessionOrigin.camera = cam;
        }
        
        // Проверяем, есть ли AR Plane Manager
        ARPlaneManager planeManager = Object.FindObjectOfType<ARPlaneManager>();
        if (planeManager == null && arSessionOrigin != null)
        {
            planeManager = arSessionOrigin.gameObject.AddComponent<ARPlaneManager>();
        }
        
        // Проверяем, есть ли AR Raycast Manager
        ARRaycastManager raycastManager = Object.FindObjectOfType<ARRaycastManager>();
        if (raycastManager == null && arSessionOrigin != null)
        {
            raycastManager = arSessionOrigin.gameObject.AddComponent<ARRaycastManager>();
        }
    }
    
    private static void SetupUI()
    {
        // Создаем базовый UI Canvas
        Canvas existingCanvas = Object.FindObjectOfType<Canvas>();
        GameObject canvasObj;
        
        if (existingCanvas != null)
        {
            canvasObj = existingCanvas.gameObject;
        }
        else
        {
            canvasObj = new GameObject("Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }
        
        // Добавляем UIManager компонент
        UIManager uiManager = canvasObj.GetComponent<UIManager>();
        if (uiManager == null)
        {
            uiManager = canvasObj.AddComponent<UIManager>();
        }
    }
    
    private static void ConnectComponents()
    {
        // Получаем ссылки на все компоненты
        ARWallPaintingApp app = Object.FindObjectOfType<ARWallPaintingApp>();
        
        if (app != null)
        {
            // Ищем компонент сегментации (любого типа)
            MonoBehaviour wallSegmentation = Object.FindObjectOfType<WallSegmentation>() as MonoBehaviour;
            if (wallSegmentation == null)
            {
                wallSegmentation = Object.FindObjectOfType<DemoWallSegmentation>() as MonoBehaviour;
            }
            
            WallPainter wallPainter = Object.FindObjectOfType<WallPainter>();
            UIManager uiManager = Object.FindObjectOfType<UIManager>();
            
            // Связываем компоненты
            SerializedObject serializedObject = new SerializedObject(app);
            serializedObject.FindProperty("wallSegmentation").objectReferenceValue = wallSegmentation;
            serializedObject.FindProperty("wallPainter").objectReferenceValue = wallPainter;
            serializedObject.FindProperty("uiManager").objectReferenceValue = uiManager;
            serializedObject.ApplyModifiedProperties();
            
            // Настраиваем ссылки в UI Manager
            if (uiManager != null)
            {
                SerializedObject uiSerializedObject = new SerializedObject(uiManager);
                uiSerializedObject.FindProperty("wallPainter").objectReferenceValue = wallPainter;
                uiSerializedObject.FindProperty("wallSegmentation").objectReferenceValue = wallSegmentation;
                uiSerializedObject.ApplyModifiedProperties();
            }
        }
    }
    
    [MenuItem("Tools/AR/Fix PaintedWall Prefab")]
    public static void FixPaintedWallPrefab()
    {
        // Удаляем существующий префаб
        string prefabPath = Path.Combine(prefabsPath, "PaintedWall.prefab");
        if (File.Exists(prefabPath))
        {
            AssetDatabase.DeleteAsset(prefabPath);
            Debug.Log("Deleted existing wall prefab at: " + prefabPath);
        }
        
        // Создаем новый префаб
        CreateWallPrefab();
        
        EditorUtility.DisplayDialog(
            "Prefab Fixed",
            "PaintedWall prefab has been recreated successfully.",
            "OK"
        );
    }
    
    [MenuItem("Tools/AR/Fix Project Issues")]
    public static void FixProjectIssues()
    {
        EditorUtility.DisplayProgressBar("Fixing Project", "Creating materials...", 0.2f);
        CreateWallPaintMaterial();
        
        EditorUtility.DisplayProgressBar("Fixing Project", "Fixing prefabs...", 0.4f);
        FixPaintedWallPrefab();
        
        EditorUtility.DisplayProgressBar("Fixing Project", "Setting up AR components...", 0.6f);
        EnsureARComponents();
        
        EditorUtility.DisplayProgressBar("Fixing Project", "Setting up UI...", 0.8f);
        SetupUI();
        
        EditorUtility.ClearProgressBar();
        
        EditorUtility.DisplayDialog(
            "Fixed Project Issues",
            "Project issues have been fixed. The following actions were performed:\n" +
            "1. Fixed WallPaintMaterial\n" +
            "2. Fixed PaintedWall prefab\n" +
            "3. Set up AR components\n" +
            "4. Set up UI\n\n" +
            "You may need to restart Unity for all changes to take effect.",
            "OK"
        );
    }
    
    [MenuItem("Tools/AR/Recreate All Assets")]
    public static void RecreateAllAssets()
    {
        bool proceed = EditorUtility.DisplayDialog(
            "Recreate All Assets",
            "This will delete and recreate the following assets:\n" +
            "- WallPaintMaterial.mat\n" +
            "- PaintedWall.prefab\n" +
            "- MainUI.prefab (if exists)\n\n" +
            "This operation is irreversible. Do you want to continue?",
            "Yes, Recreate All",
            "Cancel"
        );
        
        if (!proceed) return;
        
        try
        {
            // Удаляем и пересоздаем материал
            string materialAssetPath = Path.Combine(materialsPath, "WallPaintMaterial.mat");
            if (File.Exists(materialAssetPath))
            {
                AssetDatabase.DeleteAsset(materialAssetPath);
                Debug.Log("Deleted existing material at: " + materialAssetPath);
            }
            
            // Удаляем префаб стены
            string wallPrefabPath = Path.Combine(prefabsPath, "PaintedWall.prefab");
            if (File.Exists(wallPrefabPath))
            {
                AssetDatabase.DeleteAsset(wallPrefabPath);
                Debug.Log("Deleted existing wall prefab at: " + wallPrefabPath);
            }
            
            // Удаляем префаб UI если он существует
            string uiPrefabPath = Path.Combine(prefabsPath, "MainUI.prefab");
            if (File.Exists(uiPrefabPath))
            {
                AssetDatabase.DeleteAsset(uiPrefabPath);
                Debug.Log("Deleted existing UI prefab at: " + uiPrefabPath);
            }
            
            AssetDatabase.Refresh();
            
            // Пересоздаем материал
            EditorUtility.DisplayProgressBar("Recreating Assets", "Creating material...", 0.33f);
            CreateWallPaintMaterial();
            
            // Пересоздаем префаб стены
            EditorUtility.DisplayProgressBar("Recreating Assets", "Creating wall prefab...", 0.66f);
            CreateWallPrefab();
            
            // Пересоздаем префаб UI
            EditorUtility.DisplayProgressBar("Recreating Assets", "Creating UI prefab...", 1.0f);
            CreateMainUIPrefab();
            
            EditorUtility.ClearProgressBar();
            
            EditorUtility.DisplayDialog(
                "Assets Recreated",
                "All assets have been successfully recreated. Any broken references should now be fixed.",
                "OK"
            );
        }
        catch (System.Exception ex)
        {
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog(
                "Error",
                "An error occurred while recreating assets: " + ex.Message,
                "OK"
            );
            Debug.LogException(ex);
        }
    }
    
    private static void CreateWallPrefab()
    {
        // Проверяем существование шейдера и материала
        Shader wallPaintShader = Shader.Find("Custom/WallPaint");
        if (wallPaintShader == null)
        {
            Debug.LogError("Shader 'Custom/WallPaint' not found.");
            return;
        }
        
        // Загружаем материал
        string materialAssetPath = Path.Combine(materialsPath, "WallPaintMaterial.mat");
        Material wallPaintMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialAssetPath);
        
        if (wallPaintMaterial == null)
        {
            Debug.LogError("Material not found at path: " + materialAssetPath);
            CreateWallPaintMaterial();
            wallPaintMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialAssetPath);
            
            if (wallPaintMaterial == null)
            {
                Debug.LogError("Failed to create material at: " + materialAssetPath);
                return;
            }
        }
        
        // Убеждаемся, что директория для префабов существует
        if (!Directory.Exists(prefabsPath))
        {
            Directory.CreateDirectory(prefabsPath);
        }
        
        // Создаем новый объект Quad
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "PaintedWall";
        
        // Настраиваем MeshRenderer
        MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = wallPaintMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        
        // Сохраняем как префаб
        string prefabPath = Path.Combine(prefabsPath, "PaintedWall.prefab");
        PrefabUtility.SaveAsPrefabAsset(quad, prefabPath);
        Object.DestroyImmediate(quad);
        
        Debug.Log("Created new PaintedWall prefab at: " + prefabPath);
    }
    
    private static void CreateMainUIPrefab()
    {
        // Убеждаемся, что директория для префабов существует
        if (!Directory.Exists(prefabsPath))
        {
            Directory.CreateDirectory(prefabsPath);
        }
        
        // Создаем базовый UI Canvas
        GameObject canvasObj = new GameObject("MainUI");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        
        // Создаем панель выбора цвета
        GameObject colorPanel = new GameObject("ColorPanel");
        colorPanel.transform.SetParent(canvasObj.transform, false);
        UnityEngine.UI.Image panelImage = colorPanel.AddComponent<UnityEngine.UI.Image>();
        panelImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        
        // Настраиваем RectTransform для панели
        RectTransform panelRect = colorPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0, 0);
        panelRect.anchorMax = new Vector2(1, 0.15f);
        panelRect.offsetMin = new Vector2(10, 10);
        panelRect.offsetMax = new Vector2(-10, 0);
        
        // Создаем кнопки выбора цвета
        CreateColorButton(colorPanel.transform, "RedButton", new Color(1, 0, 0, 1), new Vector2(0.1f, 0.5f));
        CreateColorButton(colorPanel.transform, "GreenButton", new Color(0, 1, 0, 1), new Vector2(0.3f, 0.5f));
        CreateColorButton(colorPanel.transform, "BlueButton", new Color(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
        CreateColorButton(colorPanel.transform, "YellowButton", new Color(1, 1, 0, 1), new Vector2(0.7f, 0.5f));
        CreateColorButton(colorPanel.transform, "WhiteButton", new Color(1, 1, 1, 1), new Vector2(0.9f, 0.5f));
        
        // Сохраняем как префаб
        string prefabPath = Path.Combine(prefabsPath, "MainUI.prefab");
        PrefabUtility.SaveAsPrefabAsset(canvasObj, prefabPath);
        Object.DestroyImmediate(canvasObj);
        
        Debug.Log("Created new MainUI prefab at: " + prefabPath);
    }
    
    private static void CreateColorButton(Transform parent, string name, Color color, Vector2 anchorPosition)
    {
        GameObject button = new GameObject(name);
        button.transform.SetParent(parent, false);
        
        UnityEngine.UI.Image buttonImage = button.AddComponent<UnityEngine.UI.Image>();
        buttonImage.color = color;
        
        UnityEngine.UI.Button buttonComponent = button.AddComponent<UnityEngine.UI.Button>();
        buttonComponent.targetGraphic = buttonImage;
        
        // Настраиваем RectTransform
        RectTransform rectTransform = button.GetComponent<RectTransform>();
        rectTransform.anchorMin = anchorPosition - new Vector2(0.05f, 0.3f);
        rectTransform.anchorMax = anchorPosition + new Vector2(0.05f, 0.3f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;
    }
}
#endif 