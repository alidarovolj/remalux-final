using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;

/// <summary>
/// Инструмент для обновления проекта до AR Foundation 6.x
/// </summary>
public class UpdateProjectToARFoundation6 : EditorWindow
{
    private bool updateScripts = true;
    private bool updateScenes = true;
    private bool updatePrefabs = true;
    private bool makeBackup = true;
    
    private Vector2 scrollPosition;
    private List<string> logMessages = new List<string>();
    
    [MenuItem("Tools/AR/Update Project To AR Foundation 6.x", false, 60)]
    public static void ShowWindow()
    {
        EditorWindow.GetWindow(typeof(UpdateProjectToARFoundation6), false, "Update AR Foundation");
    }
    
    void OnGUI()
    {
        GUILayout.Label("AR Foundation 6.x Updater", EditorStyles.boldLabel);
        
        EditorGUILayout.HelpBox(
            "Этот инструмент поможет обновить проект до AR Foundation 6.x, заменив устаревшие API.\n\n" +
            "Будут обновлены:\n" +
            "- Ссылки на ARSessionOrigin → XROrigin\n" +
            "- Устаревший метод FindObjectOfType → FindAnyObjectByType\n" +
            "- planesChanged → trackablesChanged\n" +
            "- и другие устаревшие API", 
            MessageType.Info);
        
        EditorGUILayout.Space(10);
        
        updateScripts = EditorGUILayout.Toggle("Обновить C# скрипты", updateScripts);
        updateScenes = EditorGUILayout.Toggle("Обновить сцены", updateScenes);
        updatePrefabs = EditorGUILayout.Toggle("Обновить префабы", updatePrefabs);
        makeBackup = EditorGUILayout.Toggle("Создать резервную копию", makeBackup);
        
        EditorGUILayout.Space(10);
        
        if (GUILayout.Button("Обновить проект"))
        {
            UpdateProject();
        }
        
        EditorGUILayout.Space(10);
        
        // Отображение лога
        EditorGUILayout.LabelField("Лог:", EditorStyles.boldLabel);
        
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(300));
        
        foreach (var message in logMessages)
        {
            EditorGUILayout.HelpBox(message, MessageType.Info);
        }
        
        EditorGUILayout.EndScrollView();
    }
    
    private void UpdateProject()
    {
        logMessages.Clear();
        
        if (makeBackup)
        {
            CreateBackup();
        }
        
        LogMessage("Начало обновления проекта до AR Foundation 6.x...");
        
        if (updateScripts)
        {
            UpdateScripts();
        }
        
        if (updateScenes)
        {
            UpdateScenes();
        }
        
        if (updatePrefabs)
        {
            UpdatePrefabs();
        }
        
        AssetDatabase.Refresh();
        
        LogMessage("Обновление проекта завершено!");
    }
    
    private void UpdateScripts()
    {
        LogMessage("Обновление C# скриптов...");
        
        string[] scriptFiles = Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories);
        int updatedScriptCount = 0;
        
        foreach (string filePath in scriptFiles)
        {
            bool fileUpdated = false;
            string content = File.ReadAllText(filePath);
            string originalContent = content;
            
            // 1. Обновление using директив
            if (content.Contains("using UnityEngine.XR.ARFoundation;"))
            {
                if (!content.Contains("using Unity.XR.CoreUtils;"))
                {
                    content = content.Replace("using UnityEngine.XR.ARFoundation;", 
                        "using UnityEngine.XR.ARFoundation;\nusing Unity.XR.CoreUtils;");
                    fileUpdated = true;
                }
            }
            
            // 2. Замена ARSessionOrigin на XROrigin в объявлениях полей
            content = Regex.Replace(content, 
                @"(\[SerializeField\]\s*private\s*)ARSessionOrigin(\s+\w+\s*;)", 
                "$1XROrigin$2");
            
            content = Regex.Replace(content, 
                @"(private\s*)ARSessionOrigin(\s+\w+\s*;)", 
                "$1XROrigin$2");
            
            content = Regex.Replace(content, 
                @"(public\s*)ARSessionOrigin(\s+\w+\s*;)", 
                "$1XROrigin$2");
            
            // 3. Замена FindObjectOfType на FindAnyObjectByType
            content = Regex.Replace(content, 
                @"FindObjectOfType\s*<", 
                "UnityEngine.Object.FindAnyObjectByType<");
            
            // 4. Замена planesChanged на trackablesChanged
            content = content.Replace("planesChanged +=", "trackablesChanged +=");
            content = content.Replace("planesChanged -=", "trackablesChanged -=");
            
            // 5. Замена camera на Camera
            content = Regex.Replace(content, 
                @"(\.camera)\b(?!\s*=)", 
                ".Camera");
            
            // 6. Замена trackablesParent на TrackablesParent
            content = Regex.Replace(content, 
                @"(\.trackablesParent)\b", 
                ".TrackablesParent");
            
            // Проверка, были ли внесены изменения
            if (content != originalContent)
            {
                fileUpdated = true;
                File.WriteAllText(filePath, content);
                updatedScriptCount++;
                LogMessage($"Обновлен скрипт: {filePath}");
            }
            
            // Если файл был обновлен, пометим его как измененный
            if (fileUpdated)
            {
                AssetDatabase.ImportAsset(filePath);
            }
        }
        
        LogMessage($"Обновлено {updatedScriptCount} скриптов из {scriptFiles.Length}");
    }
    
    private void UpdateScenes()
    {
        LogMessage("Обновление сцен...");
        
        string[] sceneFiles = Directory.GetFiles("Assets", "*.unity", SearchOption.AllDirectories);
        LogMessage($"Найдено {sceneFiles.Length} сцен");
        
        // Сцены обновляются через компонент ARFixReferences, который более надежен
        if (sceneFiles.Length > 0)
        {
            // Вызываем ARFixReferences для автоматического обновления сцены
            ARFixReferences.FixReferences();
            LogMessage("Сцены обновлены через ARFixReferences");
        }
    }
    
    private void UpdatePrefabs()
    {
        LogMessage("Обновление префабов...");
        
        string[] prefabFiles = Directory.GetFiles("Assets", "*.prefab", SearchOption.AllDirectories);
        LogMessage($"Найдено {prefabFiles.Length} префабов");
        
        // Префабы также обновляются через компонент DummyModel
        if (prefabFiles.Length > 0)
        {
            // Вызываем DummyModel.FixPaintedWallPrefab для обновления префаба стены
            DummyModel.FixPaintedWallPrefab();
            LogMessage("Основные префабы обновлены");
        }
    }
    
    private void CreateBackup()
    {
        string backupFolderName = "Backup_ARFoundation_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string backupPath = Path.Combine(Application.dataPath, "..", backupFolderName);
        
        LogMessage($"Создание резервной копии в папке: {backupFolderName}");
        
        try
        {
            // Копируем только важные папки
            CopyDirectory(Path.Combine(Application.dataPath, "Scripts"), Path.Combine(backupPath, "Assets", "Scripts"));
            CopyDirectory(Path.Combine(Application.dataPath, "Scenes"), Path.Combine(backupPath, "Assets", "Scenes"));
            CopyDirectory(Path.Combine(Application.dataPath, "Prefabs"), Path.Combine(backupPath, "Assets", "Prefabs"));
            
            LogMessage("Резервная копия создана успешно");
        }
        catch (System.Exception e)
        {
            LogMessage($"Ошибка при создании резервной копии: {e.Message}");
        }
    }
    
    private void CopyDirectory(string sourcePath, string targetPath)
    {
        // Создаем директорию, если ее нет
        if (!Directory.Exists(targetPath))
        {
            Directory.CreateDirectory(targetPath);
        }
        
        // Копируем все файлы
        foreach (string filePath in Directory.GetFiles(sourcePath))
        {
            string fileName = Path.GetFileName(filePath);
            string destFile = Path.Combine(targetPath, fileName);
            File.Copy(filePath, destFile, true);
        }
        
        // Копируем все поддиректории
        foreach (string dirPath in Directory.GetDirectories(sourcePath))
        {
            string dirName = Path.GetFileName(dirPath);
            string destDir = Path.Combine(targetPath, dirName);
            CopyDirectory(dirPath, destDir);
        }
    }
    
    private void LogMessage(string message)
    {
        logMessages.Add(message);
        Debug.Log(message);
        Repaint();
    }
} 