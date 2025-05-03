using UnityEngine;
using System.IO;
using UnityEngine.XR.ARFoundation;
using Unity.XR.CoreUtils;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

/// <summary>
/// Утилита для обновления проекта с устаревших API AR Foundation на новые
/// </summary>
public static class ProjectUpdater
{
    [MenuItem("Tools/AR/Update Project To AR Foundation 6.x")]
    public static void UpdateProjectToARFoundation6()
    {
        bool proceed = EditorUtility.DisplayDialog(
            "Update Project API",
            "This will update your project from old AR Foundation API to version 6.x:\n\n" +
            "• ARSessionOrigin → XROrigin\n" +
            "• FindObjectOfType → FindAnyObjectByType\n" +
            "• planesChanged → trackablesChanged\n\n" +
            "You should backup your project before proceeding!",
            "Update Project",
            "Cancel"
        );
        
        if (!proceed) return;
        
        try
        {
            EditorUtility.DisplayProgressBar("Updating Project", "Converting scene hierarchy...", 0.1f);
            UpdateSceneHierarchy();
            
            EditorUtility.DisplayProgressBar("Updating Project", "Recreating core assets...", 0.4f);
            RecreateAssets();
            
            EditorUtility.DisplayProgressBar("Updating Project", "Setting up AR components...", 0.7f);
            SetupARComponents();
            
            EditorUtility.ClearProgressBar();
            
            EditorUtility.DisplayDialog(
                "Project Update Complete",
                "Your project has been updated to use AR Foundation 6.x API.\n\n" +
                "Note: Script references still need manual updates.\n" +
                "Please check console warnings to identify needed changes.",
                "OK"
            );
        }
        catch (System.Exception ex)
        {
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog(
                "Update Error",
                "An error occurred during update: " + ex.Message,
                "OK"
            );
            Debug.LogException(ex);
        }
    }
    
    private static void UpdateSceneHierarchy()
    {
        Scene currentScene = EditorSceneManager.GetActiveScene();
        
        // Найдем все ARSessionOrigin в сцене
        ARSessionOrigin[] sessionOrigins = Object.FindObjectsByType<ARSessionOrigin>(FindObjectsSortMode.None);
        foreach (var sessionOrigin in sessionOrigins)
        {
            // Получаем GameObject
            GameObject originObject = sessionOrigin.gameObject;
            string objectName = originObject.name;
            
            // Сохраняем ссылку на камеру
            Camera arCamera = sessionOrigin.camera;
            
            // Создаем новый XROrigin на том же месте
            GameObject xrOriginObject = new GameObject(objectName.Replace("AR Session Origin", "XR Origin"));
            xrOriginObject.transform.SetPositionAndRotation(
                originObject.transform.position,
                originObject.transform.rotation
            );
            
            // Добавляем компонент XROrigin
            XROrigin xrOrigin = xrOriginObject.AddComponent<XROrigin>();
            
            // Переносим детей (кроме камеры)
            List<Transform> children = new List<Transform>();
            foreach (Transform child in originObject.transform)
            {
                if (child.GetComponent<Camera>() != arCamera)
                {
                    children.Add(child);
                }
            }
            
            foreach (Transform child in children)
            {
                child.SetParent(xrOriginObject.transform, true);
            }
            
            // Настраиваем камеру для XROrigin
            if (arCamera != null)
            {
                GameObject cameraObject = arCamera.gameObject;
                cameraObject.transform.SetParent(xrOriginObject.transform, true);
                xrOrigin.Camera = arCamera;
            }
            
            // Если все компоненты AR находятся в ARSessionOrigin, переносим их в XROrigin
            foreach (Component component in originObject.GetComponents<Component>())
            {
                if (!(component is Transform) && !(component is ARSessionOrigin))
                {
                    UnityEditorInternal.ComponentUtility.CopyComponent(component);
                    UnityEditorInternal.ComponentUtility.PasteComponentAsNew(xrOriginObject);
                    
                    // Удаляем оригинальный компонент
                    if (Application.isEditor && !Application.isPlaying)
                    {
                        Object.DestroyImmediate(component);
                    }
                    else
                    {
                        Object.Destroy(component);
                    }
                }
            }
            
            // Удаляем оригинальный ARSessionOrigin, если он пуст
            if (originObject.transform.childCount == 0)
            {
                if (Application.isEditor && !Application.isPlaying)
                {
                    Object.DestroyImmediate(originObject);
                }
                else
                {
                    Object.Destroy(originObject);
                }
            }
            
            Debug.Log($"Converted {objectName} to XROrigin");
        }
        
        // Проверим, что ARMeshManager находится в правильном месте
        ARMeshManager[] meshManagers = Object.FindObjectsByType<ARMeshManager>(FindObjectsSortMode.None);
        XROrigin xrOriginInstance = Object.FindAnyObjectByType<XROrigin>();
        
        foreach (var meshManager in meshManagers)
        {
            if (xrOriginInstance != null && meshManager.transform.parent != xrOriginInstance.transform)
            {
                if (meshManager.gameObject.name != "AR Session Origin" && 
                    meshManager.gameObject.name != "XR Origin")
                {
                    // Перемещаем компонент из текущего GameObject в XROrigin
                    UnityEditorInternal.ComponentUtility.CopyComponent(meshManager);
                    UnityEditorInternal.ComponentUtility.PasteComponentAsNew(xrOriginInstance.gameObject);
                    
                    if (Application.isEditor && !Application.isPlaying)
                    {
                        Object.DestroyImmediate(meshManager);
                    }
                    else
                    {
                        Object.Destroy(meshManager);
                    }
                    
                    Debug.Log("Moved ARMeshManager to XROrigin");
                }
            }
        }
        
        // Проверим другие ARManager компоненты
        EnsureARManagersInXROrigin<ARPlaneManager>();
        EnsureARManagersInXROrigin<ARRaycastManager>();
        EnsureARManagersInXROrigin<ARAnchorManager>();
        EnsureARManagersInXROrigin<ARPointCloudManager>();
        
        EditorSceneManager.MarkSceneDirty(currentScene);
        EditorSceneManager.SaveScene(currentScene);
    }
    
    private static void EnsureARManagersInXROrigin<T>() where T : MonoBehaviour
    {
        T[] managers = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
        XROrigin xrOriginInstance = Object.FindAnyObjectByType<XROrigin>();
        
        if (xrOriginInstance == null) return;
        
        foreach (var manager in managers)
        {
            if (manager.transform.parent != xrOriginInstance.transform)
            {
                // Перемещаем компонент из текущего GameObject в XROrigin
                UnityEditorInternal.ComponentUtility.CopyComponent(manager);
                UnityEditorInternal.ComponentUtility.PasteComponentAsNew(xrOriginInstance.gameObject);
                
                if (Application.isEditor && !Application.isPlaying)
                {
                    Object.DestroyImmediate(manager);
                }
                else
                {
                    Object.Destroy(manager);
                }
                
                Debug.Log($"Moved {typeof(T).Name} to XROrigin");
            }
        }
    }
    
    private static void RecreateAssets()
    {
        // Используем существующий метод из SetupUtility
        System.Type setupUtilityType = System.Type.GetType("SetupUtility, Assembly-CSharp-Editor");
        if (setupUtilityType != null)
        {
            System.Reflection.MethodInfo methodInfo = setupUtilityType.GetMethod("RecreateAllAssets", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            
            if (methodInfo != null)
            {
                methodInfo.Invoke(null, null);
            }
            else
            {
                Debug.LogWarning("RecreateAllAssets method not found in SetupUtility class");
            }
        }
        else
        {
            Debug.LogWarning("SetupUtility class not found");
        }
    }
    
    private static void SetupARComponents()
    {
        // Находим или создаем AR Session
        ARSession arSession = Object.FindAnyObjectByType<ARSession>();
        if (arSession == null)
        {
            GameObject arSessionObj = new GameObject("AR Session");
            arSession = arSessionObj.AddComponent<ARSession>();
            arSessionObj.AddComponent<ARInputManager>();
        }
        
        // Находим или создаем XR Origin
        XROrigin xrOrigin = Object.FindAnyObjectByType<XROrigin>();
        if (xrOrigin == null)
        {
            GameObject xrOriginGameObj = new GameObject("XR Origin");
            xrOrigin = xrOriginGameObj.AddComponent<XROrigin>();
            
            // Создаем AR Camera
            GameObject arCameraObj = new GameObject("AR Camera");
            arCameraObj.transform.SetParent(xrOrigin.transform);
            Camera cam = arCameraObj.AddComponent<Camera>();
            arCameraObj.AddComponent<ARCameraManager>();
            arCameraObj.AddComponent<ARCameraBackground>();
            xrOrigin.Camera = cam;
        }
        
        // Убеждаемся, что все необходимые менеджеры существуют в XROrigin
        GameObject xrOriginGameObject = xrOrigin.gameObject;
        
        if (xrOriginGameObject.GetComponent<ARPlaneManager>() == null)
        {
            xrOriginGameObject.AddComponent<ARPlaneManager>();
        }
        
        if (xrOriginGameObject.GetComponent<ARRaycastManager>() == null)
        {
            xrOriginGameObject.AddComponent<ARRaycastManager>();
        }
    }
}
#endif 