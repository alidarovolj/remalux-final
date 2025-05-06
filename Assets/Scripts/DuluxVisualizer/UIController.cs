using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace DuluxVisualizer
{
      /// <summary>
      /// Контроллер пользовательского интерфейса для управления параметрами окрашивания стен
      /// </summary>
      public class UIController : MonoBehaviour
      {
            [Header("Панели интерфейса")]
            [SerializeField] private GameObject _colorPalette;
            [SerializeField] private GameObject _settingsPanel;

            [Header("Слайдеры настроек")]
            [SerializeField] private Slider _opacitySlider;
            [SerializeField] private Slider _shadowPreservationSlider;
            [SerializeField] private Slider _edgeSmoothingSlider;
            [SerializeField] private Slider _processingIntervalSlider;

            [Header("Переключатели")]
            [SerializeField] private Toggle _debugViewToggle;
            [SerializeField] private Toggle _temporalSmoothingToggle;

            [Header("Кнопки цветов")]
            [SerializeField] private List<Button> _colorButtons = new List<Button>();
            [SerializeField] private List<Color> _colors = new List<Color>();

            // Компоненты для управления
            private WallPaintBlit _wallPaintBlit;
            private WallSegmentationProcessor _segmentationProcessor;

            // Текущий цвет
            private Color _currentColor = Color.red;

            private void Awake()
            {
                  // Инициализация цветовой палитры, если она не задана
                  if (_colors.Count == 0)
                  {
                        InitializeDefaultColors();
                  }

                  // Инициализация кнопок цветов, если они не заданы
                  if (_colorButtons.Count == 0 && _colorPalette != null)
                  {
                        _colorButtons.AddRange(_colorPalette.GetComponentsInChildren<Button>());
                  }

                  // Устанавливаем цвета на кнопки
                  SetupColorButtons();
            }

            private void Start()
            {
                  // Находим компоненты в сцене
                  FindComponents();

                  // Подключаем обработчики событий
                  ConnectEventHandlers();

                  // Инициализируем начальные значения
                  InitializeSliderValues();
            }

            /// <summary>
            /// Находит необходимые компоненты в сцене
            /// </summary>
            private void FindComponents()
            {
                  // Если компоненты не назначены в инспекторе, пытаемся найти их в сцене
                  if (_wallPaintBlit == null)
                  {
                        _wallPaintBlit = FindObjectOfType<WallPaintBlit>();
                  }

                  if (_segmentationProcessor == null)
                  {
                        _segmentationProcessor = FindObjectOfType<WallSegmentationProcessor>();
                  }
            }

            /// <summary>
            /// Инициализирует стандартную палитру цветов
            /// </summary>
            private void InitializeDefaultColors()
            {
                  // Материальная палитра цветов
                  _colors = new List<Color>
            {
                new Color(0.956f, 0.262f, 0.211f), // Red
                new Color(0.913f, 0.117f, 0.388f), // Pink
                new Color(0.611f, 0.152f, 0.654f), // Purple
                new Color(0.403f, 0.227f, 0.717f), // Deep Purple
                new Color(0.247f, 0.317f, 0.709f), // Indigo
                new Color(0.129f, 0.588f, 0.952f), // Blue
                new Color(0.011f, 0.662f, 0.956f), // Light Blue
                new Color(0.0f, 0.737f, 0.831f),   // Cyan
                new Color(0.0f, 0.588f, 0.533f),   // Teal
                new Color(0.298f, 0.686f, 0.313f), // Green
                new Color(0.545f, 0.764f, 0.290f), // Light Green
                new Color(0.803f, 0.862f, 0.223f), // Lime
                new Color(1.0f, 0.921f, 0.231f),   // Yellow
                new Color(1.0f, 0.756f, 0.027f),   // Amber
                new Color(1.0f, 0.596f, 0.0f),     // Orange
                new Color(0.937f, 0.325f, 0.313f)  // Deep Orange
            };
            }

            /// <summary>
            /// Настраивает кнопки цветов
            /// </summary>
            private void SetupColorButtons()
            {
                  // Убеждаемся, что у нас достаточно кнопок
                  int maxIndex = Mathf.Min(_colorButtons.Count, _colors.Count);

                  for (int i = 0; i < maxIndex; i++)
                  {
                        Button button = _colorButtons[i];
                        Color color = _colors[i];

                        // Устанавливаем цвет кнопки
                        Image image = button.GetComponent<Image>();
                        if (image != null)
                        {
                              image.color = color;
                        }

                        // Устанавливаем обработчик нажатия
                        int colorIndex = i; // Создаем копию для замыкания
                        button.onClick.AddListener(() => OnColorButtonClicked(colorIndex));
                  }
            }

            /// <summary>
            /// Подключает обработчики событий для UI-элементов
            /// </summary>
            private void ConnectEventHandlers()
            {
                  if (_opacitySlider != null)
                  {
                        _opacitySlider.onValueChanged.AddListener(OnOpacityChanged);
                  }

                  if (_shadowPreservationSlider != null)
                  {
                        _shadowPreservationSlider.onValueChanged.AddListener(OnShadowPreservationChanged);
                  }

                  if (_edgeSmoothingSlider != null)
                  {
                        _edgeSmoothingSlider.onValueChanged.AddListener(OnEdgeSmoothingChanged);
                  }

                  if (_processingIntervalSlider != null)
                  {
                        _processingIntervalSlider.onValueChanged.AddListener(OnProcessingIntervalChanged);
                  }

                  if (_debugViewToggle != null)
                  {
                        _debugViewToggle.onValueChanged.AddListener(OnDebugViewToggled);
                  }

                  if (_temporalSmoothingToggle != null)
                  {
                        _temporalSmoothingToggle.onValueChanged.AddListener(OnTemporalSmoothingToggled);
                  }
            }

            /// <summary>
            /// Инициализирует начальные значения слайдеров
            /// </summary>
            private void InitializeSliderValues()
            {
                  if (_wallPaintBlit != null)
                  {
                        if (_opacitySlider != null)
                        {
                              _opacitySlider.value = _wallPaintBlit.opacity;
                        }

                        if (_shadowPreservationSlider != null)
                        {
                              _shadowPreservationSlider.value = _wallPaintBlit.preserveShadows;
                        }

                        if (_edgeSmoothingSlider != null)
                        {
                              _edgeSmoothingSlider.value = _wallPaintBlit.smoothEdges;
                        }

                        if (_debugViewToggle != null)
                        {
                              _debugViewToggle.isOn = _wallPaintBlit.debugView;
                        }
                  }

                  if (_segmentationProcessor != null)
                  {
                        if (_processingIntervalSlider != null)
                        {
                              // Предполагаем, что интервал от 0.05 до 0.5 секунд
                              _processingIntervalSlider.value = 0.1f;
                        }

                        if (_temporalSmoothingToggle != null)
                        {
                              _temporalSmoothingToggle.isOn = true;
                        }
                  }
            }

            /// <summary>
            /// Обработчик нажатия на кнопку цвета
            /// </summary>
            private void OnColorButtonClicked(int index)
            {
                  if (index >= 0 && index < _colors.Count)
                  {
                        _currentColor = _colors[index];

                        // Применяем цвет к компоненту окрашивания
                        if (_wallPaintBlit != null)
                        {
                              _wallPaintBlit.paintColor = _currentColor;
                        }
                  }
            }

            /// <summary>
            /// Обработчик изменения прозрачности
            /// </summary>
            private void OnOpacityChanged(float value)
            {
                  if (_wallPaintBlit != null)
                  {
                        _wallPaintBlit.opacity = value;
                  }
            }

            /// <summary>
            /// Обработчик изменения сохранения теней
            /// </summary>
            private void OnShadowPreservationChanged(float value)
            {
                  if (_wallPaintBlit != null)
                  {
                        _wallPaintBlit.preserveShadows = value;
                  }
            }

            /// <summary>
            /// Обработчик изменения сглаживания краев
            /// </summary>
            private void OnEdgeSmoothingChanged(float value)
            {
                  if (_wallPaintBlit != null)
                  {
                        _wallPaintBlit.smoothEdges = value;
                  }
            }

            /// <summary>
            /// Обработчик изменения интервала обработки
            /// </summary>
            private void OnProcessingIntervalChanged(float value)
            {
                  if (_segmentationProcessor != null)
                  {
                        _segmentationProcessor.SetProcessingInterval(value);
                  }
            }

            /// <summary>
            /// Обработчик переключения режима отладки
            /// </summary>
            private void OnDebugViewToggled(bool isOn)
            {
                  if (_wallPaintBlit != null)
                  {
                        _wallPaintBlit.debugView = isOn;
                  }
            }

            /// <summary>
            /// Обработчик переключения временного сглаживания
            /// </summary>
            private void OnTemporalSmoothingToggled(bool isOn)
            {
                  if (_segmentationProcessor != null)
                  {
                        _segmentationProcessor.SetTemporalSmoothing(isOn);
                  }
            }

            /// <summary>
            /// Переключает видимость панели настроек
            /// </summary>
            public void ToggleSettingsPanel()
            {
                  if (_settingsPanel != null)
                  {
                        _settingsPanel.SetActive(!_settingsPanel.activeSelf);
                  }
            }

            /// <summary>
            /// Переключает видимость палитры цветов
            /// </summary>
            public void ToggleColorPalette()
            {
                  if (_colorPalette != null)
                  {
                        _colorPalette.SetActive(!_colorPalette.activeSelf);
                  }
            }
      }
}