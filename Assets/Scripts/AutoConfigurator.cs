using UnityEngine;

/// <summary>
/// Автоматически настраивает компоненты в сцене при запуске
/// </summary>
public class AutoConfigurator : MonoBehaviour
{
    private void Awake()
    {
        // Убрали ссылки на RuntimeSetupManager

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