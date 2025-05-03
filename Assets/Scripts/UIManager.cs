using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.XR.ARSubsystems; // Добавляем для использования TrackableId

/// <summary>
/// Управляет пользовательским интерфейсом для AR Wall Painting
/// </summary>
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
    
    [Header("Paint Controls")]
    [SerializeField] private Button redButton;
    [SerializeField] private Button greenButton;
    [SerializeField] private Button blueButton;
    [SerializeField] private Button yellowButton;
    [SerializeField] private Button whiteButton;
    [SerializeField] private Toggle paintModeToggle;
    
    [Header("Snapshot Controls")]
    [SerializeField] private Button takeSnapshotButton;
    [SerializeField] private Button prevSnapshotButton;
    [SerializeField] private Button nextSnapshotButton;
    [SerializeField] private RawImage snapshotPreview;
    [SerializeField] private Text snapshotCountText;
    
    [Header("Debug")]
    [SerializeField] private Toggle showDebugToggle;
    [SerializeField] private RawImage segmentationDebugImage;
    
    [Header("Color Settings")]
    [SerializeField] private Color redColor = new Color(1f, 0f, 0f, 0.8f);
    [SerializeField] private Color greenColor = new Color(0f, 0.8f, 0f, 0.8f);
    [SerializeField] private Color blueColor = new Color(0f, 0f, 1f, 0.8f);
    [SerializeField] private Color yellowColor = new Color(1f, 0.9f, 0f, 0.8f);
    [SerializeField] private Color whiteColor = new Color(1f, 1f, 1f, 0.8f);
    
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
        // Находим необходимые компоненты, если они не назначены
        if (wallPainter == null)
            wallPainter = FindObjectOfType<WallPainter>();
            
        if (wallSegmentation == null)
            wallSegmentation = FindObjectOfType<WallSegmentation>();
            
        // Настраиваем кнопки выбора цвета
        SetupColorButtons();
        
        // Настраиваем слайдеры
        SetupSliders();
        
        // Настраиваем переключатели
        SetupToggles();
        
        // Настраиваем кнопки снимков
        SetupSnapshotButtons();
        
        // Обновляем интерфейс снимков
        UpdateSnapshotUI();
        
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
    
    private void SetupColorButtons()
    {
        if (redButton != null)
            redButton.onClick.AddListener(() => SetPaintColor(redColor));
            
        if (greenButton != null)
            greenButton.onClick.AddListener(() => SetPaintColor(greenColor));
            
        if (blueButton != null)
            blueButton.onClick.AddListener(() => SetPaintColor(blueColor));
            
        if (yellowButton != null)
            yellowButton.onClick.AddListener(() => SetPaintColor(yellowColor));
            
        if (whiteButton != null)
            whiteButton.onClick.AddListener(() => SetPaintColor(whiteColor));
    }
    
    private void SetupSliders()
    {
        if (brushSizeSlider != null)
        {
            // Настраиваем начальное значение
            brushSizeSlider.value = wallPainter != null ? wallPainter.GetBrushSize() : 0.2f;
            
            // Добавляем обработчик изменения
            brushSizeSlider.onValueChanged.AddListener(OnBrushSizeChanged);
        }
        
        if (intensitySlider != null)
        {
            // Настраиваем начальное значение
            intensitySlider.value = wallPainter != null ? wallPainter.GetBrushIntensity() : 0.8f;
            
            // Добавляем обработчик изменения
            intensitySlider.onValueChanged.AddListener(OnIntensityChanged);
        }
    }
    
    private void SetupToggles()
    {
        if (paintModeToggle != null)
        {
            paintModeToggle.onValueChanged.AddListener(OnPaintModeChanged);
        }
        
        if (showDebugToggle != null && wallSegmentation != null)
        {
            showDebugToggle.isOn = wallSegmentation.IsDebugVisualizationEnabled();
            showDebugToggle.onValueChanged.AddListener(OnDebugToggleChanged);
        }
    }
    
    private void SetupSnapshotButtons()
    {
        if (takeSnapshotButton != null)
            takeSnapshotButton.onClick.AddListener(OnTakeSnapshotClicked);
            
        if (prevSnapshotButton != null)
            prevSnapshotButton.onClick.AddListener(OnPrevSnapshotClicked);
            
        if (nextSnapshotButton != null)
            nextSnapshotButton.onClick.AddListener(OnNextSnapshotClicked);
    }
    
    public void SetPaintColor(Color color)
    {
        if (wallPainter != null)
        {
            wallPainter.SetColor(color);
            Debug.Log($"Установлен цвет: R:{color.r}, G:{color.g}, B:{color.b}, A:{color.a}");
        }
    }
    
    private void OnBrushSizeChanged(float size)
    {
        if (wallPainter != null)
        {
            wallPainter.SetBrushSize(size);
            Debug.Log($"Установлен размер кисти: {size}");
        }
    }
    
    private void OnIntensityChanged(float intensity)
    {
        if (wallPainter != null)
        {
            wallPainter.SetBrushIntensity(intensity);
            Debug.Log($"Установлена интенсивность кисти: {intensity}");
        }
    }
    
    private void OnPaintModeChanged(bool isOn)
    {
        if (wallPainter != null)
        {
            if (isOn)
                wallPainter.StartPainting();
            else
                wallPainter.StopPainting();
                
            Debug.Log($"Режим рисования: {(isOn ? "Включен" : "Выключен")}");
        }
    }
    
    private void OnDebugToggleChanged(bool isOn)
    {
        if (wallSegmentation != null)
        {
            wallSegmentation.EnableDebugVisualization(isOn);
            Debug.Log($"Отладочная визуализация: {(isOn ? "Включена" : "Выключена")}");
        }
    }
    
    private void OnTakeSnapshotClicked()
    {
        if (wallPainter != null)
        {
            wallPainter.CreateNewSnapshot($"Снимок {System.DateTime.Now.ToString("HH:mm:ss")}");
            UpdateSnapshotUI();
        }
    }
    
    private void OnPrevSnapshotClicked()
    {
        if (wallPainter != null)
        {
            int currentIndex = wallPainter.GetCurrentSnapshotIndex();
            if (currentIndex > 0)
            {
                wallPainter.LoadSnapshot(currentIndex - 1);
                UpdateSnapshotUI();
            }
        }
    }
    
    private void OnNextSnapshotClicked()
    {
        if (wallPainter != null)
        {
            int currentIndex = wallPainter.GetCurrentSnapshotIndex();
            int snapshotCount = wallPainter.GetSnapshots().Count;
            
            if (currentIndex < snapshotCount - 1)
            {
                wallPainter.LoadSnapshot(currentIndex + 1);
                UpdateSnapshotUI();
            }
        }
    }
    
    private void UpdateSnapshotUI()
    {
        if (wallPainter == null)
            return;
            
        List<PaintSnapshot> snapshots = wallPainter.GetSnapshots();
        int currentIndex = wallPainter.GetCurrentSnapshotIndex();
        
        // Обновляем текст с количеством снимков
        if (snapshotCountText != null)
        {
            snapshotCountText.text = $"Снимок {currentIndex + 1}/{snapshots.Count}";
        }
        
        // Обновляем превью снимка
        if (snapshotPreview != null && currentIndex >= 0 && currentIndex < snapshots.Count)
        {
            PaintSnapshot currentSnapshot = snapshots[currentIndex];
            if (currentSnapshot != null)
            {
                // Получаем превью для текущего снимка
                Texture2D previewTexture = wallPainter.GetSnapshotPreview(currentSnapshot.id);
                if (previewTexture != null)
                {
                    snapshotPreview.texture = previewTexture;
                }
            }
        }
        
        // Обновляем доступность кнопок
        if (prevSnapshotButton != null)
            prevSnapshotButton.interactable = currentIndex > 0;
            
        if (nextSnapshotButton != null)
            nextSnapshotButton.interactable = currentIndex < snapshots.Count - 1;
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