using UnityEngine;
using UnityEditor;
using UnityEngine.XR.ARFoundation;
using Unity.XR.CoreUtils;

public class ARComponentsSetup : Editor
{
    [MenuItem("Tools/AR/Setup AR Components")]
    public static void SetupARComponents()
    {
        // Находим XR Origin в сцене
        XROrigin xrOrigin = FindObjectOfType<XROrigin>();
        if (xrOrigin == null)
        {
            Debug.LogError("XR Origin не найден в сцене. Сначала создайте XR Origin.");
            return;
        }
        
        // Проверяем наличие и настраиваем ARPlaneManager
        ARPlaneManager planeManager = xrOrigin.GetComponent<ARPlaneManager>();
        if (planeManager == null)
        {
            planeManager = xrOrigin.gameObject.AddComponent<ARPlaneManager>();
            Debug.Log("ARPlaneManager добавлен на XR Origin");
        }
        
        // Назначаем префаб для ARPlaneManager, если он существует
        string planePrefabPath = "Assets/Prefabs/ARPlaneVisualizer.prefab";
        GameObject planePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(planePrefabPath);
        if (planePrefab != null)
        {
            planeManager.planePrefab = planePrefab;
            Debug.Log("Префаб плоскости назначен для ARPlaneManager");
        }
        else
        {
            Debug.LogWarning($"Префаб плоскости не найден по пути: {planePrefabPath}. Создайте его через меню Tools/AR/Create AR Plane Prefab");
        }
        
        // Проверяем наличие и настраиваем ARMeshManager
        ARMeshManager meshManager = xrOrigin.GetComponent<ARMeshManager>();
        if (meshManager == null)
        {
            meshManager = xrOrigin.gameObject.AddComponent<ARMeshManager>();
            Debug.Log("ARMeshManager добавлен на XR Origin");
        }
        
        // Создаем префаб для AR Mesh, если его нет
        string meshPrefabPath = "Assets/Prefabs/ARMeshVisualizer.prefab";
        GameObject meshPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(meshPrefabPath);
        if (meshPrefab == null)
        {
            // Создаем базовый объект для AR меша
            GameObject meshObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            meshObject.name = "ARMeshVisualizer";
            
            // Добавляем компонент ARMeshVisualizer
            meshObject.AddComponent<ARMeshVisualizer>();
            
            // Настраиваем материал
            MeshRenderer renderer = meshObject.GetComponent<MeshRenderer>();
            if (renderer != null && renderer.sharedMaterial != null)
            {
                Material meshMaterial = new Material(Shader.Find("Unlit/Color"));
                meshMaterial.color = new Color(0.5f, 1f, 0.5f, 0.3f);
                renderer.sharedMaterial = meshMaterial;
            }
            
            // Создаем директорию, если она не существует
            if (!System.IO.Directory.Exists("Assets/Prefabs"))
            {
                System.IO.Directory.CreateDirectory("Assets/Prefabs");
            }
            
            // Сохраняем объект как префаб
#if UNITY_2018_3_OR_NEWER
            meshPrefab = PrefabUtility.SaveAsPrefabAsset(meshObject, meshPrefabPath);
#else
            meshPrefab = PrefabUtility.CreatePrefab(meshPrefabPath, meshObject);
#endif
            
            // Удаляем временный объект сцены
            DestroyImmediate(meshObject);
            
            Debug.Log($"AR Mesh Prefab создан по пути: {meshPrefabPath}");
        }
        
        // Назначаем префаб для ARMeshManager
        if (meshPrefab != null)
        {
            // Получаем компонент MeshFilter из префаба
            MeshFilter meshFilter = meshPrefab.GetComponent<MeshFilter>();
            if (meshFilter != null)
            {
                meshManager.meshPrefab = meshFilter;
                Debug.Log("Префаб меша назначен для ARMeshManager");
            }
            else
            {
                Debug.LogError("Префаб меша не содержит компонент MeshFilter!");
            }
        }
        
        // Активируем мешинг
        meshManager.enabled = true;
        
        // Настраиваем AR Camera
        Camera arCamera = xrOrigin.Camera;
        if (arCamera != null)
        {
            // Убеждаемся, что AR камера имеет тег MainCamera
            if (arCamera.gameObject.tag != "MainCamera")
            {
                arCamera.gameObject.tag = "MainCamera";
                Debug.Log("AR Camera тег установлен как MainCamera");
            }
            
            // Проверяем наличие компонента AR камеры
            ARCameraManager cameraManager = arCamera.GetComponent<ARCameraManager>();
            if (cameraManager == null)
            {
                cameraManager = arCamera.gameObject.AddComponent<ARCameraManager>();
                Debug.Log("ARCameraManager добавлен на AR Camera");
            }
            
            // Проверяем наличие фона AR камеры
            ARCameraBackground cameraBackground = arCamera.GetComponent<ARCameraBackground>();
            if (cameraBackground == null)
            {
                cameraBackground = arCamera.gameObject.AddComponent<ARCameraBackground>();
                Debug.Log("ARCameraBackground добавлен на AR Camera");
            }
        }
        else
        {
            Debug.LogError("AR Camera не найдена в XR Origin");
        }
        
        // Проверяем, есть ли WallSegmentationManager и настраиваем его
        WallSegmentation wallSegmentation = FindObjectOfType<WallSegmentation>();
        if (wallSegmentation != null)
        {
            // Настраиваем ссылки
            if (wallSegmentation.GetComponent<ARCameraManager>() == null)
            {
                SerializedObject serializedObj = new SerializedObject(wallSegmentation);
                SerializedProperty cameraManagerProp = serializedObj.FindProperty("cameraManager");
                
                if (arCamera != null && arCamera.GetComponent<ARCameraManager>() != null)
                {
                    cameraManagerProp.objectReferenceValue = arCamera.GetComponent<ARCameraManager>();
                    serializedObj.ApplyModifiedProperties();
                    Debug.Log("ARCameraManager назначен для WallSegmentation");
                }
            }
        }
        
        // Сохраняем изменения сцены
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        
        Debug.Log("Настройка AR компонентов завершена");
    }
} 