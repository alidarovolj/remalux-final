using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// UI-компонент для выбора цвета стен и настройки непрозрачности
/// </summary>
public class ColorPickerUI : MonoBehaviour
{
      [System.Serializable]
      public class ColorPreset
      {
            public string name;
            public Color color;
            public Sprite thumbnail;
      }

      [Header("UI Elements")]
      [SerializeField] private Slider opacitySlider;
      [SerializeField] private Transform colorButtonsContainer;
      [SerializeField] private Button colorButtonPrefab;
      [SerializeField] private Text currentColorName;

      [Header("Color Presets")]
      [SerializeField] private List<ColorPreset> colorPresets = new List<ColorPreset>();
      [SerializeField] private int defaultColorIndex = 0;

      [Header("References")]
      [SerializeField] private WallPaintBlit wallPaintBlit;

      // Текущий индекс выбранного цвета
      private int currentColorIndex = 0;
      private List<Button> colorButtons = new List<Button>();

      private void Start()
      {
            // Находим WallPaintBlit, если не указан
            if (wallPaintBlit == null)
            {
                  wallPaintBlit = FindObjectOfType<WallPaintBlit>();
            }

            // Настраиваем слайдер непрозрачности
            if (opacitySlider != null && wallPaintBlit != null)
            {
                  // Устанавливаем начальное значение
                  opacitySlider.value = wallPaintBlit.opacity;

                  // Добавляем обработчик изменения
                  opacitySlider.onValueChanged.AddListener((value) =>
                  {
                        if (wallPaintBlit != null)
                        {
                              wallPaintBlit.opacity = value;
                        }
                  });
            }

            // Создаем кнопки для выбора цвета
            CreateColorButtons();

            // Выбираем цвет по умолчанию
            SelectColor(defaultColorIndex);
      }

      // Создаем кнопки для выбора цвета
      private void CreateColorButtons()
      {
            // Очищаем существующие кнопки
            foreach (Button button in colorButtons)
            {
                  if (button != null)
                  {
                        Destroy(button.gameObject);
                  }
            }
            colorButtons.Clear();

            // Если контейнер не задан, выходим
            if (colorButtonsContainer == null || colorButtonPrefab == null)
            {
                  Debug.LogError("Color buttons container or prefab not assigned!");
                  return;
            }

            // Создаем кнопки для каждого пресета цвета
            for (int i = 0; i < colorPresets.Count; i++)
            {
                  int colorIndex = i; // Сохраняем индекс в локальной переменной для замыкания

                  // Создаем кнопку
                  Button newButton = Instantiate(colorButtonPrefab, colorButtonsContainer);

                  // Настраиваем внешний вид
                  Image buttonImage = newButton.GetComponent<Image>();
                  if (buttonImage != null)
                  {
                        buttonImage.color = colorPresets[i].color;

                        // Если есть миниатюра, используем ее
                        if (colorPresets[i].thumbnail != null)
                        {
                              buttonImage.sprite = colorPresets[i].thumbnail;
                              buttonImage.type = Image.Type.Sliced;
                        }
                  }

                  // Добавляем обработчик нажатия
                  newButton.onClick.AddListener(() => SelectColor(colorIndex));

                  // Сохраняем кнопку в списке
                  colorButtons.Add(newButton);
            }
      }

      // Выбираем цвет по индексу
      public void SelectColor(int index)
      {
            // Проверяем корректность индекса
            if (index < 0 || index >= colorPresets.Count)
            {
                  Debug.LogWarning($"Color index out of range: {index}");
                  return;
            }

            // Обновляем текущий индекс
            currentColorIndex = index;

            // Применяем цвет к WallPaintBlit
            if (wallPaintBlit != null)
            {
                  wallPaintBlit.paintColor = colorPresets[index].color;
            }

            // Обновляем текст с названием цвета
            if (currentColorName != null)
            {
                  currentColorName.text = colorPresets[index].name;
            }

            // Обновляем визуальное состояние кнопок
            UpdateButtonsVisualState();
      }

      // Обновляем визуальное состояние кнопок (выделяем выбранную)
      private void UpdateButtonsVisualState()
      {
            for (int i = 0; i < colorButtons.Count; i++)
            {
                  if (colorButtons[i] != null)
                  {
                        // Выделяем выбранную кнопку
                        colorButtons[i].transform.localScale = (i == currentColorIndex)
                            ? new Vector3(1.2f, 1.2f, 1.2f)
                            : Vector3.one;
                  }
            }
      }

      // Добавляет предустановленные цвета в список
      public void PopulateDefaultColors()
      {
            colorPresets.Clear();

            // Добавляем стандартные цвета Dulux
            colorPresets.Add(new ColorPreset { name = "Белый", color = new Color(0.95f, 0.95f, 0.95f) });
            colorPresets.Add(new ColorPreset { name = "Бежевый", color = new Color(0.96f, 0.87f, 0.70f) });
            colorPresets.Add(new ColorPreset { name = "Песочный", color = new Color(0.95f, 0.87f, 0.73f) });
            colorPresets.Add(new ColorPreset { name = "Светло-голубой", color = new Color(0.67f, 0.85f, 0.90f) });
            colorPresets.Add(new ColorPreset { name = "Мятный", color = new Color(0.60f, 0.98f, 0.60f) });
            colorPresets.Add(new ColorPreset { name = "Лавандовый", color = new Color(0.80f, 0.60f, 0.98f) });
            colorPresets.Add(new ColorPreset { name = "Лососевый", color = new Color(0.98f, 0.63f, 0.48f) });
            colorPresets.Add(new ColorPreset { name = "Желтый", color = new Color(1.0f, 0.95f, 0.2f) });
            colorPresets.Add(new ColorPreset { name = "Серый", color = new Color(0.66f, 0.66f, 0.66f) });
            colorPresets.Add(new ColorPreset { name = "Синий", color = new Color(0.0f, 0.45f, 0.85f) });
            colorPresets.Add(new ColorPreset { name = "Зеленый", color = new Color(0.13f, 0.55f, 0.13f) });
            colorPresets.Add(new ColorPreset { name = "Красный", color = new Color(0.86f, 0.08f, 0.24f) });
      }

      // Возвращает текущий выбранный цвет
      public Color GetCurrentColor()
      {
            if (currentColorIndex >= 0 && currentColorIndex < colorPresets.Count)
            {
                  return colorPresets[currentColorIndex].color;
            }

            return Color.white;
      }

      // Возвращает текущую непрозрачность
      public float GetCurrentOpacity()
      {
            return wallPaintBlit != null ? wallPaintBlit.opacity : 0.7f;
      }
}