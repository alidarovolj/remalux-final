using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Редактор для исправления отсутствующих скриптов в сцене
/// </summary>
public class FixMissingScripts : EditorWindow
{
    // Функция теперь вызывается из SceneSetup
    public static void FixMissingScriptsInScene()
    {
        // Ищем объект RuntimeSetupManager
        var runtimeSetupManager = GameObject.Find("RuntimeSetupManager");
        
        if (runtimeSetupManager != null)
        {
            // Проверяем, есть ли отсутствующие скрипты
            var components = runtimeSetupManager.GetComponents<Component>();
            bool hasMissingScripts = false;
            
            foreach (var component in components)
            {
                if (component == null)
                {
                    hasMissingScripts = true;
                    break;
                }
            }
            
            if (hasMissingScripts)
            {
                // Создаем временный объект
                GameObject tempObj = new GameObject("TempRuntimeSetupManager");
                
                // Добавляем компонент RuntimeSetupManager
                var manager = tempObj.AddComponent<RuntimeSetupManager>();
                
                // Копируем настройки
                var originalManager = runtimeSetupManager.GetComponent<RuntimeSetupManager>();
                if (originalManager != null)
                {
                    // Использовать SerializedObject для копирования свойств
                    var originalSO = new SerializedObject(originalManager);
                    var newSO = new SerializedObject(manager);
                    
                    SerializedProperty prop = originalSO.GetIterator();
                    while (prop.NextVisible(true))
                    {
                        newSO.CopyFromSerializedProperty(prop);
                    }
                    
                    newSO.ApplyModifiedProperties();
                }
                
                // Удаляем старый объект и переименовываем новый
                GameObject.DestroyImmediate(runtimeSetupManager);
                tempObj.name = "RuntimeSetupManager";
                
                // Отмечаем сцену как измененную
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                
                Debug.Log("Объект RuntimeSetupManager пересоздан без отсутствующих скриптов");
            }
            else
            {
                Debug.Log("Отсутствующих скриптов на объекте RuntimeSetupManager не обнаружено");
            }
        }
        else
        {
            Debug.Log("Объект RuntimeSetupManager не найден в текущей сцене");
        }
    }
} 