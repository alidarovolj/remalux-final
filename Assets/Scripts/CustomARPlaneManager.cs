using UnityEngine;
using UnityEngine.XR.ARFoundation;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Управляет AR плоскостями и решает проблемы с отображением
/// </summary>
public class CustomARPlaneManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private UnityEngine.XR.ARFoundation.ARPlaneManager unityARPlaneManager;
    [SerializeField] private ARPlaneController planeController;
    
    [Header("Settings")]
    [SerializeField] private bool showPlaneVisualizers = false; // Показывать ли AR плоскости
    [SerializeField] private Material defaultPlaneMaterial; // Материал для плоскостей AR Foundation
    [SerializeField] private Color planeColor = new Color(0.5f, 0.5f, 0.5f, 0.0f); // Полностью прозрачная
    
    private List<ARPlane> fixedPlanes = new List<ARPlane>();
    
    private void Awake()
    {
        // Получаем ссылки на компоненты, если они не назначены
        if (unityARPlaneManager == null)
            unityARPlaneManager = FindObjectOfType<UnityEngine.XR.ARFoundation.ARPlaneManager>();
            
        if (planeController == null)
            planeController = FindObjectOfType<ARPlaneController>();
    }
    
    private void Start()
    {
        if (unityARPlaneManager == null)
        {
            Debug.LogError("CustomARPlaneManager: UnityEngine.XR.ARFoundation.ARPlaneManager не найден");
            return;
        }
        
        // Создаем прозрачный материал для плоскостей, если не назначен кастомный
        if (defaultPlaneMaterial == null)
        {
            defaultPlaneMaterial = new Material(Shader.Find("Transparent/Diffuse"));
            defaultPlaneMaterial.color = planeColor;
        }
        
        // Устанавливаем видимость плоскостей
        unityARPlaneManager.enabled = true;
        
        // Подписываемся на событие изменения плоскостей
        unityARPlaneManager.planesChanged += OnPlanesChanged;
        
        // Запускаем корутину для исправления плоскостей
        StartCoroutine(FixPlaneRenderersCoroutine());
    }
    
    private void OnDestroy()
    {
        if (unityARPlaneManager != null)
            unityARPlaneManager.planesChanged -= OnPlanesChanged;
    }
    
    /// <summary>
    /// Обработчик события изменения плоскостей
    /// </summary>
    private void OnPlanesChanged(ARPlanesChangedEventArgs args)
    {
        // Обрабатываем новые плоскости
        if (args.added != null && args.added.Count > 0)
        {
            foreach (var plane in args.added)
            {
                FixPlaneRenderer(plane);
            }
        }
        
        // Обрабатываем обновленные плоскости
        if (args.updated != null && args.updated.Count > 0)
        {
            foreach (var plane in args.updated)
            {
                FixPlaneRenderer(plane);
            }
        }
    }
    
    /// <summary>
    /// Исправляет рендерер на плоскости и делает его невидимым
    /// </summary>
    private void FixPlaneRenderer(ARPlane plane)
    {
        if (plane == null) return;
        
        // Проверяем, обрабатывали ли мы уже эту плоскость
        if (fixedPlanes.Contains(plane))
            return;
            
        fixedPlanes.Add(plane);
            
        // Получаем все компоненты MeshRenderer на плоскости или её детях
        MeshRenderer[] renderers = plane.GetComponentsInChildren<MeshRenderer>();
        
        foreach (var renderer in renderers)
        {
            // Включаем рендерер, но делаем материал прозрачным
            renderer.enabled = showPlaneVisualizers;
            
            // Устанавливаем прозрачный материал
            if (renderer.sharedMaterial == null || renderer.sharedMaterial.color.a > 0.01f)
            {
                renderer.sharedMaterial = defaultPlaneMaterial;
            }
        }
        
        // Добавляем компонент ARPlaneVisualizer, если его еще нет
        ARPlaneVisualizer visualizer = plane.GetComponentInChildren<ARPlaneVisualizer>();
        if (visualizer == null)
        {
            GameObject visualizerObj = new GameObject("ARPlaneVisualizer");
            visualizerObj.transform.SetParent(plane.transform);
            visualizerObj.transform.localPosition = Vector3.zero;
            visualizerObj.transform.localRotation = Quaternion.identity;
            
            visualizer = visualizerObj.AddComponent<ARPlaneVisualizer>();
        }
        
        // Устанавливаем флаг видимости для визуализатора
        if (visualizer != null)
        {
            System.Reflection.MethodInfo method = typeof(ARPlaneVisualizer).GetMethod("SetAsSegmentationPlane", 
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                
            if (method != null)
            {
                method.Invoke(visualizer, new object[] { false });
            }
        }
    }
    
    /// <summary>
    /// Корутина для периодического исправления рендереров плоскостей
    /// </summary>
    private IEnumerator FixPlaneRenderersCoroutine()
    {
        // Ждем пока система AR полностью инициализируется
        yield return new WaitForSeconds(1.0f);
        
        while (true)
        {
            FixAllPlaneRenderers();
            yield return new WaitForSeconds(2.0f); // Проверяем каждые 2 секунды
        }
    }
    
    /// <summary>
    /// Исправляет все рендереры плоскостей
    /// </summary>
    public void FixAllPlaneRenderers()
    {
        if (unityARPlaneManager == null) return;
        
        foreach (var plane in unityARPlaneManager.trackables)
        {
            FixPlaneRenderer(plane);
        }
    }
    
    /// <summary>
    /// Показать/скрыть все AR плоскости
    /// </summary>
    public void SetPlaneVisualizersVisible(bool visible)
    {
        showPlaneVisualizers = visible;
        
        foreach (var plane in fixedPlanes)
        {
            if (plane == null) continue;
            
            MeshRenderer[] renderers = plane.GetComponentsInChildren<MeshRenderer>();
            foreach (var renderer in renderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = visible;
                }
            }
        }
    }
} 