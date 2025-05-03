using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Компонент для визуализации AR плоскостей с разными цветами в зависимости от типа плоскости
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
public class ARPlaneVisualizer : MonoBehaviour
{
    [Header("Colors")]
    [SerializeField] private Color horizontalColor = new Color(0.0f, 0.5f, 1.0f, 0.3f); // Синий для пола/потолка
    [SerializeField] private Color verticalColor = new Color(1.0f, 0.5f, 0.0f, 0.3f);   // Оранжевый для стен
    [SerializeField] private Color unknownColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);    // Серый для неизвестных

    private ARPlane plane;
    private MeshRenderer meshRenderer;

    private void Awake()
    {
        plane = GetComponent<ARPlane>();
        meshRenderer = GetComponent<MeshRenderer>();
        
        // Подписываемся на событие изменения плоскости
        if (plane != null)
        {
            plane.boundaryChanged += OnBoundaryChanged;
        }
    }

    private void Start()
    {
        // Инициализируем цвет при старте
        UpdateVisualization();
    }

    private void OnBoundaryChanged(ARPlaneBoundaryChangedEventArgs args)
    {
        // Обновляем визуализацию при изменении границ плоскости
        UpdateVisualization();
    }

    private void UpdateVisualization()
    {
        if (plane == null || meshRenderer == null || meshRenderer.material == null)
            return;

        // Выбираем цвет в зависимости от типа плоскости
        Color planeColor = unknownColor;
        
        switch (plane.alignment)
        {
            case PlaneAlignment.HorizontalUp:
            case PlaneAlignment.HorizontalDown:
                planeColor = horizontalColor;
                break;
                
            case PlaneAlignment.Vertical:
                planeColor = verticalColor;
                break;
            
            default:
                planeColor = unknownColor;
                break;
        }
        
        // Применяем цвет к материалу
        meshRenderer.material.color = planeColor;
    }

    private void OnDestroy()
    {
        if (plane != null)
        {
            plane.boundaryChanged -= OnBoundaryChanged;
        }
    }
} 