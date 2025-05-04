using UnityEngine;
using UnityEngine.XR.ARFoundation;
using System.Collections;
using System; // Для доступа к системным типам
using System.Linq; // Для использования LINQ
using System.Text; // Для использования StringBuilder

/// <summary>
/// Контроллер для управления AR плоскостями
/// </summary>
public class ARPlaneController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ARPlaneManager planeManager;
    
    [Header("Settings")]
    [SerializeField] private bool forceUpdateOnStart = true;
    [SerializeField] private float updateDelay = 1.0f;
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool hideDefaultPlanes = true; // Скрывать стандартные AR плоскости
    
    [Header("Wall Generation Settings")]
    [SerializeField] private bool autoGenerateFullWallModels = true; // Автоматически генерировать полные модели стен
    [SerializeField] private float wallGenerationDelay = 0.5f; // Задержка перед генерацией стен (в секундах)
    
    private void Start()
    {
        if (planeManager == null)
        {
            planeManager = FindObjectOfType<ARPlaneManager>();
            if (planeManager == null)
            {
                Debug.LogError("ARPlaneController: ARPlaneManager не найден в сцене");
                return;
            }
        }
        
        // Подписываемся на событие изменения плоскостей
        planeManager.planesChanged += OnPlanesChanged;
        
        if (forceUpdateOnStart)
        {
            // Запускаем обновление с задержкой, чтобы убедиться, что все компоненты инициализированы
            StartCoroutine(ForceUpdatePlanesWithDelay());
        }
        
        // Запускаем скрытие стандартных плоскостей с задержкой
        if (hideDefaultPlanes)
        {
            StartCoroutine(DelayedHideDefaultPlanes());
        }
        
        // Запускаем генерацию полных моделей стен с задержкой
        if (autoGenerateFullWallModels)
        {
            StartCoroutine(DelayedGenerateWallModels(2.0f));
        }
    }
    
    private void OnDestroy()
    {
        if (planeManager != null)
        {
            planeManager.planesChanged -= OnPlanesChanged;
        }
    }
    
    /// <summary>
    /// Обработчик события изменения плоскостей
    /// </summary>
    private void OnPlanesChanged(ARPlanesChangedEventArgs args)
    {
        // Обрабатываем только добавленные плоскости
        if (args.added != null && args.added.Count > 0)
        {
            Debug.Log($"ARPlaneController: Обнаружено {args.added.Count} новых AR плоскостей");
            
            // Для каждой новой плоскости вызываем обработку
            foreach (var plane in args.added)
            {
                ProcessPlane(plane);
            }
            
            // Уведомляем WallSegmentation о новых плоскостях, чтобы запустить обновление сегментации
            NotifyWallSegmentationAboutNewPlanes(args.added.Count);
            
            // Если включена автоматическая генерация полных моделей стен, запускаем её с небольшой задержкой
            if (autoGenerateFullWallModels)
            {
                StartCoroutine(DelayedGenerateWallModels(wallGenerationDelay));
            }
        }
    }
    
    /// <summary>
    /// Уведомляет WallSegmentation о новых плоскостях для запуска обновления сегментации
    /// </summary>
    private void NotifyWallSegmentationAboutNewPlanes(int planeCount)
    {
        // Находим WallSegmentation и уведомляем его о новых плоскостях
        WallSegmentation wallSegmentation = FindObjectOfType<WallSegmentation>();
        if (wallSegmentation != null)
        {
            // Проверяем наличие метода для обработки новых плоскостей через рефлексию,
            // чтобы не создавать прямую зависимость между классами
            var handleNewPlanesMethod = wallSegmentation.GetType().GetMethod("HandleNewARPlanes", 
                                                             System.Reflection.BindingFlags.Instance | 
                                                             System.Reflection.BindingFlags.Public | 
                                                             System.Reflection.BindingFlags.NonPublic);
            if (handleNewPlanesMethod != null)
            {
                handleNewPlanesMethod.Invoke(wallSegmentation, new object[] { planeCount });
                Debug.Log($"ARPlaneController: WallSegmentation уведомлен о {planeCount} новых плоскостях");
            }
            else
            {
                // Резервный вариант - запрашиваем прямое обновление сегментации
                StartCoroutine(DelayedSegmentationUpdate(wallSegmentation));
            }
        }
    }
    
    /// <summary>
    /// Запускает обновление сегментации с задержкой
    /// </summary>
    private IEnumerator DelayedSegmentationUpdate(WallSegmentation wallSegmentation)
    {
        // Ждем 1 секунду для стабилизации AR плоскостей
        yield return new WaitForSeconds(1.0f);
        
        // Обновляем сегментацию
        if (wallSegmentation != null)
        {
            wallSegmentation.UpdatePlanesSegmentationStatus();
            Debug.Log("ARPlaneController: Запущено обновление статуса сегментации после обнаружения новых плоскостей");
        }
        else
        {
            Debug.LogWarning("ARPlaneController: WallSegmentation компонент не найден, сегментация не выполнена");
        }
    }
    
    /// <summary>
    /// Обрабатывает плоскость - скрывает стандартные AR плоскости
    /// </summary>
    private void ProcessPlane(ARPlane plane)
    {
        if (plane == null) return;
        
        // Получаем компонент визуализации
        ARPlaneVisualizer visualizer = plane.GetComponentInChildren<ARPlaneVisualizer>();
        
        // Если визуализатор найден, настраиваем его
        if (visualizer != null)
        {
            // Вместо отключения MeshRenderer используем флаг isSegmentationPlane
            // По умолчанию все плоскости НЕ являются плоскостями сегментации
            visualizer.SetAsSegmentationPlane(false);
            
            if (enableDebugLogs)
            {
                Debug.Log($"ARPlaneController: Плоскость {plane.trackableId} обработана, установлен isSegmentationPlane=false");
            }
        }
        else
        {
            // Если визуализатор не найден, создаем его
            GameObject visualizerObj = new GameObject("ARPlaneVisualizer");
            visualizerObj.transform.SetParent(plane.transform);
            visualizerObj.transform.localPosition = Vector3.zero;
            visualizerObj.transform.localRotation = Quaternion.identity;
            visualizerObj.transform.localScale = Vector3.one;
            
            // Добавляем необходимые компоненты
            MeshFilter meshFilter = visualizerObj.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = visualizerObj.AddComponent<MeshRenderer>();
            visualizer = visualizerObj.AddComponent<ARPlaneVisualizer>();
            
            // Копируем меш из ARPlane, находя его с помощью компонента ARPlaneMeshVisualizer
            ARPlaneMeshVisualizer planeMeshVisualizer = plane.GetComponent<ARPlaneMeshVisualizer>();

            // Если компонент не найден напрямую, ищем в дочерних объектах
            if (planeMeshVisualizer == null)
            {
                planeMeshVisualizer = plane.GetComponentInChildren<ARPlaneMeshVisualizer>();
            }

            try
            {
                if (planeMeshVisualizer != null && planeMeshVisualizer.mesh != null && planeMeshVisualizer.mesh.vertexCount > 0)
                {
                    Mesh meshCopy = new Mesh();
                    meshCopy.vertices = planeMeshVisualizer.mesh.vertices;
                    meshCopy.triangles = planeMeshVisualizer.mesh.triangles;
                    meshCopy.normals = planeMeshVisualizer.mesh.normals;
                    meshCopy.uv = planeMeshVisualizer.mesh.uv;
                    meshFilter.mesh = meshCopy;
                    
                    if (enableDebugLogs)
                    {
                        Debug.Log($"ARPlaneController: Скопирован меш для плоскости {plane.trackableId} с {planeMeshVisualizer.mesh.vertexCount} вершинами");
                    }
                }
                else
                {
                    Debug.LogWarning($"ARPlaneController: Не удалось получить меш для плоскости {plane.trackableId}, создаю запасной меш");
                    
                    // Создаем запасной меш
                    Mesh fallbackMesh = CreateFallbackPlaneMesh(plane);
                    meshFilter.mesh = fallbackMesh;
                    
                    if (enableDebugLogs)
                    {
                        Debug.Log($"ARPlaneController: Создан запасной меш для плоскости {plane.trackableId}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"ARPlaneController: Ошибка при попытке получить mesh: {ex.Message}");
            }
            
            // Устанавливаем флаг
            visualizer.SetAsSegmentationPlane(false);
            
            if (enableDebugLogs)
            {
                Debug.Log($"ARPlaneController: Создан новый визуализатор для плоскости {plane.trackableId}");
            }
        }
    }
    
    /// <summary>
    /// Скрывает все стандартные AR плоскости, оставляя видимыми только плоскости сегментации
    /// </summary>
    public void HideDefaultPlanes()
    {
        if (planeManager == null) return;
        
        int processedCount = 0;
        
        foreach (var plane in planeManager.trackables)
        {
            ProcessPlane(plane);
            processedCount++;
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"ARPlaneController: Обработано {processedCount} AR плоскостей");
        }
    }
    
    /// <summary>
    /// Обновляет все плоскости с задержкой
    /// </summary>
    private IEnumerator ForceUpdatePlanesWithDelay()
    {
        // Ждем полной инициализации
        yield return new WaitForSeconds(updateDelay);
        
        // Первое обновление
        ForceUpdateAllPlanes();
        
        // Повторное обновление через 1 секунду
        yield return new WaitForSeconds(1.0f);
        
        // И еще раз для уверенности
        ForceUpdateAllPlanes();
        
        if (enableDebugLogs)
        {
            Debug.Log("ARPlaneController: Завершено принудительное обновление всех плоскостей");
        }
    }
    
    /// <summary>
    /// Принудительно обновляет визуализацию всех AR плоскостей
    /// </summary>
    public void ForceUpdateAllPlanes()
    {
        if (planeManager == null || planeManager.trackables.count == 0)
        {
            if (enableDebugLogs)
            {
                Debug.Log("ARPlaneController: Нет доступных AR плоскостей для обновления");
            }
            return;
        }
        
        int updatedCount = 0;
        
        foreach (var plane in planeManager.trackables)
        {
            // Получаем все визуализаторы на плоскости
            ARPlaneVisualizer[] visualizers = plane.GetComponentsInChildren<ARPlaneVisualizer>();
            
            if (visualizers.Length > 0)
            {
                foreach (var visualizer in visualizers)
                {
                    visualizer.UpdateVisual();
                    updatedCount++;
                }
            }
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"ARPlaneController: Обновлено {updatedCount} визуализаторов на {planeManager.trackables.count} AR плоскостях");
        }
    }
    
    /// <summary>
    /// Переключает точное/смещенное размещение для всех визуализаторов
    /// </summary>
    public void ToggleExactPlacementForAll()
    {
        foreach (var plane in planeManager.trackables)
        {
            foreach (var visualizer in plane.GetComponentsInChildren<ARPlaneVisualizer>())
            {
                visualizer.ToggleExactPlacement();
            }
        }
        
        if (enableDebugLogs)
        {
            Debug.Log("ARPlaneController: Переключен режим размещения для всех визуализаторов");
        }
    }
    
    /// <summary>
    /// Переключает расширенное/нормальное отображение для всех визуализаторов
    /// </summary>
    public void ToggleExtendWallsForAll()
    {
        foreach (var plane in planeManager.trackables)
        {
            foreach (var visualizer in plane.GetComponentsInChildren<ARPlaneVisualizer>())
            {
                visualizer.ToggleExtendWalls();
            }
        }
        
        if (enableDebugLogs)
        {
            Debug.Log("ARPlaneController: Переключен режим расширения стен для всех визуализаторов");
        }
    }
    
    /// <summary>
    /// Устанавливает флаг сегментации для всех AR плоскостей
    /// </summary>
    /// <param name="isSegmentationPlane">True - плоскости сегментации, False - обычные плоскости</param>
    public void SetSegmentationFlagForAllPlanes(bool isSegmentationPlane)
    {
        if (planeManager == null)
        {
            Debug.LogWarning("ARPlaneController: planeManager не назначен");
            return;
        }
        
        int updatedCount = 0;
        
        foreach (var plane in planeManager.trackables)
        {
            ARPlaneVisualizer[] visualizers = plane.GetComponentsInChildren<ARPlaneVisualizer>();
            
            foreach (var visualizer in visualizers)
            {
                visualizer.SetAsSegmentationPlane(isSegmentationPlane);
                updatedCount++;
            }
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"ARPlaneController: Установлен флаг isSegmentationPlane={isSegmentationPlane} для {updatedCount} визуализаторов");
        }
    }
    
    /// <summary>
    /// Устанавливает флаг сегментации для определенной AR плоскости
    /// </summary>
    /// <param name="plane">AR плоскость</param>
    /// <param name="isSegmentationPlane">True - плоскость сегментации, False - обычная плоскость</param>
    public void SetSegmentationFlagForPlane(ARPlane plane, bool isSegmentationPlane)
    {
        if (plane == null)
        {
            Debug.LogWarning("ARPlaneController: plane не может быть null");
            return;
        }
        
        ARPlaneVisualizer[] visualizers = plane.GetComponentsInChildren<ARPlaneVisualizer>();
        
        foreach (var visualizer in visualizers)
        {
            visualizer.SetAsSegmentationPlane(isSegmentationPlane);
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"ARPlaneController: Установлен флаг isSegmentationPlane={isSegmentationPlane} для плоскости {plane.trackableId}");
        }
    }
    
    /// <summary>
    /// Скрывает стандартные плоскости с задержкой, чтобы сегментация успела сначала запуститься
    /// </summary>
    private IEnumerator DelayedHideDefaultPlanes()
    {
        // Ждем инициализации сегментации
        yield return new WaitForSeconds(updateDelay);
        
        // Находим компонент WallSegmentation
        WallSegmentation wallSegmentation = FindObjectOfType<WallSegmentation>();
        
        if (wallSegmentation != null)
        {
            // Ждем, пока появится текстура сегментации
            // Используем WaitUntil для ожидания инициализации модели и текстуры сегментации
            float waitStartTime = Time.time;
            float maxWaitTime = 10.0f; // Максимальное время ожидания - 10 секунд
            
            // Ждем, пока не появится текстура сегментации или не истечет время ожидания
            while (wallSegmentation.GetSegmentationTexture() == null && (Time.time - waitStartTime) < maxWaitTime)
            {
                yield return new WaitForSeconds(0.5f);
                
                // Периодически выводим информацию о процессе ожидания
                if (enableDebugLogs && Time.time - waitStartTime > 2.0f)
                {
                    Debug.Log($"ARPlaneController: Ожидание инициализации сегментации... ({(Time.time - waitStartTime):F1} сек)");
                }
            }
            
            // Если текстура появилась, скрываем стандартные плоскости
            if (wallSegmentation.GetSegmentationTexture() != null)
            {
                Debug.Log("ARPlaneController: Текстура сегментации инициализирована, скрываем стандартные AR плоскости");
            }
            else
            {
                Debug.LogWarning("ARPlaneController: Текстура сегментации не появилась после ожидания, всё равно скрываем стандартные AR плоскости");
            }
        }
        else
        {
            Debug.LogWarning("ARPlaneController: Компонент WallSegmentation не найден, скрываем стандартные AR плоскости без проверки сегментации");
            yield return new WaitForSeconds(2.0f);
        }
        
        // В любом случае скрываем стандартные плоскости после ожидания
        HideDefaultPlanes();
    }
    
    /// <summary>
    /// Устанавливает режим точного размещения для всех визуализаторов
    /// </summary>
    /// <param name="exactPlacement">True - точное размещение, False - со смещением</param>
    public void SetExactPlacementForAll(bool exactPlacement)
    {
        if (planeManager == null) return;
        
        int updatedCount = 0;
        
        foreach (var plane in planeManager.trackables)
        {
            foreach (var visualizer in plane.GetComponentsInChildren<ARPlaneVisualizer>())
            {
                // Используем рефлексию для доступа к приватному полю
                var field = visualizer.GetType().GetField("useExactPlacement", 
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                
                if (field != null)
                {
                    bool currentValue = (bool)field.GetValue(visualizer);
                    
                    // Меняем значение только если оно отличается
                    if (currentValue != exactPlacement)
                    {
                        field.SetValue(visualizer, exactPlacement);
                        visualizer.UpdateVisual();
                        updatedCount++;
                    }
                }
            }
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"ARPlaneController: Установлен режим точного размещения ({exactPlacement}) для {updatedCount} визуализаторов");
        }
    }
    
    /// <summary>
    /// Включает или отключает режим отладки позиционирования для всех визуализаторов
    /// </summary>
    /// <param name="enableDebug">True - включить отладку, False - отключить</param>
    public void EnableDebugPositioningForAll(bool enableDebug)
    {
        if (planeManager == null) return;
        
        int updatedCount = 0;
        
        foreach (var plane in planeManager.trackables)
        {
            foreach (var visualizer in plane.GetComponentsInChildren<ARPlaneVisualizer>())
            {
                // Используем рефлексию для доступа к приватному полю
                var field = visualizer.GetType().GetField("debugPositioning", 
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                
                if (field != null)
                {
                    bool currentValue = (bool)field.GetValue(visualizer);
                    
                    // Меняем значение только если оно отличается
                    if (currentValue != enableDebug)
                    {
                        field.SetValue(visualizer, enableDebug);
                        updatedCount++;
                    }
                }
            }
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"ARPlaneController: {(enableDebug ? "Включен" : "Отключен")} режим отладки позиционирования для {updatedCount} визуализаторов");
        }
    }
    
    /// <summary>
    /// Создает запасной простой меш для плоскости, если не удалось получить меш другими способами
    /// </summary>
    private Mesh CreateFallbackPlaneMesh(ARPlane plane)
    {
        Debug.Log($"ARPlaneController: Создание запасного меша для плоскости {plane.trackableId}");
        
        // Создаем новый меш
        Mesh mesh = new Mesh();
        
        // Получаем размеры плоскости
        Vector2 size = plane.size;
        float width = size.x;
        float height = size.y;
        
        // Если размеры слишком малы, увеличиваем их
        if (width < 0.1f) width = 0.1f;
        if (height < 0.1f) height = 0.1f;
        
        // Создаем простой прямоугольник
        Vector3[] vertices = new Vector3[4]
        {
            new Vector3(-width/2, 0, -height/2),
            new Vector3(width/2, 0, -height/2),
            new Vector3(width/2, 0, height/2),
            new Vector3(-width/2, 0, height/2)
        };
        
        // Индексы треугольников
        int[] triangles = new int[6]
        {
            0, 1, 2,
            0, 2, 3
        };
        
        // UV координаты
        Vector2[] uv = new Vector2[4]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(1, 1),
            new Vector2(0, 1)
        };
        
        // Нормали (все смотрят вверх)
        Vector3[] normals = new Vector3[4]
        {
            Vector3.up,
            Vector3.up,
            Vector3.up,
            Vector3.up
        };
        
        // Заполняем меш
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uv;
        mesh.normals = normals;
        
        // Пересчитываем границы меша
        mesh.RecalculateBounds();
        
        return mesh;
    }
    
    /// <summary>
    /// Устанавливает смещение от поверхности для всех визуализаторов плоскостей
    /// </summary>
    /// <param name="offset">Смещение в метрах (отрицательное значение - ближе к поверхности)</param>
    public void SetOffsetFromSurfaceForAll(float offset)
    {
        if (planeManager == null) return;
        
        int updatedCount = 0;
        
        foreach (var plane in planeManager.trackables)
        {
            ARPlaneVisualizer visualizer = plane.GetComponentInChildren<ARPlaneVisualizer>();
            if (visualizer != null)
            {
                visualizer.SetOffsetFromSurface(offset);
                updatedCount++;
            }
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"ARPlaneController: Установлено смещение от поверхности {offset}м для {updatedCount} визуализаторов плоскостей");
        }
    }
    
    /// <summary>
    /// Проверяет положение всех плоскостей и возвращает отчет
    /// </summary>
    /// <returns>Строка с отчетом о проверке всех плоскостей</returns>
    public string CheckAllPlanePositions()
    {
        if (planeManager == null) return "ARPlaneManager не найден";
        
        StringBuilder report = new StringBuilder();
        report.AppendLine("=== ОТЧЕТ О ПОЛОЖЕНИИ ПЛОСКОСТЕЙ ===");
        
        int totalPlanes = 0;
        int verticalPlanes = 0;
        
        foreach (var plane in planeManager.trackables)
        {
            totalPlanes++;
            ARPlaneVisualizer visualizer = plane.GetComponentInChildren<ARPlaneVisualizer>();
            
            if (visualizer != null)
            {
                string planeReport = visualizer.CheckPlanePosition();
                report.AppendLine(planeReport);
                report.AppendLine("---");
                
                // Считаем вертикальные плоскости
                if (plane.alignment == UnityEngine.XR.ARSubsystems.PlaneAlignment.Vertical)
                {
                    verticalPlanes++;
                }
            }
            else
            {
                report.AppendLine($"Плоскость {plane.trackableId}: Визуализатор не найден");
                report.AppendLine("---");
            }
        }
        
        report.AppendLine($"Всего плоскостей: {totalPlanes}, из них вертикальных: {verticalPlanes}");
        report.AppendLine("=================================");
        
        string reportText = report.ToString();
        
        if (enableDebugLogs)
        {
            Debug.Log(reportText);
        }
        
        return reportText;
    }
    
    /// <summary>
    /// Создает полные модели стен на основе обнаруженных AR-плоскостей
    /// </summary>
    public void GenerateFullWallModels()
    {
        if (planeManager == null) return;
        
        int wallCount = 0;
        
        Debug.Log("ARPlaneController: Генерация полных моделей стен...");
        
        // Перебираем все плоскости и создаем полные стены для вертикальных плоскостей
        foreach (var plane in planeManager.trackables)
        {
            if (plane.alignment == UnityEngine.XR.ARSubsystems.PlaneAlignment.Vertical)
            {
                // Получаем визуализатор плоскости
                ARPlaneVisualizer visualizer = plane.GetComponentInChildren<ARPlaneVisualizer>();
                
                if (visualizer != null)
                {
                    // Устанавливаем флаг сегментации (чтобы плоскость была видимой)
                    visualizer.SetAsSegmentationPlane(true);
                    
                    // Включаем расширение стен
                    visualizer.SetExtendWalls(true);
                    
                    // Обновляем визуализацию
                    visualizer.UpdateVisual();
                    
                    wallCount++;
                }
            }
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"ARPlaneController: Созданы полные модели для {wallCount} стен");
        }
    }
    
    /// <summary>
    /// Запускает генерацию полных моделей стен с задержкой
    /// </summary>
    private IEnumerator DelayedGenerateWallModels(float delay)
    {
        // Ждем указанное время
        yield return new WaitForSeconds(delay);
        
        // Генерируем полные модели стен
        GenerateFullWallModels();
    }
} 