using UnityEngine;
using System.Reflection;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Скрипт-обертка, решающий проблему отсутствия ссылки на RuntimeSetupUtility
/// </summary>
public class RuntimeSetupManager : MonoBehaviour
{
    [SerializeField] private Material wallPaintMaterial;
    [SerializeField] private GameObject wallPrefab;
    
    private RuntimeSetupUtility setupUtility;
    
    private void Awake()
    {
        // Сначала удаляем отсутствующие (Missing) скрипты, если они есть
        RemoveMissingScripts(gameObject);
        
        // Создаем компонент RuntimeSetupUtility
        setupUtility = gameObject.AddComponent<RuntimeSetupUtility>();
        
        // Используем reflection для передачи параметров
        if (setupUtility != null)
        {
            var wallPaintMaterialField = setupUtility.GetType().GetField("wallPaintMaterial", 
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                
            var wallPrefabField = setupUtility.GetType().GetField("wallPrefab", 
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            
            if (wallPaintMaterialField != null)
                wallPaintMaterialField.SetValue(setupUtility, wallPaintMaterial);
                
            if (wallPrefabField != null)
                wallPrefabField.SetValue(setupUtility, wallPrefab);
                
            Debug.Log("RuntimeSetupManager успешно инициализирован");
        }
        else
        {
            Debug.LogError("Не удалось создать компонент RuntimeSetupUtility");
        }
    }
    
    /// <summary>
    /// Удаляет отсутствующие скрипты с объекта
    /// </summary>
    private void RemoveMissingScripts(GameObject gameObject)
    {
#if UNITY_EDITOR
        // Версия для редактора
        try
        {
            // Получаем все компоненты объекта
            var components = gameObject.GetComponents<Component>();
            
            int removedCount = 0;
            
            // Проверяем каждый компонент
            for (int i = 0; i < components.Length; i++)
            {
                // Если компонент null, то это Missing Script
                if (components[i] == null)
                {
                    // Удаляем его с помощью редактора
                    var serializedObject = new SerializedObject(gameObject);
                    var property = serializedObject.FindProperty("m_Component");
                    
                    if (property != null && i < property.arraySize)
                    {
                        property.DeleteArrayElementAtIndex(i);
                        serializedObject.ApplyModifiedProperties();
                        removedCount++;
                        Debug.Log("Удален отсутствующий скрипт с объекта: " + gameObject.name);
                    }
                }
            }
            
            if (removedCount > 0)
            {
                Debug.Log($"Удалено {removedCount} отсутствующих скриптов с объекта {gameObject.name}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Ошибка при удалении отсутствующих скриптов: {e.Message}");
        }
#else
        // Версия для билда приложения - более простая
        // Просто пропускаем этот шаг
        Debug.Log("Пропускаем удаление отсутствующих скриптов в билде приложения");
#endif
    }
} 