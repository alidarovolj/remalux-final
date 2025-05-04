using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Класс для настройки OpenCV в проекте
/// </summary>
public class OpenCVSetup : Editor
{
    // Пути к директориям OpenCV плагинов, включая актуальный путь
    private const string OpenCVPluginsPath = "Assets/Plugins/OpenCVForUnity";
    private const string OpenCVDirectPath = "Assets/OpenCVForUnity"; // Добавлен актуальный путь
    
    // Символ препроцессора для включения OpenCV
    private const string OpenCVDefineSymbol = "OPENCV_ENABLED";
    
    [MenuItem("Tools/AR Wall Painting/Setup OpenCV")]
    public static void SetupOpenCV()
    {
        // Проверяем наличие директории с плагинами OpenCV
        bool openCVExists = Directory.Exists(OpenCVPluginsPath) || 
                           Directory.Exists(OpenCVDirectPath) ||  // Добавлен актуальный путь
                           Directory.Exists("Assets/Plugins/OpenCvSharp") ||
                           Directory.Exists("Assets/OpenCV+Unity");
        
        // Получаем текущие символы препроцессора для всех платформ
        string defineSymbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(
            EditorUserBuildSettings.selectedBuildTargetGroup);
        
        // Проверяем, содержит ли уже символ OpenCV
        bool hasOpenCVSymbol = defineSymbols.Contains(OpenCVDefineSymbol);
        
        if (openCVExists && !hasOpenCVSymbol)
        {
            // Добавляем символ OpenCV
            if (defineSymbols.Length > 0 && !defineSymbols.EndsWith(";"))
                defineSymbols += ";";
                
            defineSymbols += OpenCVDefineSymbol;
            
            // Устанавливаем обновленные символы
            PlayerSettings.SetScriptingDefineSymbolsForGroup(
                EditorUserBuildSettings.selectedBuildTargetGroup, defineSymbols);
                
            Debug.Log("OpenCV успешно включен! Добавлен символ " + OpenCVDefineSymbol);
        }
        else if (!openCVExists && hasOpenCVSymbol)
        {
            // Удаляем символ OpenCV, так как плагинов нет
            defineSymbols = defineSymbols.Replace(OpenCVDefineSymbol, "");
            defineSymbols = defineSymbols.Replace(";;", ";");
            
            // Устанавливаем обновленные символы
            PlayerSettings.SetScriptingDefineSymbolsForGroup(
                EditorUserBuildSettings.selectedBuildTargetGroup, defineSymbols);
                
            Debug.LogWarning("OpenCV плагины не найдены! Символ " + OpenCVDefineSymbol + " удален.");
            
            // Показываем диалоговое окно с инструкциями
            if (EditorUtility.DisplayDialog("OpenCV не найден", 
                "Не удалось найти плагины OpenCV. Хотите установить OpenCV из Asset Store?", 
                "Открыть Asset Store", "Отмена"))
            {
                // Открываем Asset Store для поиска OpenCV плагинов
                Application.OpenURL("https://assetstore.unity.com/packages/tools/integration/opencv-for-unity-21088");
            }
        }
        else if (openCVExists && hasOpenCVSymbol)
        {
            Debug.Log("OpenCV уже настроен и включен!");
            
            // Выводим информацию о найденном пути к OpenCV
            if (Directory.Exists(OpenCVDirectPath))
                Debug.Log("OpenCV найден в директории: " + OpenCVDirectPath);
            else if (Directory.Exists(OpenCVPluginsPath))
                Debug.Log("OpenCV найден в директории: " + OpenCVPluginsPath);
        }
        else
        {
            // Показываем диалоговое окно с инструкциями
            if (EditorUtility.DisplayDialog("OpenCV не найден", 
                "Не удалось найти плагины OpenCV. Хотите установить OpenCV из Asset Store?", 
                "Открыть Asset Store", "Отмена"))
            {
                // Открываем Asset Store для поиска OpenCV плагинов
                Application.OpenURL("https://assetstore.unity.com/packages/tools/integration/opencv-for-unity-21088");
            }
        }
    }
    
    // Автоматически проверяем OpenCV при загрузке проекта
    [InitializeOnLoadMethod]
    static void CheckOpenCVOnLoad()
    {
        // Проверяем наличие директории с плагинами OpenCV
        bool openCVExists = Directory.Exists(OpenCVPluginsPath) || 
                           Directory.Exists(OpenCVDirectPath) ||  // Добавлен актуальный путь
                           Directory.Exists("Assets/Plugins/OpenCvSharp") ||
                           Directory.Exists("Assets/OpenCV+Unity");
        
        // Получаем текущие символы препроцессора для текущей платформы
        string defineSymbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(
            EditorUserBuildSettings.selectedBuildTargetGroup);
        
        // Проверяем, содержит ли уже символ OpenCV
        bool hasOpenCVSymbol = defineSymbols.Contains(OpenCVDefineSymbol);
        
        if (openCVExists && !hasOpenCVSymbol)
        {
            Debug.Log("Обнаружены плагины OpenCV, но символ " + OpenCVDefineSymbol + " не задан. " +
                      "Используйте меню Tools/AR Wall Painting/Setup OpenCV для настройки.");
        }
        else if (!openCVExists && hasOpenCVSymbol)
        {
            Debug.LogWarning("Символ " + OpenCVDefineSymbol + " задан, но OpenCV плагины не найдены. " +
                             "Это может привести к ошибкам компиляции.");
        }
    }
} 