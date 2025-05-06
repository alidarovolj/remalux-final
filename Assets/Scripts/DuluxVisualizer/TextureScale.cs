using UnityEngine;
using System.Threading;

/// <summary>
/// Утилитарный класс для масштабирования текстур с высоким качеством
/// </summary>
public static class TextureScale
{
      private static Color[] texColors;
      private static Color[] newColors;
      private static int w;
      private static float ratioX;
      private static float ratioY;
      private static int w2;
      private static int finishCount;
      private static Mutex mutex;

      /// <summary>
      /// Масштабирует текстуру используя билинейную интерполяцию
      /// </summary>
      /// <param name="tex">Исходная текстура</param>
      /// <param name="newWidth">Новая ширина</param>
      /// <param name="newHeight">Новая высота</param>
      public static void Bilinear(Texture2D tex, int newWidth, int newHeight)
      {
            if (tex == null || newWidth <= 0 || newHeight <= 0)
                  return;

            // Если новые размеры совпадают с текущими, выходим
            if (tex.width == newWidth && tex.height == newHeight)
                  return;

            mutex = new Mutex(false);
            w = tex.width;
            w2 = newWidth;

            ratioX = ((float)w - 1) / (newWidth - 1);
            ratioY = ((float)tex.height - 1) / (newHeight - 1);

            texColors = tex.GetPixels();
            newColors = new Color[newWidth * newHeight];

            int cores = Mathf.Min(SystemInfo.processorCount, 8);
            int slice = newHeight / cores;

            finishCount = 0;

            if (mutex == null)
                  mutex = new Mutex(false);

            // Если текстура слишком маленькая или устройство с низкой производительностью, 
            // используем однопоточную обработку
            if (slice < 10 || cores <= 1)
            {
                  BilinearScale(new int[] { 0, newHeight });
            }
            else
            {
                  for (int i = 0; i < cores; i++)
                  {
                        int start = i * slice;
                        int end = (i == cores - 1) ? newHeight : (i + 1) * slice;
                        ThreadPool.QueueUserWorkItem(BilinearScale, new int[] { start, end });
                  }

                  // Ждем завершения всех потоков
                  while (finishCount < cores)
                  {
                        Thread.Sleep(1);
                  }
            }

            mutex.WaitOne();
            finishCount = 0;
            mutex.ReleaseMutex();

            // Применяем новые цвета к текстуре
            tex.Reinitialize(newWidth, newHeight);
            tex.SetPixels(newColors);
            tex.Apply();
      }

      /// <summary>
      /// Масштабирует текстуру с помощью билинейной интерполяции
      /// </summary>
      private static void BilinearScale(object obj)
      {
            int[] range = (int[])obj;
            int start = range[0];
            int end = range[1];

            for (int y = start; y < end; y++)
            {
                  int yFloor = (int)Mathf.Floor(y * ratioY);
                  int y1 = yFloor * w;
                  int y2 = (yFloor + 1) * w;
                  float yLerp = y * ratioY - yFloor;

                  for (int x = 0; x < w2; x++)
                  {
                        int xFloor = (int)Mathf.Floor(x * ratioX);
                        float xLerp = x * ratioX - xFloor;

                        Color c1 = ColorLerpUnclamped(texColors[y1 + xFloor], texColors[y1 + xFloor + 1], xLerp);
                        Color c2 = ColorLerpUnclamped(texColors[y2 + xFloor], texColors[y2 + xFloor + 1], xLerp);

                        newColors[y * w2 + x] = ColorLerpUnclamped(c1, c2, yLerp);
                  }
            }

            mutex.WaitOne();
            finishCount++;
            mutex.ReleaseMutex();
      }

      /// <summary>
      /// Линейная интерполяция между двумя цветами без ограничения
      /// </summary>
      private static Color ColorLerpUnclamped(Color c1, Color c2, float value)
      {
            return new Color(
                c1.r + (c2.r - c1.r) * value,
                c1.g + (c2.g - c1.g) * value,
                c1.b + (c2.b - c1.b) * value,
                c1.a + (c2.a - c1.a) * value
            );
      }
}