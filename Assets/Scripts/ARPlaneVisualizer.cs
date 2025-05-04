using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Визуализатор AR плоскостей (стен и пола)
/// </summary>
public class ARPlaneVisualizer : MonoBehaviour
{
    [Header("Материалы")]
    [SerializeField] private Material verticalPlaneMaterial; // Материал для вертикальных плоскостей (стен)
    [SerializeField] private Material horizontalPlaneMaterial; // Материал для горизонтальных плоскостей (пол/потолок)
    
    [Header("Цвета")]
    [SerializeField] private Color wallColor = new Color(0.7f, 0.4f, 0.2f, 0.4f); // Коричневый полупрозрачный (уменьшена прозрачность)
    [SerializeField] private Color floorColor = new Color(0.2f, 0.2f, 0.8f, 0.5f); // Синий полупрозрачный
    [SerializeField] private Color ceilingColor = new Color(0.2f, 0.7f, 0.2f, 0.5f); // Зеленый полупрозрачный
    
    [Header("Настройки визуализации")]
    [SerializeField] private bool useExactPlacement = true; // Использовать точное размещение на плоскости
    [SerializeField] private bool extendWalls = false; // Расширять стены для лучшей визуализации
    [SerializeField] private float minWallHeight = 2.0f; // Минимальная высота стены при расширении
    [SerializeField] private float offsetFromSurface = -0.005f; // Смещение от поверхности (-5 мм) - отрицательное для позиционирования ближе к поверхности
    [SerializeField] private bool debugPositioning = false; // Включить отладку позиционирования
    
    // Новый параметр для определения, является ли это плоскостью сегментации
    [SerializeField] private bool isSegmentationPlane = false;
    
    private ARPlane arPlane;
    private MeshRenderer meshRenderer;
    
    void Awake()
    {
        arPlane = GetComponentInParent<ARPlane>();
        meshRenderer = GetComponent<MeshRenderer>();
        
        // Если материалы не назначены, создаем их
        if (verticalPlaneMaterial == null)
        {
            // Используем shader, который поддерживает прозрачность
            verticalPlaneMaterial = new Material(Shader.Find("Transparent/Diffuse"));
            verticalPlaneMaterial.color = wallColor; // Используем коричневый цвет по умолчанию
        }
        
        if (horizontalPlaneMaterial == null)
        {
            horizontalPlaneMaterial = new Material(Shader.Find("Transparent/Diffuse"));
            horizontalPlaneMaterial.color = new Color(floorColor.r, floorColor.g, floorColor.b, 0.0f); // Полностью прозрачный
        }
        
        // Настраиваем материал для правильного отображения
        if (meshRenderer != null && meshRenderer.material != null)
        {
            // Настраиваем материал для прозрачности
            meshRenderer.material.SetFloat("_Mode", 3); // Fade mode (более качественная прозрачность)
            meshRenderer.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            meshRenderer.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            meshRenderer.material.SetInt("_ZWrite", 0);
            meshRenderer.material.DisableKeyword("_ALPHATEST_ON");
            meshRenderer.material.EnableKeyword("_ALPHABLEND_ON");
            meshRenderer.material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            meshRenderer.material.renderQueue = 3000;
            
            // Настраиваем дополнительные параметры материала для лучшего отображения
            meshRenderer.material.SetFloat("_Glossiness", 0.0f); // Матовая поверхность
            meshRenderer.material.SetFloat("_Metallic", 0.0f); // Не металлическая поверхность
            
            // По умолчанию делаем плоскости полностью прозрачными
            if (!isSegmentationPlane)
            {
                Color currentColor = meshRenderer.material.color;
                meshRenderer.material.color = new Color(currentColor.r, currentColor.g, currentColor.b, 0.0f);
            }
        }
    }
    
    void Start()
    {
        UpdateVisual();
    }
    
    void Update()
    {
        if (arPlane != null && arPlane.trackingState == TrackingState.Tracking)
        {
            // Обновляем визуализацию при изменении размера плоскости
            UpdateVisual();
        }
    }
    
    /// <summary>
    /// Обновляет визуализацию плоскости
    /// </summary>
    public void UpdateVisual()
    {
        if (arPlane == null || meshRenderer == null) return;
        
        // Если это не плоскость сегментации, делаем её полностью прозрачной
        if (!isSegmentationPlane)
        {
            Color currentColor = meshRenderer.material.color;
            meshRenderer.material.color = new Color(currentColor.r, currentColor.g, currentColor.b, 0.0f);
            return;
        }
        
        // Только для плоскостей сегментации продолжаем обычную визуализацию
        // Определяем тип плоскости и назначаем соответствующий материал и цвет
        if (arPlane.alignment == PlaneAlignment.Vertical)
        {
            // Вертикальная плоскость (стена)
            if (meshRenderer.material != verticalPlaneMaterial)
            {
                meshRenderer.material = verticalPlaneMaterial;
            }
            
            // Используем более интенсивный цвет для стен
            Color enhancedWallColor = new Color(
                wallColor.r, 
                wallColor.g, 
                wallColor.b, 
                0.4f); // Устанавливаем прозрачность в 40%
                
            meshRenderer.material.color = enhancedWallColor;
            
            // Корректируем размер и позицию для лучшего представления стены
            AdjustWallVisualization();
            
            // Настраиваем дополнительные параметры рендеринга для стен
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }
        else if (arPlane.alignment == PlaneAlignment.HorizontalUp)
        {
            // Горизонтальная плоскость, смотрящая вверх (пол)
            if (meshRenderer.material != horizontalPlaneMaterial)
            {
                meshRenderer.material = horizontalPlaneMaterial;
            }
            meshRenderer.material.color = floorColor;
        }
        else if (arPlane.alignment == PlaneAlignment.HorizontalDown)
        {
            // Горизонтальная плоскость, смотрящая вниз (потолок)
            if (meshRenderer.material != horizontalPlaneMaterial)
            {
                meshRenderer.material = horizontalPlaneMaterial;
            }
            meshRenderer.material.color = ceilingColor;
        }
        else
        {
            // Другие типы плоскостей
            if (meshRenderer.material != horizontalPlaneMaterial)
            {
                meshRenderer.material = horizontalPlaneMaterial;
            }
            meshRenderer.material.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        }
    }
    
    void AdjustWallVisualization()
    {
        if (arPlane == null) return;
        
        // Получаем размеры плоскости
        var size = arPlane.size;
        float width = size.x;
        float height = size.y;
        
        // 1. Получаем центр плоскости и исходную нормаль
        Vector3 center = arPlane.center;
        Vector3 normal = arPlane.normal.normalized;
        
        // 2. Отладка исходной информации
        if (debugPositioning)
        {
            Debug.Log($"ARPlane: {arPlane.trackableId} - Исходные данные: center={center}, normal={normal}, size={size}, alignment={arPlane.alignment}");
        }
        
        // 3. Проверяем тип плоскости для отладки
        bool isVerticalPlane = arPlane.alignment == PlaneAlignment.Vertical || 
                              (arPlane.alignment == PlaneAlignment.NotAxisAligned && 
                               Vector3.Angle(normal, Vector3.up) > 60f);
        
        // 4. Определяем размеры для визуализации - УВЕЛИЧИВАЕМ РАЗМЕРЫ ДЛЯ ПОЛНОГО ПОКРЫТИЯ СТЕН
        float wallWidth = width;
        float wallHeight = height;
        
        // Для вертикальных плоскостей (стен) всегда увеличиваем размеры для лучшего визуального представления
        if (isVerticalPlane)
        {
            // Минимальная высота стены - 2.5 метра по умолчанию
            float defaultMinWallHeight = 2.5f;
            
            // Минимальная ширина стены - 3 метра или текущая ширина * 1.5, что больше
            float minWidth = Mathf.Max(3.0f, width * 1.5f);
            
            // Расширяем стены для полного покрытия
            wallWidth = Mathf.Max(width, minWidth);
            wallHeight = Mathf.Max(height, extendWalls ? minWallHeight : defaultMinWallHeight);
        }
        
        // 5. Устанавливаем масштаб с новыми размерами
        transform.localScale = new Vector3(wallWidth, wallHeight, 0.01f); // Толщина стены 1 см
        
        // Прежде всего, убедимся, что нормаль вектор не равен нулю
        if (normal.magnitude < 0.001f)
        {
            Debug.LogWarning($"ARPlane: {arPlane.trackableId} - Нормаль плоскости слишком близка к нулю");
            normal = Vector3.forward; // Используем значение по умолчанию
        }
        
        // Для вертикальных стен, гарантируем, что они действительно вертикальные
        if (isVerticalPlane)
        {
            // Проецируем нормаль на горизонтальную плоскость
            Vector3 horizontalNormal = new Vector3(normal.x, 0, normal.z).normalized;
            
            // Если проекция нормали слишком мала, используем вектор вперед или вправо
            if (horizontalNormal.magnitude < 0.001f)
            {
                horizontalNormal = Vector3.forward;
            }
            
            // Устанавливаем горизонтальную нормаль как направление вперед
            Vector3 forwardDirection = -horizontalNormal; // Смотрим против нормали
            
            // Вверх всегда направлен вверх (строго вертикально)
            Vector3 upDirection = Vector3.up;
            
            // Вычисляем правое направление как перпендикулярное к первым двум
            Vector3 rightDirection = Vector3.Cross(upDirection, forwardDirection).normalized;
            
            // Еще раз уточняем направление вперед для ортогональности
            forwardDirection = Vector3.Cross(rightDirection, upDirection).normalized;
            
            // Создаем ортогональную матрицу вращения
            Quaternion rotationMatrix = Quaternion.LookRotation(forwardDirection, upDirection);
            
            // Устанавливаем вращение
            transform.rotation = rotationMatrix;
            
            // Получаем исходное положение от родительского объекта ARPlane
            Vector3 position = arPlane.transform.TransformPoint(center);
            
            // Применяем смещение в направлении нормали
            if (offsetFromSurface != 0)
            {
                // Используем точную горизонтальную нормаль для смещения визуализации
                position += horizontalNormal * offsetFromSurface;
                
                if (debugPositioning)
                {
                    Debug.Log($"ARPlane: {arPlane.trackableId} - Применено смещение от поверхности: {offsetFromSurface}м в направлении нормали");
                }
            }
            
            // Если это стена, смещаем вниз на половину высоты стены, чтобы низ стены был на уровне пола
            if (isVerticalPlane && wallHeight > height)
            {
                float heightDifference = wallHeight - height;
                position.y -= heightDifference * 0.4f; // Смещаем немного вниз, чтобы стена начиналась от пола
            }
            
            // Устанавливаем позицию с учетом корректного смещения
            transform.position = position;
        }
        else
        {
            // Для нестандартных плоскостей (не вертикальных) используем подход с максимальным совпадением с нормалью
            // Стараемся сделать плоскость XY совпадающей с плоскостью обнаружения
            
            // Вектор "вперед" противоположен нормали
            Vector3 forwardDirection = -normal;
            
            // Находим вектор "вверх", который максимально близок к глобальному вектору вверх
            // но при этом перпендикулярен к forwardDirection
            Vector3 approximateUp = Vector3.up;
            Vector3 rightDirection = Vector3.Cross(approximateUp, forwardDirection).normalized;
            
            // Если правый вектор близок к нулю (нормаль почти параллельна вектору вверх)
            // используем другой вектор для расчета правого направления
            if (rightDirection.magnitude < 0.001f)
            {
                rightDirection = Vector3.Cross(Vector3.forward, forwardDirection).normalized;
                
                // Если и это не сработало, используем вектор вправо
                if (rightDirection.magnitude < 0.001f)
                {
                    rightDirection = Vector3.right;
                }
            }
            
            // Рассчитываем точный вектор "вверх", перпендикулярный направлениям "вперед" и "вправо"
            Vector3 upDirection = Vector3.Cross(forwardDirection, rightDirection).normalized;
            
            // Создаем матрицу вращения
            Quaternion rotationMatrix = Quaternion.LookRotation(forwardDirection, upDirection);
            
            // Устанавливаем вращение
            transform.rotation = rotationMatrix;
            
            // Получаем исходное положение от родительского объекта ARPlane
            Vector3 position = arPlane.transform.TransformPoint(center);
            
            // Применяем смещение в направлении нормали
            if (offsetFromSurface != 0)
            {
                position += normal * offsetFromSurface;
                
                if (debugPositioning)
                {
                    Debug.Log($"ARPlane: {arPlane.trackableId} - Применено смещение от поверхности: {offsetFromSurface}м в направлении нормали");
                }
            }
            
            // Устанавливаем позицию
            transform.position = position;
        }
        
        // 8. Отладка
        if (debugPositioning)
        {
            Debug.Log($"ARPlane: {arPlane.trackableId} - Вращение применено: position={transform.position}, rotation={transform.rotation.eulerAngles}");
            Debug.Log($"ARPlane: {arPlane.trackableId} - Локальные оси: forward={transform.forward}, up={transform.up}, right={transform.right}");
            Debug.Log($"ARPlane: {arPlane.trackableId} - Размеры плоскости: исходные={width}x{height}, после корректировки={wallWidth}x{wallHeight}");
            
            // Визуализируем векторы
            Debug.DrawRay(transform.position, normal * 0.5f, Color.blue, 0.5f);
            Debug.DrawRay(transform.position, transform.forward * 0.5f, Color.red, 0.5f);
            Debug.DrawRay(transform.position, transform.up * 0.5f, Color.green, 0.5f);
            Debug.DrawRay(transform.position, transform.right * 0.5f, Color.yellow, 0.5f);
            
            // Визуализируем углы плоскости
            Debug.DrawLine(transform.position + transform.up * wallHeight/2 + transform.right * wallWidth/2, 
                          transform.position + transform.up * wallHeight/2 - transform.right * wallWidth/2, 
                          Color.magenta, 0.5f);
            Debug.DrawLine(transform.position - transform.up * wallHeight/2 + transform.right * wallWidth/2, 
                          transform.position - transform.up * wallHeight/2 - transform.right * wallWidth/2, 
                          Color.magenta, 0.5f);
            Debug.DrawLine(transform.position + transform.up * wallHeight/2 + transform.right * wallWidth/2, 
                          transform.position - transform.up * wallHeight/2 + transform.right * wallWidth/2, 
                          Color.magenta, 0.5f);
            Debug.DrawLine(transform.position + transform.up * wallHeight/2 - transform.right * wallWidth/2, 
                          transform.position - transform.up * wallHeight/2 - transform.right * wallWidth/2, 
                          Color.magenta, 0.5f);
        }
    }
    
    /// <summary>
    /// Переключает режим точного размещения
    /// </summary>
    public void ToggleExactPlacement()
    {
        useExactPlacement = !useExactPlacement;
        UpdateVisual();
    }
    
    /// <summary>
    /// Переключает режим расширения стен
    /// </summary>
    public void ToggleExtendWalls()
    {
        extendWalls = !extendWalls;
        UpdateVisual();
    }
    
    /// <summary>
    /// Принудительно обновляет все визуализаторы AR-плоскостей в сцене
    /// </summary>
    public static void UpdateAllPlaneVisualizers()
    {
        ARPlaneManager planeManager = Object.FindObjectOfType<ARPlaneManager>();
        if (planeManager == null) return;
        
        foreach (var plane in planeManager.trackables)
        {
            foreach (var visualizer in plane.GetComponentsInChildren<ARPlaneVisualizer>())
            {
                visualizer.UpdateVisual();
            }
        }
        
        Debug.Log("Обновлены все визуализаторы AR-плоскостей");
    }
    
    /// <summary>
    /// Устанавливает флаг, является ли плоскость частью сегментации стен
    /// </summary>
    /// <param name="segmentationPlane">True - плоскость сегментации, False - обычная плоскость</param>
    public void SetAsSegmentationPlane(bool segmentationPlane)
    {
        isSegmentationPlane = segmentationPlane;
        UpdateVisual();
    }
    
    /// <summary>
    /// Возвращает текущее значение флага сегментации
    /// </summary>
    public bool IsSegmentationPlane()
    {
        return isSegmentationPlane;
    }
    
    /// <summary>
    /// Устанавливает режим отладки для визуализатора
    /// </summary>
    /// <param name="enableDebug">True - включить отладочную визуализацию, False - выключить</param>
    public void SetDebugMode(bool enableDebug)
    {
        debugPositioning = enableDebug;
        UpdateVisual();
    }
    
    /// <summary>
    /// Устанавливает флаг расширения стен
    /// </summary>
    /// <param name="extend">True - расширять стены, False - использовать оригинальные размеры</param>
    public void SetExtendWalls(bool extend)
    {
        extendWalls = extend;
        UpdateVisual();
    }
    
    /// <summary>
    /// Устанавливает смещение от поверхности для визуализации
    /// </summary>
    /// <param name="offset">Смещение в метрах (отрицательное значение - ближе к поверхности)</param>
    public void SetOffsetFromSurface(float offset)
    {
        offsetFromSurface = offset;
        UpdateVisual();
        
        if (debugPositioning)
        {
            Debug.Log($"ARPlaneVisualizer: Установлено новое смещение от поверхности: {offsetFromSurface}м");
        }
    }
    
    /// <summary>
    /// Возвращает текущее смещение от поверхности
    /// </summary>
    public float GetOffsetFromSurface()
    {
        return offsetFromSurface;
    }
    
    /// <summary>
    /// Проверяет и корректирует положение плоскости относительно реальной стены
    /// </summary>
    /// <returns>Информацию о проверке положения</returns>
    public string CheckPlanePosition()
    {
        if (arPlane == null) return "Плоскость не найдена";
        
        // Получаем позицию и нормаль
        Vector3 center = arPlane.center;
        Vector3 normal = arPlane.normal.normalized;
        Vector3 position = arPlane.transform.TransformPoint(center);
        
        // Проверяем, является ли плоскость вертикальной
        bool isVerticalPlane = arPlane.alignment == PlaneAlignment.Vertical || 
                              (arPlane.alignment == PlaneAlignment.NotAxisAligned && 
                               Vector3.Angle(normal, Vector3.up) > 60f);
                               
        // Получаем угол отклонения от вертикали для вертикальных плоскостей
        float verticalDeviation = 0;
        if (isVerticalPlane)
        {
            // Угол между нормалью и горизонтальной плоскостью
            Vector3 horizontalNormal = new Vector3(normal.x, 0, normal.z).normalized;
            verticalDeviation = Vector3.Angle(normal, horizontalNormal);
            
            // Корректируем знак в зависимости от направления отклонения
            if (normal.y < 0) verticalDeviation = -verticalDeviation;
        }
        
        // Формируем отчет
        string report = $"Плоскость {arPlane.trackableId}:\n" +
                       $"- Тип: {(isVerticalPlane ? "Вертикальная (стена)" : "Не вертикальная")}\n" +
                       $"- Положение: {position}\n" +
                       $"- Нормаль: {normal}\n" +
                       $"- Отклонение от вертикали: {verticalDeviation:F1}°\n" +
                       $"- Смещение от поверхности: {offsetFromSurface*1000:F1} мм";
                       
        if (debugPositioning)
        {
            Debug.Log(report);
        }
        
        return report;
    }
} 