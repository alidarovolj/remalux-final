using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// Утилита для очистки проекта от ненужных скриптов и объектов
/// </summary>
public static class CleanUpUtility
{
    [MenuItem("Tools/AR/Clean Up Project")]
    public static void CleanUpProject()
    {
        bool proceed = EditorUtility.DisplayDialog(
            "Clean Up Project",
            "This will remove all utility scripts except for ARSceneCreator.\n\n" +
            "The following files will be removed:\n" +
            "- ProjectUpdater.cs\n" +
            "- UpdateARReferences.cs\n" +
            "- UpdatedARHelpers.cs\n" +
            "- SetupUtility.cs\n\n" +
            "This operation is irreversible. Do you want to continue?",
            "Clean Up Project",
            "Cancel"
        );
        
        if (!proceed) return;
        
        List<string> filesToRemove = new List<string>();
        List<string> removedFiles = new List<string>();
        
        // Добавляем файлы, которые нужно удалить
        filesToRemove.Add("Assets/Scripts/ProjectUpdater.cs");
        filesToRemove.Add("Assets/Scripts/UpdateARReferences.cs");
        filesToRemove.Add("Assets/Scripts/UpdatedARHelpers.cs");
        filesToRemove.Add("Assets/Scripts/SetupUtility.cs");
        
        // Удаляем файлы, если они существуют
        foreach (string filePath in filesToRemove)
        {
            if (File.Exists(filePath))
            {
                AssetDatabase.DeleteAsset(filePath);
                removedFiles.Add(filePath);
                Debug.Log("Removed: " + filePath);
            }
        }
        
        // Обновляем базу данных ассетов
        AssetDatabase.Refresh();
        
        EditorUtility.DisplayDialog(
            "Clean Up Complete",
            $"Removed {removedFiles.Count} utility files.\n\n" +
            "Your project has been cleaned up and now only contains the essential ARSceneCreator.",
            "OK"
        );
    }
} 