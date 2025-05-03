using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Редакторский скрипт для создания префаба AR плоскости
/// </summary>
public class CreateARPlanePrefab : Editor
{
    [MenuItem("Tools/AR/Create AR Plane Prefab")]
    public static void CreatePlanePrefab()
    {
        // Создаем директорию для префабов, если её ещё нет
        if (!Directory.Exists("Assets/Prefabs"))
        {
            Directory.CreateDirectory("Assets/Prefabs");
        }
        
        // Создаем базовый объект для плоскости
        GameObject planeObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
        planeObject.name = "ARPlaneVisualizer";
        
        // Добавляем компонент ARPlaneVisualizer
        ARPlaneVisualizer planeVisualizer = planeObject.AddComponent<ARPlaneVisualizer>();
        
        // Настраиваем материалы
        // Материал для вертикальных плоскостей (стены)
        Material wallMaterial = new Material(Shader.Find("Unlit/Color"));
        wallMaterial.color = new Color(0.7f, 0.4f, 0.2f, 0.7f); // Коричневый
        AssetDatabase.CreateAsset(wallMaterial, "Assets/Prefabs/WallMaterial.mat");
        
        // Материал для горизонтальных плоскостей (пол)
        Material floorMaterial = new Material(Shader.Find("Unlit/Color"));
        floorMaterial.color = new Color(0.2f, 0.2f, 0.8f, 0.7f); // Синий
        AssetDatabase.CreateAsset(floorMaterial, "Assets/Prefabs/FloorMaterial.mat");
        
        // Назначаем материалы через SerializedObject, чтобы они сохранились в префабе
        SerializedObject serializedObj = new SerializedObject(planeVisualizer);
        SerializedProperty verticalPlaneMaterialProp = serializedObj.FindProperty("verticalPlaneMaterial");
        SerializedProperty horizontalPlaneMaterialProp = serializedObj.FindProperty("horizontalPlaneMaterial");
        
        verticalPlaneMaterialProp.objectReferenceValue = wallMaterial;
        horizontalPlaneMaterialProp.objectReferenceValue = floorMaterial;
        
        serializedObj.ApplyModifiedProperties();
        
        // Настраиваем MeshRenderer
        MeshRenderer renderer = planeObject.GetComponent<MeshRenderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.material = wallMaterial;
        
        // Создаем префаб
        string prefabPath = "Assets/Prefabs/ARPlaneVisualizer.prefab";
        PrefabUtility.SaveAsPrefabAsset(planeObject, prefabPath);
        
        // Удаляем временный объект из сцены
        DestroyImmediate(planeObject);
        
        Debug.Log($"AR Plane Prefab создан: {prefabPath}");
        
        // Выбираем префаб в Project окне
        var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Selection.activeObject = prefabAsset;
        EditorGUIUtility.PingObject(prefabAsset);
    }
} 