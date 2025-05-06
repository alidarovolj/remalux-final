using UnityEngine;
using System.IO;

/// <summary>
/// Набор утилитарных функций для работы с текстурами
/// </summary>
public static class TextureUtils
{
      /// <summary>
      /// Создает RenderTexture из Texture2D
      /// </summary>
      /// <param name="source">Исходная текстура</param>
      /// <param name="format">Формат RenderTexture</param>
      /// <returns>Созданный RenderTexture</returns>
      public static RenderTexture CreateRenderTexture(Texture2D source, RenderTextureFormat format = RenderTextureFormat.ARGB32)
      {
            RenderTexture rt = new RenderTexture(source.width, source.height, 0, format);
            rt.Create();
            Graphics.Blit(source, rt);
            return rt;
      }

      /// <summary>
      /// Конвертирует RenderTexture в Texture2D
      /// </summary>
      /// <param name="rt">Исходный RenderTexture</param>
      /// <returns>Texture2D с копией данных</returns>
      public static Texture2D RenderTextureToTexture2D(RenderTexture rt)
      {
            Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            return tex;
      }

      /// <summary>
      /// Сохраняет Texture2D в PNG файл
      /// </summary>
      /// <param name="texture">Текстура для сохранения</param>
      /// <param name="filename">Имя файла</param>
      /// <returns>Полный путь к сохраненному файлу</returns>
      public static string SaveTextureToPNG(Texture2D texture, string filename)
      {
            byte[] bytes = texture.EncodeToPNG();

            // Определяем каталог для сохранения
            string directory = Application.persistentDataPath + "/Screenshots";
            if (!Directory.Exists(directory))
            {
                  Directory.CreateDirectory(directory);
            }

            string filePath = Path.Combine(directory, filename);
            File.WriteAllBytes(filePath, bytes);
            Debug.Log($"Сохранено изображение: {filePath}");

            return filePath;
      }

      /// <summary>
      /// Загружает Texture2D из файла
      /// </summary>
      /// <param name="filePath">Путь к файлу</param>
      /// <returns>Загруженная текстура или null при ошибке</returns>
      public static Texture2D LoadTextureFromFile(string filePath)
      {
            if (!File.Exists(filePath))
            {
                  Debug.LogError($"Файл не существует: {filePath}");
                  return null;
            }

            byte[] fileData = File.ReadAllBytes(filePath);
            Texture2D texture = new Texture2D(2, 2); // Временные размеры, они будут изменены при загрузке

            if (texture.LoadImage(fileData))
            {
                  return texture;
            }
            else
            {
                  Debug.LogError($"Не удалось загрузить изображение из файла: {filePath}");
                  return null;
            }
      }

      /// <summary>
      /// Создает тестовую текстуру стены
      /// </summary>
      /// <param name="width">Ширина текстуры</param>
      /// <param name="height">Высота текстуры</param>
      /// <returns>Текстура с тестовым изображением стены</returns>
      public static Texture2D CreateTestWallTexture(int width, int height)
      {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);

            // Заполняем цветом фона
            Color backgroundColor = new Color(0.9f, 0.9f, 0.9f);
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                  pixels[i] = backgroundColor;
            }

            // Рисуем рамку
            Color borderColor = new Color(0.5f, 0.5f, 0.5f);
            int borderWidth = Mathf.Max(width / 50, 2);

            for (int y = 0; y < height; y++)
            {
                  for (int x = 0; x < width; x++)
                  {
                        if (x < borderWidth || x > width - borderWidth ||
                            y < borderWidth || y > height - borderWidth)
                        {
                              pixels[y * width + x] = borderColor;
                        }
                  }
            }

            // Рисуем "кирпичи" на стене
            Color brickColor = new Color(0.8f, 0.3f, 0.2f);
            Color jointColor = new Color(0.7f, 0.7f, 0.7f);

            int brickHeight = height / 15;
            int brickWidth = width / 8;
            int jointSize = Mathf.Max(2, brickHeight / 10);

            for (int row = 0; row < 15; row++)
            {
                  int offset = (row % 2 == 0) ? 0 : brickWidth / 2;

                  for (int col = 0; col < 9; col++)
                  {
                        int startX = offset + col * brickWidth;
                        int startY = row * brickHeight;
                        int endX = startX + brickWidth;
                        int endY = startY + brickHeight;

                        if (endX > width) endX = width;
                        if (endY > height) endY = height;

                        for (int y = startY; y < endY; y++)
                        {
                              for (int x = startX; x < endX; x++)
                              {
                                    if (x < width && y < height)
                                    {
                                          // Рисуем стыки между кирпичами
                                          if (x - startX < jointSize || endX - x < jointSize ||
                                              y - startY < jointSize || endY - y < jointSize)
                                          {
                                                pixels[y * width + x] = jointColor;
                                          }
                                          else
                                          {
                                                // Добавляем небольшую вариацию цвета для кирпичей
                                                float variation = Random.Range(-0.1f, 0.1f);
                                                Color variedColor = new Color(
                                                    brickColor.r + variation,
                                                    brickColor.g + variation,
                                                    brickColor.b + variation
                                                );
                                                pixels[y * width + x] = variedColor;
                                          }
                                    }
                              }
                        }
                  }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
      }
}