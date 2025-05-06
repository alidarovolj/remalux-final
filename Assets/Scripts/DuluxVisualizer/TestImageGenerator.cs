using UnityEngine;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Класс для создания тестовых изображений стен в режиме редактора
/// </summary>
public static class TestImageGenerator
{
#if UNITY_EDITOR
    [MenuItem("Tools/AR Wall Painting/Generate Test Wall Image")]
    public static void GenerateTestWallImage()
    {
        // Создаем тестовое изображение
        Texture2D texture = TextureUtils.CreateTestWallTexture(1024, 768);

        // Создаем директорию Resources/Images, если она не существует
        if (!Directory.Exists("Assets/Resources"))
        {
            Directory.CreateDirectory("Assets/Resources");
        }
        
        if (!Directory.Exists("Assets/Resources/Images"))
        {
            Directory.CreateDirectory("Assets/Resources/Images");
        }
        
        // Сохраняем текстуру как PNG
        string path = "Assets/Resources/Images/TestWallImage.png";
        byte[] bytes = texture.EncodeToPNG();
        File.WriteAllBytes(path, bytes);
        
        // Обновляем базу ассетов Unity
        AssetDatabase.ImportAsset(path);
        
        // Настраиваем импорт текстуры
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.isReadable = true;
            
            // Применяем настройки
            importer.SaveAndReimport();
        }
        
        Debug.Log($"Тестовое изображение стены создано: {path}");
        
        // Выделяем файл в проекте
        Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(path);
    }
    
    [MenuItem("Tools/AR Wall Painting/Generate Test Wall Mask")]
    public static void GenerateTestWallMask()
    {
        // Создаем маску стены (белое пятно в центре)
        int width = 256;
        int height = 256;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        
        // Заполняем прозрачным цветом
        Color[] pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.clear;
        }
        
        // Рисуем белое пятно в центре (стена)
        float centerX = width / 2f;
        float centerY = height / 2f;
        float maxDistance = Mathf.Min(width, height) * 0.4f;
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float distance = Mathf.Sqrt((x - centerX) * (x - centerX) + (y - centerY) * (y - centerY));
                if (distance < maxDistance)
                {
                    float alpha = 1.0f - (distance / maxDistance);
                    pixels[y * width + x] = new Color(1, 1, 1, alpha);
                }
            }
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        
        // Создаем директорию Resources/Images, если она не существует
        if (!Directory.Exists("Assets/Resources"))
        {
            Directory.CreateDirectory("Assets/Resources");
        }
        
        if (!Directory.Exists("Assets/Resources/Images"))
        {
            Directory.CreateDirectory("Assets/Resources/Images");
        }
        
        // Сохраняем текстуру как PNG
        string path = "Assets/Resources/Images/TestWallMask.png";
        byte[] bytes = texture.EncodeToPNG();
        File.WriteAllBytes(path, bytes);
        
        // Обновляем базу ассетов Unity
        AssetDatabase.ImportAsset(path);
        
        // Настраиваем импорт текстуры
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.isReadable = true;
            
            // Применяем настройки
            importer.SaveAndReimport();
        }
        
        Debug.Log($"Тестовая маска стены создана: {path}");
        
        // Выделяем файл в проекте
        Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(path);
    }
#endif
}