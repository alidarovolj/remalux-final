using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Демонстрационный скрипт для показа возможностей улучшенного эффекта окрашивания стен
/// </summary>
public class EnhancedWallPaintDemo : MonoBehaviour
{
      [Header("Segmentation")]
      [SerializeField] private SentisWallSegmentation wallSegmentation;
      [SerializeField] private RenderTexture segmentationMask;

      [Header("Paint Effects")]
      [SerializeField] private ImprovedWallPaintBlit standardPaintEffect;
      [SerializeField] private GameObject enhancedPaintEffectPrefab;

      [Header("UI Controls")]
      [SerializeField] private Image colorIndicator;
      [SerializeField] private Slider opacitySlider;
      [SerializeField] private Slider preserveShadowsSlider;
      [SerializeField] private Slider preserveTextureSlider;
      [SerializeField] private Toggle debugViewToggle;
      [SerializeField] private Toggle enhancedModeToggle;

      // Текущие компоненты для эффекта окрашивания
      private MonoBehaviour currentPaintEffect;
      private ImprovedWallPaintBlit improvedPaintBlit;

      // Цвета для демонстрации
      private Color[] demoColors = new Color[]
      {
        new Color(0.2f, 0.5f, 0.9f, 0.8f),  // Синий
        new Color(0.9f, 0.3f, 0.2f, 0.8f),  // Красный
        new Color(0.3f, 0.8f, 0.2f, 0.8f),  // Зеленый
        new Color(0.8f, 0.7f, 0.1f, 0.8f),  // Желтый
        new Color(0.6f, 0.3f, 0.7f, 0.8f),  // Фиолетовый
        new Color(0.9f, 0.6f, 0.2f, 0.8f),  // Оранжевый
        new Color(0.7f, 0.7f, 0.7f, 0.8f),  // Серый
        new Color(0.9f, 0.9f, 0.9f, 0.8f)   // Белый
      };

      private int currentColorIndex = 0;

      // Режимы демонстрации
      public enum DemoMode
      {
            Manual,       // Ручное управление параметрами
            AutoColor,    // Автоматическая смена цветов
            AutoParams,   // Автоматическое изменение параметров
            FullAuto      // Полностью автоматический режим (цвета + параметры)
      }

      [Header("Demo Settings")]
      [SerializeField] private DemoMode demoMode = DemoMode.Manual;
      [SerializeField] private float autoChangeInterval = 5.0f;
      [SerializeField] private bool showIntroMessage = true;

      private float lastChangeTime;

      // Стартовая инициализация
      private void Start()
      {
            // Устанавливаем начальный эффект
            currentPaintEffect = standardPaintEffect;

            // Инициализируем UI элементы управления
            InitializeUIControls();

            // Запускаем автоматическую демонстрацию, если включена
            if (demoMode != DemoMode.Manual)
            {
                  StartCoroutine(AutoDemoRoutine());
            }

            // Показываем вводное сообщение
            if (showIntroMessage)
            {
                  Debug.Log("Демонстрация улучшенного эффекта окрашивания стен запущена.");
                  Debug.Log("Используйте UI элементы для изменения параметров или выберите автоматический режим демонстрации.");
            }
      }

      // Инициализация UI элементов управления
      private void InitializeUIControls()
      {
            if (opacitySlider != null)
            {
                  opacitySlider.onValueChanged.AddListener((value) => UpdateOpacity(value));
                  opacitySlider.value = 0.7f; // Значение по умолчанию
            }

            if (preserveShadowsSlider != null)
            {
                  preserveShadowsSlider.onValueChanged.AddListener((value) => UpdatePreserveShadows(value));
                  preserveShadowsSlider.value = 0.8f; // Значение по умолчанию
            }

            if (preserveTextureSlider != null)
            {
                  preserveTextureSlider.onValueChanged.AddListener((value) => UpdatePreserveTexture(value));
                  preserveTextureSlider.value = 0.5f; // Значение по умолчанию
            }

            if (debugViewToggle != null)
            {
                  debugViewToggle.onValueChanged.AddListener((value) => UpdateDebugView(value));
                  debugViewToggle.isOn = false; // Значение по умолчанию
            }

            if (enhancedModeToggle != null)
            {
                  enhancedModeToggle.onValueChanged.AddListener((value) => SwitchPaintMode(value));
                  enhancedModeToggle.isOn = false; // Значение по умолчанию
            }

            // Инициализируем индикатор цвета
            UpdateColorIndicator(demoColors[currentColorIndex]);
      }

      // Обновление в каждом кадре
      private void Update()
      {
            // Проверяем, создана ли маска сегментации
            if (wallSegmentation != null && segmentationMask != null)
            {
                  // Обновляем маску для текущего эффекта
                  if (currentPaintEffect == standardPaintEffect)
                  {
                        standardPaintEffect.maskTexture = segmentationMask;
                  }
                  else if (currentPaintEffect is ImprovedWallPaintBlit improvedBlit)
                  {
                        improvedBlit.maskTexture = segmentationMask;
                  }
            }

            // Ручное переключение цветов через клавиатуру (для тестирования)
            if (Input.GetKeyDown(KeyCode.Space))
            {
                  NextColor();
            }

            // Переключение режима эффекта
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                  SwitchPaintMode(!enhancedModeToggle.isOn);
                  enhancedModeToggle.isOn = !enhancedModeToggle.isOn;
            }

            // Обновление автоматического режима
            if (demoMode == DemoMode.AutoColor || demoMode == DemoMode.FullAuto)
            {
                  if (Time.time - lastChangeTime > autoChangeInterval)
                  {
                        NextColor();
                        lastChangeTime = Time.time;
                  }
            }
      }

      // Переключение на следующий цвет
      public void NextColor()
      {
            currentColorIndex = (currentColorIndex + 1) % demoColors.Length;
            Color newColor = demoColors[currentColorIndex];

            // Обновляем цвет для текущего эффекта
            if (currentPaintEffect == standardPaintEffect)
            {
                  standardPaintEffect.paintColor = newColor;
            }
            else if (currentPaintEffect is ImprovedWallPaintBlit improvedBlit)
            {
                  improvedBlit.paintColor = newColor;
            }

            // Обновляем индикатор цвета в UI
            UpdateColorIndicator(newColor);
      }

      // Обновление индикатора цвета
      private void UpdateColorIndicator(Color color)
      {
            if (colorIndicator != null)
            {
                  colorIndicator.color = color;
            }
      }

      // Обновление прозрачности
      private void UpdateOpacity(float value)
      {
            if (currentPaintEffect == standardPaintEffect)
            {
                  standardPaintEffect.opacity = value;
            }
            else if (currentPaintEffect is ImprovedWallPaintBlit improvedBlit)
            {
                  improvedBlit.opacity = value;
            }
      }

      // Обновление сохранения теней
      private void UpdatePreserveShadows(float value)
      {
            if (currentPaintEffect == standardPaintEffect)
            {
                  standardPaintEffect.preserveShadows = value;
            }
            else if (currentPaintEffect is ImprovedWallPaintBlit improvedBlit)
            {
                  improvedBlit.preserveShadows = value;
            }
      }

      // Обновление сохранения текстуры
      private void UpdatePreserveTexture(float value)
      {
            if (currentPaintEffect is ImprovedWallPaintBlit improvedBlit)
            {
                  // Этот параметр доступен только в улучшенном режиме
                  if (improvedBlit.GetType() == typeof(ImprovedWallPaintBlit))
                  {
                        // Используем рефлексию для доступа к параметру _detailPreservation
                        System.Reflection.FieldInfo fieldInfo = improvedBlit.GetType().GetField("_detailPreservation",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                        if (fieldInfo != null)
                        {
                              fieldInfo.SetValue(improvedBlit, value);
                        }
                  }
            }
      }

      // Обновление режима отладки
      private void UpdateDebugView(bool value)
      {
            if (currentPaintEffect == standardPaintEffect)
            {
                  standardPaintEffect.debugView = value;
            }
            else if (currentPaintEffect is ImprovedWallPaintBlit improvedBlit)
            {
                  improvedBlit.debugView = value;
            }
      }

      // Переключение между стандартным и улучшенным режимом
      private void SwitchPaintMode(bool useEnhanced)
      {
            if (useEnhanced)
            {
                  // Включаем улучшенный режим
                  if (currentPaintEffect == standardPaintEffect)
                  {
                        // Сохраняем текущие параметры
                        Color currentColor = standardPaintEffect.paintColor;
                        float currentOpacity = standardPaintEffect.opacity;
                        float currentPreserveShadows = standardPaintEffect.preserveShadows;
                        bool currentDebugView = standardPaintEffect.debugView;

                        // Создаем новый экземпляр улучшенного эффекта, если его еще нет
                        if (improvedPaintBlit == null)
                        {
                              if (enhancedPaintEffectPrefab != null)
                              {
                                    GameObject effectObj = Instantiate(enhancedPaintEffectPrefab, transform);
                                    improvedPaintBlit = effectObj.GetComponent<ImprovedWallPaintBlit>();
                              }
                              else
                              {
                                    // Создаем компонент на текущем объекте
                                    improvedPaintBlit = gameObject.AddComponent<ImprovedWallPaintBlit>();
                              }
                        }

                        // Передаем параметры
                        if (improvedPaintBlit != null)
                        {
                              improvedPaintBlit.maskTexture = standardPaintEffect.maskTexture;
                              improvedPaintBlit.paintColor = currentColor;
                              improvedPaintBlit.opacity = currentOpacity;
                              improvedPaintBlit.preserveShadows = currentPreserveShadows;
                              improvedPaintBlit.debugView = currentDebugView;

                              // Отключаем стандартный эффект и включаем улучшенный
                              standardPaintEffect.enabled = false;
                              improvedPaintBlit.enabled = true;

                              // Обновляем ссылку на текущий эффект
                              currentPaintEffect = improvedPaintBlit;

                              Debug.Log("Переключение на улучшенный режим окрашивания");
                        }
                        else
                        {
                              Debug.LogError("Не удалось создать улучшенный эффект окрашивания");
                        }
                  }
            }
            else
            {
                  // Возвращаемся к стандартному режиму
                  if (currentPaintEffect != standardPaintEffect && improvedPaintBlit != null)
                  {
                        // Сохраняем текущие параметры
                        Color currentColor = improvedPaintBlit.paintColor;
                        float currentOpacity = improvedPaintBlit.opacity;
                        float currentPreserveShadows = improvedPaintBlit.preserveShadows;
                        bool currentDebugView = improvedPaintBlit.debugView;

                        // Передаем параметры в стандартный эффект
                        standardPaintEffect.paintColor = currentColor;
                        standardPaintEffect.opacity = currentOpacity;
                        standardPaintEffect.preserveShadows = currentPreserveShadows;
                        standardPaintEffect.debugView = currentDebugView;

                        // Включаем стандартный эффект и отключаем улучшенный
                        standardPaintEffect.enabled = true;
                        improvedPaintBlit.enabled = false;

                        // Обновляем ссылку на текущий эффект
                        currentPaintEffect = standardPaintEffect;

                        Debug.Log("Переключение на стандартный режим окрашивания");
                  }
            }
      }

      // Корутина для автоматической демонстрации
      private IEnumerator AutoDemoRoutine()
      {
            lastChangeTime = Time.time;

            while (true)
            {
                  // Автоматическое изменение цвета
                  if (demoMode == DemoMode.AutoColor || demoMode == DemoMode.FullAuto)
                  {
                        yield return new WaitForSeconds(autoChangeInterval);
                        NextColor();
                  }

                  // Автоматическое изменение параметров
                  if (demoMode == DemoMode.AutoParams || demoMode == DemoMode.FullAuto)
                  {
                        yield return new WaitForSeconds(autoChangeInterval / 2);

                        // Случайные значения для параметров
                        float randomOpacity = Random.Range(0.5f, 1.0f);
                        float randomPreserveShadows = Random.Range(0.5f, 1.0f);
                        float randomPreserveTexture = Random.Range(0.3f, 0.8f);

                        // Обновляем UI слайдеры
                        if (opacitySlider != null) opacitySlider.value = randomOpacity;
                        if (preserveShadowsSlider != null) preserveShadowsSlider.value = randomPreserveShadows;
                        if (preserveTextureSlider != null) preserveTextureSlider.value = randomPreserveTexture;

                        // Переключаем режим окрашивания каждые несколько итераций
                        if (Random.value > 0.7f)
                        {
                              bool useEnhanced = !enhancedModeToggle.isOn;
                              enhancedModeToggle.isOn = useEnhanced;
                              SwitchPaintMode(useEnhanced);
                        }
                  }

                  yield return null;
            }
      }

      // Выбор цвета для окрашивания из палитры
      public void SelectColor(int colorIndex)
      {
            if (colorIndex >= 0 && colorIndex < demoColors.Length)
            {
                  currentColorIndex = colorIndex;
                  Color newColor = demoColors[currentColorIndex];

                  // Обновляем цвет для текущего эффекта
                  if (currentPaintEffect == standardPaintEffect)
                  {
                        standardPaintEffect.paintColor = newColor;
                  }
                  else if (currentPaintEffect is ImprovedWallPaintBlit improvedBlit)
                  {
                        improvedBlit.paintColor = newColor;
                  }

                  // Обновляем индикатор цвета в UI
                  UpdateColorIndicator(newColor);
            }
      }

      // Выбор режима демонстрации
      public void SetDemoMode(int mode)
      {
            demoMode = (DemoMode)mode;

            if (demoMode != DemoMode.Manual && !IsInvoking("AutoDemoRoutine"))
            {
                  StartCoroutine(AutoDemoRoutine());
            }
      }

      // Сброс всех параметров до значений по умолчанию
      public void ResetToDefaults()
      {
            // Устанавливаем значения слайдеров
            if (opacitySlider != null) opacitySlider.value = 0.7f;
            if (preserveShadowsSlider != null) preserveShadowsSlider.value = 0.8f;
            if (preserveTextureSlider != null) preserveTextureSlider.value = 0.5f;

            // Сбрасываем переключатели
            if (debugViewToggle != null) debugViewToggle.isOn = false;

            // Сбрасываем цвет
            currentColorIndex = 0;
            Color defaultColor = demoColors[currentColorIndex];

            // Обновляем параметры текущего эффекта
            if (currentPaintEffect == standardPaintEffect)
            {
                  standardPaintEffect.paintColor = defaultColor;
                  standardPaintEffect.opacity = 0.7f;
                  standardPaintEffect.preserveShadows = 0.8f;
                  standardPaintEffect.debugView = false;
            }
            else if (currentPaintEffect is ImprovedWallPaintBlit improvedBlit)
            {
                  improvedBlit.paintColor = defaultColor;
                  improvedBlit.opacity = 0.7f;
                  improvedBlit.preserveShadows = 0.8f;
                  improvedBlit.debugView = false;

                  // Сбрасываем дополнительные параметры улучшенного эффекта
                  System.Reflection.FieldInfo fieldInfo = improvedBlit.GetType().GetField("_detailPreservation",
                      System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                  if (fieldInfo != null)
                  {
                        fieldInfo.SetValue(improvedBlit, 0.5f);
                  }
            }

            // Обновляем индикатор цвета
            UpdateColorIndicator(defaultColor);
      }
}