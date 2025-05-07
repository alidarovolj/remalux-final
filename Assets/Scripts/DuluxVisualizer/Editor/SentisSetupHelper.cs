using UnityEngine;
using UnityEditor;
using System.IO;
using System;

namespace DuluxVisualizer.Editor
{
      /// <summary>
      /// Вспомогательный класс для настройки проекта с использованием Unity Sentis
      /// </summary>
      public static class SentisSetupHelper
      {
            [MenuItem("DuluxVisualizer/Setup Sentis References")]
            public static void SetupSentisReferences()
            {
                  try
                  {
                        // Проверяем наличие пакета Sentis в проекте
                        string manifestPath = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");
                        string manifestContent = File.ReadAllText(manifestPath);

                        if (!manifestContent.Contains("com.unity.sentis"))
                        {
                              if (EditorUtility.DisplayDialog("Sentis Not Found",
                                  "Unity Sentis package is not installed in this project. Would you like to add it?",
                                  "Yes", "No"))
                              {
                                    AddSentisPackage();
                              }
                              else
                              {
                                    Debug.LogWarning("Sentis setup canceled. Package not installed.");
                                    return;
                              }
                        }

                        // Создаем asmdef файл, если его еще нет
                        string asmdefPath = Path.Combine(Application.dataPath, "Scripts", "DuluxVisualizer", "DuluxVisualizer.asmdef");
                        if (!File.Exists(asmdefPath))
                        {
                              CreateAsmdefFile(asmdefPath);
                              Debug.Log("Created Assembly Definition file at: " + asmdefPath);
                        }
                        else
                        {
                              UpdateAsmdefFile(asmdefPath);
                              Debug.Log("Updated Assembly Definition file at: " + asmdefPath);
                        }

                        // Обновляем состояние проекта
                        AssetDatabase.Refresh();

                        EditorUtility.DisplayDialog("Setup Complete",
                            "Sentis references have been set up successfully.\n\nPlease restart the editor for changes to take effect.",
                            "OK");
                  }
                  catch (Exception ex)
                  {
                        Debug.LogError("Error setting up Sentis references: " + ex.Message);
                        EditorUtility.DisplayDialog("Setup Failed",
                            "An error occurred while setting up Sentis references: " + ex.Message,
                            "OK");
                  }
            }

            private static void AddSentisPackage()
            {
                  // Добавляем пакет Sentis через Package Manager API
                  UnityEditor.PackageManager.Client.Add("com.unity.sentis");
                  Debug.Log("Added Unity Sentis package to the project");
            }

            private static void CreateAsmdefFile(string path)
            {
                  // Создаем директорию, если её нет
                  Directory.CreateDirectory(Path.GetDirectoryName(path));

                  // Базовое содержимое asmdef файла с зависимостью от Sentis
                  string asmdefContent = @"{
    ""name"": ""DuluxVisualizer"",
    ""rootNamespace"": """",
    ""references"": [
        ""Unity.Sentis"",
        ""Unity.XR.ARFoundation"",
        ""Unity.XR.CoreUtils""
    ],
    ""includePlatforms"": [],
    ""excludePlatforms"": [],
    ""allowUnsafeCode"": false,
    ""overrideReferences"": false,
    ""precompiledReferences"": [],
    ""autoReferenced"": true,
    ""defineConstraints"": [],
    ""versionDefines"": [],
    ""noEngineReferences"": false
}";

                  File.WriteAllText(path, asmdefContent);
            }

            private static void UpdateAsmdefFile(string path)
            {
                  // Считываем существующий файл
                  string content = File.ReadAllText(path);

                  // Проверяем наличие ссылки на Sentis
                  if (!content.Contains("Unity.Sentis"))
                  {
                        // Добавляем ссылку на Sentis
                        content = content.Replace("\"references\": [", "\"references\": [\n        \"Unity.Sentis\",");

                        // Записываем обновленный файл
                        File.WriteAllText(path, content);
                  }
            }
      }
}