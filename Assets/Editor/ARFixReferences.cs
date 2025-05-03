using UnityEngine;
using UnityEditor;
using UnityEngine.XR.ARFoundation;
using Unity.XR.CoreUtils;

/// <summary>
/// Инструмент для проверки и исправления ссылок между компонентами AR сцены
/// </summary>
public class ARFixReferences : EditorWindow
{
    // Методы будут вызываться только изнутри нашего основного редактора
    public static void ShowWindow()
    {
        EditorWindow.GetWindow(typeof(ARFixReferences), false, "Fix AR References");
    }
    
    public static void FixReferences()
    {
        FixARScriptReferences();
    }
    
    void OnGUI()
    {
        GUILayout.Label("AR References Fixer", EditorStyles.boldLabel);
        
        EditorGUILayout.HelpBox(
            "Этот инструмент проверит и исправит ссылки между компонентами AR сцены.\n\n" +
            "Это поможет решить проблемы с NullReferenceException и связать компоненты, которые не были правильно настроены.", 
            MessageType.Info);
        
        if (GUILayout.Button("Fix AR References"))
        {
            FixARScriptReferences();
        }
    }
    
    /// <summary>
    /// Исправляет ссылки между компонентами AR сцены
    /// </summary>
    private static void FixARScriptReferences()
    {
        bool foundIssues = false;
        EditorUtility.DisplayProgressBar("Fixing AR References", "Проверка компонентов AR сцены...", 0.1f);
        
        // 1. Находим основные компоненты
        var arSession = Object.FindObjectOfType<ARSession>();
        var xrOrigin = Object.FindObjectOfType<XROrigin>();
        Camera arCamera = null;
        ARCameraManager cameraManager = null;
        ARPlaneManager planeManager = null;
        ARRaycastManager raycastManager = null;
        
        // 2. Ищем WallSegmentation и WallPainter
        var wallSegmentation = Object.FindObjectOfType<WallSegmentation>();
        var wallPainter = Object.FindObjectOfType<WallPainter>();
        var appController = Object.FindObjectOfType<ARWallPaintingApp>();
        var uiManager = Object.FindObjectOfType<UIManager>();
        
        // 3. Проверяем XROrigin и камеру
        if (xrOrigin != null)
        {
            arCamera = xrOrigin.Camera;
            if (arCamera == null)
            {
                Debug.LogWarning("XROrigin не имеет назначенной камеры");
                
                // Ищем камеру среди дочерних объектов
                Camera[] cameras = xrOrigin.GetComponentsInChildren<Camera>();
                if (cameras.Length > 0)
                {
                    arCamera = cameras[0];
                    xrOrigin.Camera = arCamera;
                    Debug.Log("Назначена камера для XROrigin");
                    foundIssues = true;
                }
            }
            
            // Ищем AR компоненты в XROrigin
            cameraManager = arCamera?.GetComponent<ARCameraManager>();
            planeManager = xrOrigin.GetComponent<ARPlaneManager>();
            raycastManager = xrOrigin.GetComponent<ARRaycastManager>();
            
            if (planeManager == null)
            {
                planeManager = xrOrigin.gameObject.AddComponent<ARPlaneManager>();
                Debug.Log("Добавлен ARPlaneManager на XROrigin");
                foundIssues = true;
            }
            
            if (raycastManager == null)
            {
                raycastManager = xrOrigin.gameObject.AddComponent<ARRaycastManager>();
                Debug.Log("Добавлен ARRaycastManager на XROrigin");
                foundIssues = true;
            }
        }
        else
        {
            Debug.LogError("XROrigin не найден в сцене. Исправление ссылок невозможно.");
            EditorUtility.ClearProgressBar();
            return;
        }
        
        EditorUtility.DisplayProgressBar("Fixing AR References", "Настройка компонентов WallSegmentation...", 0.4f);
        
        // 4. Исправляем ссылки в WallSegmentation
        if (wallSegmentation != null)
        {
            SerializedObject segmentationSO = new SerializedObject(wallSegmentation);
            
            // Исправляем ссылку на ARCameraManager
            SerializedProperty cameraProp = segmentationSO.FindProperty("cameraManager");
            if (cameraProp != null && cameraProp.objectReferenceValue == null && cameraManager != null)
            {
                cameraProp.objectReferenceValue = cameraManager;
                foundIssues = true;
            }
            
            // Исправляем ссылку на ARCamera
            SerializedProperty arCameraProp = segmentationSO.FindProperty("arCamera");
            if (arCameraProp != null && arCameraProp.objectReferenceValue == null && arCamera != null)
            {
                arCameraProp.objectReferenceValue = arCamera;
                foundIssues = true;
            }
            
            segmentationSO.ApplyModifiedProperties();
        }
        
        EditorUtility.DisplayProgressBar("Fixing AR References", "Настройка компонентов WallPainter...", 0.6f);
        
        // 5. Исправляем ссылки в WallPainter
        if (wallPainter != null)
        {
            SerializedObject painterSO = new SerializedObject(wallPainter);
            
            // Исправляем ссылку на ARRaycastManager
            SerializedProperty raycastProp = painterSO.FindProperty("raycastManager");
            if (raycastProp != null && raycastProp.objectReferenceValue == null && raycastManager != null)
            {
                raycastProp.objectReferenceValue = raycastManager;
                foundIssues = true;
            }
            
            // Исправляем ссылку на sessionOrigin
            SerializedProperty originProp = painterSO.FindProperty("sessionOrigin");
            if (originProp != null && originProp.objectReferenceValue == null && xrOrigin != null)
            {
                originProp.objectReferenceValue = xrOrigin;
                foundIssues = true;
            }
            
            // Исправляем ссылку на arCamera
            SerializedProperty cameraProp = painterSO.FindProperty("arCamera");
            if (cameraProp != null && cameraProp.objectReferenceValue == null && arCamera != null)
            {
                cameraProp.objectReferenceValue = arCamera;
                foundIssues = true;
            }
            
            // Исправляем ссылку на wallSegmentation
            SerializedProperty segmentationProp = painterSO.FindProperty("wallSegmentation");
            if (segmentationProp != null && segmentationProp.objectReferenceValue == null && wallSegmentation != null)
            {
                segmentationProp.objectReferenceValue = wallSegmentation;
                foundIssues = true;
            }
            
            painterSO.ApplyModifiedProperties();
        }
        
        EditorUtility.DisplayProgressBar("Fixing AR References", "Настройка AppController...", 0.8f);
        
        // 6. Исправляем ссылки в AppController
        if (appController != null)
        {
            SerializedObject appSO = new SerializedObject(appController);
            
            // Исправляем ссылку на ARSession
            SerializedProperty sessionProp = appSO.FindProperty("arSession");
            if (sessionProp != null && sessionProp.objectReferenceValue == null && arSession != null)
            {
                sessionProp.objectReferenceValue = arSession;
                foundIssues = true;
            }
            
            // Исправляем ссылку на sessionOrigin/XROrigin
            SerializedProperty originProp = appSO.FindProperty("arSessionOrigin");
            if (originProp != null && originProp.objectReferenceValue == null && xrOrigin != null)
            {
                originProp.objectReferenceValue = xrOrigin;
                foundIssues = true;
            }
            
            // Исправляем ссылку на planeManager
            SerializedProperty planeProp = appSO.FindProperty("planeManager");
            if (planeProp != null && planeProp.objectReferenceValue == null && planeManager != null)
            {
                planeProp.objectReferenceValue = planeManager;
                foundIssues = true;
            }
            
            // Исправляем ссылку на raycastManager
            SerializedProperty raycastProp = appSO.FindProperty("raycastManager");
            if (raycastProp != null && raycastProp.objectReferenceValue == null && raycastManager != null)
            {
                raycastProp.objectReferenceValue = raycastManager;
                foundIssues = true;
            }
            
            // Исправляем ссылку на wallSegmentation
            SerializedProperty segmentationProp = appSO.FindProperty("wallSegmentation");
            if (segmentationProp != null && segmentationProp.objectReferenceValue == null && wallSegmentation != null)
            {
                segmentationProp.objectReferenceValue = wallSegmentation;
                foundIssues = true;
            }
            
            // Исправляем ссылку на wallPainter
            SerializedProperty painterProp = appSO.FindProperty("wallPainter");
            if (painterProp != null && painterProp.objectReferenceValue == null && wallPainter != null)
            {
                painterProp.objectReferenceValue = wallPainter;
                foundIssues = true;
            }
            
            // Исправляем ссылку на uiManager
            SerializedProperty uiProp = appSO.FindProperty("uiManager");
            if (uiProp != null && uiProp.objectReferenceValue == null && uiManager != null)
            {
                uiProp.objectReferenceValue = uiManager;
                foundIssues = true;
            }
            
            appSO.ApplyModifiedProperties();
        }
        
        EditorUtility.ClearProgressBar();
        
        if (foundIssues)
        {
            Debug.Log("Ссылки между компонентами AR успешно исправлены");
            EditorUtility.DisplayDialog("Исправление завершено", 
                "Найдены и исправлены несколько ссылок между компонентами AR сцены.\n\nТеперь приложение должно работать корректно.", 
                "OK");
        }
        else
        {
            Debug.Log("Все ссылки между компонентами AR уже настроены правильно");
            EditorUtility.DisplayDialog("Проверка завершена", 
                "Все ссылки между компонентами AR сцены уже настроены правильно.", 
                "OK");
        }
    }
} 