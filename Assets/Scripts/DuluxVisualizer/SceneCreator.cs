using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

#if UNITY_EDITOR

#if USING_AR_FOUNDATION
using UnityEngine.XR.ARFoundation;
#endif

#if USING_XR_CORE_UTILS
using Unity.XR.CoreUtils;
#endif

namespace DuluxVisualizer
{
    /// <summary>
    /// Централизованный класс для создания AR сцены с функционалом окрашивания стен
    /// </summary>
    public static class SceneCreator
    {
        /// <summary>
        /// Создает полностью настроенную AR сцену с сегментацией стен
        /// </summary>
        public static void CreateScene()
        {
#if USING_AR_FOUNDATION && USING_XR_CORE_UTILS
            Debug.Log("Создание AR сцены для визуализатора Dulux...");
            
            // 1. Создание базовых AR объектов
            GameObject arSession = CreateARSession();
            GameObject xrOrigin = CreateXROrigin();
            
            // 2. Настройка AR камеры
            GameObject arCamera = SetupARCamera(xrOrigin);
            
            // 3. Настройка сегментации стен
            SetupWallSegmentation(arCamera);
            
            // 4. Настройка UI
            SetupUI();
            
            Debug.Log("AR сцена успешно создана!");
#else
            Debug.LogError("Для создания AR сцены необходимы пакеты AR Foundation и XR Core Utils. Пожалуйста, установите их через Package Manager.");
#endif
        }

#if USING_AR_FOUNDATION && USING_XR_CORE_UTILS
        /// <summary>
        /// Создает и настраивает AR Session
        /// </summary>
        private static GameObject CreateARSession()
        {
            GameObject arSession = new GameObject("AR Session");
            arSession.AddComponent<ARSession>();
            return arSession;
        }

        /// <summary>
        /// Создает и настраивает XR Origin
        /// </summary>
        private static GameObject CreateXROrigin()
        {
            GameObject xrOrigin = new GameObject("XR Origin");
            xrOrigin.AddComponent<XROrigin>();
            return xrOrigin;
        }

        /// <summary>
        /// Настраивает AR камеру с необходимыми компонентами
        /// </summary>
        private static GameObject SetupARCamera(GameObject xrOrigin)
        {
            // Создаем объект камеры
            GameObject arCamera = new GameObject("AR Camera");
            arCamera.tag = "MainCamera";
            arCamera.transform.SetParent(xrOrigin.transform);
            
            // Добавляем компоненты камеры
            Camera camera = arCamera.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            
            // Добавляем AR компоненты
            arCamera.AddComponent<ARCameraManager>();
            arCamera.AddComponent<ARCameraBackground>();
            
            return arCamera;
        }

        /// <summary>
        /// Настраивает сегментацию стен и компоненты визуализации
        /// </summary>
        private static void SetupWallSegmentation(GameObject arCamera)
        {
            // Создаем текстуру для маски сегментации
            RenderTexture maskRT = new RenderTexture(256, 256, 0, RenderTextureFormat.R8);
            maskRT.name = "WallSegmentationMask";
            maskRT.filterMode = FilterMode.Bilinear;
            maskRT.Create();
            
            // Добавляем компонент обработки сегментации
            WallSegmentationProcessor segmentationProcessor = arCamera.AddComponent<WallSegmentationProcessor>();
            segmentationProcessor.SetOutputTexture(maskRT);
            
            // Загружаем модель ONNX
            NNModel model = Resources.Load<NNModel>("Models/model");
            if (model != null)
            {
                segmentationProcessor.SetModel(model);
            }
            else
            {
                Debug.LogWarning("ONNX модель не найдена. Пожалуйста, импортируйте модель в папку Resources/Models.");
            }
            
            // Добавляем компонент для рендеринга стен
            ImprovedWallPaintBlit paintBlit = arCamera.AddComponent<ImprovedWallPaintBlit>();
            paintBlit.maskTexture = maskRT;
            
            // Настраиваем оптимизатор
            if (arCamera.GetComponent<WallSegmentationOptimizer>() == null) 
            {
                arCamera.AddComponent<WallSegmentationOptimizer>();
            }
        }

        /// <summary>
        /// Настраивает UI компоненты для управления окрашиванием
        /// </summary>
        private static void SetupUI()
        {
            // Создаем канвас
            GameObject canvas = new GameObject("UI Canvas");
            Canvas canvasComponent = canvas.AddComponent<Canvas>();
            canvasComponent.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvas.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            
            // Создаем панель цветов
            GameObject colorPanel = CreateColorPalettePanel(canvas.transform);
            
            // Создаем панель настроек
            GameObject settingsPanel = CreateSettingsPanel(canvas.transform);
            
            // Добавляем контроллер UI
            UIController uiController = canvas.AddComponent<UIController>();
            
            // Связываем UI-компоненты с контроллером
            SerializedObject serializedController = new SerializedObject(uiController);
            SerializedProperty colorPaletteProperty = serializedController.FindProperty("_colorPalette");
            colorPaletteProperty.objectReferenceValue = colorPanel;
            
            SerializedProperty settingsPanelProperty = serializedController.FindProperty("_settingsPanel");
            settingsPanelProperty.objectReferenceValue = settingsPanel;
            
            serializedController.ApplyModifiedProperties();
        }
        
        /// <summary>
        /// Создает панель с палитрой цветов
        /// </summary>
        private static GameObject CreateColorPalettePanel(Transform parent)
        {
            GameObject panel = new GameObject("Color Palette Panel");
            panel.transform.SetParent(parent, false);
            
            // Добавляем компоненты UI
            UnityEngine.UI.Image panelImage = panel.AddComponent<UnityEngine.UI.Image>();
            panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            
            // Настраиваем RectTransform
            RectTransform rectTransform = panel.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0, 0);
            rectTransform.anchorMax = new Vector2(1, 0);
            rectTransform.pivot = new Vector2(0.5f, 0);
            rectTransform.sizeDelta = new Vector2(0, 100);
            rectTransform.anchoredPosition = new Vector2(0, 0);
            
            // Добавляем Grid Layout Group для автоматического размещения кнопок
            UnityEngine.UI.GridLayoutGroup gridLayout = panel.AddComponent<UnityEngine.UI.GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(50, 50);
            gridLayout.spacing = new Vector2(10, 10);
            gridLayout.padding = new UnityEngine.UI.RectOffset(10, 10, 10, 10);
            gridLayout.startCorner = UnityEngine.UI.GridLayoutGroup.Corner.UpperLeft;
            gridLayout.startAxis = UnityEngine.UI.GridLayoutGroup.Axis.Horizontal;
            gridLayout.childAlignment = TextAnchor.MiddleCenter;
            gridLayout.constraintCount = 6;
            gridLayout.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
            
            // Создаем кнопки цветов
            CreateColorButtons(panel.transform);
            
            return panel;
        }
        
        /// <summary>
        /// Создает кнопки цветов для палитры
        /// </summary>
        private static void CreateColorButtons(Transform parent)
        {
            // Стандартные цвета
            Color[] colors = new Color[]
            {
                new Color(0.957f, 0.263f, 0.212f), // Red
                new Color(0.914f, 0.118f, 0.388f), // Pink
                new Color(0.612f, 0.153f, 0.655f), // Purple
                new Color(0.404f, 0.227f, 0.718f), // Deep Purple
                new Color(0.247f, 0.318f, 0.71f),  // Indigo
                new Color(0.129f, 0.588f, 0.953f), // Blue
                new Color(0.012f, 0.663f, 0.957f), // Light Blue
                new Color(0f, 0.737f, 0.831f),     // Cyan
                new Color(0f, 0.588f, 0.533f),     // Teal
                new Color(0.298f, 0.686f, 0.314f), // Green
                new Color(0.545f, 0.765f, 0.29f),  // Light Green
                new Color(0.804f, 0.863f, 0.224f)  // Lime
            };
            
            for (int i = 0; i < colors.Length; i++)
            {
                // Создаем кнопку
                GameObject button = new GameObject($"ColorButton_{i}");
                button.transform.SetParent(parent, false);
                
                // Добавляем компоненты
                UnityEngine.UI.Image image = button.AddComponent<UnityEngine.UI.Image>();
                image.color = colors[i];
                
                UnityEngine.UI.Button buttonComponent = button.AddComponent<UnityEngine.UI.Button>();
                UnityEngine.UI.ColorBlock colorBlock = buttonComponent.colors;
                colorBlock.normalColor = colors[i];
                colorBlock.highlightedColor = new Color(
                    colors[i].r * 1.2f, 
                    colors[i].g * 1.2f, 
                    colors[i].b * 1.2f, 
                    1.0f
                );
                buttonComponent.colors = colorBlock;
            }
        }
        
        /// <summary>
        /// Создает панель настроек
        /// </summary>
        private static GameObject CreateSettingsPanel(Transform parent)
        {
            GameObject panel = new GameObject("Settings Panel");
            panel.transform.SetParent(parent, false);
            
            // Добавляем компоненты UI
            UnityEngine.UI.Image panelImage = panel.AddComponent<UnityEngine.UI.Image>();
            panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            
            // Настраиваем RectTransform
            RectTransform rectTransform = panel.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0, 0.5f);
            rectTransform.anchorMax = new Vector2(0, 0.5f);
            rectTransform.pivot = new Vector2(0, 0.5f);
            rectTransform.sizeDelta = new Vector2(300, 400);
            rectTransform.anchoredPosition = new Vector2(20, 0);
            
            // Добавляем Vertical Layout Group для автоматического размещения слайдеров
            UnityEngine.UI.VerticalLayoutGroup verticalLayout = panel.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            verticalLayout.spacing = 10;
            verticalLayout.padding = new UnityEngine.UI.RectOffset(10, 10, 10, 10);
            verticalLayout.childAlignment = TextAnchor.UpperCenter;
            verticalLayout.childControlHeight = true;
            verticalLayout.childControlWidth = true;
            verticalLayout.childForceExpandHeight = false;
            verticalLayout.childForceExpandWidth = true;
            
            // Создаем заголовок
            CreateLabel(panel.transform, "Настройки окрашивания");
            
            // Создаем слайдеры настроек
            CreateSlider(panel.transform, "Прозрачность", 0.7f, 0, 1);
            CreateSlider(panel.transform, "Сохранение теней", 0.8f, 0, 1);
            CreateSlider(panel.transform, "Сглаживание краев", 0.1f, 0, 1);
            CreateSlider(panel.transform, "Интервал обработки", 0.1f, 0.05f, 0.5f);
            
            // Создаем переключатели
            CreateToggle(panel.transform, "Режим отладки", false);
            CreateToggle(panel.transform, "Временное сглаживание", true);
            
            // По умолчанию делаем панель скрытой
            panel.SetActive(false);
            
            return panel;
        }
        
        /// <summary>
        /// Создает метку с текстом
        /// </summary>
        private static GameObject CreateLabel(Transform parent, string text)
        {
            GameObject label = new GameObject("Label");
            label.transform.SetParent(parent, false);
            
            // Добавляем компоненты
            UnityEngine.UI.Text textComponent = label.AddComponent<UnityEngine.UI.Text>();
            textComponent.text = text;
            textComponent.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            textComponent.fontSize = 18;
            textComponent.alignment = TextAnchor.MiddleCenter;
            textComponent.color = Color.white;
            
            // Настраиваем RectTransform
            RectTransform rectTransform = label.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(0, 30);
            
            return label;
        }
        
        /// <summary>
        /// Создает слайдер
        /// </summary>
        private static GameObject CreateSlider(Transform parent, string name, float defaultValue, float minValue, float maxValue)
        {
            GameObject sliderGroup = new GameObject($"SliderGroup_{name}");
            sliderGroup.transform.SetParent(parent, false);
            
            // Добавляем компоненты для группы
            UnityEngine.UI.HorizontalLayoutGroup horizontalLayout = sliderGroup.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            horizontalLayout.spacing = 10;
            horizontalLayout.childAlignment = TextAnchor.MiddleLeft;
            horizontalLayout.childControlHeight = true;
            horizontalLayout.childControlWidth = true;
            horizontalLayout.childForceExpandHeight = false;
            
            // Настраиваем RectTransform для группы
            RectTransform groupRectTransform = sliderGroup.GetComponent<RectTransform>();
            groupRectTransform.sizeDelta = new Vector2(0, 30);
            
            // Добавляем компонент селектора цветов
            GameObject colorPicker = new GameObject("Color Picker");
            colorPicker.transform.SetParent(canvas.transform, false);
            colorPicker.AddComponent<ColorPickerUI>();
            
            // Это место для добавления дополнительных UI элементов
        }
#endif
    }
}

#endif