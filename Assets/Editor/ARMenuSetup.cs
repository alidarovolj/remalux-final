using UnityEditor;
using UnityEngine;

/// <summary>
/// Класс, определяющий структуру меню AR-инструментов, соответствующую скриншоту
/// </summary>
public static class ARMenuSetup
{
    // Меню для OpenCV for Unity
    [MenuItem("Tools/OpenCV for Unity/Create AR Wall Painting Scene", false, 0)]
    public static void CreateARWallPaintingSceneOpenCV()
    {
        ARSceneCreator.CreateARWallPaintingScene();
    }
    
    // Основное меню AR
    [MenuItem("AR/Create AR Wall Painting Scene", false, 0)]
    public static void CreateARWallPaintingSceneDirectAR()
    {
        ARSceneCreator.CreateARWallPaintingScene();
    }
    
    // Добавляем пункт в подменю Tools/AR
    [MenuItem("Tools/AR/Create AR Wall Painting Scene", false, 0)]
    public static void CreateARWallPaintingSceneToolsAR()
    {
        ARSceneCreator.CreateARWallPaintingScene();
    }
    
    // Пункты меню из скриншота
    [MenuItem("Tools/AR/Fix PaintedWall Prefab", false, 10)]
    public static void FixPaintedWallPrefab()
    {
        DummyModel.ShowWindow();
    }
    
    [MenuItem("Tools/AR/Fix Project Issues", false, 20)]
    public static void FixProjectIssues()
    {
        ARFixReferences.ShowWindow();
    }
    
    [MenuItem("Tools/AR/Fix Script References", false, 30)]
    public static void FixScriptReferences()
    {
        ARFixReferences.FixReferences();
    }
    
    [MenuItem("Tools/AR/Recreate All Assets", false, 40)]
    public static void RecreateAllAssets()
    {
        // Сначала исправляем префаб
        DummyModel.FixPaintedWallPrefab();
        // Затем исправляем ссылки
        ARFixReferences.FixReferences();
    }
    
    [MenuItem("Tools/AR/Setup AR Wall Painting Project", false, 50)]
    public static void SetupARWallPaintingProject()
    {
        // Создаем сцену
        ARSceneCreator.CreateARWallPaintingScene();
        // Исправляем ссылки
        ARFixReferences.FixReferences();
    }
    
    [MenuItem("Tools/AR/Update Project To AR Foundation 6.x", false, 60)]
    public static void UpdateProjectToARFoundation6()
    {
        EditorUtility.DisplayDialog("Обновление AR Foundation",
            "Этот инструмент поможет обновить проект до AR Foundation 6.x.\n\n" +
            "Будут внесены следующие изменения:\n" +
            "1. ARSessionOrigin → XROrigin\n" +
            "2. Обновление ссылок во всех скриптах\n" +
            "3. Настройка TrackedPoseDriver для камеры\n\n" +
            "Рекомендуется сделать резервную копию проекта перед выполнением.", 
            "OK");
            
        // Создаем новую сцену (которая уже использует новые компоненты)
        ARSceneCreator.CreateARWallPaintingScene();
        // Исправляем ссылки
        ARFixReferences.FixReferences();
    }
    
    [MenuItem("Tools/AR/Create Snapshot Components", false, 70)]
    public static void CreateSnapshotComponents()
    {
        EditorUtility.DisplayDialog("Добавление функционала снимков",
            "Этот инструмент добавит функциональность сохранения и загрузки снимков покраски в AR приложении.\n\n" +
            "Будут созданы следующие компоненты:\n" +
            "1. SnapshotManager - для сохранения/загрузки снимков\n" +
            "2. PaintSnapshot - для хранения данных снимков\n" +
            "3. UI элементы для работы со снимками\n\n" +
            "Убедитесь, что у вас есть готовая AR сцена с компонентами WallPainter и UIManager.", 
            "OK");
            
        // Получаем активную сцену и проверяем наличие необходимых компонентов
        var wallPainter = UnityEngine.Object.FindObjectOfType<WallPainter>();
        var uiManager = UnityEngine.Object.FindObjectOfType<UIManager>();
        
        if (wallPainter == null || uiManager == null)
        {
            EditorUtility.DisplayDialog("Ошибка",
                "Не найдены необходимые компоненты (WallPainter или UIManager).\n\n" +
                "Сначала создайте базовую AR сцену через меню Tools/AR/Create AR Wall Painting Scene.",
                "OK");
            return;
        }
        
        // Создаем префаб кнопки снимка
        GameObject snapshotButtonPrefab = ARSceneCreator.CreateSnapshotButtonPrefab();
        
        // Находим Canvas в сцене
        Canvas canvas = UnityEngine.Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Ошибка",
                "Не найден Canvas в сцене.\n\n" +
                "Сначала создайте базовую AR сцену через меню Tools/AR/Create AR Wall Painting Scene.",
                "OK");
            return;
        }
        
        // Создаем UI для снимков
        ARSceneCreator.CreateSnapshotUI(canvas.gameObject, wallPainter);
        
        // Добавляем SnapshotManager
        GameObject snapshotManagerObj = new GameObject("SnapshotManager");
        SnapshotManager snapshotManager = snapshotManagerObj.AddComponent<SnapshotManager>();
        snapshotManager.wallPainter = wallPainter;
        
        EditorUtility.DisplayDialog("Успех",
            "Компоненты для работы со снимками добавлены в сцену.\n\n" +
            "Теперь вы можете:\n" +
            "1. Создавать снимки покраски\n" +
            "2. Переключаться между разными вариантами\n" +
            "3. Сохранять снимки между сессиями",
            "OK");
    }
} 