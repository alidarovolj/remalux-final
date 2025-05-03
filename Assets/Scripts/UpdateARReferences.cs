using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine.XR.ARFoundation;

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

/// <summary>
/// Утилита для замены устаревших ссылок в скриптах
/// </summary>
public static class UpdateARReferences
{
    [MenuItem("Tools/AR/Fix Script References")]
    public static void FixScriptReferences()
    {
        bool proceed = EditorUtility.DisplayDialog(
            "Fix Script References",
            "This will modify all C# scripts in the Assets folder to update references:\n\n" +
            "• ARSessionOrigin → XROrigin\n" +
            "• FindObjectOfType → FindAnyObjectByType\n" +
            "• planesChanged → trackablesChanged\n\n" +
            "Make sure you have a backup of your project before proceeding!",
            "Update Scripts",
            "Cancel"
        );
        
        if (!proceed) return;
        
        try
        {
            EditorUtility.DisplayProgressBar("Updating Scripts", "Finding C# scripts...", 0.1f);
            
            // Получаем все CS файлы в Assets
            string[] scriptPaths = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories);
            int totalScripts = scriptPaths.Length;
            int processedScripts = 0;
            int modifiedScripts = 0;
            
            foreach (var scriptPath in scriptPaths)
            {
                processedScripts++;
                float progress = (float)processedScripts / totalScripts;
                EditorUtility.DisplayProgressBar("Updating Scripts", 
                    $"Processing {Path.GetFileName(scriptPath)} ({processedScripts}/{totalScripts})", 
                    progress);
                
                // Загружаем содержимое файла
                string content = File.ReadAllText(scriptPath);
                string originalContent = content;
                
                // Добавляем using для XROrigin
                if (content.Contains("ARSessionOrigin") && !content.Contains("using Unity.XR.CoreUtils;"))
                {
                    content = AddUsingDirective(content, "using Unity.XR.CoreUtils;");
                }
                
                // Заменяем ARSessionOrigin на XROrigin
                content = ReplaceWithCaution(content, "ARSessionOrigin", "XROrigin");
                
                // Заменяем sessionOrigin.camera на sessionOrigin.Camera
                content = ReplacePropertyCasing(content, ".camera", ".Camera");
                
                // Заменяем sessionOrigin.trackablesParent на sessionOrigin.TrackablesParent
                content = ReplacePropertyCasing(content, ".trackablesParent", ".TrackablesParent");
                
                // Заменяем FindObjectOfType на FindAnyObjectByType
                content = Regex.Replace(content, @"FindObjectOfType\s*<", "FindAnyObjectByType<");
                
                // Заменяем planesChanged на trackablesChanged
                content = ReplacePropertyCasing(content, ".planesChanged", ".trackablesChanged");
                
                // Заменяем ARPlanesChangedEventArgs на ARTrackablesChangedEventArgs<ARPlane>
                content = content.Replace("ARPlanesChangedEventArgs", "ARTrackablesChangedEventArgs<ARPlane>");
                
                // Заменяем методы обработки событий
                content = content.Replace("PlaneManager_PlanesChanged", "PlaneManager_TrackedPlanesChanged");
                
                // Если были изменения, сохраняем файл
                if (content != originalContent)
                {
                    File.WriteAllText(scriptPath, content);
                    modifiedScripts++;
                    Debug.Log($"Updated references in: {scriptPath}");
                }
            }
            
            EditorUtility.ClearProgressBar();
            
            AssetDatabase.Refresh();
            
            EditorUtility.DisplayDialog(
                "Script Update Complete",
                $"Processed {totalScripts} scripts and updated {modifiedScripts} files.\n\n" +
                "Note: You may still need to manually fix some references.",
                "OK"
            );
        }
        catch (System.Exception ex)
        {
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog(
                "Update Error",
                "An error occurred during script update: " + ex.Message,
                "OK"
            );
            Debug.LogException(ex);
        }
    }
    
    // Добавляет using директиву после других using-ов
    private static string AddUsingDirective(string content, string directive)
    {
        // Проверяем не добавлена ли уже эта директива
        if (content.Contains(directive))
            return content;
        
        // Находим позицию последней директивы using или начало файла
        int lastUsingPos = -1;
        
        // Находим все строки с using
        MatchCollection matches = Regex.Matches(content, @"using\s+[^;]+;");
        if (matches.Count > 0)
        {
            // Берем последний using
            Match lastUsing = matches[matches.Count - 1];
            lastUsingPos = lastUsing.Index + lastUsing.Length;
        }
        
        if (lastUsingPos >= 0)
        {
            // Вставляем после последнего using
            return content.Substring(0, lastUsingPos) + 
                   "\n" + directive + 
                   content.Substring(lastUsingPos);
        }
        else
        {
            // Вставляем в начало файла
            return directive + "\n" + content;
        }
    }
    
    // Заменяет с проверкой, что это не часть другого имени
    private static string ReplaceWithCaution(string content, string oldText, string newText)
    {
        return Regex.Replace(content, 
            $@"\b{Regex.Escape(oldText)}\b", 
            newText);
    }
    
    // Заменяет свойство с сохранением рег. выражения
    private static string ReplacePropertyCasing(string content, string oldProperty, string newProperty)
    {
        // Заменяем с учетом возможных пробелов
        return Regex.Replace(content, 
            $@"{Regex.Escape(oldProperty)}\b", 
            newProperty);
    }
}
#endif 