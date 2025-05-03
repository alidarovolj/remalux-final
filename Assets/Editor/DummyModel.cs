using UnityEngine;
using UnityEditor;
using System.IO;

#if UNITY_BARRACUDA
using Unity.Barracuda;
#endif

/// <summary>
/// Инструмент для создания фиктивной ONNX-модели для тестирования
/// </summary>
public class DummyModel : EditorWindow
{
    [MenuItem("Tools/AR/Fix PaintedWall Prefab", false, 20)]
    public static void ShowWindow()
    {
        EditorWindow.GetWindow(typeof(DummyModel), false, "Fix PaintedWall");
    }
    
    void OnGUI()
    {
        GUILayout.Label("AR Wall Painting Model and Prefab Fixer", EditorStyles.boldLabel);
        
        EditorGUILayout.HelpBox(
            "Этот инструмент поможет исправить проблемы с префабом стены и моделью сегментации.", 
            MessageType.Info);
        
        GUILayout.Space(10);
        
        if (GUILayout.Button("Fix PaintedWall Prefab"))
        {
            FixPaintedWallPrefab();
        }
        
#if UNITY_BARRACUDA
        GUILayout.Space(10);
        
        if (GUILayout.Button("Create Test ONNX Model"))
        {
            CreateDummyONNXModel();
        }
#else
        EditorGUILayout.HelpBox(
            "Пакет Barracuda не установлен. Добавьте его через Package Manager для создания тестовой ONNX модели.", 
            MessageType.Warning);
#endif
    }
    
    /// <summary>
    /// Исправляет или создает префаб стены для покраски
    /// </summary>
    public static void FixPaintedWallPrefab()
    {
        // 1. Проверяем, существует ли префаб
        string prefabPath = "Assets/Prefabs/PaintedWall.prefab";
        bool prefabExists = File.Exists(prefabPath);
        
        // Если директории нет, создаем ее
        if (!Directory.Exists("Assets/Prefabs"))
        {
            Directory.CreateDirectory("Assets/Prefabs");
        }
        
        // 2. Проверяем/создаем материал
        string materialPath = "Assets/Materials/WallPaintMaterial.mat";
        Material wallMaterial;
        
        if (!Directory.Exists("Assets/Materials"))
        {
            Directory.CreateDirectory("Assets/Materials");
        }
        
        if (File.Exists(materialPath))
        {
            wallMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            Debug.Log("Найден существующий материал для стены");
        }
        else
        {
            // Создаем материал
            wallMaterial = new Material(Shader.Find("Standard"));
            wallMaterial.color = new Color(1f, 1f, 1f, 0.8f);
            AssetDatabase.CreateAsset(wallMaterial, materialPath);
            Debug.Log("Создан новый материал для стены");
        }
        
        // 3. Создаем/обновляем префаб
        GameObject prefabObject;
        
        if (prefabExists)
        {
            prefabObject = PrefabUtility.LoadPrefabContents(prefabPath);
            Debug.Log("Редактирование существующего префаба стены");
        }
        else
        {
            prefabObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            prefabObject.name = "PaintedWall";
            Debug.Log("Создание нового префаба стены");
        }
        
        // 4. Настраиваем компоненты
        MeshRenderer renderer = prefabObject.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = wallMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        
        // 5. Сохраняем префаб
        if (prefabExists)
        {
            PrefabUtility.SaveAsPrefabAsset(prefabObject, prefabPath);
            PrefabUtility.UnloadPrefabContents(prefabObject);
        }
        else
        {
            PrefabUtility.SaveAsPrefabAsset(prefabObject, prefabPath);
            GameObject.DestroyImmediate(prefabObject);
        }
        
        AssetDatabase.Refresh();
        
        // 6. Обновляем ссылки в WallPainter
        WallPainter wallPainter = Object.FindAnyObjectByType<WallPainter>();
        if (wallPainter != null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            
            SerializedObject painterSO = new SerializedObject(wallPainter);
            
            // Устанавливаем префаб стены
            SerializedProperty wallPrefabProp = painterSO.FindProperty("wallPrefab");
            if (wallPrefabProp != null)
            {
                wallPrefabProp.objectReferenceValue = prefab;
            }
            
            // Устанавливаем материал стены
            SerializedProperty wallMaterialProp = painterSO.FindProperty("wallMaterial");
            if (wallMaterialProp != null)
            {
                wallMaterialProp.objectReferenceValue = wallMaterial;
            }
            
            painterSO.ApplyModifiedProperties();
        }
        
        EditorUtility.DisplayDialog("Префаб исправлен", 
            "Префаб стены и материал успешно созданы/обновлены и назначены в WallPainter.", 
            "OK");
    }
    
#if UNITY_BARRACUDA
    /// <summary>
    /// Создает фиктивную ONNX-модель для тестирования сегментации стен
    /// </summary>
    private void CreateDummyONNXModel()
    {
        // Код для создания тестовой модели
        Debug.Log("Эта функция создаст тестовую ONNX модель для отладки");
        
        // Убедимся, что папка существует
        if (!Directory.Exists("Assets/Models"))
        {
            Directory.CreateDirectory("Assets/Models");
        }
        
        EditorUtility.DisplayDialog("Dummy ONNX Model", 
            "Функционал создания тестовой модели еще не реализован. Используйте DemoWallSegmentation для тестирования без ML.", 
            "OK");
    }
#endif
} 