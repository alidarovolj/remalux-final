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
    
    // Приватные переменные
    private Texture2D segmentationTexture;
    private bool isProcessing = false;
    private float lastUpdateTime = 0;
    
    // Start is called before the first frame update
    void Start()
    {
        if (planeManager == null)
            planeManager = FindObjectOfType<ARPlaneManager>();
            
        if (cameraManager == null)
            cameraManager = FindObjectOfType<ARCameraManager>();
            
        // Подписываемся на события изменения плоскостей
        planeManager.planesChanged += OnPlanesChanged;
        
        // Создаем текстуру для отображения сегментации
        segmentationTexture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGBA32, false);
        ClearSegmentationTexture();
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
        
        // Для каждой вертикальной плоскости, отмечаем её пиксели как "стену"
        foreach (var plane in planeManager.trackables)
        {
            if (IsWall(plane))
            {
                // Проецируем вершины плоскости на экран
                MarkPlaneOnTexture(plane);
            }
        }
        
        // Применяем изменения к текстуре
        segmentationTexture.Apply();
        
        // Отображаем результат
        if (showDebugVisualization && debugImage != null)
        {
            debugImage.texture = segmentationTexture;
        }
        
        yield return null;
        isProcessing = false;
    }
    
    // Определяем, является ли плоскость стеной (вертикальной)
    private bool IsWall(ARPlane plane)
    {
        return plane.alignment == UnityEngine.XR.ARSubsystems.PlaneAlignment.Vertical;
    }
    
    // Проецируем плоскость на текстуру
    private void MarkPlaneOnTexture(ARPlane plane)
    {
        // Получаем вершины плоскости
        var mesh = plane.GetComponent<MeshFilter>().mesh;
        var vertices = mesh.vertices;
        var planeTransform = plane.transform;
        
        // Проецируем каждую вершину на экран и закрашиваем соответствующие области
        foreach (var vertex in vertices)
        {
            // Переводим из локальных координат плоскости в мировые
            Vector3 worldPos = planeTransform.TransformPoint(vertex);
            
            // Проецируем из мировых координат в экранные
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);
            
            if (screenPos.z > 0) // если точка перед камерой
            {
                // Закрашиваем пиксель красным с полупрозрачностью
                int x = Mathf.RoundToInt(screenPos.x);
                int y = Mathf.RoundToInt(screenPos.y);
                
                // Проверяем границы экрана
                if (x >= 0 && x < segmentationTexture.width && y >= 0 && y < segmentationTexture.height)
                {
                    segmentationTexture.SetPixel(x, y, new Color(1, 0, 0, 0.5f));
                    
                    // Закрашиваем соседние пиксели для лучшей видимости
                    for (int dx = -3; dx <= 3; dx++)
                    {
                        for (int dy = -3; dy <= 3; dy++)
                        {
                            int nx = x + dx;
                            int ny = y + dy;
                            if (nx >= 0 && nx < segmentationTexture.width && ny >= 0 && ny < segmentationTexture.height)
                            {
                                segmentationTexture.SetPixel(nx, ny, new Color(1, 0, 0, 0.3f));
                            }
                        }
                    }
                }
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
} 