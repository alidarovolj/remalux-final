using UnityEngine;
using UnityEngine.XR.ARFoundation;
using System.Collections;

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
} 