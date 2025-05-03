using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WallPainter wallPainter;
    [SerializeField] private WallSegmentation wallSegmentation;
    
    [Header("UI Elements")]
    [SerializeField] private GameObject colorPalette;
    [SerializeField] private Button[] colorButtons;
    [SerializeField] private Slider brushSizeSlider;
    [SerializeField] private Slider intensitySlider;
    [SerializeField] private Button resetButton;
    [SerializeField] private Button togglePaletteButton;
    [SerializeField] private TextMeshProUGUI statusText;
    
    // Предопределенная палитра цветов
    private Color[] predefinedColors = new Color[]
    {
        new Color(0.85f, 0.85f, 0.85f), // Белый
        new Color(0.95f, 0.95f, 0.80f), // Кремовый
        new Color(0.80f, 0.60f, 0.40f), // Бежевый
        new Color(0.86f, 0.70f, 0.70f), // Светло-розовый
        new Color(0.70f, 0.80f, 0.90f), // Голубой
        new Color(0.70f, 0.90f, 0.70f), // Светло-зеленый
        new Color(0.95f, 0.86f, 0.60f), // Песочный
        new Color(0.60f, 0.60f, 0.80f)  // Лавандовый
    };
    
    private bool isPaletteVisible = false;
    
    private void Start()
    {
        if (wallPainter == null)
            wallPainter = FindObjectOfType<WallPainter>();
        
        if (wallSegmentation == null)
            wallSegmentation = FindObjectOfType<WallSegmentation>();
            
        // Инициализация UI элементов
        InitializeUI();
        
        // По умолчанию скрываем палитру
        colorPalette.SetActive(false);
    }
    
    // Инициализация UI элементов
    private void InitializeUI()
    {
        // Настройка цветовых кнопок
        for (int i = 0; i < colorButtons.Length && i < predefinedColors.Length; i++)
        {
            Color buttonColor = predefinedColors[i];
            int colorIndex = i; // Для захвата в лямбда-выражении
            
            // Установка цвета кнопки
            Image buttonImage = colorButtons[i].GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.color = buttonColor;
            }
            
            // Добавление обработчика события
            colorButtons[i].onClick.AddListener(() => SetColor(colorIndex));
        }
        
        // Настройка слайдера размера кисти
        if (brushSizeSlider != null)
        {
            brushSizeSlider.onValueChanged.AddListener(OnBrushSizeChanged);
            
            // Установка начального значения
            wallPainter.SetBrushSize(brushSizeSlider.value);
        }
        
        // Настройка слайдера интенсивности
        if (intensitySlider != null)
        {
            intensitySlider.onValueChanged.AddListener(OnIntensityChanged);
            
            // Установка начального значения
            wallPainter.SetBrushIntensity(intensitySlider.value);
        }
        
        // Настройка кнопки сброса
        if (resetButton != null)
        {
            resetButton.onClick.AddListener(OnResetButtonClicked);
        }
        
        // Настройка кнопки переключения палитры
        if (togglePaletteButton != null)
        {
            togglePaletteButton.onClick.AddListener(ToggleColorPalette);
        }
    }
    
    // Установка цвета кисти
    private void SetColor(int colorIndex)
    {
        if (colorIndex >= 0 && colorIndex < predefinedColors.Length)
        {
            wallPainter.SetColor(predefinedColors[colorIndex]);
            
            if (statusText != null)
            {
                statusText.text = "Цвет выбран";
                StartCoroutine(ClearStatusAfterDelay(1.5f));
            }
        }
    }
    
    // Обработчик изменения размера кисти
    private void OnBrushSizeChanged(float value)
    {
        wallPainter.SetBrushSize(value);
    }
    
    // Обработчик изменения интенсивности кисти
    private void OnIntensityChanged(float value)
    {
        wallPainter.SetBrushIntensity(value);
    }
    
    // Обработчик нажатия кнопки сброса
    private void OnResetButtonClicked()
    {
        wallPainter.ResetPainting();
        
        if (statusText != null)
        {
            statusText.text = "Покраска сброшена";
            StartCoroutine(ClearStatusAfterDelay(1.5f));
        }
    }
    
    // Переключение отображения палитры цветов
    private void ToggleColorPalette()
    {
        isPaletteVisible = !isPaletteVisible;
        colorPalette.SetActive(isPaletteVisible);
    }
    
    // Очистка статусного текста через заданный промежуток времени
    private IEnumerator ClearStatusAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (statusText != null)
        {
            statusText.text = "";
        }
    }
    
    // Обновление информации о распознавании стен
    public void UpdateWallDetectionStatus(bool wallsDetected)
    {
        if (statusText != null)
        {
            if (wallsDetected)
            {
                statusText.text = "Стены обнаружены";
                statusText.color = Color.green;
            }
            else
            {
                statusText.text = "Ищем стены...";
                statusText.color = Color.yellow;
            }
        }
    }
} 