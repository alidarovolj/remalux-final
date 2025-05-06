using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Компонент, управляющий палитрой цветов для покраски стен
/// </summary>
public class ColorPalette : MonoBehaviour
{
      [System.Serializable]
      public class ColorPreset
      {
            public string name;
            public Color color;
            public GameObject buttonObject;
      }

      [Header("Настройки палитры")]
      [SerializeField] private List<ColorPreset> colorPresets = new List<ColorPreset>();

      [Header("Связь с компонентами")]
      [SerializeField] private WallPaintingTextureUpdater wallPaintingUpdater;
      [SerializeField] private WallPaintingSetup wallPaintingSetup;

      [Header("Шаблон кнопки")]
      [SerializeField] private GameObject buttonPrefab;
      [SerializeField] private Transform buttonsContainer;

      [Header("Параметры UI")]
      [SerializeField] private float buttonSize = 60f;
      [SerializeField] private float buttonSpacing = 10f;
      [SerializeField] private float buttonBorderSize = 3f;
      [SerializeField] private Color selectedBorderColor = Color.white;
      [SerializeField] private Color normalBorderColor = Color.gray;

      private void Start()
      {
            // Если не указаны компоненты, находим их автоматически
            if (wallPaintingUpdater == null)
                  wallPaintingUpdater = FindObjectOfType<WallPaintingTextureUpdater>();

            if (wallPaintingSetup == null)
                  wallPaintingSetup = FindObjectOfType<WallPaintingSetup>();

            // Если палитра пуста, добавляем базовые цвета
            if (colorPresets.Count == 0)
            {
                  AddDefaultColors();
            }

            // Если нет родительского объекта для кнопок, создаем его
            if (buttonsContainer == null)
            {
                  // Ищем Canvas
                  Canvas canvas = FindObjectOfType<Canvas>();
                  if (canvas == null)
                  {
                        // Создаем Canvas
                        GameObject canvasObj = new GameObject("Canvas");
                        canvas = canvasObj.AddComponent<Canvas>();
                        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                        canvasObj.AddComponent<CanvasScaler>();
                        canvasObj.AddComponent<GraphicRaycaster>();
                  }

                  // Создаем контейнер для кнопок
                  GameObject containerObj = new GameObject("ColorButtonsContainer");
                  containerObj.transform.SetParent(canvas.transform, false);

                  // Настраиваем RectTransform для нижней части экрана
                  RectTransform containerRect = containerObj.AddComponent<RectTransform>();
                  containerRect.anchorMin = new Vector2(0, 0);
                  containerRect.anchorMax = new Vector2(1, 0.15f);
                  containerRect.offsetMin = Vector2.zero;
                  containerRect.offsetMax = Vector2.zero;

                  // Добавляем горизонтальную группу для автоматического размещения кнопок
                  HorizontalLayoutGroup layoutGroup = containerObj.AddComponent<HorizontalLayoutGroup>();
                  layoutGroup.spacing = buttonSpacing;
                  layoutGroup.childAlignment = TextAnchor.MiddleCenter;
                  layoutGroup.childForceExpandWidth = false;
                  layoutGroup.childForceExpandHeight = false;
                  layoutGroup.padding = new RectOffset(10, 10, 10, 10);

                  buttonsContainer = containerRect;
            }

            // Создаем кнопки для всех цветов из палитры
            CreateColorButtons();

            // Выбираем первый цвет по умолчанию
            if (colorPresets.Count > 0)
            {
                  SelectColor(0);
            }
      }

      /// <summary>
      /// Добавляет базовые цвета в палитру
      /// </summary>
      private void AddDefaultColors()
      {
            colorPresets.Add(new ColorPreset { name = "Красный", color = new Color(0.85f, 0.1f, 0.1f, 1.0f) });
            colorPresets.Add(new ColorPreset { name = "Зеленый", color = new Color(0.1f, 0.7f, 0.3f, 1.0f) });
            colorPresets.Add(new ColorPreset { name = "Синий", color = new Color(0.2f, 0.4f, 0.8f, 1.0f) });
            colorPresets.Add(new ColorPreset { name = "Желтый", color = new Color(0.9f, 0.8f, 0.1f, 1.0f) });
            colorPresets.Add(new ColorPreset { name = "Розовый", color = new Color(0.9f, 0.4f, 0.7f, 1.0f) });
            colorPresets.Add(new ColorPreset { name = "Бежевый", color = new Color(0.93f, 0.87f, 0.73f, 1.0f) });
      }

      /// <summary>
      /// Создает кнопки для цветов палитры
      /// </summary>
      private void CreateColorButtons()
      {
            for (int i = 0; i < colorPresets.Count; i++)
            {
                  ColorPreset preset = colorPresets[i];

                  // Создаем кнопку (используем шаблон или создаем с нуля)
                  GameObject buttonObj;
                  if (buttonPrefab != null)
                  {
                        buttonObj = Instantiate(buttonPrefab, buttonsContainer);
                  }
                  else
                  {
                        buttonObj = new GameObject($"ColorButton_{preset.name}");
                        buttonObj.transform.SetParent(buttonsContainer, false);

                        // Настраиваем RectTransform для кнопки
                        RectTransform rectTransform = buttonObj.AddComponent<RectTransform>();
                        rectTransform.sizeDelta = new Vector2(buttonSize, buttonSize);

                        // Добавляем изображение для кнопки (фон)
                        GameObject imageObj = new GameObject("Background");
                        imageObj.transform.SetParent(buttonObj.transform, false);
                        RectTransform imageRect = imageObj.AddComponent<RectTransform>();
                        imageRect.anchorMin = Vector2.zero;
                        imageRect.anchorMax = Vector2.one;
                        imageRect.offsetMin = new Vector2(buttonBorderSize, buttonBorderSize);
                        imageRect.offsetMax = new Vector2(-buttonBorderSize, -buttonBorderSize);

                        Image colorImage = imageObj.AddComponent<Image>();
                        colorImage.color = preset.color;

                        // Добавляем рамку для кнопки
                        GameObject borderObj = new GameObject("Border");
                        borderObj.transform.SetParent(buttonObj.transform, false);
                        borderObj.transform.SetAsFirstSibling(); // Ставим под изображение цвета

                        RectTransform borderRect = borderObj.AddComponent<RectTransform>();
                        borderRect.anchorMin = Vector2.zero;
                        borderRect.anchorMax = Vector2.one;
                        borderRect.offsetMin = Vector2.zero;
                        borderRect.offsetMax = Vector2.zero;

                        Image borderImage = borderObj.AddComponent<Image>();
                        borderImage.color = normalBorderColor;

                        // Добавляем компонент кнопки
                        Button button = buttonObj.AddComponent<Button>();
                        button.targetGraphic = borderImage;

                        ColorBlock colors = button.colors;
                        colors.normalColor = normalBorderColor;
                        colors.highlightedColor = Color.white;
                        colors.pressedColor = selectedBorderColor;
                        colors.selectedColor = selectedBorderColor;
                        button.colors = colors;
                  }

                  // Сохраняем ссылку на кнопку в пресете
                  preset.buttonObject = buttonObj;
                  colorPresets[i] = preset;

                  // Получаем компонент кнопки (если его еще нет)
                  Button colorButton = buttonObj.GetComponent<Button>();
                  if (colorButton == null)
                  {
                        colorButton = buttonObj.AddComponent<Button>();
                  }

                  // Устанавливаем цвет кнопки
                  Image buttonImage = buttonObj.GetComponentInChildren<Image>();
                  if (buttonImage != null)
                  {
                        buttonImage.color = preset.color;
                  }

                  // Добавляем обработчик нажатия 
                  int colorIndex = i; // Сохраняем индекс для замыкания
                  colorButton.onClick.AddListener(() => SelectColor(colorIndex));
            }
      }

      /// <summary>
      /// Выбирает цвет из палитры и обновляет настройки покраски
      /// </summary>
      public void SelectColor(int colorIndex)
      {
            if (colorIndex < 0 || colorIndex >= colorPresets.Count) return;

            ColorPreset selectedPreset = colorPresets[colorIndex];

            // Обновляем визуализацию кнопок
            for (int i = 0; i < colorPresets.Count; i++)
            {
                  ColorPreset preset = colorPresets[i];
                  if (preset.buttonObject != null)
                  {
                        // Выделяем выбранную кнопку
                        Image borderImage = preset.buttonObject.GetComponentInChildren<Image>();
                        if (borderImage != null)
                        {
                              borderImage.color = (i == colorIndex) ? selectedBorderColor : normalBorderColor;
                        }
                  }
            }

            // Обновляем цвет покраски в компонентах
            if (wallPaintingUpdater != null)
            {
                  wallPaintingUpdater.paintColor = selectedPreset.color;
            }

            if (wallPaintingSetup != null)
            {
                  wallPaintingSetup.paintColor = selectedPreset.color;
            }

            Debug.Log($"ColorPalette: Выбран цвет '{selectedPreset.name}': {selectedPreset.color}");
      }
}