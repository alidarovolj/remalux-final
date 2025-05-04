using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Класс для обработки нажатия на кнопку обновления сегментации
/// </summary>
public class SegmentationButtonHandler : MonoBehaviour
{
    [SerializeField] private WallSegmentation wallSegmentation;
    [SerializeField] private Button updateButton; // Кнопка обновления сегментации
    [SerializeField] private Text buttonText; // Текст кнопки
    [SerializeField] private float cooldownTime = 2.0f; // Время "перезарядки" кнопки в секундах
    
    private bool isProcessing = false; // Флаг процесса обновления
    private string originalButtonText; // Исходный текст кнопки
    
    private void Start()
    {
        // Если кнопка не назначена, пытаемся найти её
        if (updateButton == null)
        {
            updateButton = GetComponent<Button>();
            if (updateButton == null && transform.parent != null)
            {
                updateButton = transform.parent.GetComponent<Button>();
            }
        }
        
        // Если текст кнопки не назначен, пытаемся найти его
        if (buttonText == null && updateButton != null)
        {
            buttonText = updateButton.GetComponentInChildren<Text>();
        }
        
        // Сохраняем оригинальный текст кнопки
        if (buttonText != null)
        {
            originalButtonText = buttonText.text;
        }
    }
    
    /// <summary>
    /// Вызывается при нажатии на кнопку обновления сегментации
    /// </summary>
    public void UpdateSegmentation()
    {
        // Предотвращаем повторные нажатия во время обработки
        if (isProcessing)
        {
            Debug.Log("Обновление сегментации уже выполняется...");
            return;
        }
        
        StartCoroutine(ProcessSegmentationUpdate());
    }
    
    /// <summary>
    /// Корутина для обработки обновления сегментации с визуальным индикатором
    /// </summary>
    private IEnumerator ProcessSegmentationUpdate()
    {
        isProcessing = true;
        
        // Меняем внешний вид кнопки и блокируем её
        if (updateButton != null)
        {
            updateButton.interactable = false;
        }
        
        // Меняем текст кнопки с индикатором прогресса
        if (buttonText != null)
        {
            buttonText.text = "Обновление...";
        }
        
        Debug.Log("Обновление сегментации плоскостей...");
        
        // Находим WallSegmentation, если он не назначен
        if (wallSegmentation == null)
        {
            wallSegmentation = FindObjectOfType<WallSegmentation>();
        }
        
        // Выполняем обновление сегментации
        int updatedCount = 0;
        if (wallSegmentation != null)
        {
            // Переключаем режим на ExternalModel, если он не установлен
            if (wallSegmentation.GetCurrentMode() != WallSegmentation.SegmentationMode.ExternalModel)
            {
                Debug.Log("Переключение режима сегментации на ExternalModel...");
                wallSegmentation.SwitchMode(WallSegmentation.SegmentationMode.ExternalModel);
                
                // Даем время на загрузку модели
                yield return new WaitForSeconds(0.5f);
            }
            
            // Запускаем обработку кадра камеры с корректным запуском корутины
            var processFramesMethod = wallSegmentation.GetType().GetMethod("ProcessCameraImage", 
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                
            if (processFramesMethod != null)
            {
                Debug.Log("Запуск обработки кадра камеры для сегментации...");
                // Получаем IEnumerator из метода и запускаем его как корутину
                IEnumerator coroutine = (IEnumerator)processFramesMethod.Invoke(wallSegmentation, null);
                if (coroutine != null)
                {
                    // Запускаем полученную корутину через StartCoroutine
                    wallSegmentation.StartCoroutine(coroutine);
                    Debug.Log("Корутина ProcessCameraImage успешно запущена");
                }
                else
                {
                    Debug.LogError("Не удалось получить корутину из метода ProcessCameraImage");
                }
            }
            else
            {
                Debug.LogError("Метод ProcessCameraImage не найден в классе WallSegmentation");
            }
            
            // Даем время на обработку
            yield return new WaitForSeconds(1.0f); // Увеличиваем время ожидания
            
            // Обновляем статус плоскостей
            updatedCount = wallSegmentation.UpdatePlanesSegmentationStatus();
            Debug.Log($"Обновление сегментации завершено, обработано {updatedCount} плоскостей");
            
            // Пытаемся вызвать метод включения отладки для всех визуализаторов через рефлексию
            var enableDebugMethod = wallSegmentation.GetType().GetMethod("EnableDebugForAllVisualizers", 
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                
            if (enableDebugMethod != null)
            {
                Debug.Log("Принудительное включение отладки визуализаторов...");
                enableDebugMethod.Invoke(wallSegmentation, null);
            }
        }
        else
        {
            Debug.LogError("Компонент WallSegmentation не найден!");
        }
        
        // Небольшая задержка для визуализации процесса
        yield return new WaitForSeconds(1.0f);
        
        // Возвращаем оригинальный вид кнопки
        if (buttonText != null && !string.IsNullOrEmpty(originalButtonText))
        {
            buttonText.text = originalButtonText;
        }
        
        // Запускаем таймер "перезарядки" кнопки
        yield return new WaitForSeconds(cooldownTime);
        
        // Разблокируем кнопку
        if (updateButton != null)
        {
            updateButton.interactable = true;
        }
        
        isProcessing = false;
    }
} 