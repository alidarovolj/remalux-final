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
    [SerializeField] private Color wallColor = new Color(0.7f, 0.4f, 0.2f, 0.7f); // Коричневый полупрозрачный
    [SerializeField] private Color floorColor = new Color(0.2f, 0.2f, 0.8f, 0.7f); // Синий полупрозрачный
    [SerializeField] private Color ceilingColor = new Color(0.2f, 0.7f, 0.2f, 0.7f); // Зеленый полупрозрачный
    
    [Header("Настройки визуализации")]
    [SerializeField] private bool useExactPlacement = true; // Использовать точное размещение на плоскости
    [SerializeField] private bool extendWalls = false; // Расширять стены для лучшей визуализации
    [SerializeField] private float minWallHeight = 2.0f; // Минимальная высота стены при расширении
    [SerializeField] private float offsetFromSurface = 0.005f; // Смещение от поверхности (5 мм)
    [SerializeField] private bool debugPositioning = false; // Включить отладку позиционирования
    
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
            verticalPlaneMaterial.color = new Color(0.0f, 0.2f, 1.0f, 0.7f); // Ярко-синий полупрозрачный
            wallColor = verticalPlaneMaterial.color; // Синхронизируем цвет
        }
        
        if (horizontalPlaneMaterial == null)
        {
            horizontalPlaneMaterial = new Material(Shader.Find("Transparent/Diffuse"));
            horizontalPlaneMaterial.color = floorColor;
        }
        
        // Настраиваем материал для правильного отображения
        if (meshRenderer != null && meshRenderer.material != null)
        {
            meshRenderer.material.SetFloat("_Mode", 2); // Transparent mode
            meshRenderer.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            meshRenderer.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            meshRenderer.material.SetInt("_ZWrite", 0);
            meshRenderer.material.DisableKeyword("_ALPHATEST_ON");
            meshRenderer.material.EnableKeyword("_ALPHABLEND_ON");
            meshRenderer.material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            meshRenderer.material.renderQueue = 3000;
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
        
        // Определяем тип плоскости и назначаем соответствующий материал и цвет
        if (arPlane.alignment == PlaneAlignment.Vertical)
        {
            // Вертикальная плоскость (стена)
            if (meshRenderer.material != verticalPlaneMaterial)
            {
                meshRenderer.material = verticalPlaneMaterial;
            }
            meshRenderer.material.color = wallColor;
            
            // Корректируем размер и позицию для лучшего представления стены
            AdjustWallVisualization();
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
        
        // Получаем информацию об AR плоскости
        Vector3 center = arPlane.center;
        Vector3 normal = arPlane.normal;
        
        // Переменные для определения режима визуализации
        float wallHeight = height;
        Vector3 visualPosition = center;
        
        if (extendWalls && arPlane.alignment == PlaneAlignment.Vertical)
        {
            // Расширенный режим - увеличиваем высоту стены для лучшей визуализации
            wallHeight = Mathf.Max(height, 2.0f); // Минимальная высота стены 2 метра
            
            // Корректируем положение центра с учетом увеличенной высоты
            float heightDifference = wallHeight - height;
            // Для стен сдвигаем центр вверх на половину разницы высот
            visualPosition.y += heightDifference * 0.5f;
        }
        
        // Параметр смещения от стены для избежания z-fighting
        float offsetFromWall = 0.002f; // 2 мм
        
        if (useExactPlacement)
        {
            // Используем точное размещение без смещения
            offsetFromWall = 0;
        }
        
        // Применяем масштаб
        transform.localScale = new Vector3(width, wallHeight, 1.0f);
        
        // Устанавливаем положение визуализации
        transform.position = visualPosition;
        
        // Ориентируем визуализацию по нормали плоскости
        if (normal != Vector3.zero)
        {
            // Создаем поворот, смотрящий в сторону нормали
            transform.rotation = Quaternion.LookRotation(-normal);
            
            // Применяем небольшое смещение в направлении нормали для избежания z-fighting
            transform.position += normal * offsetFromWall;
        }
        
        // Отладка позиции
        if (debugPositioning)
        {
            Debug.Log($"ARPlane: {arPlane.trackableId} - Позиция: {transform.position}, Нормаль: {normal}, Размер: {width}x{wallHeight}");
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
} 