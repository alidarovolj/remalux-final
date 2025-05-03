using UnityEngine;
using UnityEditor;
using System.IO;

public class CreateARPlanePrefab : Editor
{
    [MenuItem("Tools/AR/Create AR Plane Prefab")]
    public static void CreatePlanePrefab()
    {
        // Создаем базовый объект для плоскости
        GameObject planeObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
        planeObject.name = "ARPlaneVisualizer";
        
        // Устанавливаем правильную ориентацию для AR плоскости
        planeObject.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        
        // Добавляем компонент ARPlaneVisualizer
        planeObject.AddComponent<ARPlaneVisualizer>();
        
        // Настраиваем материал
        MeshRenderer renderer = planeObject.GetComponent<MeshRenderer>();
        if (renderer != null && renderer.sharedMaterial != null)
        {
            // Создаем новый материал с полупрозрачностью
            Material planeMaterial = new Material(Shader.Find("Unlit/Color"));
            planeMaterial.color = new Color(0.5f, 0.5f, 0.5f, 0.3f);
            renderer.sharedMaterial = planeMaterial;
            
            // Настраиваем свойства материала для полупрозрачности
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        
        // Создаем директорию, если она не существует
        if (!Directory.Exists("Assets/Prefabs"))
        {
            Directory.CreateDirectory("Assets/Prefabs");
        }
        
        // Сохраняем объект как префаб
        string prefabPath = "Assets/Prefabs/ARPlaneVisualizer.prefab";
        
        // В Unity 2018.3 и выше используем PrefabUtility.SaveAsPrefabAsset
#if UNITY_2018_3_OR_NEWER
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(planeObject, prefabPath);
#else
        GameObject prefab = PrefabUtility.CreatePrefab(prefabPath, planeObject);
#endif
        
        // Удаляем временный объект сцены
        DestroyImmediate(planeObject);
        
        // Выводим сообщение об успешном создании
        Debug.Log($"AR Plane Prefab создан по пути: {prefabPath}");
        
        // Выделяем созданный префаб в Project view
        Selection.activeObject = prefab;
    }
} 