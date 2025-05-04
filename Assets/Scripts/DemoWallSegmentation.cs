using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.UI;
using System.Linq; // Для использования LINQ запросов
using System;

/// <summary>
/// Демонстрационная реализация сегментации стен для отладки без использования моделей машинного обучения.
/// Использует простые геометрические правила для идентификации вертикальных плоскостей как стен.
/// </summary>
public class DemoWallSegmentation : MonoBehaviour
{
    [Header("AR Components")]
    [SerializeField] private ARPlaneManager planeManager;
    [SerializeField] private ARCameraManager cameraManager;
    
    [Header("Debug Visualization")]
    [SerializeField] private bool showDebugVisualization = true;
    [SerializeField] private RawImage debugImage;
    [SerializeField] private float updateInterval = 0.5f; // как часто обновлять визуализацию
    [SerializeField] private Text wallCountText; // Текст для отображения количества стен
    [SerializeField] private Color wallOverlayColor = new Color(1, 0, 0, 0.5f); // Цвет наложения для стен
    
    [Header("Fullscreen Mode Settings")]
    [SerializeField] private float fullscreenAlpha = 0.7f; // Прозрачность визуализации
    [SerializeField] private Color fullscreenWallColor = new Color(0.8f, 0.2f, 0.2f, 0.5f); // Цвет стен
    [SerializeField] private int fullscreenBrushSize = 12; // Размер кисти
    
    [Header("UI Elements")]
    [SerializeField] private bool showHelpTooltip = true; // Показывать подсказку о переключении режима
    [SerializeField] private float tooltipDuration = 3f; // Длительность отображения подсказки в секундах
    
    // Приватные переменные
    private Texture2D segmentationTexture;
    private bool isProcessing = false;
    private float lastUpdateTime = 0;
    private int lastWallCount = 0;
    private RectTransform debugImageRectTransform; // Ссылка на RectTransform для изменения размера
    
    // Start is called before the first frame update
    void Start()
    {
        if (planeManager == null)
            planeManager = UnityEngine.Object.FindAnyObjectByType<ARPlaneManager>();
            
        if (cameraManager == null)
            cameraManager = UnityEngine.Object.FindAnyObjectByType<ARCameraManager>();
            
        // Подписываемся на события изменения плоскостей
        planeManager.planesChanged += OnPlanesChanged;
        
        // Создаем текстуру для отображения сегментации
        segmentationTexture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGBA32, false);
        ClearSegmentationTexture();
        
        // Получаем ссылку на RectTransform для debugImage
        if (debugImage != null)
        {
            debugImageRectTransform = debugImage.GetComponent<RectTransform>();
            UpdateDebugImageSize();
        }
    }
    
    // Update is called once per frame
    void Update()
    {
        // Периодически обновляем визуализацию
        if (Time.time - lastUpdateTime > updateInterval && !isProcessing)
        {
            lastUpdateTime = Time.time;
            StartCoroutine(UpdateSegmentation());
        }
    }
    
    // Обновление сегментации на основе обнаруженных плоскостей
    private IEnumerator UpdateSegmentation()
    {
        isProcessing = true;
        
        // Очищаем текстуру
        ClearSegmentationTexture();
        
        // Отладочная информация - сколько стен обнаружено
        int wallCount = 0;
        
        // Для каждой вертикальной плоскости, отмечаем её пиксели как "стену"
        foreach (var plane in planeManager.trackables)
        {
            if (IsWall(plane))
            {
                wallCount++;
                // Проецируем вершины плоскости на экран
                MarkPlaneOnTexture(plane);
            }
        }
        
        // Обновляем UI текст с количеством стен, если он назначен
        if (wallCountText != null)
        {
            wallCountText.text = $"Стены: {wallCount}";
        }
        
        // Если количество стен изменилось, выводим сообщение
        if (wallCount != lastWallCount)
        {
            Debug.Log($"DemoWallSegmentation: обнаружено вертикальных плоскостей: {wallCount}");
            lastWallCount = wallCount;
        }
        
        // Применяем изменения к текстуре
        segmentationTexture.Apply();
        
        // Отображаем результат
        if (showDebugVisualization && debugImage != null)
        {
            // Обновляем текстуру
            debugImage.texture = segmentationTexture;
            
            // Обновляем размеры в соответствии с режимом отображения
            UpdateDebugImageAppearance();
            
            // Выводим сообщение только при первом обновлении или при изменении количества стен
            if (Time.frameCount % 300 == 0 || wallCount != lastWallCount)
            {
                Debug.Log("DemoWallSegmentation: Обновлена текстура отладки");
            }
        }
        else
        {
            if (Time.frameCount % 300 == 0) // Ограничиваем частоту сообщений
            {
                Debug.LogWarning($"DemoWallSegmentation: Проблема с отображением отладки. showDebugVisualization={showDebugVisualization}, debugImage={debugImage != null}");
            }
        }
        
        yield return null;
        isProcessing = false;
    }
    
    // Определяем, является ли плоскость стеной (вертикальной)
    private bool IsWall(ARPlane plane)
    {
        bool isVertical = plane.alignment == UnityEngine.XR.ARSubsystems.PlaneAlignment.Vertical;
        
        if (isVertical)
        {
            Debug.Log($"Обнаружена вертикальная плоскость (стена): {plane.trackableId}");
        }
        
        return isVertical;
    }
    
    // Проецируем плоскость на текстуру
    private void MarkPlaneOnTexture(ARPlane plane)
    {
        Debug.Log($"MarkPlaneOnTexture: Начинаем обработку плоскости {plane.trackableId}");
        
        // Проверяем наличие компонента MeshFilter
        MeshFilter meshFilter = plane.GetComponent<MeshFilter>();
        Mesh mesh = null;
        
        // Проверяем наличие камеры
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            // Если Camera.main не доступна, пробуем найти любую камеру
            mainCamera = FindObjectOfType<Camera>();
            if (mainCamera == null)
            {
                Debug.LogError("MarkPlaneOnTexture: Не найдена основная камера (Camera.main). Визуализация плоскостей невозможна.");
                return;
            }
            else
            {
                Debug.LogWarning("MarkPlaneOnTexture: Camera.main не доступна, используется альтернативная камера.");
            }
        }
        
        if (meshFilter != null)
        {
            // Используем mesh из MeshFilter, если он есть
            mesh = meshFilter.mesh;
            Debug.Log($"MarkPlaneOnTexture: Найден MeshFilter на плоскости {plane.trackableId}");
        }
        else
        {
            try
            {
                Debug.Log($"MarkPlaneOnTexture: MeshFilter не найден, ищем альтернативы для плоскости {plane.trackableId}");
                
                // Если MeshFilter не найден, пробуем получить mesh через ARPlaneMeshVisualizer
                ARPlaneMeshVisualizer planeMeshVisualizer = plane.GetComponent<ARPlaneMeshVisualizer>();
                
                // Если компонент не найден напрямую, ищем в дочерних объектах
                if (planeMeshVisualizer == null)
                {
                    planeMeshVisualizer = plane.GetComponentInChildren<ARPlaneMeshVisualizer>();
                    Debug.Log($"MarkPlaneOnTexture: Поиск ARPlaneMeshVisualizer в дочерних объектах: {(planeMeshVisualizer != null ? "найден" : "не найден")}");
                }
                else
                {
                    Debug.Log($"MarkPlaneOnTexture: ARPlaneMeshVisualizer найден непосредственно на плоскости");
                }
                
                if (planeMeshVisualizer != null && planeMeshVisualizer.mesh != null)
                {
                    mesh = planeMeshVisualizer.mesh;
                    Debug.Log($"MarkPlaneOnTexture: Использован mesh из ARPlaneMeshVisualizer для плоскости {plane.trackableId}, вершин: {mesh.vertexCount}");
                }
                
                // Если и через ARPlaneMeshVisualizer не получилось, пробуем получить меш из дочерних объектов
                if (mesh == null)
                {
                    Debug.Log($"MarkPlaneOnTexture: Mesh через ARPlaneMeshVisualizer не получен, ищем MeshFilter в дочерних объектах");
                    
                    // Проверяем, есть ли MeshFilter на дочерних объектах
                    MeshFilter[] childMeshFilters = plane.GetComponentsInChildren<MeshFilter>();
                    Debug.Log($"MarkPlaneOnTexture: Найдено {childMeshFilters.Length} MeshFilter компонентов в дочерних объектах");
                    
                    if (childMeshFilters.Length > 0 && childMeshFilters[0] != null && childMeshFilters[0].mesh != null)
                    {
                        mesh = childMeshFilters[0].mesh;
                        Debug.Log($"MarkPlaneOnTexture: Использован mesh из дочернего MeshFilter для плоскости {plane.trackableId}, вершин: {mesh.vertexCount}");
                    }
                }
                
                // Если mesh все еще не доступен, создаем запасной меш
                if (mesh == null || mesh.vertexCount == 0)
                {
                    Debug.LogWarning($"MarkPlaneOnTexture: Не удалось получить mesh для плоскости {plane.trackableId}, создаю запасной меш");
                    mesh = CreateFallbackPlaneMesh(plane);
                    
                    if (mesh == null)
                    {
                        Debug.LogError($"MarkPlaneOnTexture: Не удалось создать даже запасной mesh для плоскости {plane.trackableId}");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"MarkPlaneOnTexture: Ошибка при попытке получить mesh: {ex.Message}");
                return;
            }
        }
        
        var vertices = mesh.vertices;
        var planeTransform = plane.transform;
        
        // Используем цвет из настроек для визуализации в зависимости от режима
        Color currentWallColor = fullscreenWallColor;
        Color edgeColor = new Color(currentWallColor.r, currentWallColor.g + 0.2f, currentWallColor.b, currentWallColor.a + 0.2f); // Более яркий цвет для краев
        
        // Проецируем каждую вершину на экран и закрашиваем соответствующие области
        foreach (var vertex in vertices)
        {
            // Переводим из локальных координат плоскости в мировые
            Vector3 worldPos = planeTransform.TransformPoint(vertex);
            
            // Проецируем из мировых координат в экранные
            Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);
            
            if (screenPos.z > 0) // если точка перед камерой
            {
                // Закрашиваем пиксель
                int x = Mathf.RoundToInt(screenPos.x);
                int y = Mathf.RoundToInt(screenPos.y);
                
                // Проверяем границы экрана
                if (x >= 0 && x < segmentationTexture.width && y >= 0 && y < segmentationTexture.height)
                {
                    segmentationTexture.SetPixel(x, y, currentWallColor);
                    
                    // Увеличиваем область закрашивания для лучшей видимости
                    for (int dx = -fullscreenBrushSize; dx <= fullscreenBrushSize; dx++)
                    {
                        for (int dy = -fullscreenBrushSize; dy <= fullscreenBrushSize; dy++)
                        {
                            // Рассчитываем расстояние от центра
                            float distance = Mathf.Sqrt(dx * dx + dy * dy);
                            if (distance <= fullscreenBrushSize)
                            {
                                int nx = x + dx;
                                int ny = y + dy;
                                if (nx >= 0 && nx < segmentationTexture.width && ny >= 0 && ny < segmentationTexture.height)
                                {
                                    // Применяем цвет с затуханием от центра
                                    float alpha = 1.0f - (distance / fullscreenBrushSize);
                                    Color pixelColor = distance < fullscreenBrushSize/2 ? currentWallColor : edgeColor;
                                    
                                    // Задаем прозрачность в зависимости от режима
                                    float modeAlpha = 0.6f;
                                    pixelColor.a *= alpha * modeAlpha;
                                    
                                    // Комбинируем с текущим цветом для сглаживания
                                    Color currentColor = segmentationTexture.GetPixel(nx, ny);
                                    if (currentColor.a < pixelColor.a)
                                    {
                                        segmentationTexture.SetPixel(nx, ny, pixelColor);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        
        try
        {
            // Дополнительно - выделяем границы плоскости
            var indices = mesh.triangles;
            for (int i = 0; i < indices.Length; i += 3)
            {
                var v1 = planeTransform.TransformPoint(vertices[indices[i]]);
                var v2 = planeTransform.TransformPoint(vertices[indices[i+1]]);
                var v3 = planeTransform.TransformPoint(vertices[indices[i+2]]);
                
                DrawLine(mainCamera.WorldToScreenPoint(v1), mainCamera.WorldToScreenPoint(v2), edgeColor);
                DrawLine(mainCamera.WorldToScreenPoint(v2), mainCamera.WorldToScreenPoint(v3), edgeColor);
                DrawLine(mainCamera.WorldToScreenPoint(v3), mainCamera.WorldToScreenPoint(v1), edgeColor);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"MarkPlaneOnTexture: Ошибка при отрисовке треугольников: {ex.Message}");
        }
    }
    
    // Вспомогательный метод для рисования линий
    private void DrawLine(Vector3 start, Vector3 end, Color color)
    {
        if (start.z <= 0 || end.z <= 0) return; // Пропускаем точки за камерой
        
        int x0 = Mathf.RoundToInt(start.x);
        int y0 = Mathf.RoundToInt(start.y);
        int x1 = Mathf.RoundToInt(end.x);
        int y1 = Mathf.RoundToInt(end.y);
        
        // Алгоритм Брезенхэма для рисования линии
        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;
        
        // Увеличиваем толщину линии в полноэкранном режиме
        int lineThickness = 3;
        float lineOpacity = 0.8f;
        
        while (true)
        {
            // Проверяем границы текстуры
            if (x0 >= 0 && x0 < segmentationTexture.width && y0 >= 0 && y0 < segmentationTexture.height)
            {
                // Создаем немного более яркий цвет для линий, чтобы они выделялись
                Color lineColor = new Color(color.r * 1.2f, color.g * 1.2f, color.b * 1.2f, color.a * lineOpacity);
                segmentationTexture.SetPixel(x0, y0, lineColor);
                
                // Делаем линию толще для лучшей видимости
                for (int i = -lineThickness; i <= lineThickness; i++)
                {
                    for (int j = -lineThickness; j <= lineThickness; j++)
                    {
                        // Проверяем расстояние от центра для создания более округлой линии
                        float dist = Mathf.Sqrt(i*i + j*j);
                        if (dist <= lineThickness)
                        {
                            int nx = x0 + i;
                            int ny = y0 + j;
                            if (nx >= 0 && nx < segmentationTexture.width && ny >= 0 && ny < segmentationTexture.height)
                            {
                                // Уменьшаем непрозрачность по мере удаления от центра
                                float fadeAlpha = 0.7f * (1.0f - dist / lineThickness);
                                Color fadeColor = lineColor;
                                fadeColor.a *= fadeAlpha;
                                
                                // Проверяем текущий цвет и комбинируем с ним
                                Color currentColor = segmentationTexture.GetPixel(nx, ny);
                                if (currentColor.a < fadeColor.a)
                                {
                                    segmentationTexture.SetPixel(nx, ny, fadeColor);
                                }
                            }
                        }
                    }
                }
            }
            
            if (x0 == x1 && y0 == y1) break;
            
            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }
    
    // Очистка текстуры сегментации
    private void ClearSegmentationTexture()
    {
        Color[] clearColors = new Color[segmentationTexture.width * segmentationTexture.height];
        for (int i = 0; i < clearColors.Length; i++)
        {
            clearColors[i] = new Color(0, 0, 0, 0); // полностью прозрачный
        }
        
        segmentationTexture.SetPixels(clearColors);
        segmentationTexture.Apply();
    }
    
    // Обработчик события изменения плоскостей
    private void OnPlanesChanged(ARPlanesChangedEventArgs args)
    {
        // Обновляем сегментацию при изменении плоскостей
        StartCoroutine(UpdateSegmentation());
    }
    
    // Очистка ресурсов
    private void OnDestroy()
    {
        if (planeManager != null)
        {
            planeManager.planesChanged -= OnPlanesChanged;
        }
    }
    
    /// <summary>
    /// Создает запасной простой меш для плоскости, если не удалось получить меш другими способами
    /// </summary>
    private Mesh CreateFallbackPlaneMesh(ARPlane plane)
    {
        Debug.Log($"DemoWallSegmentation: Создание запасного меша для плоскости {plane.trackableId}");
        
        try
        {
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
        catch (Exception ex)
        {
            Debug.LogError($"DemoWallSegmentation: Ошибка при создании запасного меша: {ex.Message}");
            return null;
        }
    }
    
    // Метод для обновления размера debugImage
    private void UpdateDebugImageSize()
    {
        if (debugImageRectTransform != null)
        {
            // Растягиваем изображение на весь экран
            debugImageRectTransform.anchorMin = Vector2.zero;
            debugImageRectTransform.anchorMax = Vector2.one;
            debugImageRectTransform.offsetMin = Vector2.zero;
            debugImageRectTransform.offsetMax = Vector2.zero;
        }
    }
    
    // Метод для обновления внешнего вида debugImage
    private void UpdateDebugImageAppearance()
    {
        // Обновляем размер
        UpdateDebugImageSize();
        
        // Обновляем прозрачность
        if (debugImage != null)
        {
            Color imageColor = debugImage.color;
            imageColor.a = fullscreenAlpha;
            debugImage.color = imageColor;
        }
    }
} 