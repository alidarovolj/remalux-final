using UnityEngine;
using UnityEngine.XR.ARFoundation;
using Unity.XR.CoreUtils;
using UnityEngine.UI;
using System.Reflection;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class CreateARScene : MonoBehaviour
{
#if UNITY_EDITOR
    public static void SetupARScene()
    {
        // 1. Создаем XR Origin
        GameObject xrOriginObj = new GameObject("XR Origin");
        XROrigin xrOrigin = xrOriginObj.AddComponent<XROrigin>();
        
        // 2. Создаем Camera Offset
        GameObject cameraOffsetObj = new GameObject("Camera Offset");
        cameraOffsetObj.transform.SetParent(xrOriginObj.transform);
        cameraOffsetObj.transform.localPosition = Vector3.zero;
        cameraOffsetObj.transform.localRotation = Quaternion.identity;
        
        // 3. Находим существующую камеру или создаем новую
        Camera mainCamera = Camera.main;
        GameObject cameraObj;
        
        if (mainCamera != null)
        {
            cameraObj = mainCamera.gameObject;
            // Перемещаем существующую камеру в нашу иерархию
            cameraObj.transform.SetParent(cameraOffsetObj.transform);
            cameraObj.transform.localPosition = Vector3.zero;
            cameraObj.transform.localRotation = Quaternion.identity;
            
            // Убеждаемся, что тег установлен
            if (cameraObj.tag != "MainCamera")
            {
                cameraObj.tag = "MainCamera";
                Debug.Log("Установлен тег MainCamera для существующей камеры");
            }
        }
        else
        {
            // Создаем новую камеру
            cameraObj = new GameObject("Main Camera");
            cameraObj.transform.SetParent(cameraOffsetObj.transform);
            cameraObj.transform.localPosition = Vector3.zero;
            cameraObj.transform.localRotation = Quaternion.identity;
            cameraObj.tag = "MainCamera";
            Camera camera = cameraObj.AddComponent<Camera>();
            cameraObj.AddComponent<AudioListener>();
            Debug.Log("Создана новая камера с тегом MainCamera");
        }
        
        // 4. Добавляем необходимые AR компоненты
        ARCameraManager cameraManager = cameraObj.AddComponent<ARCameraManager>();
        cameraObj.AddComponent<ARCameraBackground>();
        
        // 5. Устанавливаем ссылки в XROrigin
        xrOrigin.Camera = cameraObj.GetComponent<Camera>();
        xrOrigin.CameraFloorOffsetObject = cameraOffsetObj;
        
        // 6. Добавляем AR Session и AR Session Origin
        GameObject arSessionObj = new GameObject("AR Session");
        arSessionObj.AddComponent<ARSession>();
        arSessionObj.AddComponent<ARInputManager>();
        
        // 7. Добавляем AR Plane Manager для обнаружения плоскостей
        ARPlaneManager planeManager = xrOriginObj.AddComponent<ARPlaneManager>();
        
        // 8. Добавляем AR Raycast Manager для лучей взаимодействия
        xrOriginObj.AddComponent<ARRaycastManager>();
        
        // 9. Добавляем компонент ARPlaneOverflowFixer
        ARPlaneOverflowFixer overflowFixer = xrOriginObj.AddComponent<ARPlaneOverflowFixer>();
        overflowFixer.planeManager = planeManager;
        overflowFixer.sessionOrigin = xrOrigin;
        
        // 10. Добавляем UI для отладки сегментации
        GameObject canvasObj = new GameObject("DebugCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();
        
        // Добавляем панель отладки сегментации
        GameObject debugPanel = new GameObject("DebugPanel");
        debugPanel.transform.SetParent(canvasObj.transform, false);
        RectTransform debugRect = debugPanel.AddComponent<RectTransform>();
        debugRect.anchorMin = new Vector2(0.7f, 0.7f); // Размещаем в правом верхнем углу
        debugRect.anchorMax = new Vector2(1.0f, 1.0f);
        debugRect.offsetMin = Vector2.zero;
        debugRect.offsetMax = Vector2.zero;
        
        // Добавляем RawImage для отображения результатов сегментации
        GameObject debugImageObj = new GameObject("SegmentationDebugImage");
        debugImageObj.transform.SetParent(debugPanel.transform, false);
        RectTransform imageRect = debugImageObj.AddComponent<RectTransform>();
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = Vector2.zero;
        imageRect.offsetMax = Vector2.zero;
        RawImage rawImage = debugImageObj.AddComponent<RawImage>();
        
        // Создаем текстуру по умолчанию и назначаем RawImage
        Texture2D defaultTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[32 * 32];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Color(0, 0, 0, 0.5f); // Полупрозрачный черный цвет для отладки
        }
        defaultTexture.SetPixels(pixels);
        defaultTexture.Apply();
        
        // Назначаем текстуру для RawImage
        rawImage.texture = defaultTexture;
        
        // 11. Добавляем компонент WallSegmentation для сегментации стен
        GameObject wallSegObj = new GameObject("WallSegmentation");
        WallSegmentation wallSegmentation = wallSegObj.AddComponent<WallSegmentation>();
        
        // 13. Создаем компонент UI Manager для обработки пользовательского интерфейса
        GameObject uiManagerObj = new GameObject("UIManager");
        UIManager uiManager = uiManagerObj.AddComponent<UIManager>();
        
        // 12. Добавляем компонент для управления АР-приложением
        GameObject appManagerObj = new GameObject("ARWallPaintingApp");
        ARWallPaintingApp appManager = appManagerObj.AddComponent<ARWallPaintingApp>();
        
        // Устанавливаем WallSegmentation сначала
        SerializedObject serializedWallSeg = new SerializedObject(wallSegmentation);
        serializedWallSeg.FindProperty("cameraManager").objectReferenceValue = cameraManager;
        serializedWallSeg.FindProperty("arCamera").objectReferenceValue = cameraObj.GetComponent<Camera>();
        serializedWallSeg.FindProperty("debugImage").objectReferenceValue = rawImage;
        serializedWallSeg.FindProperty("showDebugVisualisation").boolValue = true;
        serializedWallSeg.FindProperty("currentMode").enumValueIndex = 2; // ExternalModel
        serializedWallSeg.FindProperty("externalModelPath").stringValue = "Models/model.onnx";
        serializedWallSeg.ApplyModifiedProperties();
        
        // Принудительное применение изменений - вызываем EnableDebugVisualization напрямую
        wallSegmentation.SendMessage("EnableDebugVisualization", true);
        
        // Затем настраиваем ARWallPaintingApp 
        SerializedObject serializedAppManager = new SerializedObject(appManager);
        serializedAppManager.FindProperty("arSession").objectReferenceValue = arSessionObj.GetComponent<ARSession>();
        serializedAppManager.FindProperty("xrOrigin").objectReferenceValue = xrOrigin;
        serializedAppManager.FindProperty("planeManager").objectReferenceValue = planeManager;
        serializedAppManager.FindProperty("raycastManager").objectReferenceValue = xrOriginObj.GetComponent<ARRaycastManager>();
        serializedAppManager.FindProperty("wallSegmentation").objectReferenceValue = wallSegmentation;
        serializedAppManager.FindProperty("segmentationDebugImage").objectReferenceValue = rawImage;
        serializedAppManager.FindProperty("uiManager").objectReferenceValue = uiManager;
        serializedAppManager.ApplyModifiedProperties();
        
        Debug.Log("AR scene setup complete! Created XR Origin structure with all necessary components including WallSegmentation.");
        
        // Добавляем текстовую информацию для наглядности
        Debug.Log("Обратите внимание! Для корректной работы отладочной визуализации:");
        Debug.Log("1. RawImage создан и подключен к WallSegmentation");
        Debug.Log("2. UIManager создан и подключен к ARWallPaintingApp");
        Debug.Log("3. Все компоненты инициализированы и готовы к использованию");
        
        // Выбираем созданный объект для удобства
        Selection.activeGameObject = xrOriginObj;
    }
#endif
} 