using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using Unity.XR.CoreUtils;

/// <summary>
/// Утилита для создания базовой AR сцены для покраски стен
/// </summary>
public static class ARSceneCreator
{
    [MenuItem("Tools/AR/Create AR Wall Painting Scene")]
    public static void CreateARWallPaintingScene()
    {
        // Создаем новую сцену
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        
        EditorUtility.DisplayProgressBar("Creating AR Scene", "Setting up AR Foundation components...", 0.1f);
        
        // 1. Создаем базовые AR объекты
        SetupARComponents();
        
        EditorUtility.DisplayProgressBar("Creating AR Scene", "Setting up application controllers...", 0.3f);
        
        // 2. Создаем основные контроллеры приложения
        SetupApplicationControllers();
        
        EditorUtility.DisplayProgressBar("Creating AR Scene", "Creating UI elements...", 0.6f);
        
        // 3. Создаем UI
        SetupUI();
        
        EditorUtility.DisplayProgressBar("Creating AR Scene", "Creating materials and prefabs...", 0.8f);
        
        // 4. Создаем необходимые ассеты
        CreateRequiredAssets();
        
        EditorUtility.ClearProgressBar();
        
        // 5. Сохраняем сцену
        string scenePath = "Assets/Scenes/ARWallPainting.unity";
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), scenePath);
        
        Debug.Log("AR Wall Painting scene created at: " + scenePath);
        
        EditorUtility.DisplayDialog(
            "AR Scene Created",
            "AR Wall Painting scene has been created successfully.\n\n" +
            "The scene includes all necessary AR components and controllers.",
            "OK"
        );
    }
    
    private static void SetupARComponents()
    {
        // Создаем AR Session
        GameObject arSessionObj = new GameObject("AR Session");
        ARSession arSession = arSessionObj.AddComponent<ARSession>();
        arSessionObj.AddComponent<ARInputManager>();
        
        // Создаем XR Origin
        GameObject xrOriginObj = new GameObject("XR Origin");
        XROrigin xrOrigin = xrOriginObj.AddComponent<XROrigin>();
        
        // Добавляем AR компоненты на XR Origin
        xrOriginObj.AddComponent<ARPlaneManager>();
        xrOriginObj.AddComponent<ARRaycastManager>();
        
        // Создаем AR камеру
        GameObject cameraObj = new GameObject("AR Camera");
        Camera cam = cameraObj.AddComponent<Camera>();
        cameraObj.transform.SetParent(xrOriginObj.transform);
        
        // Добавляем компоненты камеры
        cameraObj.AddComponent<ARCameraManager>();
        cameraObj.AddComponent<ARCameraBackground>();
        
        // Связываем камеру с XROrigin
        xrOrigin.Camera = cam;
    }
    
    private static void SetupApplicationControllers()
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
            wallSegObj.AddComponent<WallSegmentation>();
        }
        
        // Создаем WallPainterManager
        GameObject wallPainterObj = new GameObject("WallPainterManager");
        wallPainterObj.AddComponent<WallPainter>();
    }
    
    private static void SetupUI()
    {
        // Создаем Canvas
        GameObject canvasObj = new GameObject("Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        
        // Добавляем UIManager
        canvasObj.AddComponent<UIManager>();
        
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
        
        // Добавляем ColorButtons
        CreateColorButton(panel.transform, "RedButton", Color.red, new Vector2(0.1f, 0.5f));
        CreateColorButton(panel.transform, "GreenButton", Color.green, new Vector2(0.3f, 0.5f));
        CreateColorButton(panel.transform, "BlueButton", Color.blue, new Vector2(0.5f, 0.5f));
        CreateColorButton(panel.transform, "YellowButton", Color.yellow, new Vector2(0.7f, 0.5f));
        CreateColorButton(panel.transform, "WhiteButton", Color.white, new Vector2(0.9f, 0.5f));
    }
    
    private static void CreateColorButton(Transform parent, string name, Color color, Vector2 anchorPosition)
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
    }
    
    private static void CreateRequiredAssets()
    {
        // Проверяем и создаем папки, если их нет
        if (!System.IO.Directory.Exists("Assets/Materials"))
            System.IO.Directory.CreateDirectory("Assets/Materials");
            
        if (!System.IO.Directory.Exists("Assets/Prefabs"))
            System.IO.Directory.CreateDirectory("Assets/Prefabs");
            
        if (!System.IO.Directory.Exists("Assets/Scenes"))
            System.IO.Directory.CreateDirectory("Assets/Scenes");
        
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
        Material wallPaintMaterial = new Material(wallPaintShader);
        wallPaintMaterial.color = new Color(1f, 1f, 1f, 0.7f);
        
        // Сохраняем материал
        AssetDatabase.CreateAsset(wallPaintMaterial, "Assets/Materials/WallPaintMaterial.mat");
        
        // Создаем префаб стены
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "PaintedWall";
        
        // Настраиваем MeshRenderer
        MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = wallPaintMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        
        // Сохраняем как префаб
        if (!System.IO.Directory.Exists("Assets/Prefabs"))
            System.IO.Directory.CreateDirectory("Assets/Prefabs");
            
        PrefabUtility.SaveAsPrefabAsset(quad, "Assets/Prefabs/PaintedWall.prefab");
        Object.DestroyImmediate(quad);
        
        AssetDatabase.Refresh();
    }
} 