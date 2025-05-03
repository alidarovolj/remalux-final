using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;

/// <summary>
/// Класс для управления сохранением и загрузкой снимков покраски между сессиями
/// </summary>
public class SnapshotManager : MonoBehaviour
{
    [SerializeField] public WallPainter wallPainter;
    
    // Путь к папке для сохранения снимков
    private string SaveFolderPath => Path.Combine(Application.persistentDataPath, "Snapshots");
    
    // Класс-обертка для сериализации списка снимков
    [Serializable]
    private class SnapshotList
    {
        public List<PaintSnapshot> snapshots = new List<PaintSnapshot>();
    }
    
    private void Start()
    {
        if (wallPainter == null)
            wallPainter = UnityEngine.Object.FindAnyObjectByType<WallPainter>();
            
        // Создаем директорию для сохранений, если ее нет
        if (!Directory.Exists(SaveFolderPath))
        {
            Directory.CreateDirectory(SaveFolderPath);
        }
    }
    
    /// <summary>
    /// Сохранить все снимки в файл
    /// </summary>
    public void SaveSnapshots()
    {
        try
        {
            SnapshotList snapshotList = new SnapshotList();
            snapshotList.snapshots = wallPainter.GetSnapshots();
            
            string json = JsonUtility.ToJson(snapshotList, true);
            string filePath = Path.Combine(SaveFolderPath, "snapshots.json");
            
            File.WriteAllText(filePath, json);
            Debug.Log("Снимки успешно сохранены: " + filePath);
        }
        catch (Exception e)
        {
            Debug.LogError("Ошибка при сохранении снимков: " + e.Message);
        }
    }
    
    /// <summary>
    /// Загрузить снимки из файла
    /// </summary>
    public void LoadSnapshots()
    {
        try
        {
            string filePath = Path.Combine(SaveFolderPath, "snapshots.json");
            
            if (File.Exists(filePath))
            {
                string json = File.ReadAllText(filePath);
                SnapshotList snapshotList = JsonUtility.FromJson<SnapshotList>(json);
                
                // TODO: Здесь нужна логика для восстановления снимков в WallPainter
                // Это потребует дополнительной реализации для применения сохраненных снимков
                
                Debug.Log("Снимки успешно загружены: " + filePath);
            }
            else
            {
                Debug.Log("Файл сохранений не найден: " + filePath);
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Ошибка при загрузке снимков: " + e.Message);
        }
    }
    
    /// <summary>
    /// Экспорт снимка в изображение
    /// </summary>
    public void ExportSnapshotAsImage(PaintSnapshot snapshot)
    {
        // TODO: Реализовать экспорт снимка в изображение
        // Это может включать в себя:
        // 1. Рендер сцены с текущей покраской в RenderTexture
        // 2. Сохранение текстуры как PNG файла
    }
    
    /// <summary>
    /// Захват скриншота и сохранение в файл
    /// </summary>
    private IEnumerator CaptureScreenshot(string filePath)
    {
        // Ждем конец кадра для корректного рендеринга
        yield return new WaitForEndOfFrame();
        
        // Создаем текстуру размером с экран
        Texture2D screenshot = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        
        // Считываем пиксели с экрана
        screenshot.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
        screenshot.Apply();
        
        // Сохраняем в файл
        byte[] bytes = screenshot.EncodeToPNG();
        File.WriteAllBytes(filePath, bytes);
        
        // Освобождаем ресурсы
        Destroy(screenshot);
    }
    
    /// <summary>
    /// Автоматическое сохранение при выходе из приложения
    /// </summary>
    private void OnApplicationPause(bool pause)
    {
        if (pause)
        {
            SaveSnapshots();
        }
    }
    
    /// <summary>
    /// Автоматическое сохранение при закрытии приложения
    /// </summary>
    private void OnApplicationQuit()
    {
        SaveSnapshots();
    }
} 