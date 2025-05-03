using UnityEngine;
using UnityEditor;
using UnityEngine.XR.ARFoundation;
using System.Collections.Generic;

/// <summary>
/// Редакторский скрипт для обновления визуализации AR плоскостей в текущей сцене
/// </summary>
public class UpdateARPlaneVisualization : Editor
{
    [MenuItem("Tools/AR/Update AR Plane Visualization")]
    public static void UpdatePlaneVisualization()
    {
        // Находим ARPlaneManager в текущей сцене
        ARPlaneManager planeManager = GameObject.FindObjectOfType<ARPlaneManager>();
        if (planeManager == null)
        {
            Debug.LogError("Не найден ARPlaneManager в сцене!");
            return;
        }
        
        // Проверяем, настроен ли prefab
        if (planeManager.planePrefab == null)
        {
            Debug.LogWarning("ARPlaneManager не имеет настроенного префаба плоскости!");
            
            // Пытаемся найти префаб в проекте
            string prefabPath = "Assets/Prefabs/ARPlaneVisualizer.prefab";
            GameObject planePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            
            if (planePrefab != null)
            {
                // Назначаем префаб для ARPlaneManager
                SerializedObject serializedObj = new SerializedObject(planeManager);
                SerializedProperty planePrefabProp = serializedObj.FindProperty("m_PlanePrefab");
                planePrefabProp.objectReferenceValue = planePrefab;
                serializedObj.ApplyModifiedProperties();
                
                Debug.Log($"Автоматически назначен префаб {prefabPath} для ARPlaneManager");
            }
            else
            {
                Debug.LogError($"Префаб {prefabPath} не найден! Сначала создайте его через Tools/AR/Create AR Plane Prefab");
                return;
            }
        }
        
        // Получаем все созданные плоскости
        List<ARPlane> planes = new List<ARPlane>();
        foreach (ARPlane plane in planeManager.trackables)
        {
            planes.Add(plane);
        }
        
        if (planes.Count == 0)
        {
            Debug.Log("В сцене нет AR плоскостей для обновления. Обнаружьте сначала несколько плоскостей.");
            return;
        }
        
        // Обновляем визуализацию каждой плоскости
        int updatedCount = 0;
        foreach (ARPlane plane in planes)
        {
            ARPlaneVisualizer[] visualizers = plane.GetComponentsInChildren<ARPlaneVisualizer>();
            foreach (ARPlaneVisualizer visualizer in visualizers)
            {
                // Вызываем метод Awake и Start через рефлексию для переинициализации
                System.Reflection.MethodInfo awakeMethod = typeof(ARPlaneVisualizer).GetMethod("Awake", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                System.Reflection.MethodInfo startMethod = typeof(ARPlaneVisualizer).GetMethod("Start", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (awakeMethod != null)
                {
                    awakeMethod.Invoke(visualizer, null);
                }
                
                if (startMethod != null)
                {
                    startMethod.Invoke(visualizer, null);
                }
                
                // Форсируем вызов методов обновления
                System.Reflection.MethodInfo updateVisualMethod = typeof(ARPlaneVisualizer).GetMethod("UpdateVisual", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | 
                    System.Reflection.BindingFlags.Instance);
                    
                if (updateVisualMethod != null)
                {
                    updateVisualMethod.Invoke(visualizer, null);
                }
                
                updatedCount++;
            }
        }
        
        Debug.Log($"Обновлено {updatedCount} визуализаторов плоскостей для {planes.Count} AR плоскостей");
    }
} 