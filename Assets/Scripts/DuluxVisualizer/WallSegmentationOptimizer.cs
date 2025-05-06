using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Компонент для оптимизации работы WallSegmentation с временной интерполяцией
/// </summary>
[RequireComponent(typeof(WallSegmentation))]
public class WallSegmentationOptimizer : MonoBehaviour
{
      [Header("Optimization Settings")]
      [SerializeField, Range(0.1f, 2.0f)] private float processingInterval = 0.5f; // Интервал между обработками кадров
      [SerializeField, Range(0, 1)] private float temporalSmoothing = 0.6f; // Сглаживание временных изменений (0 - нет сглаживания, 1 - полное сглаживание)
      [SerializeField] private int bufferSize = 3; // Размер буфера для временного сглаживания

      [Header("Performance Monitoring")]
      [SerializeField] private bool showPerformanceStats = false; // Показывать статистику производительности
      [SerializeField] private bool adaptiveInterval = true; // Адаптивно настраивать интервал обработки

      [Header("UI Integration")]
      [SerializeField] private bool updateUIComponents = true; // Обновлять UI компоненты с результатом сегментации

      // Приватные переменные
      private WallSegmentation wallSegmentation;
      private float lastProcessTime = 0f;
      private Queue<Texture2D> textureBuffer = new Queue<Texture2D>();
      private RenderTexture temporalBlendTexture;
      private bool isInitialized = false;
      private float avgProcessingTime = 0f;
      private int frameCounter = 0;

      // Статистика производительности
      private float minProcessingTime = float.MaxValue;
      private float maxProcessingTime = 0f;
      private float totalProcessingTime = 0f;

      // Start вызывается перед первым кадром
      void Start()
      {
            // Получаем ссылку на компонент WallSegmentation
            wallSegmentation = GetComponent<WallSegmentation>();

            if (wallSegmentation == null)
            {
                  Debug.LogError("WallSegmentationOptimizer: Не найден компонент WallSegmentation");
                  enabled = false;
                  return;
            }

            // Получаем доступ к приватному полю processingInterval через рефлексию
            var intervalField = typeof(WallSegmentation).GetField("processingInterval",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (intervalField != null)
            {
                  // Устанавливаем интервал обработки в WallSegmentation
                  intervalField.SetValue(wallSegmentation, processingInterval);
            }
            else
            {
                  Debug.LogWarning("WallSegmentationOptimizer: Не удалось найти поле processingInterval в WallSegmentation");
            }

            // Инициализируем буфер для временной интерполяции
            InitializeBuffer();

            // Запускаем корутину для мониторинга производительности
            if (showPerformanceStats)
            {
                  StartCoroutine(MonitorPerformance());
            }
      }

      // Update вызывается каждый кадр
      void Update()
      {
            // Если прошло достаточно времени с последней обработки, запускаем новую
            if (Time.time - lastProcessTime >= processingInterval)
            {
                  // Замеряем время начала обработки
                  float startTime = Time.realtimeSinceStartup;

                  // Обрабатываем новый кадр
                  ProcessFrame();

                  // Обновляем статистику производительности
                  float processingTime = Time.realtimeSinceStartup - startTime;
                  UpdatePerformanceStats(processingTime);

                  // Адаптивно настраиваем интервал обработки, если это включено
                  if (adaptiveInterval)
                  {
                        AdjustProcessingInterval(processingTime);
                  }

                  // Обновляем время последней обработки
                  lastProcessTime = Time.time;
            }
      }

      /// <summary>
      /// Обрабатывает текущий кадр и применяет временную интерполяцию
      /// </summary>
      private void ProcessFrame()
      {
            // Получаем текущую текстуру сегментации
            Texture2D currentSegmentation = wallSegmentation.GetSegmentationTexture();

            if (currentSegmentation == null)
            {
                  return;
            }

            // Если буфер не инициализирован, инициализируем его
            if (!isInitialized)
            {
                  InitializeBuffer();
            }

            // Применяем временную интерполяцию
            Texture2D blendedTexture = ApplyTemporalSmoothing(currentSegmentation);

            // Обновляем RenderTexture для отображения
            UpdateRenderTexture(blendedTexture);

            // Обновляем UI компоненты, если это включено
            if (updateUIComponents)
            {
                  UpdateUIComponents(blendedTexture);
            }
      }

      /// <summary>
      /// Инициализирует буфер для временной интерполяции
      /// </summary>
      private void InitializeBuffer()
      {
            // Получаем текущую текстуру сегментации
            Texture2D currentSegmentation = wallSegmentation.GetSegmentationTexture();

            if (currentSegmentation == null)
            {
                  return;
            }

            // Очищаем буфер
            textureBuffer.Clear();

            // Создаем копии текстуры для заполнения буфера
            for (int i = 0; i < bufferSize; i++)
            {
                  Texture2D copy = new Texture2D(currentSegmentation.width, currentSegmentation.height, TextureFormat.RGBA32, false);
                  copy.SetPixels(currentSegmentation.GetPixels());
                  copy.Apply();

                  textureBuffer.Enqueue(copy);
            }

            // Создаем RenderTexture для временной интерполяции
            if (temporalBlendTexture != null)
            {
                  temporalBlendTexture.Release();
            }

            temporalBlendTexture = new RenderTexture(currentSegmentation.width, currentSegmentation.height, 0, RenderTextureFormat.ARGB32);
            temporalBlendTexture.Create();

            // Устанавливаем флаг инициализации
            isInitialized = true;
      }

      /// <summary>
      /// Применяет временную интерполяцию к текстуре сегментации
      /// </summary>
      private Texture2D ApplyTemporalSmoothing(Texture2D currentTexture)
      {
            if (textureBuffer.Count == 0 || temporalSmoothing <= 0)
            {
                  return currentTexture;
            }

            // Удаляем самую старую текстуру из буфера
            if (textureBuffer.Count >= bufferSize)
            {
                  Texture2D oldTexture = textureBuffer.Dequeue();
                  Destroy(oldTexture);
            }

            // Создаем копию текущей текстуры и добавляем ее в буфер
            Texture2D textureCopy = new Texture2D(currentTexture.width, currentTexture.height, TextureFormat.RGBA32, false);
            textureCopy.SetPixels(currentTexture.GetPixels());
            textureCopy.Apply();

            textureBuffer.Enqueue(textureCopy);

            // Создаем результирующую текстуру с интерполяцией
            Texture2D result = new Texture2D(currentTexture.width, currentTexture.height, TextureFormat.RGBA32, false);
            Color[] resultPixels = new Color[currentTexture.width * currentTexture.height];

            // Инициализируем пиксели
            for (int i = 0; i < resultPixels.Length; i++)
            {
                  resultPixels[i] = Color.clear;
            }

            // Получаем все текстуры из буфера
            Texture2D[] textureArray = textureBuffer.ToArray();

            // Вычисляем вес для каждой текстуры в буфере
            float[] weights = new float[textureArray.Length];
            float totalWeight = 0;

            for (int i = 0; i < textureArray.Length; i++)
            {
                  // Более новые текстуры имеют больший вес
                  weights[i] = Mathf.Pow(1f - temporalSmoothing, textureArray.Length - i - 1);
                  totalWeight += weights[i];
            }

            // Нормализуем веса
            for (int i = 0; i < weights.Length; i++)
            {
                  weights[i] /= totalWeight;
            }

            // Смешиваем пиксели с учетом весов
            for (int i = 0; i < textureArray.Length; i++)
            {
                  Color[] pixels = textureArray[i].GetPixels();

                  for (int j = 0; j < pixels.Length; j++)
                  {
                        resultPixels[j] += pixels[j] * weights[i];
                  }
            }

            // Применяем результат
            result.SetPixels(resultPixels);
            result.Apply();

            return result;
      }

      /// <summary>
      /// Обновляет RenderTexture для отображения
      /// </summary>
      private void UpdateRenderTexture(Texture2D blendedTexture)
      {
            if (blendedTexture == null || temporalBlendTexture == null)
            {
                  return;
            }

            // Копируем текстуру в RenderTexture
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture.active = temporalBlendTexture;

            GL.Clear(true, true, Color.clear);
            Graphics.Blit(blendedTexture, temporalBlendTexture);

            RenderTexture.active = previousActive;

            // Устанавливаем RenderTexture в качестве выходной текстуры WallSegmentation
            wallSegmentation.outputRenderTexture = temporalBlendTexture;
      }

      /// <summary>
      /// Обновляет UI компоненты с результатом сегментации
      /// </summary>
      private void UpdateUIComponents(Texture2D blendedTexture)
      {
            // Находим все WallPaintBlit компоненты и обновляем их
            WallPaintBlit[] paintBlits = FindObjectsOfType<WallPaintBlit>();

            foreach (WallPaintBlit blit in paintBlits)
            {
                  if (blit != null && blit.maskTexture == null)
                  {
                        blit.maskTexture = temporalBlendTexture;
                  }
            }

            // Находим все RawImage компоненты, которые могут отображать результат сегментации
            UnityEngine.UI.RawImage[] rawImages = FindObjectsOfType<UnityEngine.UI.RawImage>();

            foreach (UnityEngine.UI.RawImage image in rawImages)
            {
                  // Проверяем имя объекта, чтобы определить, используется ли он для отображения сегментации
                  if (image != null && image.name.Contains("Debug") && image.texture == null)
                  {
                        image.texture = temporalBlendTexture;
                  }
            }
      }

      /// <summary>
      /// Мониторит производительность обработки кадров
      /// </summary>
      private IEnumerator MonitorPerformance()
      {
            while (true)
            {
                  // Выводим статистику каждые 5 секунд
                  yield return new WaitForSeconds(5f);

                  if (frameCounter > 0)
                  {
                        Debug.Log($"[WallSegmentationOptimizer] Performance: " +
                                  $"Avg: {avgProcessingTime * 1000:F2}ms, " +
                                  $"Min: {minProcessingTime * 1000:F2}ms, " +
                                  $"Max: {maxProcessingTime * 1000:F2}ms, " +
                                  $"FPS: {1f / processingInterval:F1}, " +
                                  $"Interval: {processingInterval:F2}s");
                  }

                  // Сбрасываем статистику
                  frameCounter = 0;
                  totalProcessingTime = 0f;
                  minProcessingTime = float.MaxValue;
                  maxProcessingTime = 0f;
            }
      }

      /// <summary>
      /// Обновляет статистику производительности
      /// </summary>
      private void UpdatePerformanceStats(float processingTime)
      {
            // Обновляем статистику
            totalProcessingTime += processingTime;
            frameCounter++;

            // Обновляем минимальное и максимальное время обработки
            minProcessingTime = Mathf.Min(minProcessingTime, processingTime);
            maxProcessingTime = Mathf.Max(maxProcessingTime, processingTime);

            // Обновляем среднее время обработки
            avgProcessingTime = totalProcessingTime / frameCounter;
      }

      /// <summary>
      /// Адаптивно настраивает интервал обработки
      /// </summary>
      private void AdjustProcessingInterval(float processingTime)
      {
            // Целевое время кадра: 30 мс (33 FPS)
            float targetFrameTime = 0.03f;

            // Если время обработки превышает целевое, увеличиваем интервал
            if (processingTime > targetFrameTime)
            {
                  // Увеличиваем интервал пропорционально превышению
                  float ratio = processingTime / targetFrameTime;
                  processingInterval = Mathf.Min(processingInterval * ratio, 2.0f);
            }
            // Если время обработки меньше целевого, уменьшаем интервал
            else if (processingTime < targetFrameTime * 0.5f && processingInterval > 0.1f)
            {
                  // Уменьшаем интервал пропорционально запасу
                  float ratio = targetFrameTime / processingTime;
                  processingInterval = Mathf.Max(processingInterval / ratio, 0.1f);
            }

            // Обновляем интервал обработки в WallSegmentation
            var intervalField = typeof(WallSegmentation).GetField("processingInterval",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (intervalField != null)
            {
                  intervalField.SetValue(wallSegmentation, processingInterval);
            }
      }

      // OnDestroy вызывается при уничтожении объекта
      void OnDestroy()
      {
            // Очищаем ресурсы
            foreach (Texture2D texture in textureBuffer)
            {
                  Destroy(texture);
            }

            textureBuffer.Clear();

            if (temporalBlendTexture != null)
            {
                  temporalBlendTexture.Release();
                  Destroy(temporalBlendTexture);
            }
      }
}