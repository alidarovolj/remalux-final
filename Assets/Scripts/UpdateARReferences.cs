using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine.XR.ARFoundation;
using Unity.XR.CoreUtils;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Утилита для обновления ссылок и использования новых API AR Foundation
/// </summary>
public class UpdateARReferences : MonoBehaviour
{
    [MenuItem("Tools/AR/Update References in Scene")]
    public static void UpdateReferencesInScene()
    {
        // Просматриваем все объекты на сцене с компонентами, которые могут иметь ссылки на ARSessionOrigin
        UpdateARWallPaintingAppReferences();
        UpdateWallPainterReferences();
        UpdateWallSegmentationReferences();
        UpdateDemoWallSegmentationReferences();
        
        // Сохраняем сцену
        Scene currentScene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(currentScene);
        EditorSceneManager.SaveScene(currentScene);
        
        EditorUtility.DisplayDialog(
            "Update Complete",
            "References updated to use new AR Foundation APIs.",
            "OK"
        );
    }
    
    [MenuItem("Tools/AR/Fix TrackablesChanged Subscriptions")]
    public static void FixTrackablesChangedSubscriptions()
    {
        // Поиск всех скриптов, которые могут подписываться на событие trackablesChanged
        ARWallPaintingApp[] apps = Object.FindObjectsByType<ARWallPaintingApp>(FindObjectsSortMode.None);
        DemoWallSegmentation[] demoSegmentations = Object.FindObjectsByType<DemoWallSegmentation>(FindObjectsSortMode.None);
        
        // Обновляем скрипты, уведомляем пользователя о необходимости проверки кода
        if (apps.Length > 0 || demoSegmentations.Length > 0)
        {
            EditorUtility.DisplayDialog(
                "Manual Code Update Required",
                $"Found {apps.Length} ARWallPaintingApp and {demoSegmentations.Length} DemoWallSegmentation scripts " +
                "that might be subscribing to trackablesChanged. Code needs manual update to use planesChanged and ARPlanesChangedEventArgs.",
                "OK"
            );
        }
        else
        {
            EditorUtility.DisplayDialog(
                "No issues found",
                "No objects found that might be subscribing to trackablesChanged.",
                "OK"
            );
        }
    }
    
    private static void UpdateARWallPaintingAppReferences()
    {
        ARWallPaintingApp[] apps = Object.FindObjectsByType<ARWallPaintingApp>(FindObjectsSortMode.None);
        foreach (var app in apps)
        {
            SerializedObject so = new SerializedObject(app);
            
            // Проверяем, есть ли свойство arSessionOrigin
            SerializedProperty arSessionOriginProp = so.FindProperty("arSessionOrigin");
            SerializedProperty xrOriginProp = so.FindProperty("xrOrigin");
            
            if (arSessionOriginProp != null && xrOriginProp != null)
            {
                // Если есть ссылка на ARSessionOrigin, но нет на XROrigin
                if (arSessionOriginProp.objectReferenceValue != null && xrOriginProp.objectReferenceValue == null)
                {
                    // Ищем XROrigin на сцене
                    XROrigin xrOrigin = Object.FindFirstObjectByType<XROrigin>();
                    if (xrOrigin != null)
                    {
                        xrOriginProp.objectReferenceValue = xrOrigin;
                        Debug.Log($"Updated {app.name} to use XROrigin reference");
                    }
                }
            }
            
            so.ApplyModifiedProperties();
        }
    }
    
    private static void UpdateWallPainterReferences()
    {
        WallPainter[] painters = Object.FindObjectsByType<WallPainter>(FindObjectsSortMode.None);
        foreach (var painter in painters)
        {
            SerializedObject so = new SerializedObject(painter);
            
            // Обновляем ссылки с sessionOrigin на xrOrigin
            SerializedProperty sessionOriginProp = so.FindProperty("sessionOrigin");
            SerializedProperty xrOriginProp = so.FindProperty("xrOrigin");
            
            if (sessionOriginProp != null && xrOriginProp != null)
            {
                if (sessionOriginProp.objectReferenceValue != null && xrOriginProp.objectReferenceValue == null)
                {
                    XROrigin xrOrigin = Object.FindFirstObjectByType<XROrigin>();
                    if (xrOrigin != null)
                    {
                        xrOriginProp.objectReferenceValue = xrOrigin;
                        Debug.Log($"Updated {painter.name} to use XROrigin reference");
                    }
                }
            }
            
            so.ApplyModifiedProperties();
        }
    }
    
    private static void UpdateWallSegmentationReferences()
    {
        WallSegmentation[] segmentations = Object.FindObjectsByType<WallSegmentation>(FindObjectsSortMode.None);
        foreach (var segmentation in segmentations)
        {
            SerializedObject so = new SerializedObject(segmentation);
            so.ApplyModifiedProperties();
        }
    }
    
    private static void UpdateDemoWallSegmentationReferences()
    {
        DemoWallSegmentation[] demoSegmentations = Object.FindObjectsByType<DemoWallSegmentation>(FindObjectsSortMode.None);
        foreach (var demo in demoSegmentations)
        {
            SerializedObject so = new SerializedObject(demo);
            so.ApplyModifiedProperties();
        }
    }
}
#endif 