using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using System.Collections.Generic;
using UnityEngine.EventSystems;

/// <summary>
/// Компонент для создания и управления UI элементами визуализации стен
/// </summary>
public class ARWallVisualizationUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ARPlaneManager planeManager;
    [SerializeField] private Canvas uiCanvas;

    [Header("Controls Settings")]
    [SerializeField] private bool createControlsAutomatically = true;
    [SerializeField] private Vector2 controlsPanelPosition = new Vector2(20, 60);
    [SerializeField] private Vector2 buttonSize = new Vector2(160, 50);
    [SerializeField] private float buttonSpacing = 10f;
    [SerializeField] private int fontSize = 14;

    // Кнопки управления
    private Button toggleVisibilityButton;
    private Button toggleExactPlacementButton;
    private Button toggleExtendWallsButton;
    private Text statusText;

    // Состояния визуализации
    private bool planesVisible = true;
    private bool usingExactPlacement = true;
    private bool extendingWalls = false;

    // Ссылка на панель
    private GameObject controlsPanel;

    private void Start()
    {
        // Находим ARPlaneManager, если не указан
        if (planeManager == null)
            planeManager = FindObjectOfType<ARPlaneManager>();

        // Находим Canvas через CanvasManager, если не указан
        if (uiCanvas == null)
        {
            // Пробуем использовать CanvasManager
            CanvasManager canvasManager = FindObjectOfType<CanvasManager>();
            if (canvasManager != null)
            {
                uiCanvas = canvasManager.GetCanvas();
                Debug.Log("ARWallVisualizationUI: Получен Canvas из CanvasManager");
            }
            else
            {
                // Резервный вариант - ищем напрямую
                uiCanvas = FindObjectOfType<Canvas>();

                // Если Canvas не найден, создаем новый с помощью CanvasManager
                if (uiCanvas == null)
                {
                    GameObject canvasManagerObj = new GameObject("CanvasManager");
                    canvasManager = canvasManagerObj.AddComponent<CanvasManager>();
                    uiCanvas = canvasManager.GetCanvas();
                    Debug.Log("ARWallVisualizationUI: Создан CanvasManager и Canvas");
                }
            }
        }

        // Создаем элементы UI если включено автоматическое создание
        if (createControlsAutomatically && uiCanvas != null)
        {
            CreateUI();
        }

        // Активируем режим отладки позиционирования и включаем точное размещение
        EnableDebugPositioningForAllVisualizers(true);
        SetExactPlacementForAllVisualizers(true);
    }

    /// <summary>
    /// Создает UI элементы для управления визуализацией
    /// </summary>
    private void CreateUI()
    {
        // Проверяем, что Canvas доступен
        if (uiCanvas == null)
        {
            Debug.LogError("ARWallVisualizationUI: Не удается создать UI, Canvas не найден");
            return;
        }

        // Проверяем наличие GraphicRaycaster
        GraphicRaycaster raycaster = uiCanvas.GetComponent<GraphicRaycaster>();
        if (raycaster == null)
        {
            raycaster = uiCanvas.gameObject.AddComponent<GraphicRaycaster>();
            Debug.Log("ARWallVisualizationUI: Добавлен GraphicRaycaster на Canvas");
        }

        // Проверяем режим Canvas
        if (uiCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Debug.Log("ARWallVisualizationUI: Canvas установлен в режим ScreenSpaceOverlay");
        }

        // Создаем панель для кнопок
        controlsPanel = new GameObject("WallVisualizationControls");
        RectTransform panelRect = controlsPanel.AddComponent<RectTransform>();
        controlsPanel.transform.SetParent(uiCanvas.transform, false);

        // Настраиваем размер и позицию
        panelRect.anchorMin = new Vector2(0, 0);
        panelRect.anchorMax = new Vector2(0, 0);
        panelRect.pivot = new Vector2(0, 0);
        panelRect.anchoredPosition = controlsPanelPosition;

        // Добавляем фон
        Image panelImage = controlsPanel.AddComponent<Image>();
        panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.7f);
        panelImage.raycastTarget = true; // Включаем рейкаст для фона

        // Создаем кнопки
        CreateButton("ToggleVisibilityBtn", "Показать/скрыть стены", 0, out toggleVisibilityButton);
        toggleVisibilityButton.onClick.AddListener(ToggleVisibility);

        CreateButton("ToggleExactBtn", "Точное/смещенное положение", 1, out toggleExactPlacementButton);
        toggleExactPlacementButton.onClick.AddListener(ToggleExactPlacement);

        CreateButton("ToggleExtendBtn", "Нормальные/расширенные стены", 2, out toggleExtendWallsButton);
        toggleExtendWallsButton.onClick.AddListener(ToggleExtendWalls);

        // Создаем текст статуса
        GameObject statusObj = new GameObject("StatusText");
        RectTransform statusRect = statusObj.AddComponent<RectTransform>();
        statusObj.transform.SetParent(controlsPanel.transform, false);

        statusText = statusObj.AddComponent<Text>();
        statusText.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
        statusText.fontSize = fontSize;
        statusText.alignment = TextAnchor.MiddleLeft;
        statusText.color = Color.white;
        statusText.raycastTarget = false; // Отключаем рейкаст для текста

        // Настраиваем размер и позицию текста
        statusRect.anchorMin = new Vector2(0, 0);
        statusRect.anchorMax = new Vector2(1, 0);
        statusRect.pivot = new Vector2(0, 0);
        statusRect.anchoredPosition = new Vector2(10, buttonSize.y * 3 + buttonSpacing * 3 + 10);
        statusRect.sizeDelta = new Vector2(-20, 60);

        // Настраиваем размер панели
        panelRect.sizeDelta = new Vector2(buttonSize.x + 20, buttonSize.y * 3 + buttonSpacing * 4 + 80);

        // Обновляем текст статуса
        UpdateStatusText();

        // Выводим сообщение в консоль для отладки
        Debug.Log("UI для управления визуализацией стен успешно создан");
    }

    /// <summary>
    /// Создает кнопку управления
    /// </summary>
    private void CreateButton(string name, string text, int index, out Button button)
    {
        GameObject buttonObj = new GameObject(name);
        RectTransform buttonRect = buttonObj.AddComponent<RectTransform>();
        buttonObj.transform.SetParent(controlsPanel.transform, false);

        // Настраиваем размер и позицию
        buttonRect.anchorMin = new Vector2(0.5f, 0);
        buttonRect.anchorMax = new Vector2(0.5f, 0);
        buttonRect.pivot = new Vector2(0.5f, 0);
        buttonRect.anchoredPosition = new Vector2(0, buttonSpacing + (buttonSize.y + buttonSpacing) * index);
        buttonRect.sizeDelta = buttonSize;

        // Добавляем графику и кнопку
        Image buttonImage = buttonObj.AddComponent<Image>();
        buttonImage.color = new Color(0.2f, 0.2f, 0.2f, 1);
        buttonImage.raycastTarget = true; // Явно включаем рейкаст для изображения

        button = buttonObj.AddComponent<Button>();

        // Настраиваем цвета состояний кнопки
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.2f, 0.2f, 0.2f, 1);
        colors.highlightedColor = new Color(0.3f, 0.3f, 0.3f, 1);
        colors.pressedColor = new Color(0.1f, 0.1f, 0.1f, 1);
        colors.selectedColor = new Color(0.25f, 0.25f, 0.25f, 1);
        colors.disabledColor = new Color(0.15f, 0.15f, 0.15f, 0.5f);
        colors.colorMultiplier = 1.2f; // Увеличиваем контраст между состояниями
        colors.fadeDuration = 0.1f; // Быстрее переход между состояниями
        button.colors = colors;

        // Настраиваем навигацию кнопки
        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.Automatic;
        button.navigation = navigation;

        // Настраиваем transition
        button.transition = Selectable.Transition.ColorTint;

        // Добавляем текст
        GameObject textObj = new GameObject("Text");
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textObj.transform.SetParent(buttonObj.transform, false);

        // Настраиваем размер и позицию текста
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text buttonText = textObj.AddComponent<Text>();
        buttonText.text = text;
        buttonText.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
        buttonText.fontSize = fontSize;
        buttonText.alignment = TextAnchor.MiddleCenter;
        buttonText.color = Color.white;
        buttonText.raycastTarget = false; // Отключаем рейкаст для текста

        // Устанавливаем targetGraphic для кнопки
        button.targetGraphic = buttonImage;

        // Добавляем компонент EventTrigger для дополнительной обработки событий
        EventTrigger eventTrigger = buttonObj.AddComponent<EventTrigger>();

        // Добавляем обработчик для отладки кликов
        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.PointerClick;
        entry.callback.AddListener((data) => { Debug.Log($"Кнопка {name} была нажата!"); });
        eventTrigger.triggers.Add(entry);
    }

    /// <summary>
    /// Переключает видимость AR плоскостей
    /// </summary>
    public void ToggleVisibility()
    {
        if (planeManager == null) return;

        planesVisible = !planesVisible;

        foreach (var plane in planeManager.trackables)
        {
            foreach (var visualizer in plane.GetComponentsInChildren<MeshRenderer>())
            {
                visualizer.enabled = planesVisible;
            }
        }

        UpdateStatusText();
    }

    /// <summary>
    /// Переключает точное/смещенное размещение визуализации плоскостей
    /// </summary>
    public void ToggleExactPlacement()
    {
        if (planeManager == null) return;

        usingExactPlacement = !usingExactPlacement;

        // Проверяем наличие контроллера плоскостей
        ARPlaneController planeController = FindObjectOfType<ARPlaneController>();
        if (planeController != null)
        {
            // Используем контроллер для переключения всех визуализаторов одновременно
            planeController.ToggleExactPlacementForAll();
            Debug.Log("Использован ARPlaneController для переключения режима размещения");
        }
        else
        {
            // Если контроллер не найден, переключаем каждый визуализатор отдельно
            foreach (var plane in planeManager.trackables)
            {
                foreach (var visualizer in plane.GetComponentsInChildren<ARPlaneVisualizer>())
                {
                    visualizer.ToggleExactPlacement();
                }
            }
            Debug.Log("Переключен режим размещения для всех визуализаторов");
        }

        UpdateStatusText();
    }

    /// <summary>
    /// Переключает нормальное/расширенное отображение стен
    /// </summary>
    public void ToggleExtendWalls()
    {
        if (planeManager == null) return;

        extendingWalls = !extendingWalls;

        // Проверяем наличие контроллера плоскостей
        ARPlaneController planeController = FindObjectOfType<ARPlaneController>();
        if (planeController != null)
        {
            // Используем контроллер для переключения всех визуализаторов одновременно
            planeController.ToggleExtendWallsForAll();
            Debug.Log("Использован ARPlaneController для переключения режима отображения стен");
        }
        else
        {
            // Если контроллер не найден, переключаем каждый визуализатор отдельно
            foreach (var plane in planeManager.trackables)
            {
                foreach (var visualizer in plane.GetComponentsInChildren<ARPlaneVisualizer>())
                {
                    visualizer.ToggleExtendWalls();
                }
            }
            Debug.Log("Переключен режим отображения стен для всех визуализаторов");
        }

        UpdateStatusText();
    }

    /// <summary>
    /// Обновляет текст статуса
    /// </summary>
    private void UpdateStatusText()
    {
        if (statusText != null)
        {
            string placementMode = usingExactPlacement ? "Точное" : "Смещенное";
            string wallMode = extendingWalls ? "Расширенные" : "Реальные";

            statusText.text = $"Плоскости: {(planesVisible ? "Видимы" : "Скрыты")}\n" +
                             $"Размещение: {placementMode}\n" +
                             $"Стены: {wallMode}";
        }
    }

    /// <summary>
    /// Включает или отключает режим отладки позиционирования для всех ARPlaneVisualizer
    /// </summary>
    public void EnableDebugPositioningForAllVisualizers(bool enable)
    {
        if (planeManager == null) return;

        foreach (var plane in planeManager.trackables)
        {
            foreach (var visualizer in plane.GetComponentsInChildren<ARPlaneVisualizer>())
            {
                // Использование рефлексии для доступа к приватному полю
                var debugField = visualizer.GetType().GetField("debugPositioning",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

                if (debugField != null)
                {
                    debugField.SetValue(visualizer, enable);
                }
            }
        }

        Debug.Log($"Режим отладки позиционирования для всех визуализаторов: {(enable ? "включен" : "отключен")}");
    }

    /// <summary>
    /// Устанавливает режим точного размещения для всех ARPlaneVisualizer
    /// </summary>
    public void SetExactPlacementForAllVisualizers(bool exactPlacement)
    {
        if (planeManager == null) return;

        // Проверяем наличие контроллера плоскостей
        ARPlaneController planeController = FindObjectOfType<ARPlaneController>();
        if (planeController != null)
        {
            // Используем контроллер, если он есть
            var method = planeController.GetType().GetMethod("SetExactPlacementForAll",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);

            if (method != null)
            {
                method.Invoke(planeController, new object[] { exactPlacement });
                Debug.Log($"Установлен режим точного размещения (через ARPlaneController): {exactPlacement}");
                return;
            }
        }

        // Если контроллер не найден или метод отсутствует, устанавливаем напрямую
        foreach (var plane in planeManager.trackables)
        {
            foreach (var visualizer in plane.GetComponentsInChildren<ARPlaneVisualizer>())
            {
                var exactField = visualizer.GetType().GetField("useExactPlacement",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

                if (exactField != null)
                {
                    exactField.SetValue(visualizer, exactPlacement);
                    visualizer.UpdateVisual(); // Обновляем визуализацию после изменения
                }
            }
        }

        Debug.Log($"Установлен режим точного размещения для всех визуализаторов: {exactPlacement}");
        usingExactPlacement = exactPlacement;
        UpdateStatusText();
    }

    /// <summary>
    /// Включает отладочную визуализацию для всех AR плоскостей
    /// </summary>
    public void EnableDebugForAllPlanes()
    {
        WallSegmentation wallSegmentation = FindObjectOfType<WallSegmentation>();
        if (wallSegmentation != null)
        {
            // Попытка вызвать метод через рефлексию
            var enableDebugMethod = wallSegmentation.GetType().GetMethod("EnableDebugForAllVisualizers",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            if (enableDebugMethod != null)
            {
                Debug.Log("Принудительное включение отладки визуализаторов через UI...");
                enableDebugMethod.Invoke(wallSegmentation, null);
            }
        }

        // Также принудительно включаем отладочную визуализацию через ARPlaneVisualizer
        ARPlaneVisualizer[] visualizers = FindObjectsOfType<ARPlaneVisualizer>();
        foreach (var visualizer in visualizers)
        {
            visualizer.SetDebugMode(true);
            visualizer.SetExtendWalls(true);

            Debug.Log($"Включена отладочная визуализация для ARPlaneVisualizer на {visualizer.transform.parent?.name}");
        }
    }

    private void OnDestroy()
    {
        // Отписываемся от событий
        if (toggleVisibilityButton != null)
            toggleVisibilityButton.onClick.RemoveListener(ToggleVisibility);

        if (toggleExactPlacementButton != null)
            toggleExactPlacementButton.onClick.RemoveListener(ToggleExactPlacement);

        if (toggleExtendWallsButton != null)
            toggleExtendWallsButton.onClick.RemoveListener(ToggleExtendWalls);
    }
}