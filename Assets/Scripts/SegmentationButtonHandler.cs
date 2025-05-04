using UnityEngine;

/// <summary>
/// Класс для обработки нажатия на кнопку обновления сегментации
/// </summary>
public class SegmentationButtonHandler : MonoBehaviour
{
    [SerializeField] private WallSegmentation wallSegmentation;
    
    /// <summary>
    /// Вызывается при нажатии на кнопку обновления сегментации
    /// </summary>
    public void UpdateSegmentation()
    {
        if (wallSegmentation != null)
        {
            Debug.Log("Обновление сегментации плоскостей...");
            wallSegmentation.UpdatePlanesSegmentationStatus();
        }
        else
        {
            Debug.LogError("Компонент WallSegmentation не найден!");
            
            // Попробуем найти WallSegmentation
            wallSegmentation = FindObjectOfType<WallSegmentation>();
            if (wallSegmentation != null)
            {
                Debug.Log("WallSegmentation найден, обновляем сегментацию...");
                wallSegmentation.UpdatePlanesSegmentationStatus();
            }
        }
    }
} 