using UnityEngine;

/// <summary>
/// Автоматически настраивает компоненты в сцене при запуске
/// </summary>
public class AutoConfigurator : MonoBehaviour
{
    private void Awake()
    {
        // Конфигурируем RuntimeSetupManager
        var runtimeSetupManager = GameObject.Find("RuntimeSetupManager");
        if (runtimeSetupManager != null)
        {
            if (runtimeSetupManager.GetComponent<RuntimeSetupManager>() == null)
            {
                runtimeSetupManager.AddComponent<RuntimeSetupManager>();
                Debug.Log("Добавлен компонент RuntimeSetupManager");
            }
        }
        else
        {
            Debug.LogWarning("Объект RuntimeSetupManager не найден");
        }
        
        // Конфигурируем WallSegmentation
        var wallSegmentationManager = GameObject.Find("WallSegmentationManager");
        if (wallSegmentationManager != null)
        {
            if (wallSegmentationManager.GetComponent<ForceDemoMode>() == null)
            {
                var forceDemo = wallSegmentationManager.AddComponent<ForceDemoMode>();
                forceDemo.enabled = true;
                Debug.Log("Добавлен компонент ForceDemoMode");
            }
        }
        else
        {
            Debug.LogWarning("Объект WallSegmentationManager не найден");
        }
        
        // Отключаем себя после выполнения
        enabled = false;
    }
} 