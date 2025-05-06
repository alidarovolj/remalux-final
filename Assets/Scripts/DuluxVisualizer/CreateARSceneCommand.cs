using UnityEngine;
using UnityEngine.XR.ARFoundation;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

#if UNITY_EDITOR
/// <summary>
/// Команда для создания готовой AR сцены для покраски стен
/// </summary>
public static class CreateARSceneCommand
{
    private const string ScenePath = "Assets/Scenes/ARWallPainting.unity";

    [MenuItem("Tools/AR Wall Painting/1. Create AR Scene Template", false, 5)]
    public static void CreateARSceneTemplate()
    {
        // Сохраняем текущую сцену при необходимости
        if (EditorSceneManager.GetActiveScene().isDirty)
        {
            bool save = EditorUtility.DisplayDialog(
                "Сохранить текущую сцену?",
                "Текущая сцена содержит несохраненные изменения. Сохранить перед созданием новой сцены?",
                "Сохранить", "Не сохранять");

            if (save)
                EditorSceneManager.SaveOpenScenes();
        }

        // Создаем новую сцену
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Создаем основные компоненты AR
        // 1. AR Session
        var sessionObj = new GameObject("AR Session");
        sessionObj.AddComponent<ARSession>();

        // 2. XR Origin (AR Session Origin)
        var originObj = new GameObject("XR Origin");
        var origin = originObj.AddComponent<XROrigin>();

        // 3. Trackables Parent для плоскостей
        var trackablesObj = new GameObject("Trackables");
        trackablesObj.transform.SetParent(originObj.transform);

        // 4. Camera Offset для управления высотой камеры
        var cameraOffsetObj = new GameObject("Camera Offset");
        cameraOffsetObj.transform.SetParent(originObj.transform);
        origin.CameraFloorOffsetObject = cameraOffsetObj;

        // 5. AR Camera
        var cameraObj = new GameObject("AR Camera");
        cameraObj.transform.SetParent(cameraOffsetObj.transform);
        var camera = cameraObj.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 20f;

        // Добавляем AR компоненты к камере
        var cameraManager = cameraObj.AddComponent<ARCameraManager>();
        var cameraBackground = cameraObj.AddComponent<ARCameraBackground>();
        cameraObj.AddComponent<ARPoseDriver>();

        // Связываем камеру с XR Origin
        origin.Camera = camera;

        // 6. AR Managers
        var planeManagerObj = new GameObject("AR Plane Manager");
        planeManagerObj.transform.SetParent(originObj.transform);
        var planeManager = planeManagerObj.AddComponent<ARPlaneManager>();
        planeManager.requestedDetectionMode = UnityEngine.XR.ARSubsystems.PlaneDetectionMode.Vertical; // Только вертикальные плоскости (стены)

        // 7. Добавляем компонент WallSegmentation к камере
        var wallSeg = cameraObj.AddComponent<WallSegmentation>();

        // 8. Добавляем UI Canvas
        var canvasObj = new GameObject("UI Canvas");
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // 9. Создаем слайдер непрозрачности
        CreateOpacitySlider(canvasObj);

        // 10. Создаем кнопки выбора цвета
        CreateColorButtons(canvasObj);

        // 11. Добавляем компонент WallPaintBlit к камере
        var blit = cameraObj.AddComponent<WallPaintBlit>();

        // 12. Создаем RenderTexture для маски
        var maskRT = new RenderTexture(256, 256, 0, RenderTextureFormat.R8);
        maskRT.name = "WallMaskRT";

        // Сохраняем сцену
        if (!Directory.Exists("Assets/Scenes"))
            Directory.CreateDirectory("Assets/Scenes");

        EditorSceneManager.SaveScene(scene, ScenePath);

        Debug.Log("AR сцена для покраски стен создана и сохранена в " + ScenePath);
        EditorUtility.DisplayDialog("Готово", "AR сцена для покраски стен создана и сохранена!", "OK");
    }

    private static void CreateOpacitySlider(GameObject parent)
    {
        // Создаем панель для слайдера
        var sliderPanel = new GameObject("Opacity Panel");
        sliderPanel.transform.SetParent(parent.transform, false);
        var sliderRect = sliderPanel.AddComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0.05f, 0.85f);
        sliderRect.anchorMax = new Vector2(0.5f, 0.95f);
        sliderRect.anchoredPosition = Vector2.zero;
        sliderRect.sizeDelta = Vector2.zero;

        // Создаем фон для слайдера
        var background = new GameObject("Background");
        background.transform.SetParent(sliderPanel.transform, false);
        var bgRect = background.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        var bgImage = background.AddComponent<UnityEngine.UI.Image>();
        bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.7f);

        // Создаем текст "Opacity"
        var textObj = new GameObject("Label");
        textObj.transform.SetParent(sliderPanel.transform, false);
        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.05f, 0.6f);
        textRect.anchorMax = new Vector2(0.5f, 1f);
        textRect.sizeDelta = Vector2.zero;
        var text = textObj.AddComponent<UnityEngine.UI.Text>();
        text.text = "Непрозрачность";
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 14;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleLeft;

        // Создаем слайдер
        var sliderObj = new GameObject("Slider");
        sliderObj.transform.SetParent(sliderPanel.transform, false);
        var sliderRectTransform = sliderObj.AddComponent<RectTransform>();
        sliderRectTransform.anchorMin = new Vector2(0.05f, 0.1f);
        sliderRectTransform.anchorMax = new Vector2(0.95f, 0.6f);
        sliderRectTransform.sizeDelta = Vector2.zero;

        var slider = sliderObj.AddComponent<UnityEngine.UI.Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0.7f;

        // Создаем фон слайдера
        var sliderBg = new GameObject("Background");
        sliderBg.transform.SetParent(sliderObj.transform, false);
        var sliderBgRect = sliderBg.AddComponent<RectTransform>();
        sliderBgRect.anchorMin = new Vector2(0, 0.25f);
        sliderBgRect.anchorMax = new Vector2(1, 0.75f);
        sliderBgRect.sizeDelta = Vector2.zero;
        var sliderBgImage = sliderBg.AddComponent<UnityEngine.UI.Image>();
        sliderBgImage.color = new Color(0.1f, 0.1f, 0.1f, 1f);

        // Создаем заполнение слайдера
        var fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderObj.transform, false);
        var fillRect = fillArea.AddComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0, 0.25f);
        fillRect.anchorMax = new Vector2(1, 0.75f);
        fillRect.sizeDelta = new Vector2(-20, 0);
        fillRect.anchoredPosition = new Vector2(0, 0);

        var fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        var fillImageRect = fill.AddComponent<RectTransform>();
        fillImageRect.anchorMin = new Vector2(0, 0);
        fillImageRect.anchorMax = new Vector2(0.5f, 1);
        fillImageRect.sizeDelta = Vector2.zero;
        var fillImage = fill.AddComponent<UnityEngine.UI.Image>();
        fillImage.color = new Color(0.2f, 0.5f, 0.9f, 1);

        slider.fillRect = fillImageRect;

        // Создаем ручку слайдера
        var handleArea = new GameObject("Handle Slide Area");
        handleArea.transform.SetParent(sliderObj.transform, false);
        var handleAreaRect = handleArea.AddComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.sizeDelta = new Vector2(-20, 0);
        handleAreaRect.anchoredPosition = Vector2.zero;

        var handle = new GameObject("Handle");
        handle.transform.SetParent(handleArea.transform, false);
        var handleRect = handle.AddComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0.5f, 0);
        handleRect.anchorMax = new Vector2(0.5f, 1);
        handleRect.sizeDelta = new Vector2(20, 0);
        handleRect.anchoredPosition = Vector2.zero;
        var handleImage = handle.AddComponent<UnityEngine.UI.Image>();
        handleImage.color = Color.white;

        slider.handleRect = handleRect;

        // Добавляем обработчик события изменения значения (будет назначен в runtime)
    }

    private static void CreateColorButtons(GameObject parent)
    {
        // Создаем панель для кнопок выбора цвета
        var colorPanel = new GameObject("Color Panel");
        colorPanel.transform.SetParent(parent.transform, false);
        var colorRect = colorPanel.AddComponent<RectTransform>();
        colorRect.anchorMin = new Vector2(0.05f, 0.05f);
        colorRect.anchorMax = new Vector2(0.95f, 0.2f);
        colorRect.anchoredPosition = Vector2.zero;
        colorRect.sizeDelta = Vector2.zero;

        // Создаем фон для панели
        var background = new GameObject("Background");
        background.transform.SetParent(colorPanel.transform, false);
        var bgRect = background.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        var bgImage = background.AddComponent<UnityEngine.UI.Image>();
        bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.7f);

        // Создаем заголовок
        var titleObj = new GameObject("Title");
        titleObj.transform.SetParent(colorPanel.transform, false);
        var titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 0.7f);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.sizeDelta = Vector2.zero;
        var titleText = titleObj.AddComponent<UnityEngine.UI.Text>();
        titleText.text = "Выберите цвет";
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 14;
        titleText.color = Color.white;
        titleText.alignment = TextAnchor.MiddleCenter;

        // Создаем контейнер для кнопок
        var buttonsContainer = new GameObject("Buttons Container");
        buttonsContainer.transform.SetParent(colorPanel.transform, false);
        var containerRect = buttonsContainer.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0, 0);
        containerRect.anchorMax = new Vector2(1, 0.7f);
        containerRect.sizeDelta = Vector2.zero;

        // Добавляем кнопки выбора цвета
        AddColorButton(buttonsContainer, "Red", Color.red, 0.1f);
        AddColorButton(buttonsContainer, "Green", new Color(0, 0.7f, 0), 0.3f);
        AddColorButton(buttonsContainer, "Blue", new Color(0, 0.4f, 0.9f), 0.5f);
        AddColorButton(buttonsContainer, "Yellow", Color.yellow, 0.7f);
        AddColorButton(buttonsContainer, "White", Color.white, 0.9f);
    }

    private static void AddColorButton(GameObject parent, string name, Color color, float xPosition)
    {
        var buttonObj = new GameObject(name + " Button");
        buttonObj.transform.SetParent(parent.transform, false);

        // Настройка положения кнопки
        var buttonRect = buttonObj.AddComponent<RectTransform>();
        float buttonWidth = 0.1f;
        buttonRect.anchorMin = new Vector2(xPosition - buttonWidth / 2, 0.1f);
        buttonRect.anchorMax = new Vector2(xPosition + buttonWidth / 2, 0.9f);
        buttonRect.anchoredPosition = Vector2.zero;

        // Добавляем компоненты кнопки
        var buttonImage = buttonObj.AddComponent<UnityEngine.UI.Image>();
        buttonImage.color = color;

        var button = buttonObj.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = buttonImage;

        // Настройка цветов для выделения и нажатия
        var colors = button.colors;
        colors.highlightedColor = new Color(color.r, color.g, color.b, 0.8f);
        colors.pressedColor = new Color(color.r, color.g, color.b, 0.6f);
        button.colors = colors;

        // Добавляем обработчик события нажатия (будет назначен в runtime)
    }
}
#endif