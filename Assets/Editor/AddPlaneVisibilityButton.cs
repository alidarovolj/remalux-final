using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// Редактор для добавления кнопки видимости плоскостей
/// </summary>
public class AddPlaneVisibilityButton : EditorWindow
{
    // Функция вызывается из SceneSetup и не требует отдельного пункта меню
    public static void AddPlaneToggleButton()
    {
        // Ищем Canvas
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Ошибка", "Canvas не найден в сцене. Сначала создайте Canvas.", "OK");
            return;
        }
        
        // Ищем ARPlaneManager
        ARPlaneManager planeManager = Object.FindObjectOfType<ARPlaneManager>();
        if (planeManager == null)
        {
            EditorUtility.DisplayDialog("Ошибка", "ARPlaneManager не найден в сцене.", "OK");
            return;
        }
        
        // Проверяем, существует ли уже кнопка
        TogglePlanesVisibility existingToggle = Object.FindObjectOfType<TogglePlanesVisibility>();
        if (existingToggle != null)
        {
            EditorUtility.DisplayDialog("Информация", "Кнопка переключения видимости плоскостей уже существует в сцене.", "OK");
            return;
        }
        
        // Создаем кнопку
        GameObject buttonObj = new GameObject("PlanesVisibilityButton");
        RectTransform rectTransform = buttonObj.AddComponent<RectTransform>();
        buttonObj.transform.SetParent(canvas.transform, false);
        
        // Настраиваем положение
        rectTransform.anchorMin = new Vector2(0, 0);
        rectTransform.anchorMax = new Vector2(0, 0);
        rectTransform.pivot = new Vector2(0, 0);
        rectTransform.anchoredPosition = new Vector2(10, 10);
        rectTransform.sizeDelta = new Vector2(200, 50);
        
        // Добавляем компоненты
        Image image = buttonObj.AddComponent<Image>();
        image.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        
        Button button = buttonObj.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);
        colors.pressedColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
        button.colors = colors;
        
        // Добавляем текст
        GameObject textObj = new GameObject("Text");
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textObj.transform.SetParent(buttonObj.transform, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        
        Text text = textObj.AddComponent<Text>();
        text.text = "Показать плоскости";
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = 18;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        
        // Добавляем компонент TogglePlanesVisibility
        TogglePlanesVisibility toggle = buttonObj.AddComponent<TogglePlanesVisibility>();
        toggle.enabled = true;
        
        // Устанавливаем ссылки
        SerializedObject serializedObject = new SerializedObject(toggle);
        serializedObject.FindProperty("planeManager").objectReferenceValue = planeManager;
        serializedObject.FindProperty("toggleButton").objectReferenceValue = button;
        serializedObject.FindProperty("buttonText").objectReferenceValue = text;
        serializedObject.ApplyModifiedProperties();
        
        // Отмечаем сцену как измененную
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        
        Debug.Log("Кнопка переключения видимости плоскостей добавлена");
    }
} 