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
    
    [Header("Snapshot UI")]
    [SerializeField] private GameObject snapshotPanel;
    [SerializeField] private Button createSnapshotButton;
    [SerializeField] private Button toggleSnapshotPanelButton;
    [SerializeField] private Transform snapshotContainer;
    [SerializeField] private GameObject snapshotButtonPrefab;
    [SerializeField] private TMP_InputField snapshotNameInput;
    
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
    private bool isSnapshotPanelVisible = false;
    private List<GameObject> snapshotButtons = new List<GameObject>();
    
    private void Start()
    {
        if (wallPainter == null)
            wallPainter = Object.FindAnyObjectByType<WallPainter>();
        
        if (wallSegmentation == null)
            wallSegmentation = Object.FindAnyObjectByType<WallSegmentation>();
            
        // Инициализация UI элементов
        InitializeUI();
        
        // По умолчанию скрываем палитру и панель снимков
        colorPalette.SetActive(false);
        
        if (snapshotPanel != null)
            snapshotPanel.SetActive(false);
            
        // Подписываемся на событие изменения списка снимков
        if (wallPainter != null)
        {
            wallPainter.OnSnapshotsChanged += OnSnapshotsChanged;
            
            // Обновляем отображение снимков
            UpdateSnapshotsList(wallPainter.GetSnapshots(), wallPainter.GetCurrentSnapshotIndex());
        }
    }
    
    // Инициализация UI элементов
    private void InitializeUI()
    {
        // Настройка цветовых кнопок
        if (colorButtons != null && colorButtons.Length > 0)
        {
            for (int i = 0; i < colorButtons.Length; i++)
            {
                if (colorButtons[i] != null)
                {
                    // Устанавливаем цвет кнопки из палитры
                    int colorIndex = i % predefinedColors.Length;
                    Color buttonColor = predefinedColors[colorIndex];
                    
                    // Устанавливаем цвет фона кнопки
                    Image buttonImage = colorButtons[i].GetComponent<Image>();
                    if (buttonImage != null)
                    {
                        buttonImage.color = buttonColor;
                    }
                    
                    // Добавляем обработчик нажатия
                    int capturedIndex = colorIndex; // Необходимо для замыкания
                    colorButtons[i].onClick.AddListener(() => OnColorButtonClick(capturedIndex));
                }
            }
        }
        
        // Настройка слайдера размера кисти
        if (brushSizeSlider != null)
        {
            brushSizeSlider.minValue = 0.05f;
            brushSizeSlider.maxValue = 0.5f;
            brushSizeSlider.value = 0.2f;
            brushSizeSlider.onValueChanged.AddListener(OnBrushSizeChanged);
        }
        
        // Настройка слайдера интенсивности
        if (intensitySlider != null)
        {
            intensitySlider.minValue = 0f;
            intensitySlider.maxValue = 1f;
            intensitySlider.value = 0.8f;
            intensitySlider.onValueChanged.AddListener(OnIntensityChanged);
        }
        
        // Настройка кнопки сброса
        if (resetButton != null)
        {
            resetButton.onClick.AddListener(OnResetButtonClick);
        }
        
        // Настройка кнопки переключения палитры
        if (togglePaletteButton != null)
        {
            togglePaletteButton.onClick.AddListener(OnTogglePaletteButtonClick);
        }
        
        // Настройка кнопок снимков
        if (createSnapshotButton != null)
        {
            createSnapshotButton.onClick.AddListener(OnCreateSnapshotButtonClick);
        }
        
        if (toggleSnapshotPanelButton != null)
        {
            toggleSnapshotPanelButton.onClick.AddListener(OnToggleSnapshotPanelButtonClick);
        }
        
        // Устанавливаем текст по умолчанию
        if (statusText != null)
        {
            statusText.text = "Сканируйте окружение...";
        }
    }
    
    // Обновление UI при обнаружении стен
    public void UpdateWallDetectionStatus(bool wallsDetected)
    {
        if (statusText != null)
        {
            statusText.text = wallsDetected ? 
                "Стены обнаружены. Начните покраску!" : 
                "Сканируйте окружение, чтобы найти стены...";
        }
    }
    
    // Обработчики событий UI
    
    private void OnColorButtonClick(int colorIndex)
    {
        if (wallPainter != null)
        {
            wallPainter.SetColor(predefinedColors[colorIndex]);
        }
    }
    
    private void OnBrushSizeChanged(float size)
    {
        if (wallPainter != null)
        {
            wallPainter.SetBrushSize(size);
        }
    }
    
    private void OnIntensityChanged(float intensity)
    {
        if (wallPainter != null)
        {
            wallPainter.SetBrushIntensity(intensity);
        }
    }
    
    private void OnResetButtonClick()
    {
        if (wallPainter != null)
        {
            wallPainter.ResetPainting();
        }
    }
    
    private void OnTogglePaletteButtonClick()
    {
        isPaletteVisible = !isPaletteVisible;
        
        if (colorPalette != null)
        {
            colorPalette.SetActive(isPaletteVisible);
        }
    }
    
    // Снимки
    private void OnCreateSnapshotButtonClick()
    {
        string snapshotName = "Вариант";
        
        if (snapshotNameInput != null && !string.IsNullOrEmpty(snapshotNameInput.text))
        {
            snapshotName = snapshotNameInput.text;
        }
        else
        {
            // Если имя не указано, добавляем номер
            if (wallPainter != null)
            {
                var snapshots = wallPainter.GetSnapshots();
                snapshotName += " " + (snapshots.Count + 1);
            }
        }
        
        if (wallPainter != null)
        {
            wallPainter.CreateNewSnapshot(snapshotName);
        }
        
        // Очищаем поле ввода
        if (snapshotNameInput != null)
        {
            snapshotNameInput.text = "";
        }
    }
    
    private void OnToggleSnapshotPanelButtonClick()
    {
        isSnapshotPanelVisible = !isSnapshotPanelVisible;
        
        if (snapshotPanel != null)
        {
            snapshotPanel.SetActive(isSnapshotPanelVisible);
        }
    }
    
    // Обработчик события изменения списка снимков
    private void OnSnapshotsChanged(List<PaintSnapshot> snapshots, int activeIndex)
    {
        UpdateSnapshotsList(snapshots, activeIndex);
    }
    
    // Обновление UI списка снимков
    private void UpdateSnapshotsList(List<PaintSnapshot> snapshots, int activeIndex)
    {
        if (snapshotContainer == null || snapshotButtonPrefab == null)
            return;
            
        // Очищаем текущие кнопки
        foreach (var button in snapshotButtons)
        {
            Destroy(button);
        }
        snapshotButtons.Clear();
        
        // Создаем кнопки для каждого снимка
        for (int i = 0; i < snapshots.Count; i++)
        {
            GameObject buttonObj = Instantiate(snapshotButtonPrefab, snapshotContainer);
            Button button = buttonObj.GetComponent<Button>();
            
            // Изменяем текст кнопки
            TextMeshProUGUI buttonText = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = snapshots[i].name;
            }
            
            // Особо выделяем активный снимок
            Image buttonImage = buttonObj.GetComponent<Image>();
            if (buttonImage != null && i == activeIndex)
            {
                buttonImage.color = new Color(0.2f, 0.5f, 0.3f);
            }
            
            // Добавляем обработчик нажатия
            int snapshotIndex = i; // Замыкание для правильного индекса
            button.onClick.AddListener(() => OnSnapshotButtonClick(snapshotIndex));
            
            snapshotButtons.Add(buttonObj);
        }
    }
    
    // Обработчик нажатия на кнопку снимка
    private void OnSnapshotButtonClick(int index)
    {
        if (wallPainter != null)
        {
            wallPainter.LoadSnapshot(index);
        }
    }
    
    // Очистка при уничтожении
    private void OnDestroy()
    {
        if (wallPainter != null)
        {
            wallPainter.OnSnapshotsChanged -= OnSnapshotsChanged;
        }
    }
} 