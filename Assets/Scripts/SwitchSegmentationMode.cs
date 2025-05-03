using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Компонент для переключения режимов сегментации во время работы приложения
/// </summary>
public class SwitchSegmentationMode : MonoBehaviour
{
    [Header("Сегментация")]
    [SerializeField] private WallSegmentation wallSegmentation;
    
    [Header("UI элементы")]
    [SerializeField] private Button demoModeButton;
    [SerializeField] private Button embeddedModelButton;
    [SerializeField] private Button externalModelButton;
    [SerializeField] private Text statusText;
    
    // Текущий режим
    private WallSegmentation.SegmentationMode currentMode;
    
    private void Start()
    {
        // Если ссылка на WallSegmentation не указана, пытаемся найти компонент
        if (wallSegmentation == null)
        {
            wallSegmentation = FindObjectOfType<WallSegmentation>();
            if (wallSegmentation == null)
            {
                Debug.LogError("WallSegmentation не найден на сцене");
                enabled = false;
                return;
            }
        }
        
        // Добавляем обработчики событий для кнопок
        if (demoModeButton != null)
            demoModeButton.onClick.AddListener(() => SwitchMode(WallSegmentation.SegmentationMode.Demo));
            
        if (embeddedModelButton != null)
            embeddedModelButton.onClick.AddListener(() => SwitchMode(WallSegmentation.SegmentationMode.EmbeddedModel));
            
        if (externalModelButton != null)
            externalModelButton.onClick.AddListener(() => SwitchMode(WallSegmentation.SegmentationMode.ExternalModel));
        
        // Получаем и отображаем текущий режим
        UpdateCurrentMode();
    }
    
    /// <summary>
    /// Переключение режима сегментации
    /// </summary>
    public void SwitchMode(WallSegmentation.SegmentationMode newMode)
    {
        if (wallSegmentation == null)
            return;
            
        wallSegmentation.SwitchMode(newMode);
        UpdateCurrentMode();
        
        // Отображаем информацию о переключении режима
        Debug.Log($"Режим сегментации изменен на: {newMode}");
    }
    
    /// <summary>
    /// Обновление информации о текущем режиме
    /// </summary>
    private void UpdateCurrentMode()
    {
        if (wallSegmentation == null)
            return;
            
        // Получаем текущий режим из компонента сегментации
        currentMode = wallSegmentation.GetCurrentMode();
        
        // Отображаем информацию о режиме в UI
        if (statusText != null)
        {
            string modeName = "Неизвестно";
            bool isDemoActive = wallSegmentation.IsUsingDemoMode();
            
            // Определяем название режима
            switch (currentMode)
            {
                case WallSegmentation.SegmentationMode.Demo:
                    modeName = "Демо (без ML)";
                    break;
                case WallSegmentation.SegmentationMode.EmbeddedModel:
                    modeName = isDemoActive ? "Встроенная модель (Демо активен)" : "Встроенная модель";
                    break;
                case WallSegmentation.SegmentationMode.ExternalModel:
                    modeName = isDemoActive ? "Внешняя модель (Демо активен)" : "Внешняя модель";
                    break;
            }
            
            statusText.text = $"Режим: {modeName}";
        }
        
        // Визуально выделяем активную кнопку
        if (demoModeButton != null)
            demoModeButton.interactable = currentMode != WallSegmentation.SegmentationMode.Demo;
            
        if (embeddedModelButton != null)
            embeddedModelButton.interactable = currentMode != WallSegmentation.SegmentationMode.EmbeddedModel;
            
        if (externalModelButton != null)
            externalModelButton.interactable = currentMode != WallSegmentation.SegmentationMode.ExternalModel;
    }
    
    /// <summary>
    /// Переключение на следующий режим (для тестирования)
    /// </summary>
    public void NextMode()
    {
        // Получаем следующий режим по циклу
        int nextMode = ((int)currentMode + 1) % 3;
        SwitchMode((WallSegmentation.SegmentationMode)nextMode);
    }
} 