using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Barracuda;
using Unity.Barracuda.ONNX;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.UI;
using Unity.XR.CoreUtils;

/// <summary>
/// Компонент для сегментации стен с использованием нейросети
/// </summary>
public class WallSegmentation : MonoBehaviour
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
    [SerializeField] private string externalModelPath = "Models/model.onnx"; // Изменено на новую модель SegFormer

    [Header("Barracuda Model")]
    [SerializeField] public NNModel modelAsset; // Модель ONNX через asset

    [Header("Segmentation Parameters")]
    [SerializeField] private int inputWidth = 32; // Для BiseNet.onnx должно быть 960
    [SerializeField] private int inputHeight = 32; // Для BiseNet.onnx должно быть 720
    [SerializeField] private int inputChannels = 3; // Исправлено с 32 на 3, т.к. модели ожидают RGB (3 канала)
    [SerializeField] private string inputName = "pixel_values"; // Для использования BiseNet.onnx изменить на "image"
    [SerializeField] private string outputName = "logits"; // Для использования BiseNet.onnx изменить на "predict"
    [SerializeField, Range(0, 1)] private float threshold = 0.5f;
    [SerializeField] private int wallClassIndex = 9; // Для model.onnx лучше использовать 0 или 3

    [Header("Debug & Performance")]
    [SerializeField] private bool forceDemoMode = false; // Отключаем принудительный демо-режим
    [SerializeField] private bool showDebugVisualisation = true;
    [SerializeField] private RawImage debugImage;
    [SerializeField] private float processingInterval = 0.3f;
    [SerializeField] private bool enableDebugLogs = true; // Добавлен флаг включения отладочных логов
    [SerializeField] private bool debugPositioning = true; // Флаг для отладки позиционирования стен (включен по умолчанию)
    [SerializeField] private bool useARPlaneController = true; // Использовать ARPlaneController для управления плоскостями

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
    private Model model;
    private IWorker worker;
    private bool isProcessing;
    private Tensor inputTensor;
    private bool useDemoMode = false; // Используем демо-режим при ошибке модели
    private int errorCount = 0; // Счетчик ошибок нейросети
    private NNModel currentModelAsset; // Текущая используемая модель
    private List<GameObject> currentWalls = new List<GameObject>(); // Список текущих стен
    private List<GameObject> demoWalls = new List<GameObject>(); // Список демо-стен
    private bool isModelInitialized = false; // Флаг инициализации модели
    private float lastProcessTime = 0f; // Время последней обработки
    private bool useNCHW = true; // Флаг формата тензора

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
            Debug.LogError("Включена визуализация отладки (showDebugVisualisation), но не назначен компонент debugImage!");
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
            Debug.Log("WallSegmentation: Запуск автоматической сегментации в режиме ExternalModel...");

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

    // Переключение в демо-режим
    private void SwitchToDemoMode()
    {
        // Код метода SwitchToDemoMode
        useDemoMode = true;
    }

    // Остальной код класса, необходимый для работы...

    // Метод для проверки и обновления параметров модели
    private void CheckAndUpdateModelParameters()
    {
        // Устанавливаем безопасные значения по умолчанию
        int defaultWidth = 128;
        int defaultHeight = 128;
        int defaultChannels = 32;
        bool useDefaultValues = false;

        // Проверяем и обновляем имена входов/выходов модели
        if (model != null)
        {
            // Проверяем существование входа с заданным именем
            bool inputFound = false;
            foreach (var input in model.inputs)
            {
                if (input.name == inputName)
                {
                    inputFound = true;

                    // Если нашли вход, проверяем его форму и обновляем параметры
                    try
                    {
                        // Пробуем получить размерности тензора
                        int width = (int)input.shape[1];
                        int height = (int)input.shape[2];
                        int channels = (int)input.shape[3];

                        // Проверяем на неверные значения и установка безопасных значений по умолчанию
                        if (width <= 0 || height <= 0 || channels <= 0)
                        {
                            Debug.LogWarning($"Обнаружены неверные размеры тензора: {width}x{height}x{channels}. Устанавливаем безопасные значения по умолчанию.");
                            useDefaultValues = true;
                        }
                        else
                        {
                            inputWidth = width;
                            inputHeight = height;
                            inputChannels = channels;

                            Debug.Log($"Найден вход '{inputName}' с размерами {inputWidth}x{inputHeight}x{inputChannels}");
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"Ошибка при получении размеров тензора: {e.Message}. Устанавливаем безопасные значения по умолчанию.");
                        useDefaultValues = true;
                    }
                }
            }

            if (!inputFound)
            {
                // Если не нашли вход с заданным именем, берем первый доступный
                if (model.inputs.Count > 0)
                {
                    inputName = model.inputs[0].name;

                    // Обновляем размерности из найденного входа
                    try
                    {
                        // Пробуем получить размерности тензора
                        int width = (int)model.inputs[0].shape[1];
                        int height = (int)model.inputs[0].shape[2];
                        int channels = (int)model.inputs[0].shape[3];

                        // Проверка на корректность значений
                        if (width <= 0 || height <= 0 || channels <= 0)
                        {
                            Debug.LogWarning($"Обнаружены неверные размеры тензора: {width}x{height}x{channels}. Устанавливаем безопасные значения по умолчанию.");
                            useDefaultValues = true;
                        }
                        else
                        {
                            inputWidth = width;
                            inputHeight = height;
                            inputChannels = channels;

                            Debug.Log($"Найден вход '{inputName}' с размерами {inputWidth}x{inputHeight}x{inputChannels}");
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"Ошибка при получении размеров тензора: {e.Message}. Устанавливаем безопасные значения по умолчанию.");
                        useDefaultValues = true;
                    }
                }
                else
                {
                    Debug.LogWarning("Модель не имеет входов! Устанавливаем безопасные значения по умолчанию.");
                    useDefaultValues = true;
                }
            }

            // Проверяем выходной тензор
            try
            {
                var outputTensorShape = model.GetShapeByName(outputName);
                if (outputTensorShape.HasValue)
                {
                    // Проверяем размерность, пробуя получить доступ к 4 элементам
                    try
                    {
                        int outHeight = (int)outputTensorShape.Value[1];
                        int outWidth = (int)outputTensorShape.Value[2];
                        int outChannels = (int)outputTensorShape.Value[3];

                        // Проверяем корректность размеров выходного тензора
                        if (outHeight <= 0 || outWidth <= 0 || outChannels <= 0)
                        {
                            Debug.LogWarning($"Модель имеет некорректные размеры выходного тензора: {outHeight}x{outWidth}x{outChannels}. Возможны проблемы при выполнении.");
                        }
                        else
                        {
                            Debug.Log($"Модель имеет выходной тензор с размерами: {outHeight}x{outWidth}x{outChannels}");
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"Ошибка при получении размеров выходного тензора: {e.Message}");
                    }
                }
                else
                {
                    Debug.LogWarning($"Не удалось получить информацию о выходном тензоре: Shape {outputName} not found!");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Ошибка при получении информации о выходном тензоре: {e.Message}");
            }
        }

        // Устанавливаем безопасные значения по умолчанию, если необходимо
        if (useDefaultValues || inputWidth <= 0 || inputHeight <= 0 || inputChannels <= 0)
        {
            inputWidth = defaultWidth;
            inputHeight = defaultHeight;
            inputChannels = defaultChannels;
            Debug.LogWarning($"Установлены безопасные значения по умолчанию: {inputWidth}x{inputHeight}x{inputChannels}");
        }
    }

    /// <summary>
    /// Метод для загрузки выбранной модели сегментации
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

            // Если уже есть модель, очищаем ее
            if (model != null)
            {
                model.Dispose();
                model = null;
            }

            // В зависимости от режима, загружаем модель
            switch (currentMode)
            {
                case SegmentationMode.EmbeddedModel:
                    if (modelAsset != null)
                    {
                        model = ModelLoader.Load(modelAsset);
                        currentModelAsset = modelAsset;
                    }
                    else
                    {
                        Debug.LogError("Не задана модель в режиме EmbeddedModel. Переключение в демо-режим.");
                        SwitchToDemoMode();
                        return;
                    }
                    break;

                case SegmentationMode.ExternalModel:
                    try
                    {
                        // Сначала пробуем загрузить из Resources
                        NNModel loadedModel = Resources.Load<NNModel>(externalModelPath);
                        if (loadedModel != null)
                        {
                            model = ModelLoader.Load(loadedModel);
                            currentModelAsset = loadedModel;
                            Debug.Log($"Успешно загружена модель из Resources: {externalModelPath}");
                        }
                        else
                        {
                            // Если не нашли в Resources, пробуем StreamingAssets
                            string modelPath = Path.Combine(Application.streamingAssetsPath, externalModelPath);
                            if (File.Exists(modelPath))
                            {
                                byte[] modelData = File.ReadAllBytes(modelPath);
                                model = ModelLoader.LoadFromBytes(modelData);
                                Debug.Log($"Успешно загружена модель из StreamingAssets: {modelPath}");
                            }
                            else
                            {
                                // Ищем среди всех ONNX моделей в Resources
                                NNModel[] allModels = Resources.LoadAll<NNModel>("");
                                if (allModels != null && allModels.Length > 0)
                                {
                                    model = ModelLoader.Load(allModels[0]);
                                    currentModelAsset = allModels[0];
                                    Debug.Log($"Загружена первая доступная модель: {currentModelAsset.name}");
                                }
                                else
                                {
                                    Debug.LogError($"Не удалось найти модель по пути: {externalModelPath}. Переключение в демо-режим.");
                                    SwitchToDemoMode();
                                    return;
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Ошибка при загрузке модели из файла: {e.Message}. Переключение в демо-режим.");
                        SwitchToDemoMode();
                        return;
                    }
                    break;

                case SegmentationMode.Demo:
                    // В демо-режиме не загружаем модель
                    SwitchToDemoMode();
                    return;
            }

            // Проверка успешной загрузки модели
            if (model == null)
            {
                Debug.LogError("Не удалось загрузить модель. Переключение в демо-режим.");
                SwitchToDemoMode();
                return;
            }

            // Проверяем наличие нужных входов/выходов в модели
            if (!model.inputs.Any(i => i.name == inputName))
            {
                Debug.LogError($"В модели нет входа с именем {inputName}. Переключение в демо-режим.");
                SwitchToDemoMode();
                return;
            }

            if (!model.outputs.Any(o => o.name == outputName))
            {
                Debug.LogError($"В модели нет выхода с именем {outputName}. Переключение в демо-режим.");
                SwitchToDemoMode();
                return;
            }

            // Создаем воркер для выполнения модели
            WorkerFactory.Type workerType = WorkerFactory.ValidateType(WorkerFactory.Type.CSharpBurst);
            worker = WorkerFactory.CreateWorker(workerType, model);

            // Устанавливаем флаг инициализации
            isModelInitialized = true;
            useDemoMode = false;
            errorCount = 0;

            Debug.Log($"Модель сегментации успешно загружена и инициализирована. Тип воркера: {workerType}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Критическая ошибка при загрузке модели: {e.Message}. Переключение в демо-режим.");
            SwitchToDemoMode();
        }
    }

    // Создание демо-стен
    private void CreateDemoWalls()
    {
        Debug.Log("Создание демо-стен для визуализации");
        // Здесь должна быть реализация создания демонстрационных стен
    }

    // Полная реализация методов-заглушек для процесса обработки кадров и подписки на события
    private IEnumerator ProcessFrames()
    {
        while (true)
        {
            if (!isProcessing && cameraTexture != null)
            {
                // Обработка кадра
                Debug.Log("Обработка кадра");
            }

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

    // Обработчик события получения кадра с AR камеры
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
            image.Convert(conversionParams, buffer, buffer.Length);

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

    // Метод для запуска сегментации с моделью
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
            if (model == null || worker == null)
            {
                Debug.LogWarning("Модель не инициализирована. Используем демо-сегментацию.");
                return DemoSegmentation(sourceTexture);
            }

            // Масштабируем текстуру до размера входа модели
            TextureScale.Bilinear(sourceTexture, inputWidth, inputHeight);

            // Преобразование текстуры в входной тензор модели
            float[] inputData = ConvertTextureToTensor(sourceTexture, inputWidth, inputHeight, inputChannels);

            // Создаем входной тензор
            if (inputTensor != null)
            {
                inputTensor.Dispose();
            }

            // Создаем тензор с правильным форматом данных
            if (useNCHW)
            {
                // Для ONNX моделей: NCHW (batch, channels, height, width)
                inputTensor = new Tensor(1, inputChannels, inputHeight, inputWidth, inputData);
            }
            else
            {
                // Для Unity: NHWC (batch, height, width, channels)
                inputTensor = new Tensor(1, inputHeight, inputWidth, inputChannels, inputData);
            }

            // Запускаем инференс модели
            worker.Execute(inputTensor);

            // Получаем результат из выходного тензора
            Tensor outputTensor = worker.PeekOutput(outputName);

            // Создаем текстуру сегментации на основе результата модели
            Texture2D segmentationResult = CreateSegmentationTexture(outputTensor, sourceTexture.width, sourceTexture.height);

            // Возвращаем результат сегментации
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

    // Преобразует текстуру в одномерный массив нормализованных значений для тензора
    private float[] ConvertTextureToTensor(Texture2D texture, int width, int height, int channels)
    {
        // Проверяем размеры текстуры и при необходимости изменяем
        if (texture.width != width || texture.height != height)
        {
            TextureScale.Bilinear(texture, width, height);
        }

        // Получаем цвета текстуры
        Color[] pixels = texture.GetPixels();

        // Создаем массив для данных тензора
        float[] tensorData;

        // В зависимости от формата (NCHW или NHWC) организуем данные по-разному
        if (useNCHW)
        {
            // Формат NCHW (batch, channel, height, width) для ONNX моделей
            tensorData = new float[channels * height * width];

            // Заполняем массив данных для каждого канала
            for (int c = 0; c < channels; c++)
            {
                for (int h = 0; h < height; h++)
                {
                    for (int w = 0; w < width; w++)
                    {
                        int pixelIndex = h * width + w;
                        int tensorIndex = c * height * width + h * width + w;

                        // Нормализуем значения в диапазон [0, 1]
                        switch (c)
                        {
                            case 0: // R канал
                                tensorData[tensorIndex] = pixels[pixelIndex].r;
                                break;
                            case 1: // G канал
                                tensorData[tensorIndex] = pixels[pixelIndex].g;
                                break;
                            case 2: // B канал
                                tensorData[tensorIndex] = pixels[pixelIndex].b;
                                break;
                            default: // Если больше 3-х каналов, заполняем нулями
                                tensorData[tensorIndex] = 0;
                                break;
                        }
                    }
                }
            }
        }
        else
        {
            // Формат NHWC (batch, height, width, channel) для Unity
            tensorData = new float[height * width * channels];

            // Заполняем массив данных
            for (int h = 0; h < height; h++)
            {
                for (int w = 0; w < width; w++)
                {
                    int pixelIndex = h * width + w;

                    // Нормализуем RGB значения в диапазон [0, 1]
                    tensorData[pixelIndex * channels + 0] = pixels[pixelIndex].r;
                    tensorData[pixelIndex * channels + 1] = pixels[pixelIndex].g;
                    tensorData[pixelIndex * channels + 2] = pixels[pixelIndex].b;

                    // Если больше 3-х каналов, заполняем нулями
                    for (int c = 3; c < channels; c++)
                    {
                        tensorData[pixelIndex * channels + c] = 0;
                    }
                }
            }
        }

        return tensorData;
    }

    /// <summary>
    /// Создает текстуру сегментации из выходного тензора модели
    /// </summary>
    private Texture2D CreateSegmentationTexture(Tensor outputTensor, int targetWidth, int targetHeight)
    {
        // Получаем размеры выходного тензора
        int tensorWidth = outputTensor.width;
        int tensorHeight = outputTensor.height;
        int tensorChannels = outputTensor.channels;

        // Создаем текстуру для результата сегментации
        Texture2D segmentationTexture = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);

        // Заполняем текстуру в соответствии с данными тензора
        for (int y = 0; y < targetHeight; y++)
        {
            for (int x = 0; x < targetWidth; x++)
            {
                // Масштабируем координаты к размеру тензора
                int tensorX = (int)(x * (float)tensorWidth / targetWidth);
                int tensorY = (int)(y * (float)tensorHeight / targetHeight);

                // Проверяем границы
                tensorX = Mathf.Clamp(tensorX, 0, tensorWidth - 1);
                tensorY = Mathf.Clamp(tensorY, 0, tensorHeight - 1);

                // Получаем значение для пикселя
                // Для сегментационных моделей обычно выходом является тензор [batch, height, width, classes]
                float value = 0;

                // Проверяем формат выходных данных модели
                if (tensorChannels == 1) // Если это маска (1 канал)
                {
                    // Просто берем значение маски
                    value = outputTensor[0, tensorY, tensorX, 0];
                }
                else // Если это multi-channel logits (несколько каналов)
                {
                    // Для модели с multi-class output, берем нужный класс (например, "wall" или "background")
                    int channelIndex = wallClassIndex;
                    if (channelIndex >= tensorChannels)
                    {
                        Debug.LogWarning($"Указанный индекс класса стен ({wallClassIndex}) больше количества каналов в выходном тензоре ({tensorChannels}). Используем индекс 0.");
                        channelIndex = 0;
                    }

                    // Получаем значение из тензора
                    if (useNCHW)
                    {
                        // NCHW формат (batch, channel, height, width)
                        value = outputTensor[0, channelIndex, tensorY, tensorX];
                    }
                    else
                    {
                        // NHWC формат (batch, height, width, channel)
                        value = outputTensor[0, tensorY, tensorX, channelIndex];
                    }
                }

                // Применяем порог для бинаризации
                Color pixelColor = value > threshold ? wallColor : Color.clear;

                // Устанавливаем цвет пикселя
                segmentationTexture.SetPixel(x, y, pixelColor);
            }
        }

        // Применяем изменения к текстуре
        segmentationTexture.Apply();

        return segmentationTexture;
    }

    // Обновление плоскостей на основе результатов сегментации
    private IEnumerator UpdatePlanesBasedOnSegmentation()
    {
        // Получаем ARPlaneManager
        ARPlaneManager planeManager = FindObjectOfType<ARPlaneManager>();
        if (planeManager == null)
        {
            yield break;
        }

        // Если нет текстуры сегментации или она неправильно инициализирована
        if (segmentationTexture == null)
        {
            yield break;
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

    // Публичные методы, используемые другими скриптами

    // Переключение режима сегментации
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

    // Получение текущего режима
    public SegmentationMode GetCurrentMode()
    {
        return currentMode;
    }

    // Проверка использования демо-режима
    public bool IsUsingDemoMode()
    {
        return useDemoMode || currentMode == SegmentationMode.Demo;
    }

    // Обновляет статус всех AR плоскостей на основе результатов сегментации
    public int UpdatePlanesSegmentationStatus()
    {
        Debug.Log("Обновление статуса сегментации плоскостей");
        // Заглушка, возвращает количество обновленных плоскостей
        return 0;
    }

    // Получает текстуру сегментации
    public Texture2D GetSegmentationTexture()
    {
        return segmentationTexture;
    }

    // Получает процент покрытия плоскости маской сегментации
    public float GetPlaneCoverageByMask(ARPlane plane)
    {
        // Заглушка, возвращает процент покрытия
        return 0.5f;
    }

    // Включает/отключает отладочную визуализацию
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

    // Проверка состояния отладочной визуализации
    public bool IsDebugVisualizationEnabled()
    {
        return showDebugVisualisation;
    }

    // Асинхронная обработка изображения
    private IEnumerator ProcessImageAsync(Texture2D sourceTexture)
    {
        // Ждем один кадр для лучшей отзывчивости UI
        yield return null;

        try
        {
            // Обработка изображения через модель или демо-режим
            Texture2D resultTexture;
            if (useDemoMode || forceDemoMode)
            {
                resultTexture = DemoSegmentation(sourceTexture);
            }
            else
            {
                resultTexture = RunModelSegmentation(sourceTexture);
            }

            // Загружаем результат сегментации
            if (resultTexture != null)
            {
                segmentationTexture = resultTexture;

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
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Ошибка при асинхронной обработке изображения: {e.Message}");
        }
        finally
        {
            // Завершаем обработку
            isProcessing = false;
        }
    }
}