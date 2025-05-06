using UnityEngine;
using UnityEditor;

#if UNITY_EDITOR
/// <summary>
/// Кнопка быстрого создания AR сцены в панели инструментов
/// </summary>
[InitializeOnLoad]
public static class ARSceneToolsButton
{
      static ARSceneToolsButton()
      {
            // Подписываемся на событие отрисовки панели инструментов
            ToolbarExtender.ToolbarGUI += OnToolbarGUI;
      }

      // Отрисовка кнопки на панели инструментов
      static void OnToolbarGUI()
      {
            GUILayout.FlexibleSpace();

            GUIStyle customButtonStyle = new GUIStyle(GUI.skin.button);
            customButtonStyle.normal.textColor = new Color(0.2f, 0.5f, 0.9f);
            customButtonStyle.fontStyle = FontStyle.Bold;

            if (GUILayout.Button(new GUIContent("Создать AR сцену", "Быстрое создание новой AR сцены для покраски стен"), customButtonStyle, GUILayout.Width(150)))
            {
                  ARSceneTools.CreateARWallPaintingScene();
            }
      }
}

/// <summary>
/// Расширение панели инструментов для добавления пользовательских элементов
/// </summary>
[InitializeOnLoad]
public static class ToolbarExtender
{
      public static event System.Action ToolbarGUI;

      static ToolbarExtender()
      {
            EditorApplication.update += OnUpdate;
      }

      static void OnUpdate()
      {
            // Отписываемся от события update, так как нам нужно запуститься только один раз
            EditorApplication.update -= OnUpdate;

            // Получаем тип панели инструментов через рефлексию
            var toolbarType = typeof(Editor).Assembly.GetType("UnityEditor.Toolbar");
            if (toolbarType == null)
                  return;

            // Используем рефлексию для получения доступа к отрисовке панели инструментов
            var guiMethod = toolbarType.GetMethod("OnGUI", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (guiMethod == null)
                  return;

            var repaintMethod = typeof(EditorApplication).GetMethod("RepaintToolbar", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (repaintMethod == null)
                  return;

            // Регистрируем наш обработчик для отрисовки дополнительных элементов
            EditorApplication.update += () => repaintMethod.Invoke(null, null);

            EditorApplication.delayCall += () =>
            {
                  // Замена метода OnGUI для внедрения своего кода
                  var toolbars = Resources.FindObjectsOfTypeAll(toolbarType);
                  if (toolbars.Length == 0)
                        return;

                  var toolbar = toolbars[0];
                  var guiDelegate = System.Delegate.CreateDelegate(typeof(EditorApplication.CallbackFunction), null, guiMethod);
                  guiDelegate = System.Delegate.Combine(guiDelegate, new EditorApplication.CallbackFunction(OnGUIHandler));

                  var fieldInfo = toolbarType.GetField("k_ToolbarGUI", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                  if (fieldInfo != null)
                        fieldInfo.SetValue(null, guiDelegate);
            };
      }

      static void OnGUIHandler()
      {
            if (ToolbarGUI != null)
            {
                  using (new GUILayout.HorizontalScope())
                  {
                        ToolbarGUI();
                  }
            }
      }
}
#endif