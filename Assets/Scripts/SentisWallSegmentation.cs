using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Sentis;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.UI;
using Unity.XR.CoreUtils;

/// <summary>
/// Компонент для сегментации стен с использованием нейросети (Unity Sentis)
/// </summary>
public class SentisWallSegmentation : MonoBehaviour
{
      // Режим работы сегментации стен
      public enum SegmentationMode
      {
            Demo,             // Простая демонстрация без нейросети
            EmbeddedModel,    // Используется модель, привязанная через инспектор
            ExternalModel     // Используется модель из StreamingAssets
      }

      [Header("AR Components")]
      [SerializeField] public ARCameraManager cameraManager;
      [SerializeField] private Camera arCamera;

      [Header("Segmentation Mode")]
      [SerializeField] private SegmentationMode currentMode = SegmentationMode.ExternalModel;
      [SerializeField] private string externalModelPath = "Models/model.onnx"; // Путь к ONNX модели

      [Header("Sentis Model")]
      [SerializeField] public ModelAsset modelAsset; // Модель ONNX через asset
      [SerializeField] public bool forceUseEmbeddedModel = true; // Принудительно использовать встроенную модель

      [Header("Segmentation Parameters")]
      [SerializeField] private int inputWidth = 128; // Размер ширины входного изображения для модели
      [SerializeField] private int inputHeight = 128; // Размер высоты входного изображения для модели
      [SerializeField] private int inputChannels = 3; // RGB входное изображение (3 канала)
      [SerializeField] private string inputName = "image"; // Имя входа модели
      [SerializeField] private string outputName = "predict"; // Имя выхода модели
      [SerializeField, Range(0, 1)] private float threshold = 0.3f; // Порог для бинаризации маски
      [SerializeField] private int wallClassIndex = 9; // Индекс класса стены в выходном тензоре

      [Header("Alternative Model Names")]
      [SerializeField] private string altInputName = "image"; // Альтернативное имя входа
      [SerializeField] private string altOutputName = "predict"; // Альтернативное имя выхода
      [SerializeField] private int altWallClassIndex = 9; // Альтернативный индекс класса стены

      [Header("Debug & Performance")]
      [SerializeField] private bool forceDemoMode = false; // Отключаем принудительный демо-режим
      [SerializeField] private bool showDebugVisualisation = true;
      [SerializeField] private RawImage debugImage;
      [SerializeField] private float processingInterval = 0.3f;
      [SerializeField] private bool enableDebugLogs = true; // Добавлен флаг включения отладочных логов
      [SerializeField] private bool debugPositioning = true; // Флаг для отладки позиционирования стен (включен по умолчанию)
      [SerializeField] private bool useARPlaneController = true; // Использовать ARPlaneController для управления плоскостями
      [SerializeField] private bool useSmoothInterpolation = true; // Сглаживание результатов между кадрами для устранения мерцания
      [SerializeField, Range(0f, 1f)] private float smoothingFactor = 0.2f; // Коэффициент сглаживания (0 - нет сглаживания, 1 - полное сглаживание)
      [SerializeField, Range(0, 3)] private int stabilizationBufferSize = 2; // Размер буфера стабилизации (0 - отключено)

      [Header("Wall Visualization")]
      [SerializeField] private Material wallMaterial; // Материал для стен
      [SerializeField] private Color wallColor = new Color(0.2f, 0.5f, 0.9f, 0.8f); // Цвет для визуализации стен
      [SerializeField] private RenderTexture _outputRenderTexture; // Выходная маска сегментации для внешнего использования

      // Property to access the output render texture
      public RenderTexture outputRenderTexture
      {
            get { return _outputRenderTexture; }
            set { _outputRenderTexture = value; }
      }

      // Приватные переменные
      private Texture2D cameraTexture;
      private Texture2D segmentationTexture;
      private Model runtimeModel;
      private IWorker worker;
      private bool isProcessing;
      private TensorFloat inputTensor;
      private bool useDemoMode = false; // Используем демо-режим при ошибке модели
      private int errorCount = 0; // Счетчик ошибок нейросети
      private ModelAsset currentModelAsset; // Текущая используемая модель
      private List<GameObject> currentWalls = new List<GameObject>(); // Список текущих стен
      private List<GameObject> demoWalls = new List<GameObject>(); // Список демо-стен
      private bool isModelInitialized = false; // Флаг инициализации модели
      private float lastProcessTime = 0f; // Время последней обработки
      private bool useNCHW = true; // Флаг формата тензора

      // Буфер для сглаживания результатов
      private List<Texture2D> resultBuffer = new List<Texture2D>();
      private int currentBufferIndex = 0;
      private bool bufferInitialized = false;

      // Инициализация
      private void Start()
      {
            // Определяем первоначальный режим
            if (currentMode == SegmentationMode.Demo)
            {
                  useDemoMode = true;
            }

            // Проверяем наличие ARCameraManager для получения изображения с камеры
            if (cameraManager == null)
            {
                  cameraManager = FindObjectOfType<ARCameraManager>();
            }

            if (cameraManager != null)
            {
                  cameraManager.frameReceived += OnCameraFrameReceived;
            }
            else
            {
                  Debug.LogError("AR Camera Manager не найден. Переключение в демо-режим.");
                  SwitchToDemoMode();
            }

            // Проверка начальных размеров параметров модели
            if (inputWidth <= 0 || inputHeight <= 0)
            {
                  Debug.LogError($"Некорректные размеры входных данных модели: {inputWidth}x{inputHeight}. Устанавливаем размеры по умолчанию (32x32) и переключаемся в демо-режим.");
                  inputWidth = 32;
                  inputHeight = 32;
                  inputChannels = 32;
                  SwitchToDemoMode();
            }

            // Предупреждение о большом размере входных данных
            if (inputWidth * inputHeight > 65536)
            {
                  Debug.LogWarning($"Размер входных данных ({inputWidth}x{inputHeight}={inputWidth * inputHeight} пикселей) может быть слишком большим для модели. Рекомендуемый максимум: 256x256=65536 пикселей.");
            }

            // Инициализируем текстуры с безопасными размерами по умолчанию
            int textureWidth = 256;
            int textureHeight = 256;

            // Проверяем и обновляем размеры текстуры на основе параметров модели
            if (inputWidth > 0 && inputHeight > 0)
            {
                  textureWidth = inputWidth;
                  textureHeight = inputHeight;
            }
            else
            {
                  Debug.LogWarning($"Некорректные размеры входа модели: {inputWidth}x{inputHeight}. Используем безопасные значения по умолчанию: {textureWidth}x{textureHeight}");
            }

            cameraTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
            segmentationTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);

            // Проверка наличия компонента для отображения отладки
            if (showDebugVisualisation && debugImage == null)
            {
                  Debug.LogWarning("Включена визуализация отладки (showDebugVisualisation), но не назначен компонент debugImage. Отладочный дисплей будет автоматически создан при необходимости.");
                  // Попробуем найти RawImage в сцене
                  debugImage = FindObjectOfType<RawImage>();
            }

            // Проверяем размер входных данных и предупреждаем если они слишком большие
            int totalPixels = inputWidth * inputHeight;
            if (totalPixels > 65536)
            {
                  Debug.LogWarning($"Размер входных данных ({inputWidth}x{inputHeight}={totalPixels}) может быть слишком большим для модели. " +
                                 "Рекомендуемый максимум: 256x256=65536 пикселей.");
            }

            // Загружаем нужную модель в зависимости от выбранного режима
            LoadSelectedModel();

            // Подписка на событие обновления текстуры камеры
            if (cameraManager != null)
            {
                  cameraManager.frameReceived += OnCameraFrameReceived;
            }

            // Инициализация обработки через интервал времени
            StartCoroutine(ProcessFrames());

            // Подписываемся на событие обнаружения новых плоскостей
            SubscribeToPlaneEvents();

            // Автоматический запуск сегментации при запуске приложения
            if (currentMode == SegmentationMode.ExternalModel && !forceDemoMode)
            {
                  Debug.Log("SentisWallSegmentation: Запуск автоматической сегментации в режиме ExternalModel...");

                  // Через небольшую задержку запускаем первую сегментацию
                  StartCoroutine(DelayedFirstSegmentationUpdate(3.0f));
            }
            else
            {
                  // Обычная задержка для инициализации AR
                  StartCoroutine(DelayedFirstSegmentationUpdate());
            }

            // Новый метод для принудительного включения отладочных настроек для всех визуализаторов плоскостей
            EnableDebugForAllVisualizers();
      }

      /// <summary>
      /// Создает демо-сегментацию без использования модели
      /// </summary>
      private Texture2D DemoSegmentation(Texture2D sourceTexture)
      {
            // Если модель сегментации имеет проблемы, используем простую демо-сегментацию
            int width = sourceTexture.width;
            int height = sourceTexture.height;

            // Создаем новую текстуру для результата
            Texture2D resultTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);

            // Демонстрационная сегментация - просто выделяем центральную часть синим цветом
            for (int y = 0; y < height; y++)
            {
                  for (int x = 0; x < width; x++)
                  {
                        // Определяем, находится ли пиксель в центральной области (имитация стены)
                        bool isWall =
                              (x > width * 0.3f && x < width * 0.7f) &&
                              (y > height * 0.2f && y < height * 0.8f);

                        // Устанавливаем цвет пикселя
                        if (isWall)
                        {
                              resultTexture.SetPixel(x, y, wallColor);
                        }
                        else
                        {
                              resultTexture.SetPixel(x, y, Color.clear);
                        }
                  }
            }

            resultTexture.Apply();
            Debug.LogWarning("Используется демо-режим сегментации");
            return resultTexture;
      }

      /// <summary>
      /// Переключение в демо-режим
      /// </summary>
      private void SwitchToDemoMode()
      {
            useDemoMode = true;
            Debug.LogWarning("Переключение в демо-режим сегментации");
      }

      /// <summary>
      /// Метод для загрузки выбранной модели сегментации с использованием Unity Sentis
      /// </summary>
      private void LoadSelectedModel()
      {
            try
            {
                  // Если уже есть рабочий воркер, освобождаем его
                  if (worker != null)
                  {
                        worker.Dispose();
                        worker = null;
                  }

                  // Если уже есть модель, очищаем ресурсы
                  if (runtimeModel != null)
                  {
                        runtimeModel = null;
                  }

                  if (currentMode == SegmentationMode.Demo || forceDemoMode)
                  {
                        Debug.Log("Используется демо-режим сегментации");
                        useDemoMode = true;
                        isModelInitialized = true;
                        return;
                  }

                  // Если нужно использовать встроенную модель из редактора
                  if (forceUseEmbeddedModel && modelAsset != null)
                  {
                        try
                        {
                              Debug.Log("Принудительное использование встроенной модели из редактора (forceUseEmbeddedModel=true)");
                              runtimeModel = ModelLoader.Load(modelAsset);
                              Debug.Log($"Успешно загружена встроенная модель: {modelAsset.name}");
                              currentModelAsset = modelAsset;
                        }
                        catch (Exception e)
                        {
                              Debug.LogError($"Ошибка при загрузке встроенной модели: {e.Message}");
                              SwitchToDemoMode();
                              return;
                        }
                  }
                  // Если форсированная встроенная модель не загружена или не установлена, пробуем остальные пути
                  else
                  {
                        // Попытка загрузить встроенную модель, если она указана
                        if (currentMode == SegmentationMode.EmbeddedModel && modelAsset != null)
                        {
                              try
                              {
                                    runtimeModel = ModelLoader.Load(modelAsset);
                                    Debug.Log("Модель успешно загружена из ресурсов");
                                    currentModelAsset = modelAsset;
                              }
                              catch (Exception e)
                              {
                                    Debug.LogWarning($"Не удалось загрузить модель из ресурсов: {e.Message}, пробуем альтернативные модели");
                                    runtimeModel = null;
                              }
                        }

                        // Если не удалось загрузить из ресурсов, пробуем искать в Resources
                        if (runtimeModel == null)
                        {
                              Debug.Log("Поиск моделей в директории Resources...");

                              try
                              {
                                    // Ищем модели в Resources
                                    ModelAsset[] modelAssets = Resources.LoadAll<ModelAsset>("");
                                    if (modelAssets != null && modelAssets.Length > 0)
                                    {
                                          Debug.Log($"Найдено {modelAssets.Length} моделей в Resources, загружаем первую: {modelAssets[0].name}");
                                          runtimeModel = ModelLoader.Load(modelAssets[0]);
                                          currentModelAsset = modelAssets[0];
                                    }
                                    else
                                    {
                                          Debug.LogWarning("Модели в Resources не найдены");
                                    }
                              }
                              catch (Exception e)
                              {
                                    Debug.LogError($"Ошибка при поиске моделей в Resources: {e.Message}");
                              }
                        }
                  }

                  // Проверяем, что модель загружена
                  if (runtimeModel != null)
                  {
                        // Выбираем правильные имена тензоров на основе модели
                        SelectCorrectTensorNames();

                        // Анализируем входы и выходы модели
                        AnalyzeModelInputs(runtimeModel);

                        try
                        {
                              // Создаем рабочий экземпляр для запуска модели
                              BackendType backendType = SystemInfo.supportsComputeShaders
                                    ? BackendType.GPUCompute
                                    : BackendType.CPU;

                              worker = WorkerFactory.CreateWorker(backendType, runtimeModel);
                              Debug.Log($"Модель сегментации успешно загружена и инициализирована. Тип бэкенда: {backendType}");
                              isModelInitialized = true;
                              useDemoMode = false;
                        }
                        catch (Exception e)
                        {
                              Debug.LogError($"Ошибка при создании воркера для модели: {e.Message}");
                              SwitchToDemoMode();
                        }
                  }
                  else
                  {
                        Debug.LogError("Не удалось загрузить ни одну модель. Используем демо-режим сегментации.");
                        SwitchToDemoMode();
                  }
            }
            catch (Exception e)
            {
                  Debug.LogError($"Ошибка загрузки модели: {e.Message}");
                  SwitchToDemoMode();
            }
      }

      /// <summary>
      /// Анализирует входные параметры модели для определения правильных размерностей
      /// </summary>
      private void AnalyzeModelInputs(Model model)
      {
            if (model == null)
            {
                  Debug.LogWarning("Модель не содержит информации о входных данных");
                  return;
            }

            Debug.Log("Анализ модели Sentis");

            // Получаем информацию о входах модели
            var inputs = model.inputs;
            Debug.Log($"Количество входов: {inputs.Length}");

            foreach (var input in inputs)
            {
                  Debug.Log($"Вход: {input.name}");
                  Debug.Log($"Форма: {string.Join(",", input.shape)}");

                  // Детальный анализ формы тензора
                  if (input.shape.Length >= 4)
                  {
                        Debug.Log("Детальный анализ формы тензора:");

                        // Вывод всех значений формы для диагностики
                        for (int i = 0; i < input.shape.Length; i++)
                        {
                              Debug.Log($"  Размерность[{i}] = {input.shape[i]}");
                        }

                        // Проверка на размеры, равные нулю, и их исправление
                        bool hasZeroDimensions = false;
                        for (int i = 0; i < input.shape.Length; i++)
                        {
                              if (input.shape[i] == 0)
                              {
                                    hasZeroDimensions = true;
                                    Debug.LogWarning($"Обнаружена нулевая размерность в индексе {i}. Будут использованы безопасные значения по умолчанию.");
                              }
                        }

                        if (hasZeroDimensions)
                        {
                              Debug.Log("Применение безопасных размеров для тензора с нулевыми размерностями");

                              // Определяем формат по количеству ненулевых измерений и их позиции
                              int nonZeroCount = 0;
                              for (int i = 0; i < input.shape.Length; i++)
                              {
                                    if (input.shape[i] > 0) nonZeroCount++;
                              }

                              // Если большинство размерностей нулевые, используем безопасные значения
                              if (nonZeroCount < input.shape.Length / 2)
                              {
                                    Debug.Log("Большинство размерностей нулевые, используем безопасные значения");
                                    useNCHW = true;
                                    inputChannels = 3;  // Используем 3 канала (RGB)
                                    inputHeight = 128;  // Безопасное значение
                                    inputWidth = 128;   // Безопасное значение
                                    Debug.Log($"Установлены безопасные значения: формат=NCHW, размеры={inputWidth}x{inputHeight}x{inputChannels}");
                                    return;
                              }
                        }

                        // Определение формата на основе анализа формы
                        bool looksLikeNCHW = false;
                        bool looksLikeNHWC = false;

                        // NCHW: [batch, channels, height, width]
                        // Обычно количество каналов меньше размеров (1-4 для RGB/RGBA)
                        if (input.shape[1] > 0 && input.shape[1] <= 4)
                        {
                              looksLikeNCHW = true;
                              Debug.Log("Форма похожа на NCHW: [batch, channels, height, width]");
                        }

                        // NHWC: [batch, height, width, channels]
                        // Обычно последнее измерение - каналы (1-4 для RGB/RGBA)
                        if (input.shape.Length >= 4 && input.shape[input.shape.Length - 1] > 0 && input.shape[input.shape.Length - 1] <= 4)
                        {
                              looksLikeNHWC = true;
                              Debug.Log("Форма похожа на NHWC: [batch, height, width, channels]");
                        }

                        // Если модель не соответствует стандартным признакам, пробуем определить по другой логике
                        if (!looksLikeNCHW && !looksLikeNHWC)
                        {
                              Debug.Log("Форма не соответствует стандартным признакам, анализируем детальнее");

                              // Предполагаем, что наибольшие значения - это размеры (высота/ширина)
                              int maxValue = 0;
                              int maxIndex = -1;

                              for (int i = 1; i < input.shape.Length; i++)
                              {
                                    if (input.shape[i] > maxValue)
                                    {
                                          maxValue = (int)input.shape[i];
                                          maxIndex = i;
                                    }
                              }

                              if (maxIndex == 1)
                              {
                                    // Если наибольшее значение во втором измерении, больше шансов что это NHWC
                                    useNCHW = false;
                                    Debug.Log("Предполагаем формат NHWC на основе анализа максимальных значений");
                              }
                              else if (maxIndex >= 2)
                              {
                                    // Если наибольшее значение в третьем или четвертом измерении, больше шансов что это NCHW
                                    useNCHW = true;
                                    Debug.Log("Предполагаем формат NCHW на основе анализа максимальных значений");
                              }
                        }
                        else
                        {
                              // Используем результат стандартного анализа
                              if (looksLikeNCHW && !looksLikeNHWC)
                              {
                                    useNCHW = true;
                              }
                              else if (!looksLikeNCHW && looksLikeNHWC)
                              {
                                    useNCHW = false;
                              }
                              else
                              {
                                    // Если подходит оба формата, используем безопасный вариант
                                    Debug.Log("Подходят оба формата, используем безопасный вариант (NCHW)");
                                    useNCHW = true;
                              }
                        }

                        // Обновляем параметры на основе определенного формата
                        if (useNCHW)
                        {
                              // В NCHW формате: [batch, channels, height, width]
                              if (input.shape[1] > 0 && input.shape[1] <= 128)
                              {
                                    inputChannels = (int)input.shape[1];
                                    Debug.Log($"Установлено количество каналов (NCHW): {inputChannels}");
                              }

                              if (input.shape.Length >= 3 && input.shape[2] > 0)
                              {
                                    inputHeight = (int)input.shape[2];
                                    Debug.Log($"Установлена высота (NCHW): {inputHeight}");
                              }

                              if (input.shape.Length >= 4 && input.shape[3] > 0)
                              {
                                    inputWidth = (int)input.shape[3];
                                    Debug.Log($"Установлена ширина (NCHW): {inputWidth}");
                              }
                        }
                        else
                        {
                              // В NHWC формате: [batch, height, width, channels]
                              if (input.shape.Length >= 4 && input.shape[3] > 0 && input.shape[3] <= 128)
                              {
                                    inputChannels = (int)input.shape[3];
                                    Debug.Log($"Установлено количество каналов (NHWC): {inputChannels}");
                              }

                              if (input.shape[1] > 0)
                              {
                                    inputHeight = (int)input.shape[1];
                                    Debug.Log($"Установлена высота (NHWC): {inputHeight}");
                              }

                              if (input.shape.Length >= 3 && input.shape[2] > 0)
                              {
                                    inputWidth = (int)input.shape[2];
                                    Debug.Log($"Установлена ширина (NHWC): {inputWidth}");
                              }
                        }
                  }
            }

            // Проверяем имя входа модели после анализа
            foreach (var input in inputs)
            {
                  if (input.name == "pixel_values")
                  {
                        Debug.Log("Обнаружен вход 'pixel_values' - применяем специфические параметры для YOLOv8");
                        inputChannels = 3; // channels = 3 (RGB)
                        useNCHW = true;
                        Debug.Log($"Принудительно установлены параметры для YOLOv8: {inputWidth}x{inputHeight}x{inputChannels}, формат=NCHW");
                        break;
                  }
            }

            Debug.Log($"Итоговые параметры модели: формат={(useNCHW ? "NCHW" : "NHWC")}, размеры={inputWidth}x{inputHeight}x{inputChannels}");
      }

      /// <summary>
      /// Обработчик события получения кадра с AR камеры
      /// </summary>
      private void OnCameraFrameReceived(ARCameraFrameEventArgs args)
      {
            if (isProcessing || useDemoMode || Time.time - lastProcessTime < processingInterval)
            {
                  return; // Пропускаем обработку, если уже обрабатывается кадр или не прошел интервал
            }

            // Пытаемся получить XRCpuImage
            if (!cameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
            {
                  return;
            }

            // Начинаем обработку
            isProcessing = true;
            lastProcessTime = Time.time;

            try
            {
                  // Настраиваем конвертацию изображения
                  var conversionParams = new XRCpuImage.ConversionParams
                  {
                        inputRect = new RectInt(0, 0, image.width, image.height),
                        outputDimensions = new Vector2Int(inputWidth, inputHeight),
                        outputFormat = TextureFormat.RGBA32,
                        transformation = XRCpuImage.Transformation.MirrorY
                  };

                  // Получаем размер буфера для конвертированного изображения
                  int size = image.GetConvertedDataSize(conversionParams);
                  var buffer = new byte[size];

                  // Конвертируем изображение в буфер
                  unsafe
                  {
                        fixed (byte* ptr = buffer)
                        {
                              image.Convert(conversionParams, new IntPtr(ptr), buffer.Length);
                        }
                  }

                  // Освобождаем ресурсы изображения
                  image.Dispose();

                  // Обновляем текстуру с камеры
                  if (cameraTexture == null || cameraTexture.width != inputWidth || cameraTexture.height != inputHeight)
                  {
                        if (cameraTexture != null)
                              Destroy(cameraTexture);

                        cameraTexture = new Texture2D(inputWidth, inputHeight, TextureFormat.RGBA32, false);
                  }

                  cameraTexture.LoadRawTextureData(buffer);
                  cameraTexture.Apply();

                  // Запускаем процесс сегментации асинхронно
                  StartCoroutine(ProcessImageAsync(cameraTexture));
            }
            catch (System.Exception e)
            {
                  Debug.LogError($"Ошибка при обработке кадра: {e.Message}");
                  isProcessing = false;

                  // Учитываем ошибку и при необходимости переключаемся в демо-режим
                  errorCount++;
                  if (errorCount > 5)
                  {
                        Debug.LogWarning("Слишком много ошибок при обработке кадров. Переключаемся в демо-режим.");
                        SwitchToDemoMode();
                  }
            }
      }

      /// <summary>
      /// Асинхронная обработка изображения
      /// </summary>
      private IEnumerator ProcessImageAsync(Texture2D sourceTexture)
      {
            // Ждем один кадр для лучшей отзывчивости UI
            yield return null;

            Texture2D resultTexture = null;

            try
            {
                  // Обработка изображения через модель или демо-режим
                  if (useDemoMode || forceDemoMode)
                  {
                        resultTexture = DemoSegmentation(sourceTexture);
                  }
                  else
                  {
                        resultTexture = RunModelSegmentation(sourceTexture);
                  }
            }
            catch (System.Exception e)
            {
                  Debug.LogError($"Ошибка при асинхронной обработке изображения: {e.Message}");
            }

            // Применяем результаты вне блока try-catch
            if (resultTexture != null)
            {
                  // Применяем сглаживание результатов между кадрами
                  Texture2D smoothedResult = InterpolateSegmentationResults(resultTexture);
                  segmentationTexture = smoothedResult;

                  // Копируем результат в RenderTexture для использования в шейдере
                  if (_outputRenderTexture != null)
                  {
                        // Убедимся, что RenderTexture имеет правильный размер
                        if (_outputRenderTexture.width != resultTexture.width ||
                              _outputRenderTexture.height != resultTexture.height)
                        {
                              // Пересоздаем RenderTexture с правильным размером
                              if (_outputRenderTexture != null)
                                    _outputRenderTexture.Release();

                              _outputRenderTexture = new RenderTexture(
                                    resultTexture.width,
                                    resultTexture.height,
                                    0,
                                    RenderTextureFormat.R8);
                              _outputRenderTexture.Create();
                        }

                        // Копируем данные в RenderTexture
                        Graphics.Blit(resultTexture, _outputRenderTexture);
                  }

                  // Показываем отладочное изображение, если нужно
                  if (showDebugVisualisation && debugImage != null)
                  {
                        debugImage.texture = resultTexture;
                  }

                  // Обновляем статус плоскостей на основе сегментации
                  if (useARPlaneController)
                  {
                        yield return StartCoroutine(UpdatePlanesBasedOnSegmentation());
                  }
            }

            // Завершаем обработку
            isProcessing = false;
      }

      // Заглушки для вспомогательных методов
      private IEnumerator ProcessFrames()
      {
            while (true)
            {
                  yield return new WaitForSeconds(processingInterval);
            }
      }

      private void SubscribeToPlaneEvents()
      {
            Debug.Log("Подписка на события плоскостей");
      }

      private IEnumerator DelayedFirstSegmentationUpdate()
      {
            Debug.Log("Обновление сегментации с задержкой");
            yield return new WaitForSeconds(2.0f);
      }

      private IEnumerator DelayedFirstSegmentationUpdate(float delayTime)
      {
            Debug.Log($"Обновление сегментации с задержкой {delayTime} сек");
            yield return new WaitForSeconds(delayTime);
      }

      private void EnableDebugForAllVisualizers()
      {
            Debug.Log("Включение отладочной визуализации для всех визуализаторов");
      }

      /// <summary>
      /// Метод для сглаживания результатов сегментации между кадрами
      /// </summary>
      private Texture2D InterpolateSegmentationResults(Texture2D newResult)
      {
            if (!useSmoothInterpolation || newResult == null)
                  return newResult;

            try
            {
                  // Стабилизация через буфер изображений
                  if (stabilizationBufferSize > 0)
                  {
                        // Инициализация буфера при первом вызове
                        if (!bufferInitialized)
                        {
                              resultBuffer.Clear();
                              for (int i = 0; i < stabilizationBufferSize; i++)
                              {
                                    // Создаем копии текущего результата для заполнения буфера
                                    Texture2D bufferCopy = new Texture2D(newResult.width, newResult.height, TextureFormat.RGBA32, false);
                                    bufferCopy.SetPixels(newResult.GetPixels());
                                    bufferCopy.Apply();
                                    resultBuffer.Add(bufferCopy);
                              }
                              bufferInitialized = true;
                        }

                        // Обновляем текущий элемент буфера
                        if (resultBuffer.Count > currentBufferIndex)
                        {
                              // Если размеры не совпадают, переинициализируем буфер
                              if (resultBuffer[currentBufferIndex].width != newResult.width ||
                                    resultBuffer[currentBufferIndex].height != newResult.height)
                              {
                                    bufferInitialized = false;
                                    return newResult;
                              }

                              // Обновляем текущий элемент буфера
                              resultBuffer[currentBufferIndex].SetPixels(newResult.GetPixels());
                              resultBuffer[currentBufferIndex].Apply();
                        }

                        // Обновляем индекс буфера
                        currentBufferIndex = (currentBufferIndex + 1) % stabilizationBufferSize;

                        // Создаем результат с усреднением всех изображений в буфере
                        Texture2D averagedResult = new Texture2D(newResult.width, newResult.height, TextureFormat.RGBA32, false);
                        Color[] avgPixels = new Color[newResult.width * newResult.height];

                        // Инициализируем средние значения пикселей
                        for (int i = 0; i < avgPixels.Length; i++)
                        {
                              avgPixels[i] = Color.clear;
                        }

                        // Суммируем значения пикселей из всех буферов
                        foreach (Texture2D bufferTexture in resultBuffer)
                        {
                              Color[] bufferPixels = bufferTexture.GetPixels();
                              for (int i = 0; i < avgPixels.Length; i++)
                              {
                                    avgPixels[i] += bufferPixels[i] / resultBuffer.Count;
                              }
                        }

                        // Применяем средние значения пикселей
                        averagedResult.SetPixels(avgPixels);
                        averagedResult.Apply();

                        return averagedResult;
                  }
                  // Сглаживание через интерполяцию с предыдущим результатом
                  else if (segmentationTexture != null && smoothingFactor > 0)
                  {
                        // Проверяем совпадение размеров текстур
                        if (segmentationTexture.width != newResult.width ||
                              segmentationTexture.height != newResult.height)
                        {
                              return newResult;
                        }

                        // Создаем результат с интерполяцией между текущим и предыдущим кадрами
                        Texture2D interpolatedResult = new Texture2D(newResult.width, newResult.height, TextureFormat.RGBA32, false);

                        // Получаем пиксели текущего и предыдущего результатов
                        Color[] newPixels = newResult.GetPixels();
                        Color[] oldPixels = segmentationTexture.GetPixels();
                        Color[] resultPixels = new Color[newPixels.Length];

                        // Интерполируем пиксели
                        for (int i = 0; i < newPixels.Length; i++)
                        {
                              // Используем линейную интерполяцию между старым и новым значениями
                              resultPixels[i] = Color.Lerp(newPixels[i], oldPixels[i], smoothingFactor);
                        }

                        // Применяем результат
                        interpolatedResult.SetPixels(resultPixels);
                        interpolatedResult.Apply();

                        return interpolatedResult;
                  }
            }
            catch (System.Exception e)
            {
                  Debug.LogError($"Ошибка при сглаживании результатов сегментации: {e.Message}");
            }

            return newResult;
      }

      /// <summary>
      /// Обновляет плоскости на основе результатов сегментации
      /// </summary>
      private IEnumerator UpdatePlanesBasedOnSegmentation()
      {
            // Получаем ARPlaneManager
            ARPlaneManager planeManager = FindObjectOfType<ARPlaneManager>();
            if (planeManager == null)
            {
                  Debug.LogWarning("ARPlaneManager не найден");
                  yield break;
            }

            // Если нет текстуры сегментации или она неправильно инициализирована
            if (segmentationTexture == null)
            {
                  Debug.LogWarning("Текстура сегментации не доступна");
                  yield break;
            }

            // Проверка и инициализация arCamera
            if (arCamera == null)
            {
                  Debug.LogWarning("AR Camera не настроена, пробуем найти камеру");
                  // Сначала пытаемся получить главную камеру
                  arCamera = Camera.main;

                  // Если main camera не найдена, ищем любую активную камеру
                  if (arCamera == null)
                  {
                        Camera[] cameras = FindObjectsOfType<Camera>();
                        if (cameras != null && cameras.Length > 0)
                        {
                              arCamera = cameras[0]; // используем первую найденную камеру
                              Debug.Log("Используем первую найденную камеру: " + arCamera.name);
                        }
                        else
                        {
                              Debug.LogError("Не удалось найти ни одной камеры");
                              yield break;
                        }
                  }
            }

            // Получаем все обнаруженные плоскости
            foreach (ARPlane plane in planeManager.trackables)
            {
                  // Проверяем только вертикальные плоскости (возможные стены)
                  if (plane.alignment == PlaneAlignment.Vertical)
                  {
                        // Получаем позицию плоскости в экранных координатах
                        Vector2 screenPos = arCamera.WorldToScreenPoint(plane.center);

                        // Проверяем, находится ли плоскость в пределах экрана
                        if (screenPos.x >= 0 && screenPos.x < Screen.width &&
                              screenPos.y >= 0 && screenPos.y < Screen.height)
                        {
                              // Нормализуем координаты экрана к размеру текстуры сегментации
                              int textureX = (int)(screenPos.x * segmentationTexture.width / Screen.width);
                              int textureY = (int)(screenPos.y * segmentationTexture.height / Screen.height);

                              // Проверяем пиксель на наличие стены (непрозрачный пиксель означает стену)
                              Color pixelColor = segmentationTexture.GetPixel(
                                    Mathf.Clamp(textureX, 0, segmentationTexture.width - 1),
                                    Mathf.Clamp(textureY, 0, segmentationTexture.height - 1)
                              );

                              // Если пиксель не прозрачный - это стена
                              if (pixelColor.a > 0.5f)
                              {
                                    // Помечаем плоскость как стену для дальнейшего использования
                                    // Можно добавить какой-то визуальный эффект или метку
                                    if (debugPositioning && plane.gameObject.GetComponent<Renderer>() != null)
                                    {
                                          // Изменяем цвет визуализатора плоскости для отладки
                                          plane.gameObject.GetComponent<Renderer>().material.color = wallColor;
                                    }
                              }
                        }
                  }
            }

            yield return null;
      }

      /// <summary>
      /// Метод для запуска сегментации с использованием модели Sentis
      /// </summary>
      private Texture2D RunModelSegmentation(Texture2D sourceTexture)
      {
            try
            {
                  // Проверяем инициализацию модели
                  if (!isModelInitialized)
                  {
                        LoadSelectedModel();
                  }

                  // Если модель не загружена или worker не создан, возвращаем демо-сегментацию
                  if (runtimeModel == null || worker == null)
                  {
                        Debug.LogWarning("Модель не инициализирована. Используем демо-сегментацию.");
                        return DemoSegmentation(sourceTexture);
                  }

                  if (enableDebugLogs)
                  {
                        Debug.Log($"Запуск сегментации с параметрами: размеры входа={inputWidth}x{inputHeight}x{inputChannels}, формат={(useNCHW ? "NCHW" : "NHWC")}");
                  }

                  // Создаем тензор из текстуры
                  try
                  {
                        // Подготавливаем входной тензор из текстуры
                        if (inputTensor != null)
                        {
                              inputTensor.Dispose();
                              inputTensor = null;
                        }

                        // Создаем текстуру нужного размера для модели
                        RenderTexture rt = RenderTexture.GetTemporary(inputWidth, inputHeight, 0, RenderTextureFormat.ARGB32);
                        Graphics.Blit(sourceTexture, rt);

                        // Определяем формат входных данных модели
                        TensorShape inputShape;
                        TextureTransform texTransform = new TextureTransform();
                        texTransform.SetDimensions(inputWidth, inputHeight, inputChannels);

                        if (useNCHW)
                        {
                              // NCHW формат (batch, channels, height, width)
                              inputShape = new TensorShape(1, inputChannels, inputHeight, inputWidth);
                              texTransform.FlipY(); // Обычно в компьютерном зрении Y инвертирован
                        }
                        else
                        {
                              // NHWC формат (batch, height, width, channels)
                              inputShape = new TensorShape(1, inputHeight, inputWidth, inputChannels);
                              texTransform.FlipY();
                        }

                        // Создаем входной тензор
                        inputTensor = new TensorFloat(inputShape);

                        // Конвертируем текстуру в тензор
                        TextureConverter.ToTensor(rt, inputTensor, texTransform);

                        // Освобождаем временную текстуру
                        RenderTexture.ReleaseTemporary(rt);

                        if (enableDebugLogs)
                        {
                              Debug.Log($"Тензор создан: {inputTensor.shape}");
                        }
                  }
                  catch (Exception e)
                  {
                        Debug.LogError($"Ошибка при создании входного тензора: {e.Message}");
                        return DemoSegmentation(sourceTexture);
                  }

                  // Запускаем инференс модели
                  try
                  {
                        // Проверяем наличие имени входного тензора в модели
                        string actualInputName = inputName;

                        // Создаем словарь с нашим входным тензором
                        var inputs = new Dictionary<string, Tensor>();
                        inputs[actualInputName] = inputTensor;

                        // Выполняем инференс
                        worker.Execute(inputs);

                        if (enableDebugLogs)
                        {
                              Debug.Log("Инференс модели успешно выполнен");
                        }
                  }
                  catch (Exception ex)
                  {
                        Debug.LogError($"Ошибка при выполнении инференса: {ex.Message}");

                        // Пробуем альтернативное имя или прямой запуск
                        try
                        {
                              if (altInputName != inputName)
                              {
                                    Debug.Log($"Пробуем альтернативное имя входа: {altInputName}");
                                    var inputs = new Dictionary<string, Tensor>();
                                    inputs[altInputName] = inputTensor;
                                    worker.Execute(inputs);
                              }
                              else
                              {
                                    Debug.Log("Пробуем прямой запуск без имени входа");
                                    worker.Execute(inputTensor);
                              }
                        }
                        catch (Exception innerEx)
                        {
                              Debug.LogError($"Не удалось выполнить инференс: {innerEx.Message}");
                              return DemoSegmentation(sourceTexture);
                        }
                  }

                  // Получаем результат из выходного тензора
                  TensorFloat outputTensor = null;

                  // Безопасное получение выходного тензора
                  try
                  {
                        // Пробуем получить по основному имени выхода
                        outputTensor = worker.PeekOutput(outputName) as TensorFloat;

                        // Если не получилось, пробуем альтернативное имя
                        if (outputTensor == null && altOutputName != outputName)
                        {
                              outputTensor = worker.PeekOutput(altOutputName) as TensorFloat;

                              if (outputTensor != null)
                              {
                                    Debug.Log($"Получен выходной тензор по альтернативному имени: {altOutputName}");
                                    outputName = altOutputName; // Запоминаем правильное имя
                              }
                        }

                        // Если и это не сработало, попробуем взять первый доступный выходной тензор
                        if (outputTensor == null)
                        {
                              var outputNames = worker.GetOutputNames();
                              if (outputNames.Length > 0)
                              {
                                    outputTensor = worker.PeekOutput(outputNames[0]) as TensorFloat;
                                    if (outputTensor != null)
                                    {
                                          Debug.Log($"Получен выходной тензор по первому доступному имени: {outputNames[0]}");
                                          outputName = outputNames[0]; // Запоминаем правильное имя
                                    }
                              }
                        }

                        if (outputTensor == null)
                        {
                              Debug.LogError("Не удалось получить выходной тензор: выходной тензор равен null");
                              errorCount++;
                              if (errorCount > 3)
                              {
                                    Debug.LogWarning("Слишком много ошибок при получении тензора. Переключаемся в демо-режим.");
                                    SwitchToDemoMode();
                              }
                              return DemoSegmentation(sourceTexture);
                        }

                        // Анализируем выходной тензор для отладки
                        if (outputTensor != null && enableDebugLogs)
                        {
                              Debug.Log($"Получен выходной тензор: {outputTensor.shape}");
                        }
                  }
                  catch (Exception e)
                  {
                        Debug.LogError($"Ошибка при получении выходного тензора: {e.Message}");
                        return DemoSegmentation(sourceTexture);
                  }

                  // Создаем текстуру сегментации на основе результата модели
                  Texture2D segmentationResult = CreateSegmentationTexture(outputTensor, sourceTexture.width, sourceTexture.height);

                  return segmentationResult;
            }
            catch (System.Exception e)
            {
                  Debug.LogError($"Ошибка при выполнении сегментации с моделью: {e.Message}");
                  errorCount++;

                  if (errorCount > 3)
                  {
                        Debug.LogWarning("Слишком много ошибок при работе с моделью. Переключаемся в демо-режим.");
                        SwitchToDemoMode();
                  }

                  // В случае ошибки возвращаем демо-сегментацию
                  return DemoSegmentation(sourceTexture);
            }
      }

      /// <summary>
      /// Создает текстуру сегментации из выходного тензора модели
      /// </summary>
      private Texture2D CreateSegmentationTexture(TensorFloat outputTensor, int targetWidth, int targetHeight)
      {
            try
            {
                  // Получаем размеры выходного тензора
                  var tensorShape = outputTensor.shape;

                  // Определяем размеры и формат тензора
                  int tensorWidth, tensorHeight, tensorChannels;
                  bool isMultiChannel = false;

                  // Анализируем форму выходного тензора
                  if (tensorShape.rank >= 4)
                  {
                        // Для форматов [batch, channels, height, width] или [batch, height, width, channels]
                        if (useNCHW)
                        {
                              tensorChannels = tensorShape[1]; // NCHW: [batch, channels, height, width]
                              tensorHeight = tensorShape[2];
                              tensorWidth = tensorShape[3];
                              isMultiChannel = tensorChannels > 1;

                              Debug.Log($"Обнаружен NCHW формат: {tensorChannels} каналов, {tensorHeight}x{tensorWidth}");
                        }
                        else
                        {
                              tensorHeight = tensorShape[1]; // NHWC: [batch, height, width, channels]
                              tensorWidth = tensorShape[2];
                              tensorChannels = tensorShape[3];
                              isMultiChannel = tensorChannels > 1;

                              Debug.Log($"Обнаружен NHWC формат: {tensorHeight}x{tensorWidth}, {tensorChannels} каналов");
                        }
                  }
                  else if (tensorShape.rank == 3)
                  {
                        // Для форматов [batch, height, width] - одноканальный выход
                        tensorHeight = tensorShape[1];
                        tensorWidth = tensorShape[2];
                        tensorChannels = 1;
                        isMultiChannel = false;

                        Debug.Log($"Обнаружен одноканальный формат: {tensorHeight}x{tensorWidth}");
                  }
                  else
                  {
                        // Если формат неизвестен, используем безопасные значения
                        Debug.LogWarning($"Неизвестный формат тензора с размерностью {tensorShape.rank}. Используем безопасные значения.");
                        tensorHeight = targetHeight;
                        tensorWidth = targetWidth;
                        tensorChannels = 1;
                        isMultiChannel = false;
                  }

                  // Создаем текстуру для результата сегментации
                  Texture2D segmentationTexture = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);

                  // Проверяем, есть ли в тензоре индекс класса стены
                  int effectiveWallClassIndex = wallClassIndex;
                  if (isMultiChannel)
                  {
                        // Для многоканального выхода используем индекс класса стены
                        effectiveWallClassIndex = Mathf.Clamp(wallClassIndex, 0, tensorChannels - 1);
                        Debug.Log($"Используется индекс стены: {effectiveWallClassIndex} из {tensorChannels} классов");
                  }

                  // Создаем временную текстуру для вывода результата сегментации
                  RenderTexture outputRT = RenderTexture.GetTemporary(tensorWidth, tensorHeight, 0, RenderTextureFormat.ARGBFloat);
                  outputRT.enableRandomWrite = true;
                  outputRT.Create();

                  // Используем TextureConverter для преобразования выходного тензора в текстуру
                  if (isMultiChannel)
                  {
                        // Для многоканального выхода нужно выделить нужный канал
                        // Создаем новый тензор для класса стены
                        using (var wallClassTensor = new TensorFloat(new TensorShape(1, 1, tensorHeight, tensorWidth)))
                        {
                              // Заполняем его данными из нужного канала выходного тензора
                              for (int y = 0; y < tensorHeight; y++)
                              {
                                    for (int x = 0; x < tensorWidth; x++)
                                    {
                                          float value;
                                          if (useNCHW)
                                          {
                                                value = outputTensor[0, effectiveWallClassIndex, y, x];
                                          }
                                          else
                                          {
                                                value = outputTensor[0, y, x, effectiveWallClassIndex];
                                          }

                                          // Применяем порог для бинаризации
                                          value = value > threshold ? 1.0f : 0.0f;

                                          wallClassTensor[0, 0, y, x] = value;
                                    }
                              }

                              // Конвертируем тензор в текстуру
                              TextureConverter.RenderToTexture(wallClassTensor, outputRT);
                        }
                  }
                  else
                  {
                        // Для одноканального выхода просто конвертируем тензор в текстуру
                        // с применением порога для бинаризации
                        using (var thresholdedTensor = new TensorFloat(new TensorShape(1, 1, tensorHeight, tensorWidth)))
                        {
                              for (int y = 0; y < tensorHeight; y++)
                              {
                                    for (int x = 0; x < tensorWidth; x++)
                                    {
                                          float value;
                                          if (tensorShape.rank == 4)
                                          {
                                                if (useNCHW)
                                                {
                                                      value = outputTensor[0, 0, y, x];
                                                }
                                                else
                                                {
                                                      value = outputTensor[0, y, x, 0];
                                                }
                                          }
                                          else // rank == 3
                                          {
                                                value = outputTensor[0, y, x];
                                          }

                                          // Применяем порог для бинаризации
                                          value = value > threshold ? 1.0f : 0.0f;

                                          thresholdedTensor[0, 0, y, x] = value;
                                    }
                              }

                              // Конвертируем тензор в текстуру
                              TextureConverter.RenderToTexture(thresholdedTensor, outputRT);
                        }
                  }

                  // Копируем результат из RenderTexture в Texture2D
                  RenderTexture.active = outputRT;
                  Texture2D tempTexture = new Texture2D(tensorWidth, tensorHeight, TextureFormat.RGBA32, false);
                  tempTexture.ReadPixels(new Rect(0, 0, tensorWidth, tensorHeight), 0, 0);
                  tempTexture.Apply();
                  RenderTexture.active = null;
                  RenderTexture.ReleaseTemporary(outputRT);

                  // Масштабируем результат до нужного размера
                  if (tempTexture.width != targetWidth || tempTexture.height != targetHeight)
                  {
                        RenderTexture resizeRT = RenderTexture.GetTemporary(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32);
                        Graphics.Blit(tempTexture, resizeRT);

                        RenderTexture.active = resizeRT;
                        segmentationTexture.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
                        segmentationTexture.Apply();
                        RenderTexture.active = null;
                        RenderTexture.ReleaseTemporary(resizeRT);

                        Destroy(tempTexture);
                  }
                  else
                  {
                        // Если размеры совпадают, просто используем временную текстуру
                        segmentationTexture = tempTexture;
                  }

                  // Применяем цвет стены к сегментации
                  Color[] pixels = segmentationTexture.GetPixels();
                  for (int i = 0; i < pixels.Length; i++)
                  {
                        // Если пиксель сегментации не черный (значение > 0), устанавливаем цвет стены
                        if (pixels[i].r > 0.1f || pixels[i].g > 0.1f || pixels[i].b > 0.1f)
                        {
                              pixels[i] = wallColor;
                        }
                        else
                        {
                              pixels[i] = Color.clear;
                        }
                  }
                  segmentationTexture.SetPixels(pixels);
                  segmentationTexture.Apply();

                  return segmentationTexture;
            }
            catch (Exception ex)
            {
                  Debug.LogError($"Ошибка при создании текстуры сегментации: {ex.Message}");

                  // В случае ошибки возвращаем пустую текстуру
                  Texture2D fallbackTexture = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
                  Color[] clearColors = new Color[targetWidth * targetHeight];
                  for (int i = 0; i < clearColors.Length; i++)
                        clearColors[i] = Color.clear;

                  fallbackTexture.SetPixels(clearColors);
                  fallbackTexture.Apply();

                  return fallbackTexture;
            }
      }

      /// <summary>
      /// Выбор правильных имен тензоров на основе загруженной модели
      /// </summary>
      private void SelectCorrectTensorNames()
      {
            if (runtimeModel == null)
                  return;

            // Получаем входы модели
            var inputs = runtimeModel.inputs;
            if (inputs.Length == 0)
            {
                  Debug.LogWarning("Модель не имеет входов!");
                  return;
            }

            // Параметры для YOLOv8
            string yoloInput = "images";
            string yoloOutput = "output0";
            int yoloWallIndex = 0; // В YOLOv8-seg класс 0 обычно person, для стен нужно обучить модель специально

            // Параметры для MobileNet
            string mobileNetInput = "serving_default_input:0";
            string mobileNetOutput = "StatefulPartitionedCall:0";
            int mobileNetWallIndex = 9; // Класс 9 в общих моделях сегментации часто wall

            // Проверяем наличие входного тензора обоих типов моделей
            bool hasYoloInput = false;
            bool hasMobileNetInput = false;

            foreach (var input in inputs)
            {
                  if (input.name == yoloInput)
                  {
                        hasYoloInput = true;
                        Debug.Log($"Найден входной тензор с именем '{yoloInput}' (YOLOv8)");
                  }
                  else if (input.name == mobileNetInput)
                  {
                        hasMobileNetInput = true;
                        Debug.Log($"Найден входной тензор с именем '{mobileNetInput}' (MobileNet)");
                  }
            }

            // Проверяем выходы модели
            var outputs = runtimeModel.outputs;
            bool hasYoloOutput = false;
            bool hasMobileNetOutput = false;

            foreach (var output in outputs)
            {
                  if (output.name == yoloOutput)
                  {
                        hasYoloOutput = true;
                        Debug.Log($"Найден выходной тензор с именем '{yoloOutput}' (YOLOv8)");
                  }
                  else if (output.name == mobileNetOutput)
                  {
                        hasMobileNetOutput = true;
                        Debug.Log($"Найден выходной тензор с именем '{mobileNetOutput}' (MobileNet)");
                  }
            }

            // По умолчанию используем параметры MobileNet, как более надежной модели
            inputName = mobileNetInput;
            outputName = mobileNetOutput;
            wallClassIndex = mobileNetWallIndex;
            Debug.Log($"По умолчанию используем параметры MobileNet: вход='{inputName}', выход='{outputName}', индекс стены={wallClassIndex}");

            // Проверяем совпадения по типам моделей и обновляем параметры
            if (hasYoloInput && hasYoloOutput)
            {
                  Debug.Log($"Обнаружена модель YOLOv8, используем соответствующие имена тензоров");
                  inputName = yoloInput;
                  outputName = yoloOutput;
                  wallClassIndex = yoloWallIndex;
                  useNCHW = true; // YOLOv8 использует NCHW формат
                  Debug.Log($"Используем имена тензоров YOLOv8: вход='{inputName}', выход='{outputName}', индекс стены={wallClassIndex}");
            }
            else if (hasMobileNetInput && hasMobileNetOutput)
            {
                  Debug.Log($"Обнаружена модель MobileNet, используем соответствующие имена тензоров");
                  inputName = mobileNetInput;
                  outputName = mobileNetOutput;
                  wallClassIndex = mobileNetWallIndex;
                  useNCHW = false; // MobileNet часто использует NHWC формат
                  Debug.Log($"Используем имена тензоров MobileNet: вход='{inputName}', выход='{outputName}', индекс стены={wallClassIndex}");
            }
            else
            {
                  // Если не удалось определить модель, используем первый вход и выход
                  if (inputs.Length > 0)
                  {
                        inputName = inputs[0].name;
                        Debug.Log($"Не удалось определить тип модели, используем первый найденный вход: '{inputName}'");
                  }

                  if (outputs.Length > 0)
                  {
                        outputName = outputs[0].name;
                        Debug.Log($"Не удалось определить тип модели, используем первый найденный выход: '{outputName}'");
                  }
            }

            Debug.Log($"Итоговые параметры: вход='{inputName}', выход='{outputName}', индекс стены={wallClassIndex}, формат={(useNCHW ? "NCHW" : "NHWC")}");
      }

      /// <summary>
      /// Переключение режима сегментации
      /// </summary>
      public void SwitchMode(SegmentationMode newMode)
      {
            // Если режим не меняется и это не принудительный Demo режим, то выходим
            if (currentMode == newMode && newMode != SegmentationMode.Demo)
            {
                  Debug.Log($"Режим сегментации не изменен (уже активен {newMode})");
                  return;
            }

            currentMode = newMode;

            // Сохраняем текущее состояние демо-режима до загрузки модели
            bool wasUsingDemoMode = useDemoMode;

            // Сбрасываем принудительный демо-режим
            useDemoMode = newMode == SegmentationMode.Demo;

            // Сбрасываем счетчик ошибок
            errorCount = 0;

            // Останавливаем текущий рабочий процесс, если он существует
            if (worker != null)
            {
                  worker.Dispose();
                  worker = null;
            }

            // Загружаем и инициализируем новую модель
            LoadSelectedModel();

            // Проверяем, изменился ли режим работы
            if (wasUsingDemoMode != useDemoMode)
            {
                  Debug.Log($"Режим сегментации переключен: {newMode}, Демо-режим: {useDemoMode}");

                  // Дополнительная информация, если переключение в демо-режим произошло из-за ошибки
                  if (useDemoMode && newMode != SegmentationMode.Demo)
                  {
                        Debug.LogWarning("Принудительное переключение в демо-режим из-за ошибки загрузки модели");
                  }
            }
            else
            {
                  Debug.Log($"Режим сегментации переключен: {newMode}");
            }
      }

      /// <summary>
      /// Получение текущего режима
      /// </summary>
      public SegmentationMode GetCurrentMode()
      {
            return currentMode;
      }

      /// <summary>
      /// Проверка использования демо-режима
      /// </summary>
      public bool IsUsingDemoMode()
      {
            return useDemoMode || currentMode == SegmentationMode.Demo;
      }

      /// <summary>
      /// Обновляет статус всех AR плоскостей на основе результатов сегментации
      /// </summary>
      public int UpdatePlanesSegmentationStatus()
      {
            Debug.Log("Обновление статуса сегментации плоскостей");
            return 0; // Заглушка, возвращает количество обновленных плоскостей
      }

      /// <summary>
      /// Получает текстуру сегментации
      /// </summary>
      public Texture2D GetSegmentationTexture()
      {
            return segmentationTexture;
      }

      /// <summary>
      /// Получает процент покрытия плоскости маской сегментации
      /// </summary>
      public float GetPlaneCoverageByMask(ARPlane plane)
      {
            return 0.5f; // Заглушка, возвращает процент покрытия
      }

      /// <summary>
      /// Включает/отключает отладочную визуализацию
      /// </summary>
      public void EnableDebugVisualization(bool enable)
      {
            showDebugVisualisation = enable;

            // Если мы включаем визуализацию, убедимся что есть связь с RawImage
            if (enable && debugImage == null)
            {
                  Debug.LogWarning("Отладочная визуализация включена, но RawImage не назначен");

                  // Можно попробовать найти RawImage автоматически
                  debugImage = FindObjectOfType<RawImage>();
                  if (debugImage == null)
                  {
                        Debug.LogError("Не удалось найти компонент RawImage для отладочной визуализации");
                  }
            }
      }

      /// <summary>
      /// Проверка состояния отладочной визуализации
      /// </summary>
      public bool IsDebugVisualizationEnabled()
      {
            return showDebugVisualisation;
      }

      private void OnDestroy()
      {
            // Освобождаем ресурсы Sentis
            if (worker != null)
            {
                  worker.Dispose();
                  worker = null;
            }

            if (inputTensor != null)
            {
                  inputTensor.Dispose();
                  inputTensor = null;
            }

            // Отписываемся от событий AR камеры
            if (cameraManager != null)
            {
                  cameraManager.frameReceived -= OnCameraFrameReceived;
            }

            // Освобождаем буфер сглаживания
            foreach (var texture in resultBuffer)
            {
                  if (texture != null)
                  {
                        Destroy(texture);
                  }
            }
            resultBuffer.Clear();

            // Освобождаем текстуры
            if (cameraTexture != null)
            {
                  Destroy(cameraTexture);
            }

            if (segmentationTexture != null)
            {
                  Destroy(segmentationTexture);
            }

            // Освобождаем RenderTexture
            if (_outputRenderTexture != null)
            {
                  _outputRenderTexture.Release();
                  _outputRenderTexture = null;
            }
      }
}