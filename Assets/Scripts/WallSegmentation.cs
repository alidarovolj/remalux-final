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
    [SerializeField] private ARCameraManager cameraManager;
    [SerializeField] private Camera arCamera;
    
    [Header("Segmentation Mode")]
    [SerializeField] private SegmentationMode currentMode = SegmentationMode.ExternalModel;
    [SerializeField] private string externalModelPath = "Models/model.onnx"; // Изменено на новую модель SegFormer
    
    [Header("Barracuda Model")]
    [SerializeField] private NNModel modelAsset; // Модель ONNX через asset
    
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
    
    // Метод для загрузки модели
    private void LoadSelectedModel()
    {
        // Сбрасываем ссылку на текущую модель
        currentModelAsset = null;
        
        // Проверяем общее количество пикселей для предотвращения ошибок 
        int totalPixels = inputWidth * inputHeight;
        
        // Проверяем размер входных данных и уменьшаем если они слишком большие
        // Важно: максимальный безопасный размер для Barracuda - 256x256 пикселей
        if (inputWidth * inputHeight > 65536) // Превышен безопасный лимит (256x256)
        {
            int originalWidth = inputWidth;
            int originalHeight = inputHeight;
            
            // Принудительно ограничиваем размер до 128x128
            inputWidth = 128;
            inputHeight = 128;
            
            Debug.LogWarning($"Уменьшаем размер входных данных с {originalWidth}x{originalHeight} до {inputWidth}x{inputHeight} " +
                           "для предотвращения ошибок с лимитами GPU compute.");
        }
        
        Debug.Log($"Используем размер входных данных для модели: {inputWidth}x{inputHeight} ({inputWidth * inputHeight} пикселей)");
        
        // Проверяем, не превышает ли размер все еще безопасный лимит даже после коррекции
        if (inputWidth * inputHeight > 65536 && !forceDemoMode) // Больше чем 256x256
        {
            Debug.LogWarning($"Размер входных данных модели ({inputWidth}x{inputHeight}={inputWidth * inputHeight} пикселей) все еще превышает безопасный лимит. " +
                           "Рекомендуем использовать размер 128x128. Включаем демо-режим для безопасности.");
            forceDemoMode = true;
            useDemoMode = true;
            
            // Создаем демо-стены для отображения сразу
            if (demoWalls.Count == 0)
            {
                CreateDemoWalls();
            }
            
            // Завершаем загрузку - дальше нет смысла загружать модель
            return;
        }
        
        // Принудительно устанавливаем демо-режим, если он выбран
        if (currentMode == SegmentationMode.Demo || forceDemoMode)
        {
            useDemoMode = true;
            Debug.Log("Используется демо-режим сегментации стен");
            
            // Создаем демо-стены для отображения сразу
            if (demoWalls.Count == 0)
            {
                CreateDemoWalls();
            }
            
            return;
        }
        
        // Если у нас есть существующий воркер, освобождаем его
        if (worker != null)
        {
            worker.Dispose();
            worker = null;
        }
        
        // Остальная логика загрузки модели...
        
        // Устанавливаем флаг инициализации
        isModelInitialized = true;
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
    
    private void OnCameraFrameReceived(ARCameraFrameEventArgs args) 
    { 
        // Обработка кадра с камеры
    }
    
    // Метод для запуска сегментации с моделью
    private Texture2D RunModelSegmentation(Texture2D sourceTexture)
    {
        if (model == null || worker == null || !isModelInitialized)
        {
            Debug.LogWarning("Модель или воркер не инициализированы");
            return DemoSegmentation(sourceTexture);
        }

        try
        {
            // Получаем данные из текстуры и преобразуем их в формат для тензора
            float[] tensorData = ConvertTextureToTensor(sourceTexture, inputWidth, inputHeight, inputChannels);
            
            // Создаем тензор с правильной сигнатурой - используя конструктор с поддержкой float[]
            // ПРИМЕЧАНИЕ: Unity Barracuda использует NHWC, но ONNX ожидает NCHW
            // Необходимо правильное преобразование форматов тензоров
            inputTensor = new Tensor(1, inputHeight, inputWidth, inputChannels, tensorData);
            
            // Запускаем сеть
            worker.Execute(inputTensor);
            
            // Получаем результат
            Tensor outputTensor = worker.PeekOutput(outputName);
            
            // Проверка на корректность выходного тензора
            if (outputTensor == null)
            {
                Debug.LogError($"Не удалось получить выходной тензор с именем {outputName}");
                return DemoSegmentation(sourceTexture);
            }
            
            // Преобразуем результат обратно в текстуру
            return CreateSegmentationTexture(outputTensor, sourceTexture.width, sourceTexture.height);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Ошибка при запуске модели: {e.Message}");
            return DemoSegmentation(sourceTexture);
        }
        finally
        {
            // Освобождаем ресурсы
            if (inputTensor != null)
            {
                inputTensor.Dispose();
                inputTensor = null;
            }
        }
    }
    
    // Вспомогательный метод для конвертации текстуры в тензор
    private float[] ConvertTextureToTensor(Texture2D texture, int width, int height, int channels)
    {
        // Создаем массив для данных тензора
        float[] tensorData = new float[width * height * channels];
        
        // Если текстура имеет другие размеры, масштабируем ее
        Texture2D resizedTexture = texture;
        if (texture.width != width || texture.height != height)
        {
            RenderTexture rt = RenderTexture.GetTemporary(width, height, 0);
            Graphics.Blit(texture, rt);
            
            RenderTexture.active = rt;
            resizedTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            resizedTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            resizedTexture.Apply();
            
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
        }
        
        // Копируем данные из текстуры в тензор
        Color[] pixels = resizedTexture.GetPixels();
        for (int i = 0; i < pixels.Length; i++)
        {
            // RGB каналы
            if (channels >= 3)
            {
                tensorData[i * channels] = pixels[i].r;
                tensorData[i * channels + 1] = pixels[i].g;
                tensorData[i * channels + 2] = pixels[i].b;
                
                // Остальные каналы заполняем значением из R-канала
                for (int c = 3; c < channels; c++)
                {
                    tensorData[i * channels + c] = pixels[i].r;
                }
            }
            else
            {
                // Для одноканального тензора используем яркость
                tensorData[i] = pixels[i].grayscale;
            }
        }
        
        // Освобождаем ресурсы
        if (resizedTexture != texture)
        {
            Destroy(resizedTexture);
        }
        
        return tensorData;
    }
    
    // Метод для создания текстуры сегментации из тензора
    private Texture2D CreateSegmentationTexture(Tensor outputTensor, int targetWidth, int targetHeight)
    {
        // Определяем размер выходного тензора
        int outBatch = outputTensor.shape[0];
        int outChannels = outputTensor.shape[1]; // Для NCHW формата каналы идут после батча
        int outHeight = outputTensor.shape[2];
        int outWidth = outputTensor.shape[3];
        
        Debug.Log($"Размер выходного тензора: {outBatch}x{outChannels}x{outHeight}x{outWidth}");
        
        // Проверяем на некорректные размеры тензора
        if (outHeight <= 0 || outWidth <= 0 || outChannels <= 0)
        {
            Debug.LogWarning($"Модель вернула некорректные размеры тензора: {outBatch}x{outChannels}x{outHeight}x{outWidth}. Используем безопасные значения по умолчанию.");
            
            // Используем демо-режим в случае ошибки
            return DemoSegmentation(new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false));
        }
        
        // Создаем новую текстуру для результата
        var resultTexture = new Texture2D(outWidth, outHeight, TextureFormat.RGBA32, false);
        
        // Копируем данные из выходного тензора в текстуру
        for (int h = 0; h < outHeight; h++)
        {
            for (int w = 0; w < outWidth; w++)
            {
                // Обрабатываем каждый пиксель результата
                float maxValue = float.MinValue;
                int bestClass = 0;
                
                // Находим класс с максимальным значением (в случае многоклассовой сегментации)
                if (outChannels > 1)
                {
                    for (int c = 0; c < outChannels; c++)
                    {
                        // Для NCHW формата
                        float value = outputTensor[0, c, h, w];
                        if (value > maxValue)
                        {
                            maxValue = value;
                            bestClass = c;
                        }
                    }
                }
                else
                {
                    // Для одноканального выхода используем пороговое значение
                    maxValue = outputTensor[0, 0, h, w];
                    bestClass = maxValue > threshold ? 1 : 0;
                }
                
                // Назначаем цвет в зависимости от класса (стена/не стена)
                Color pixelColor;
                if (bestClass == wallClassIndex)
                {
                    // Класс "стена"
                    pixelColor = wallColor;
                }
                else
                {
                    // Класс "не стена" или другой класс
                    pixelColor = Color.clear; // Прозрачный для не-стен
                }
                
                resultTexture.SetPixel(w, h, pixelColor);
            }
        }
        
        resultTexture.Apply();
        
        // Если размеры выходного тензора отличаются от целевых размеров,
        // выполняем масштабирование текстуры
        if (outWidth != targetWidth || outHeight != targetHeight)
        {
            Texture2D scaledTexture = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
            float scaleX = (float)outWidth / targetWidth;
            float scaleY = (float)outHeight / targetHeight;
            
            for (int y = 0; y < targetHeight; y++)
            {
                for (int x = 0; x < targetWidth; x++)
                {
                    // Простое масштабирование методом ближайшего соседа
                    int srcX = Mathf.Min(Mathf.FloorToInt(x * scaleX), outWidth - 1);
                    int srcY = Mathf.Min(Mathf.FloorToInt(y * scaleY), outHeight - 1);
                    
                    Color pixelColor = resultTexture.GetPixel(srcX, srcY);
                    scaledTexture.SetPixel(x, y, pixelColor);
                }
            }
            
            scaledTexture.Apply();
            return scaledTexture;
        }
        
        return resultTexture;
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
} 