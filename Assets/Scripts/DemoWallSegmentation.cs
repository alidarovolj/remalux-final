using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.UI;

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

    // Приватные переменные
    private Texture2D segmentationTexture;
    private bool isProcessing = false;
    private float lastUpdateTime = 0;
    private int lastWallCount = 0;
    private bool isInitialized = false;

    // Start is called before the first frame update
    void Start()
    {
        Debug.Log("DemoWallSegmentation: Start initialization");

        try
        {
            // Создаем текстуру для отображения сегментации
            segmentationTexture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGBA32, false);

            if (planeManager == null)
                planeManager = FindObjectOfType<ARPlaneManager>();

            if (cameraManager == null)
                cameraManager = FindObjectOfType<ARCameraManager>();

            // Если AR компоненты по-прежнему не найдены, используем упрощенный режим для отладки
            if (planeManager == null)
                Debug.LogWarning("DemoWallSegmentation: ARPlaneManager не найден. Используется упрощенный режим.");

            if (cameraManager == null)
                Debug.LogWarning("DemoWallSegmentation: ARCameraManager не найден. Используется упрощенный режим.");

            // Подписываемся на события изменения плоскостей только если planeManager доступен
            if (planeManager != null)
                planeManager.planesChanged += OnPlanesChanged;

            // Очищаем текстуру
            ClearSegmentationTexture();

            // Отмечаем компонент как инициализированный
            isInitialized = true;
            Debug.Log("DemoWallSegmentation: Initialization complete");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"DemoWallSegmentation: Error during initialization: {ex.Message}\n{ex.StackTrace}");
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (!isInitialized) return;

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
        if (!isInitialized || segmentationTexture == null)
        {
            Debug.LogWarning("DemoWallSegmentation: UpdateSegmentation cannot run because component is not initialized or texture is null");
            yield break;
        }

        isProcessing = true;

        // Очищаем текстуру
        ClearSegmentationTexture();

        // Отладочная информация - сколько стен обнаружено
        int wallCount = 0;

        // Для каждой вертикальной плоскости, отмечаем её пиксели как "стену"
        if (planeManager != null)
        {
            foreach (var plane in planeManager.trackables)
            {
                if (plane != null && IsWall(plane))
                {
                    wallCount++;
                    // Проецируем вершины плоскости на экран
                    MarkPlaneOnTexture(plane);
                }
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
            debugImage.texture = segmentationTexture;

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
        if (plane == null) return false;

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
        if (plane == null || segmentationTexture == null) return;

        try
        {
            // Получаем вершины плоскости
            var meshFilter = plane.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.mesh == null) return;

            var mesh = meshFilter.mesh;
            var vertices = mesh.vertices;
            var planeTransform = plane.transform;

            // Более яркий и заметный цвет для стены
            Color wallColor = new Color(1, 0, 0, 0.8f); // Яркий красный с высокой непрозрачностью
            Color edgeColor = new Color(1, 1, 0, 0.6f); // Желтый для краев

            // Проецируем каждую вершину на экран и закрашиваем соответствующие области
            foreach (var vertex in vertices)
            {
                // Переводим из локальных координат плоскости в мировые
                Vector3 worldPos = planeTransform.TransformPoint(vertex);

                // Проецируем из мировых координат в экранные
                if (Camera.main == null) continue;
                Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);

                if (screenPos.z > 0) // если точка перед камерой
                {
                    // Закрашиваем пиксель
                    int x = Mathf.RoundToInt(screenPos.x);
                    int y = Mathf.RoundToInt(screenPos.y);

                    // Проверяем границы экрана
                    if (x >= 0 && x < segmentationTexture.width && y >= 0 && y < segmentationTexture.height)
                    {
                        segmentationTexture.SetPixel(x, y, wallColor);

                        // Увеличиваем область закрашивания для лучшей видимости
                        int brushSize = 8; // Увеличенный размер кисти
                        for (int dx = -brushSize; dx <= brushSize; dx++)
                        {
                            for (int dy = -brushSize; dy <= brushSize; dy++)
                            {
                                // Рассчитываем расстояние от центра
                                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                                if (distance <= brushSize)
                                {
                                    int nx = x + dx;
                                    int ny = y + dy;
                                    if (nx >= 0 && nx < segmentationTexture.width && ny >= 0 && ny < segmentationTexture.height)
                                    {
                                        // Применяем цвет с затуханием от центра
                                        float alpha = 1.0f - (distance / brushSize);
                                        Color pixelColor = distance < brushSize / 2 ? wallColor : edgeColor;
                                        pixelColor.a *= alpha * 0.7f;

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

            // Дополнительно - выделяем границы плоскости
            var indices = mesh.triangles;
            for (int i = 0; i < indices.Length; i += 3)
            {
                if (Camera.main == null) continue;

                var v1 = planeTransform.TransformPoint(vertices[indices[i]]);
                var v2 = planeTransform.TransformPoint(vertices[indices[i + 1]]);
                var v3 = planeTransform.TransformPoint(vertices[indices[i + 2]]);

                DrawLine(Camera.main.WorldToScreenPoint(v1), Camera.main.WorldToScreenPoint(v2), edgeColor);
                DrawLine(Camera.main.WorldToScreenPoint(v2), Camera.main.WorldToScreenPoint(v3), edgeColor);
                DrawLine(Camera.main.WorldToScreenPoint(v3), Camera.main.WorldToScreenPoint(v1), edgeColor);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"DemoWallSegmentation: Error in MarkPlaneOnTexture: {ex.Message}");
        }
    }

    // Вспомогательный метод для рисования линий
    private void DrawLine(Vector3 start, Vector3 end, Color color)
    {
        if (segmentationTexture == null) return;
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

        while (true)
        {
            // Проверяем границы текстуры
            if (x0 >= 0 && x0 < segmentationTexture.width && y0 >= 0 && y0 < segmentationTexture.height)
            {
                segmentationTexture.SetPixel(x0, y0, color);

                // Делаем линию толще для лучшей видимости
                for (int i = -2; i <= 2; i++)
                {
                    for (int j = -2; j <= 2; j++)
                    {
                        int nx = x0 + i;
                        int ny = y0 + j;
                        if (nx >= 0 && nx < segmentationTexture.width && ny >= 0 && ny < segmentationTexture.height)
                        {
                            Color fadeColor = color;
                            fadeColor.a *= 0.7f;
                            segmentationTexture.SetPixel(nx, ny, fadeColor);
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
        if (segmentationTexture == null)
        {
            Debug.LogWarning("DemoWallSegmentation: Can't clear null segmentation texture");
            return;
        }

        try
        {
            Color[] clearColors = new Color[segmentationTexture.width * segmentationTexture.height];
            for (int i = 0; i < clearColors.Length; i++)
            {
                clearColors[i] = new Color(0, 0, 0, 0); // полностью прозрачный
            }

            segmentationTexture.SetPixels(clearColors);
            segmentationTexture.Apply();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"DemoWallSegmentation: Error while clearing texture: {ex.Message}");
        }
    }

    // Обработчик события изменения плоскостей
    private void OnPlanesChanged(ARPlanesChangedEventArgs args)
    {
        if (!isInitialized) return;

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

        if (segmentationTexture != null)
        {
            Destroy(segmentationTexture);
            segmentationTexture = null;
        }
    }
}