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
        
        if (forceUpdateOnStart)
        {
            // Запускаем обновление с задержкой, чтобы убедиться, что все компоненты инициализированы
            StartCoroutine(ForceUpdatePlanesWithDelay());
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
} 