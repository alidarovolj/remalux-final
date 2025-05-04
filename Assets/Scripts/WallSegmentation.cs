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
/// Класс для хранения данных о стене, обнаруженной через сегментацию
/// </summary>
[Serializable]
public class WallData
{
    public int id;
    public Vector3 position;
    public Quaternion rotation;
    public float width;
    public float height;
}

/// <summary>
/// Класс для передачи результатов сегментации стен
/// </summary>
[Serializable]
public class WallPrediction
{
    public List<WallData> walls = new List<WallData>();
}

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
    [SerializeField] private NNModel embeddedModelAsset; // Модель, привязанная в Unity
    [SerializeField] private string inputName = "input"; // Для SegFormer
    [SerializeField] private string outputName = "logits"; // Для SegFormer
    
    [Header("Segmentation Parameters")]
    [SerializeField] private int inputWidth = 128; // Уменьшено до 128 для предотвращения ошибок
    [SerializeField] private int inputHeight = 128; // Уменьшено до 128 для предотвращения ошибок
    [SerializeField] private float threshold = 0.5f;
    [SerializeField] private int wallClassIndex = 9; // Используем класс с индексом 9 для стен
    
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
    
    // Инициализация
    private void Start()
    {
        // Сбрасываем флаг демо-режима при запуске (если не установлен принудительно)
        useDemoMode = forceDemoMode;
        
        if (cameraManager == null)
            cameraManager = UnityEngine.Object.FindAnyObjectByType<ARCameraManager>();
            
        if (arCamera == null)
        {
            // Ищем камеру через XROrigin
            var xrOrigin = UnityEngine.Object.FindAnyObjectByType<XROrigin>();
            if (xrOrigin != null && xrOrigin.Camera != null)
            {
                arCamera = xrOrigin.Camera;
            }
            else
            {
                // Попробуем найти камеру в сцене
                arCamera = Camera.main;
                if (arCamera == null)
                {
                    Debug.LogError("Не удалось найти AR камеру для сегментации стен");
                }
            }
        }
        
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
        cameraManager.frameReceived += OnCameraFrameReceived;
        
        // Создаем текстуру для сегментации
        segmentationTexture = new Texture2D(inputWidth, inputHeight, TextureFormat.RGBA32, false);
        
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
    
    // Дополнительный метод для импорта ONNX модели
    private bool ImportONNXModel(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogWarning($"ONNX модель не найдена по пути: {filePath}");
            return false;
        }

        try
        {
            Debug.Log($"Импорт ONNX модели из: {filePath}");
            
            // Считываем ONNX файл в байты
            byte[] onnxBytes = File.ReadAllBytes(filePath);
            
            // Используем конвертер ONNX для преобразования в модель Barracuda
            Unity.Barracuda.ONNX.ONNXModelConverter converter = 
                new Unity.Barracuda.ONNX.ONNXModelConverter(
                    optimizeModel: true, 
                    treatErrorsAsWarnings: false, 
                    forceArbitraryBatchSize: true);
            
            model = converter.Convert(onnxBytes);
            
            Debug.Log("ONNX модель успешно импортирована и конвертирована в формат Barracuda");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Ошибка импорта ONNX модели: {e.Message}\n{e.StackTrace}");
            return false;
        }
    }

    // Загрузка модели в зависимости от выбранного режима
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
        
        // Загружаем встроенную модель
        if (currentMode == SegmentationMode.EmbeddedModel)
        {
            if (embeddedModelAsset != null)
            {
                currentModelAsset = embeddedModelAsset;
                Debug.Log("Используется встроенная модель сегментации");
            }
            else
            {
                Debug.LogWarning("Встроенная модель не найдена. Переключаемся на демо-режим");
                useDemoMode = true;
                
                // Создаем демо-стены для отображения
                if (demoWalls.Count == 0)
                {
                    CreateDemoWalls();
                }
                
                return;
            }
        }
        // Загружаем внешнюю модель из StreamingAssets или Assets/Models
        else if (currentMode == SegmentationMode.ExternalModel)
        {
            string streamingAssetsPath = Path.Combine(Application.streamingAssetsPath, externalModelPath);
            string assetsPath = Path.Combine(Application.dataPath, externalModelPath);
            string fullPath = "";
            
            // Проверяем, существует ли файл в StreamingAssets
            if (File.Exists(streamingAssetsPath))
            {
                fullPath = streamingAssetsPath;
                Debug.Log($"Модель найдена в StreamingAssets: {fullPath}");
            }
            // Затем проверяем в директории Assets
            else if (File.Exists(assetsPath))
            {
                fullPath = assetsPath;
                Debug.Log($"Модель найдена в Assets: {fullPath}");
            }
            // Если файл не найден, но указан относительный путь, ищем в корне проекта
            else if (File.Exists(externalModelPath))
            {
                fullPath = externalModelPath;
                Debug.Log($"Модель найдена по относительному пути: {fullPath}");
            }
            else
            {
                Debug.LogWarning($"ONNX модель не найдена по путям: {streamingAssetsPath}, {assetsPath}, {externalModelPath}");
                Debug.LogWarning($"Переключаемся в демо-режим.");
                useDemoMode = true;
                
                // Создаем демо-стены для отображения
                if (demoWalls.Count == 0)
                {
                    CreateDemoWalls();
                }
                
                return;
            }
            
            // Пытаемся импортировать модель с обработкой исключений
            try
            {
                // Используем метод ImportONNXModel для импорта модели
                bool success = ImportONNXModel(fullPath);
                
                if (!success)
                {
                    Debug.LogWarning($"Не удалось импортировать ONNX модель. Переключаемся в демо-режим.");
                    useDemoMode = true;
                    
                    // Создаем демо-стены для отображения
                    if (demoWalls.Count == 0)
                    {
                        CreateDemoWalls();
                    }
                    
                    return;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Ошибка при импорте ONNX модели: {ex.Message}. Переключаемся в демо-режим.");
                useDemoMode = true;
                
                // Создаем демо-стены для отображения
                if (demoWalls.Count == 0)
                {
                    CreateDemoWalls();
                }
                
                return;
            }
        }
        
        // Инициализируем модель, если она доступна
        if (currentModelAsset != null || (currentMode == SegmentationMode.ExternalModel && model != null))
        {
            try
            {
                InitializeModel();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Ошибка при инициализации модели: {ex.Message}. Переключаемся в демо-режим.");
                useDemoMode = true;
                
                // Создаем демо-стены для отображения
                if (demoWalls.Count == 0)
                {
                    CreateDemoWalls();
                }
            }
        }
    }
    
    // Дополнительный метод для получения и вывода имен выходных тензоров модели
    private string GetCorrectOutputTensorName()
    {
        if (model == null)
        {
            Debug.LogError("Модель не загружена, невозможно получить имена выходных тензоров");
            return outputName; // возвращаем имя по умолчанию, хотя оно скорее всего будет неверным
        }

        // Выводим все доступные выходы модели
        Debug.Log($"Доступные выходные тензоры: {string.Join(", ", model.outputs)}");
        
        // Если выходов нет, возвращаем значение по умолчанию
        if (model.outputs.Count == 0)
        {
            Debug.LogError("Модель не имеет выходных тензоров!");
            return outputName;
        }
        
        // Возвращаем первое имя выходного тензора из списка
        return model.outputs[0];
    }
    
    // Дополнительный метод для инспекции модели и вывода информации о тензорах
    private void InspectModelOperators()
    {
        if (model == null)
        {
            Debug.LogError("Модель не загружена, невозможно определить операторы");
            return;
        }

        Debug.Log($"=== Операторы модели ({model.layers.Count}) ===");
        foreach (var layer in model.layers)
        {
            Debug.Log($"Оператор: {layer.name}, Тип: {layer.type}");
            Debug.Log($"  Входы: {string.Join(", ", layer.inputs)}");
            
            // Вывод информации о выходах, используя тип string
            if (layer.outputs != null && layer.outputs.Length > 0)
            {
                Debug.Log($"  Выходы: {layer.outputs.Length} тензоров");
            }
        }

        Debug.Log($"=== Входные тензоры модели ===");
        Debug.Log(string.Join(", ", model.inputs));

        Debug.Log($"=== Выходные тензоры модели ===");
        Debug.Log(string.Join(", ", model.outputs));
    }
    
    // Инициализация модели Barracuda
    private void InitializeModel()
    {
        if (worker != null)
        {
            worker.Dispose();
            worker = null;
        }
        
        try
        {
            // Если у нас уже есть загруженный объект модели из импорта внешней модели
            if (model == null && currentModelAsset != null)
            {
                model = ModelLoader.Load(currentModelAsset);
            }
            
            if (model == null)
            {
                Debug.LogError("Не удалось загрузить модель сегментации");
                SwitchToDemoMode();
                return;
            }
            
            // Анализируем входы и выходы модели
            Debug.Log($"Модель загружена: {model.outputs.Count} выходов, {model.inputs.Count} входов");
            
            if (model.inputs.Count > 0)
            {
                string inputInfo = "Входы модели: ";
                foreach (var input in model.inputs)
                {
                    inputInfo += $"{input.name} (формат: {input.shape}), ";
                }
                Debug.Log(inputInfo);
                
                // Используем первый вход, если не указан явно
                if (string.IsNullOrEmpty(inputName) && model.inputs.Count > 0)
                {
                    inputName = model.inputs[0].name;
                }
            }
            
            if (model.outputs.Count > 0)
            {
                string outputInfo = "Выходы модели: ";
                foreach (var output in model.outputs)
                {
                    outputInfo += $"{output}, ";
                }
                Debug.Log(outputInfo);
                
                // Используем первый выход, если не указан явно
                if (string.IsNullOrEmpty(outputName) && model.outputs.Count > 0)
                {
                    outputName = model.outputs[0];
                }
            }
            
            // Сначала пробуем создать GPU воркер
            try
            {
                Debug.Log("Пытаемся создать GPU воркер для Barracuda...");
                var workerType = WorkerFactory.Type.ComputePrecompiled;
                worker = WorkerFactory.CreateWorker(workerType, model);
                Debug.Log("GPU воркер успешно создан");
            }
            catch (System.Exception gpuEx)
            {
                Debug.LogWarning($"Не удалось создать GPU воркер: {gpuEx.Message}. Пробуем CPU.");
                
                try
                {
                    // Если GPU не доступен, используем CPU
                    var workerType = WorkerFactory.Type.CSharp;
                    worker = WorkerFactory.CreateWorker(workerType, model);
                    Debug.Log("CPU воркер успешно создан");
                }
                catch (System.Exception cpuEx)
                {
                    Debug.LogError($"Не удалось создать CPU воркер: {cpuEx.Message}");
                    SwitchToDemoMode();
                    return;
                }
            }
            
            Debug.Log($"Модель сегментации успешно инициализирована с размером входа {inputWidth}x{inputHeight}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Ошибка при инициализации модели: {e.Message}\n{e.StackTrace}");
            SwitchToDemoMode();
        }
    }
    
    // Обработка кадра камеры
    private void OnCameraFrameReceived(ARCameraFrameEventArgs args)
    {
        // Захват текстуры камеры
        if (cameraTexture == null || cameraTexture.width != Screen.width || cameraTexture.height != Screen.height)
        {
            if (cameraTexture != null)
                Destroy(cameraTexture);
                
            cameraTexture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGBA32, false);
        }
        
        // Считываем текущее изображение с камеры
        var cameraParams = cameraManager.GetComponent<ARCameraBackground>();
        if (cameraParams != null && cameraParams.material != null)
        {
            // Используем текстуру из материала AR камеры если доступна
            // Это может быть эффективнее чем ReadPixels
        }
        else
        {
            // Используем старый подход с чтением пикселей
            cameraTexture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            cameraTexture.Apply();
        }
    }
    
    // Процесс обработки кадров с определенным интервалом
    private IEnumerator ProcessFrames()
    {
        while (true)
        {
            if (!isProcessing && cameraTexture != null)
            {
                StartCoroutine(ProcessCameraImage());
            }
            
            yield return new WaitForSeconds(processingInterval);
        }
    }
    
    // Обработка изображения с камеры
    private IEnumerator ProcessCameraImage()
    {
        isProcessing = true;
        
        // Пропускаем обработку если камеры нет или текстура не создана
        if (cameraTexture == null)
        {
            isProcessing = false;
            yield break;
        }
        
        // Выполняем сегментацию
        Texture2D result = RunSegmentation(cameraTexture);
        
        if (result != null)
        {
            // Обновляем текстуру сегментации с проверкой размеров
            if (segmentationTexture == null || segmentationTexture.width != inputWidth || segmentationTexture.height != inputHeight)
            {
                if (segmentationTexture != null)
                {
                    Destroy(segmentationTexture);
                }
                segmentationTexture = new Texture2D(inputWidth, inputHeight, TextureFormat.RGBA32, false);
            }
            
            // Копируем пиксели с проверкой размеров
            if (result.width == inputWidth && result.height == inputHeight)
            {
                segmentationTexture.SetPixels(result.GetPixels());
            }
            else
            {
                // Для разных размеров используем блит
                RenderTexture rt = RenderTexture.GetTemporary(inputWidth, inputHeight, 0);
                Graphics.Blit(result, rt);
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                segmentationTexture.ReadPixels(new Rect(0, 0, inputWidth, inputHeight), 0, 0);
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }
            
            segmentationTexture.Apply();
            
            // Отображаем результат для отладки
            if (showDebugVisualisation && debugImage != null) 
            {
                debugImage.texture = segmentationTexture;
            }
            
            // Очищаем временную текстуру результата
            Destroy(result);
            
            // Обновляем статус всех плоскостей на основе результатов сегментации
            UpdatePlanesSegmentationStatus();

            // Выводим отладочную информацию о количестве обнаруженных стен
            Debug.Log($"Сегментация завершена, обрабатываем плоскости для определения стен...");
        }
        
        yield return null;
        isProcessing = false;
    }
    
    /// <summary>
    /// Запускает сегментацию изображения
    /// </summary>
    /// <param name="cameraTexture">Текстура с камеры</param>
    /// <returns>Обработанная текстура с сегментацией</returns>
    private Texture2D RunSegmentation(Texture2D cameraTexture)
    {
        if (cameraTexture == null)
        {
            return null;
        }
        
        try
        {
            // Если мы в режиме демо или произошла ошибка с моделью, используем демо-сегментацию
            if (useDemoMode || currentMode == SegmentationMode.Demo || forceDemoMode)
            {
                return RunDemoSegmentation();
            }
            
            Texture2D segmentationResult = null;
            
            switch (currentMode)
            {
                case SegmentationMode.Demo:
                    // Демонстрационный режим без использования нейросети
                    segmentationResult = RunDemoSegmentation();
                    break;
                
                case SegmentationMode.EmbeddedModel:
                    // Используем встроенную модель
                    if (worker == null)
                    {
                        Debug.LogError("Модель ONNX не загружена или имеет неправильный формат");
                        SwitchToDemoMode();
                        return RunDemoSegmentation();
                    }
                    segmentationResult = RunModelSegmentation(cameraTexture);
                    break;
                
                case SegmentationMode.ExternalModel:
                    // Используем внешнюю модель
                    if (worker == null)
                    {
                        Debug.LogError("Модель ONNX не загружена или имеет неправильный формат");
                        SwitchToDemoMode();
                        return RunDemoSegmentation();
                    }
                    segmentationResult = RunExternalModelSegmentation(cameraTexture);
                    break;
                
                default:
                    // По умолчанию запускаем демо
                    segmentationResult = RunDemoSegmentation();
                    break;
            }
            
            // Применяем OpenCV для улучшения качества сегментации
            OpenCVProcessor openCVProcessor = GetComponent<OpenCVProcessor>();
            if (openCVProcessor != null && segmentationResult != null)
            {
                Debug.Log("WallSegmentation: Применяем постобработку OpenCV к результату сегментации");
                Texture2D enhancedMask = openCVProcessor.EnhanceSegmentationMask(segmentationResult);
                if (enhancedMask != null)
                {
                    // Освобождаем память от оригинального результата сегментации
                    if (segmentationResult != enhancedMask)
                    {
                        Destroy(segmentationResult);
                    }
                    return enhancedMask;
                }
            }
            else if (openCVProcessor == null)
            {
                Debug.LogWarning("WallSegmentation: OpenCVProcessor не найден, постобработка не будет применена");
            }
            
            return segmentationResult;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Ошибка при сегментации: {e.Message}\n{e.StackTrace}");
            // При любой ошибке возвращаем демо-режим
            SwitchToDemoMode();
            return RunDemoSegmentation();
        }
    }
    
    /// <summary>
    /// Запускает демо-сегментацию без использования нейросети
    /// </summary>
    private Texture2D RunDemoSegmentation()
    {
        // Если мы перешли в демо-режим не по выбору пользователя
        if (currentMode != SegmentationMode.Demo && !forceDemoMode && useDemoMode)
        {
            Debug.LogWarning("WallSegmentation: Используем демо-режим из-за ошибки нейросети");
        }

        // Создаем текстуру соответствующего размера
        Texture2D demoTexture = new Texture2D(inputWidth, inputHeight, TextureFormat.RGBA32, false);
        
        // Генерируем паттерн-сетку для демонстрации
        for (int y = 0; y < inputHeight; y++)
        {
            for (int x = 0; x < inputWidth; x++)
            {
                // Простой способ определения "стен" - вертикальные полосы по краям экрана
                bool isWall = false;
                
                // Левая и правая границы экрана считаются стенами
                if (x < inputWidth * 0.15f || x > inputWidth * 0.85f)
                {
                    isWall = true;
                }
                // Или шахматный паттерн в центре для демонстрации
                else if (x > inputWidth * 0.4f && x < inputWidth * 0.6f && 
                         y > inputHeight * 0.3f && y < inputHeight * 0.7f)
                {
                    isWall = (x / 30 + y / 30) % 2 == 0;
                }
                
                // Цвет для стен аналогичен цвету из нейросети - синий
                Color pixelColor = isWall
                    ? new Color(0.2f, 0.5f, 0.9f, 0.8f) // Синий для стен
                    : new Color(0, 0, 0, 0); // Прозрачный для не-стены
                
                demoTexture.SetPixel(x, y, pixelColor);
            }
        }
        
        demoTexture.Apply();
        
        // Создаем демо-стены, если их еще нет
        if (demoWalls.Count == 0)
        {
            CreateDemoWalls();
        }
        
        // После создания демонстрационных стен помечаем их как плоскости сегментации
        foreach (var wall in demoWalls)
        {
            if (wall != null)
            {
                ARPlaneVisualizer visualizer = wall.GetComponentInChildren<ARPlaneVisualizer>();
                if (visualizer != null)
                {
                    visualizer.SetAsSegmentationPlane(true);
                }
            }
        }
        
        return demoTexture;
    }
    
    /// <summary>
    /// Создает демонстрационные стены для визуализации
    /// </summary>
    private void CreateDemoWalls()
    {
        // Очищаем существующие демо-стены
        ClearDemoWalls();
        
        // Проверяем, инициализирован ли список
        if (demoWalls == null)
        {
            demoWalls = new List<GameObject>();
        }
        
        // Количество стен для создания
        int numWalls = 3;
        
        // Создаем новые демо-стены
        for (int i = 0; i < numWalls; i++)
        {
            // Создаем игровой объект для демо-стены
            GameObject wall = new GameObject($"DemoWall_{i}");
            wall.transform.SetParent(transform);
            
            // Добавляем компонент для визуализации стены
            WallVisualization wallVis = wall.AddComponent<WallVisualization>();
            
            // Устанавливаем параметры стены
            float angle = (360f / numWalls) * i;
            float distance = 2.0f;
            
            // Вычисляем позицию стены вокруг центра
            float x = Mathf.Sin(angle * Mathf.Deg2Rad) * distance;
            float z = Mathf.Cos(angle * Mathf.Deg2Rad) * distance;
            
            wall.transform.position = new Vector3(x, 0f, z);
            wall.transform.LookAt(Vector3.zero);
            
            // Задаем размеры стены
            float width = 1.5f;
            float height = 2.0f;
            
            // Создаем меш для стены
            MeshFilter meshFilter = wall.AddComponent<MeshFilter>();
            Mesh mesh = new Mesh();
            meshFilter.mesh = mesh;
            
            // Устанавливаем вершины и треугольники для меша
            Vector3[] vertices = new Vector3[4]
            {
                new Vector3(-width/2, -height/2, 0),
                new Vector3(width/2, -height/2, 0),
                new Vector3(-width/2, height/2, 0),
                new Vector3(width/2, height/2, 0)
            };
            
            int[] triangles = new int[6]
            {
                0, 2, 1,
                2, 3, 1
            };
            
            Vector2[] uvs = new Vector2[4]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(0, 1),
                new Vector2(1, 1)
            };
            
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            mesh.RecalculateNormals();
            
            // Добавляем рендерер и устанавливаем материал
            MeshRenderer renderer = wall.AddComponent<MeshRenderer>();
            if (wallMaterial != null)
            {
                renderer.material = wallMaterial;
            }
            else
            {
                // Создаем простой материал, если основной материал не найден
                Material defaultMaterial = new Material(Shader.Find("Standard"));
                defaultMaterial.color = new Color(0.2f, 0.5f, 0.9f, 0.8f);
                renderer.material = defaultMaterial;
            }
            
            // Добавляем демо-стену в список
            demoWalls.Add(wall);
        }
        
        Debug.Log($"Создано {numWalls} демонстрационных стен");
    }
    
    /// <summary>
    /// Очищает все созданные демонстрационные стены
    /// </summary>
    private void ClearDemoWalls()
    {
        if (demoWalls != null)
        {
            foreach (var wall in demoWalls)
            {
                if (wall != null)
                {
                    Destroy(wall);
                }
            }
            
            demoWalls.Clear();
        }
    }
    
    /// <summary>
    /// Запускает сегментацию с помощью встроенной модели
    /// </summary>
    private Texture2D RunModelSegmentation(Texture2D inputTexture)
    {
        if (worker == null || (currentModelAsset == null && model == null))
        {
            Debug.LogError("Модель ONNX не загружена или имеет неправильный формат");
            SwitchToDemoMode();
            return RunDemoSegmentation();
        }
        
        try
        {
            // Подготавливаем входное изображение к размеру, требуемому моделью
            Texture2D resizedTexture = new Texture2D(inputWidth, inputHeight, TextureFormat.RGB24, false);
            
            // Создаем временный RenderTexture для масштабирования
            RenderTexture rt = RenderTexture.GetTemporary(inputWidth, inputHeight, 24);
            Graphics.Blit(inputTexture, rt);
            
            // Сохраняем текущий RenderTexture
            RenderTexture activeRT = RenderTexture.active;
            RenderTexture.active = rt;
            
            // Копируем содержимое в новую текстуру
            resizedTexture.ReadPixels(new Rect(0, 0, inputWidth, inputHeight), 0, 0);
            resizedTexture.Apply();
            
            // Восстанавливаем активный RenderTexture
            RenderTexture.active = activeRT;
            RenderTexture.ReleaseTemporary(rt);
            
            // Освобождаем предыдущий тензор, если он существует
            if (inputTensor != null)
            {
                inputTensor.Dispose();
                inputTensor = null;
            }
        
            // Создаем тензор в формате NCHW (batch, channels, height, width)
            inputTensor = new Tensor(resizedTexture, channels: 3);
            
            Debug.Log($"Создан входной тензор для модели: {inputTensor.shape}");
            
            try
            {
                // Запускаем инференс модели с обработкой исключений
                worker.Execute(inputTensor);
            }
            catch (System.Exception barracudaEx)
            {
                Debug.LogError($"Ошибка выполнения Barracuda: {barracudaEx.Message}. Переключение в демо-режим");
                Destroy(resizedTexture);
                
                // Если произошла ошибка при выполнении модели, переключаемся в демо-режим
                SwitchToDemoMode();
                
                return RunDemoSegmentation();
            }
            
            // Проверяем, существует ли указанный выходной тензор
            if (string.IsNullOrEmpty(outputName))
            {
                // Берем первый доступный выходной тензор, если имя не задано
                if (model.outputs.Count > 0)
                {
                    outputName = model.outputs[0];
                    Debug.Log($"Используем первый доступный выходной тензор: {outputName}");
                }
                else
                {
                    Debug.LogError("Модель не имеет выходных тензоров");
                    SwitchToDemoMode();
                    return RunDemoSegmentation();
                }
            }
            
            // Получаем результат
            Tensor output = null;
            try
            {
                output = worker.PeekOutput(outputName);
                Debug.Log($"Получен выходной тензор: {output.shape} (batch={output.shape[0]}, channels={output.shape[1]}, height={output.shape[2]}, width={output.shape[3]})");
                
                // Если это первый запуск или есть флаг отладки, выполняем анализ классов
                if (errorCount == 0 || enableDebugLogs)
                {
                    AnalyzeOutputClasses(output, resizedTexture);
                }
            }
            catch (System.Exception outEx)
            {
                Debug.LogError($"Ошибка при получении выходного тензора: {outEx.Message}. Переключение в демо-режим");
                Destroy(resizedTexture);
                SwitchToDemoMode();
                return RunDemoSegmentation();
            }
            
            // Результирующая текстура по умолчанию
            Texture2D resultTexture = null;
            
            try
            {
                // Создаем результирующую текстуру для сегментированного изображения
                resultTexture = new Texture2D(inputWidth, inputHeight, TextureFormat.RGBA32, false);
                
                // Интерпретируем результат сегментации
                for (int y = 0; y < output.shape[2]; y++)
                {
                    for (int x = 0; x < output.shape[3]; x++)
                    {
                        // Проверяем канал соответствующий классу стены (wallClassIndex)
                        // Если wallClassIndex больше чем количество каналов, используем безопасный индекс
                        float wallProb = 0;
                        
                        if (wallClassIndex < output.shape[1])
                        {
                            // Используем указанный класс стены
                            wallProb = output[0, wallClassIndex, y, x];
                            
                            if (enableDebugLogs && x == output.shape[3]/2 && y == output.shape[2]/2)
                            {
                                Debug.Log($"Значение класса стены (индекс {wallClassIndex}) в центре: {wallProb}");
                            }
                        }
                        else
                        {
                            // Если индекс слишком большой, смотрим только первые несколько классов для поиска максимума
                            for (int c = 0; c < Mathf.Min(output.shape[1], 20); c++)
                            {
                                float val = output[0, c, y, x];
                                if (val > wallProb)
                                {
                                    wallProb = val;
                                }
                            }
                            
                            if (enableDebugLogs && x == 0 && y == 0 && !useDemoMode)
                            {
                                Debug.LogWarning($"Индекс класса стены ({wallClassIndex}) превышает количество классов модели ({output.shape[1]}). Используем максимальный класс.");
                            }
                        }
                        
                        // Определяем цвет пикселя на основе вероятности
                        Color pixelColor = Color.clear; // Прозрачный по умолчанию
                        
                        // Если вероятность стены выше порога
                        if (wallProb > threshold)
                        {
                            pixelColor = new Color(0.2f, 0.5f, 0.9f, 0.8f); // Синий для стен
                        }
                        
                        // Масштабируем координаты для соответствия размеру выходной текстуры
                        int destX = (x * inputWidth) / output.shape[3];
                        int destY = (y * inputHeight) / output.shape[2];
                        
                        // Определяем размер блока для масштабирования
                        int blockWidth = inputWidth / output.shape[3];
                        int blockHeight = inputHeight / output.shape[2];
                        
                        // Устанавливаем цвет для блока пикселей
                        for (int oy = 0; oy < blockHeight && (destY + oy) < inputHeight; oy++)
                        {
                            for (int ox = 0; ox < blockWidth && (destX + ox) < inputWidth; ox++)
                            {
                                resultTexture.SetPixel(destX + ox, destY + oy, pixelColor);
                            }
                        }
                    }
                }
                
                resultTexture.Apply();
            }
            catch (System.Exception pixelEx)
            {
                Debug.LogError($"Ошибка при обработке пикселей: {pixelEx.Message}");
                // Если не удалось обработать результат, освобождаем ресурсы и переходим в демо-режим
                if (resultTexture != null)
                {
                    Destroy(resultTexture);
                }
                SwitchToDemoMode();
                return RunDemoSegmentation();
            }
            finally
            {
                // Очищаем ресурсы
                Destroy(resizedTexture);
                if (output != null)
                {
                    output.Dispose();
                }
            }
            
            Debug.Log("Сегментация успешно выполнена");
            return resultTexture;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Критическая ошибка при сегментации изображения: {e.Message}\n{e.StackTrace}");
            SwitchToDemoMode();
            return RunDemoSegmentation();
        }
        finally
        {
            if (inputTensor != null)
            {
                inputTensor.Dispose();
                inputTensor = null;
            }
        }
    }
    
    /// <summary>
    /// Запускает сегментацию с помощью внешней модели
    /// </summary>
    private Texture2D RunExternalModelSegmentation(Texture2D inputTexture)
    {
        // Для внешней модели используем тот же метод, что и для встроенной
        return RunModelSegmentation(inputTexture);
    }

    /// <summary>
    /// Создает меш для стены
    /// </summary>
    private Mesh CreateWallMesh(float width, float height)
    {
        Mesh mesh = new Mesh();
        
        // Создаем вершины (простой прямоугольник)
        Vector3[] vertices = new Vector3[4]
        {
            new Vector3(-width/2, -height/2, 0),
            new Vector3(width/2, -height/2, 0),
            new Vector3(width/2, height/2, 0),
            new Vector3(-width/2, height/2, 0)
        };
        
        // УФ-координаты
        Vector2[] uv = new Vector2[4]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(1, 1),
            new Vector2(0, 1)
        };
        
        // Треугольники
        int[] triangles = new int[6]
        {
            0, 1, 2,
            2, 3, 0
        };
        
        // Установка данных в меш
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        
        // Пересчитываем нормали и границы
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        
        return mesh;
    }

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
        useDemoMode = false;
        
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
    
    // Получение статуса загрузки модели
    public string GetModelStatus()
    {
        if (IsUsingDemoMode())
        {
            if (currentMode == SegmentationMode.Demo)
            {
                return "Демо-режим (выбран пользователем)";
            }
            else
            {
                return "Демо-режим (ошибка загрузки модели)";
            }
        }
        else if (currentMode == SegmentationMode.EmbeddedModel)
        {
            return "Встроенная модель";
        }
        else if (currentMode == SegmentationMode.ExternalModel)
        {
            return "Внешняя модель";
        }
        
        return "Неизвестно";
    }

    // Привязка маски сегментации к AR плоскостям
    public bool IsPlaneInSegmentationMask(ARPlane plane, float minCoverage = 0.3f)
    {
        if (segmentationTexture == null || plane == null)
            return false;
            
        // Получаем меш плоскости
        Mesh planeMesh = plane.GetComponent<MeshFilter>()?.mesh;
        if (planeMesh == null)
            return false;
            
        // Получаем вершины плоскости
        Vector3[] vertices = planeMesh.vertices;
        int[] triangles = planeMesh.triangles;
        
        if (vertices == null || vertices.Length == 0 || triangles == null || triangles.Length == 0)
            return false;
            
        // Счетчики для определения покрытия
        int totalVertices = 0;
        int maskedVertices = 0;
        
        // Используем более низкий порог для SegFormer
        float wallThreshold = 0.2f;
        
        // Добавляем отладочную информацию
        if (enableDebugLogs && debugPositioning)
        {
            Debug.Log($"Проверка плоскости {plane.trackableId}: размер маски сегментации {segmentationTexture.width}x{segmentationTexture.height}, " +
                     $"размер экрана {Screen.width}x{Screen.height}, вершин в меше: {vertices.Length}");
        }
        
        // Для отладки - тестовые точки центра плоскости
        Vector3 planeCenter = plane.transform.TransformPoint(Vector3.zero);
        Vector3 screenPosCenter = arCamera.WorldToScreenPoint(planeCenter);
        
        if (enableDebugLogs && debugPositioning)
        {
            Debug.Log($"Центр плоскости: мировая позиция {planeCenter}, экранная позиция {screenPosCenter}");
            
            // Отладочные лучи от камеры до центра плоскости
            Debug.DrawLine(arCamera.transform.position, planeCenter, Color.yellow, 1.0f);
        }
        
        // Расширенная проверка по нескольким стратегическим точкам плоскости, не только вершинам
        Vector3[] strategicPoints = new Vector3[5]; // Центр + 4 направления
        strategicPoints[0] = Vector3.zero; // Центр
        
        // Вычисляем крайние точки плоскости относительно центра
        Vector3 boundsExtents = planeMesh.bounds.extents;
        strategicPoints[1] = new Vector3(boundsExtents.x, 0, 0); // Вправо от центра
        strategicPoints[2] = new Vector3(-boundsExtents.x, 0, 0); // Влево от центра
        strategicPoints[3] = new Vector3(0, boundsExtents.y, 0); // Вверх от центра
        strategicPoints[4] = new Vector3(0, -boundsExtents.y, 0); // Вниз от центра
        
        // Проверяем стратегические точки
        foreach (var localPoint in strategicPoints)
        {
            Vector3 worldPos = plane.transform.TransformPoint(localPoint);
            Vector3 screenPos = arCamera.WorldToScreenPoint(worldPos);
            
            // Пропускаем точки за камерой
            if (screenPos.z <= 0)
                continue;
                
            totalVertices++;
            
            // Преобразуем экранные координаты в координаты текстуры сегментации
            int texX = Mathf.RoundToInt((screenPos.x / Screen.width) * segmentationTexture.width);
            int texY = Mathf.RoundToInt((screenPos.y / Screen.height) * segmentationTexture.height);
            
            // Проверяем, находится ли точка в пределах текстуры
            if (texX >= 0 && texX < segmentationTexture.width && texY >= 0 && texY < segmentationTexture.height)
            {
                // Получаем цвет пикселя и проверяем, относится ли он к классу стены
                Color pixelColor = segmentationTexture.GetPixel(texX, texY);
                
                // Для модели SegFormer проверяем канал синего (наша маркировка стен)
                if (pixelColor.b > wallThreshold)
                {
                    maskedVertices++;
                    if (enableDebugLogs && debugPositioning)
                    {
                        Debug.Log($"Найдена точка стены: texX={texX}, texY={texY}, цвет={pixelColor}, стратег. точка={localPoint}");
                    }
                }
            }
        }
        
        // Перебираем все вершины меша для полного покрытия
        for (int i = 0; i < vertices.Length; i += 5) // Проверяем каждую пятую вершину для оптимизации
        {
            Vector3 worldPos = plane.transform.TransformPoint(vertices[i]);
            Vector3 screenPos = arCamera.WorldToScreenPoint(worldPos);
            
            // Пропускаем вершины за камерой
            if (screenPos.z <= 0)
                continue;
                
            totalVertices++;
            
            // Преобразуем экранные координаты в координаты текстуры сегментации
            int texX = Mathf.RoundToInt((screenPos.x / Screen.width) * segmentationTexture.width);
            int texY = Mathf.RoundToInt((screenPos.y / Screen.height) * segmentationTexture.height);
            
            // Проверяем, находится ли точка в пределах текстуры
            if (texX >= 0 && texX < segmentationTexture.width && texY >= 0 && texY < segmentationTexture.height)
            {
                // Получаем цвет пикселя и проверяем, относится ли он к классу стены
                Color pixelColor = segmentationTexture.GetPixel(texX, texY);
                
                // Для модели SegFormer проверяем канал синего (наша маркировка стен)
                if (pixelColor.b > wallThreshold)
                {
                    maskedVertices++;
                }
            }
        }
        
        // Рассчитываем процент покрытия
        float coverage = totalVertices > 0 ? (float)maskedVertices / totalVertices : 0;
        
        // Отладка
        if (totalVertices > 0)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"Плоскость {plane.trackableId}: покрытие маской = {coverage:F2} ({maskedVertices}/{totalVertices})");
            }
        }
        
        // Возвращаем true, если покрытие превышает минимальный порог
        return coverage >= minCoverage;
    }
    
    // Получить процент покрытия плоскости маской сегментации
    public float GetPlaneCoverageByMask(ARPlane plane)
    {
        if (segmentationTexture == null || plane == null)
            return 0f;
            
        // Получаем меш плоскости
        Mesh planeMesh = plane.GetComponent<MeshFilter>()?.mesh;
        if (planeMesh == null)
            return 0f;
            
        // Получаем вершины плоскости
        Vector3[] vertices = planeMesh.vertices;
        
        if (vertices == null || vertices.Length == 0)
            return 0f;
            
        // Счетчики для определения покрытия
        int totalVertices = 0;
        int maskedVertices = 0;
        
        // Используем более низкий порог для SegFormer
        float wallThreshold = 0.2f;
        
        // Проверка стратегических точек плоскости (аналогично IsPlaneInSegmentationMask)
        Vector3[] strategicPoints = new Vector3[5]; // Центр + 4 направления
        strategicPoints[0] = Vector3.zero; // Центр
        
        // Вычисляем крайние точки плоскости относительно центра
        Vector3 boundsExtents = planeMesh.bounds.extents;
        strategicPoints[1] = new Vector3(boundsExtents.x, 0, 0); // Вправо от центра
        strategicPoints[2] = new Vector3(-boundsExtents.x, 0, 0); // Влево от центра
        strategicPoints[3] = new Vector3(0, boundsExtents.y, 0); // Вверх от центра
        strategicPoints[4] = new Vector3(0, -boundsExtents.y, 0); // Вниз от центра
        
        // Проверяем стратегические точки
        foreach (var localPoint in strategicPoints)
        {
            Vector3 worldPos = plane.transform.TransformPoint(localPoint);
            Vector3 screenPos = arCamera.WorldToScreenPoint(worldPos);
            
            // Пропускаем точки за камерой
            if (screenPos.z <= 0)
                continue;
                
            totalVertices++;
            
            // Преобразуем экранные координаты в координаты текстуры сегментации
            int texX = Mathf.RoundToInt((screenPos.x / Screen.width) * segmentationTexture.width);
            int texY = Mathf.RoundToInt((screenPos.y / Screen.height) * segmentationTexture.height);
            
            // Проверяем, находится ли точка в пределах текстуры
            if (texX >= 0 && texX < segmentationTexture.width && texY >= 0 && texY < segmentationTexture.height)
            {
                // Получаем цвет пикселя и проверяем, относится ли он к классу стены
                Color pixelColor = segmentationTexture.GetPixel(texX, texY);
                
                // Для модели SegFormer проверяем канал синего (наша маркировка стен)
                if (pixelColor.b > wallThreshold)
                {
                    maskedVertices++;
                }
            }
        }
        
        // Перебираем все вершины меша для полного покрытия
        for (int i = 0; i < vertices.Length; i += 5) // Проверяем каждую пятую вершину для оптимизации
        {
            Vector3 worldPos = plane.transform.TransformPoint(vertices[i]);
            Vector3 screenPos = arCamera.WorldToScreenPoint(worldPos);
            
            // Пропускаем вершины за камерой
            if (screenPos.z <= 0)
                continue;
                
            totalVertices++;
            
            // Преобразуем экранные координаты в координаты текстуры сегментации
            int texX = Mathf.RoundToInt((screenPos.x / Screen.width) * segmentationTexture.width);
            int texY = Mathf.RoundToInt((screenPos.y / Screen.height) * segmentationTexture.height);
            
            // Проверяем, находится ли точка в пределах текстуры
            if (texX >= 0 && texX < segmentationTexture.width && texY >= 0 && texY < segmentationTexture.height)
            {
                // Получаем цвет пикселя и проверяем, относится ли он к классу стены
                Color pixelColor = segmentationTexture.GetPixel(texX, texY);
                
                // Для модели SegFormer проверяем канал синего (наша маркировка стен)
                if (pixelColor.b > wallThreshold)
                {
                    maskedVertices++;
                }
            }
        }
        
        // Рассчитываем процент покрытия
        return totalVertices > 0 ? (float)maskedVertices / totalVertices : 0;
    }
    
    // Получить текущую текстуру сегментации (для других компонентов)
    public Texture2D GetSegmentationTexture()
    {
        return segmentationTexture;
    }

    // Проверка состояния отладочной визуализации
    public bool IsDebugVisualizationEnabled()
    {
        return showDebugVisualisation;
    }
    
    // Включение/выключение отладочной визуализации
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

    // Подписка на события обнаружения плоскостей
    private void SubscribeToPlaneEvents()
    {
        ARPlaneManager planeManager = FindObjectOfType<ARPlaneManager>();
        if (planeManager != null)
        {
            // Подписываемся на событие изменения плоскостей
            planeManager.planesChanged += OnPlanesChanged;
            Debug.Log("WallSegmentation: Подписка на событие обнаружения плоскостей выполнена");
        }
        else
        {
            Debug.LogWarning("WallSegmentation: ARPlaneManager не найден, автоматическое обновление при новых плоскостях недоступно");
        }
    }

    // Отписка от событий при уничтожении объекта
    private void OnDestroy()
    {
        ARPlaneManager planeManager = FindObjectOfType<ARPlaneManager>();
        if (planeManager != null)
        {
            planeManager.planesChanged -= OnPlanesChanged;
        }
    }

    // Обработчик события обнаружения новых плоскостей
    private void OnPlanesChanged(ARPlanesChangedEventArgs args)
    {
        if (args.added != null && args.added.Count > 0)
        {
            Debug.Log($"WallSegmentation: Обнаружено {args.added.Count} новых AR плоскостей, запускаем обновление сегментации");
            // Не запускаем обновление мгновенно, добавляем небольшую задержку для стабилизации
            StartCoroutine(DelayedSegmentationUpdate());
        }
    }

    // Корутина для задержки первого обновления сегментации
    private IEnumerator DelayedFirstSegmentationUpdate()
    {
        // Ждем 2 секунды после инициализации, чтобы AR успел обнаружить плоскости
        yield return new WaitForSeconds(2.0f);
        UpdatePlanesSegmentationStatus();
        Debug.Log("WallSegmentation: Выполнено первое обновление статуса сегментации");
    }

    // Перегрузка метода с указанием времени задержки
    private IEnumerator DelayedFirstSegmentationUpdate(float delayTime)
    {
        Debug.Log($"WallSegmentation: Ожидание {delayTime} сек для инициализации плоскостей и модели...");
        
        // Ждем указанное время после инициализации
        yield return new WaitForSeconds(delayTime);
        
        // Запускаем принудительную обработку одного кадра камеры
        if (!useDemoMode && worker != null)
        {
            Debug.Log("WallSegmentation: Запуск принудительной обработки кадра...");
            yield return StartCoroutine(ProcessCameraImage());
            
            // Дополнительная задержка для завершения обработки
            yield return new WaitForSeconds(0.5f);
        }
        
        // Обновляем статус всех плоскостей
        int updatedCount = UpdatePlanesSegmentationStatus();
        Debug.Log($"WallSegmentation: Выполнено первое обновление статуса сегментации, обработано {updatedCount} плоскостей");
    }

    // Корутина для задержки обновления сегментации
    private IEnumerator DelayedSegmentationUpdate()
    {
        // Ждем 1 секунду после обнаружения новых плоскостей
        yield return new WaitForSeconds(1.0f);
        UpdatePlanesSegmentationStatus();
    }

    /// <summary>
    /// Публичный метод для обновления статуса плоскостей сегментации, который может быть вызван из других скриптов
    /// </summary>
    public void RunPlanesSegmentationUpdate()
    {
        // Проверяем, инициализирована ли сегментация
        if (segmentationTexture == null)
        {
            Debug.LogWarning("WallSegmentation: Текстура сегментации не инициализирована, создаем...");
            segmentationTexture = new Texture2D(inputWidth, inputHeight, TextureFormat.RGBA32, false);
            ClearSegmentationTexture();
        }
        
        // Запускаем обновление статуса плоскостей
        StartCoroutine(DelayedSegmentationUpdate());
    }
    
    /// <summary>
    /// Обновляет статус всех AR плоскостей на основе результатов сегментации
    /// Метод теперь возвращает количество обработанных плоскостей
    /// </summary>
    public int UpdatePlanesSegmentationStatus()
    {
        // Получаем ссылку на ARPlaneManager
        ARPlaneManager planeManager = FindObjectOfType<ARPlaneManager>();
        if (planeManager == null)
        {
            Debug.LogWarning("WallSegmentation: ARPlaneManager не найден в сцене");
            return 0;
        }
        
        // Используем новый метод для применения маски к плоскостям
        int updatedCount = AssignMaskToARPlanes();
        
        // Включаем отладочные настройки для всех визуализаторов
        EnableDebugForAllVisualizers();
        
        return updatedCount;
    }
    
    /// <summary>
    /// Применяет маску сегментации к обнаруженным AR плоскостям
    /// </summary>
    /// <returns>Количество обновленных плоскостей</returns>
    private int AssignMaskToARPlanes()
    {
        // Получаем ссылку на ARPlaneManager
        ARPlaneManager planeManager = FindObjectOfType<ARPlaneManager>();
        if (planeManager == null)
        {
            Debug.LogWarning("WallSegmentation: AR Plane Manager отсутствует");
            return 0;
        }
        
        int updatedCount = 0;
        int wallCount = 0;
        
        // Получаем контроллер AR плоскостей, если он есть
        ARPlaneController planeController = null;
        if (useARPlaneController)
        {
            planeController = FindObjectOfType<ARPlaneController>();
            if (planeController == null && enableDebugLogs)
            {
                Debug.LogWarning("WallSegmentation: Не найден ARPlaneController, хотя был запрошен его поиск");
            }
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"WallSegmentation: Начинаем обработку плоскостей с маской сегментации. Режим: {currentMode}, UseDemoMode: {useDemoMode}");
        }
        
        // Список для проверки конфликтующих областей
        Dictionary<ARPlane, float> planeCoverages = new Dictionary<ARPlane, float>();
        
        // Первый проход - определяем степень покрытия каждой плоскости маской сегментации
        foreach (var plane in planeManager.trackables)
        {
            // Получаем процент покрытия плоскости маской сегментации
            float coverage = GetPlaneCoverageByMask(plane);
            
            // Сохраняем информацию о покрытии
            planeCoverages[plane] = coverage;
            
            if (enableDebugLogs && coverage > 0.1f)
            {
                Debug.Log($"WallSegmentation: Плоскость {plane.trackableId} имеет покрытие {coverage:F2}");
            }
        }
        
        // Второй проход - отфильтровываем плоскости с наибольшим покрытием и отмечаем их как стены
        foreach (var plane in planeManager.trackables)
        {
            // Проверяем ориентацию плоскости
            bool isVerticalPlane = IsVerticalPlane(plane);
            
            // Получаем процент покрытия плоскости маской из словаря
            float coverage;
            planeCoverages.TryGetValue(plane, out coverage);
            
            // Определяем, является ли плоскость стеной
            bool isWall = coverage >= 0.3f && isVerticalPlane;
            
            // Для вертикальных плоскостей с хорошим покрытием - отмечаем как стены
            if (isWall)
            {
                wallCount++;
                if (enableDebugLogs)
                {
                    float angleWithUp = Vector3.Angle(plane.normal, Vector3.up);
                    Debug.Log($"WallSegmentation: Плоскость {plane.trackableId} определена как стена " +
                              $"(Выравнивание: {plane.alignment}, Нормаль: {plane.normal}, Угол с вертикалью: {angleWithUp}°, Покрытие: {coverage:F2})");
                }
            }
            else if (coverage >= 0.3f && !isVerticalPlane)
            {
                // Подозрительная ситуация - маска показывает стену, но плоскость не вертикальная
                if (enableDebugLogs)
                {
                    float angleWithUp = Vector3.Angle(plane.normal, Vector3.up);
                    Debug.LogWarning($"WallSegmentation: Плоскость {plane.trackableId} определена как стена по сегментации (покрытие {coverage:F2}), " +
                                     $"но имеет невертикальное выравнивание {plane.alignment}! Угол с вертикалью: {angleWithUp}°");
                }
                
                // Если угол с вертикалью между 60° и 120°, считаем её стеной даже если alignment не Vertical
                if (IsNearlyVertical(plane.normal))
                {
                    if (enableDebugLogs)
                    {
                        Debug.Log($"WallSegmentation: Плоскость {plane.trackableId} принудительно считаем стеной из-за вертикальной нормали");
                    }
                    // Отмечаем как стену
                    isWall = true;
                    wallCount++;
                }
            }
            
            // Получаем все визуализаторы на плоскости
            ARPlaneVisualizer[] visualizers = plane.GetComponentsInChildren<ARPlaneVisualizer>();
            
            // Если визуализаторы есть, обновляем их статус
            if (visualizers.Length > 0)
            {
                foreach (var visualizer in visualizers)
                {
                    // Устанавливаем текущую плоскость как стену
                    visualizer.SetAsSegmentationPlane(isWall);
                    
                    // Обновляем дополнительные настройки визуализатора
                    if (isWall)
                    {
                        // Включаем отладочную визуализацию только если debugPositioning включен
                        bool shouldShowDebug = debugPositioning && enableDebugLogs;
                        visualizer.SetDebugMode(shouldShowDebug);
                        
                        // Расширяем стены, если это стандартные стены
                        visualizer.SetExtendWalls(true);
                    }
                    
                    updatedCount++;
                }
            }
            // Если используем ARPlaneController, обновляем через него
            else if (planeController != null)
            {
                planeController.SetSegmentationFlagForPlane(plane, isWall);
                updatedCount++;
            }
            
            // Применяем коррекцию масштаба для стен
            if (isWall)
            {
                AdjustPlaneScaleAfterSegmentation(plane, isWall);
            }
        }
        
        Debug.Log($"WallSegmentation: Обновлен статус сегментации для {updatedCount} плоскостей, обнаружено {wallCount} стен");
        
        // Обновляем информацию AR Debug Manager, если он есть
        UpdateARDebugInfo(wallCount);
        
        return updatedCount;
    }
    
    /// <summary>
    /// Проверяет, является ли плоскость вертикальной (стеной) по её выравниванию и нормали
    /// </summary>
    private bool IsVerticalPlane(ARPlane plane)
    {
        // Проверяем выравнивание плоскости
        bool isVerticalAlignment = plane.alignment == PlaneAlignment.Vertical;
        
        // Проверяем также нестандартно выровненные плоскости
        bool isNonAxisAligned = plane.alignment == PlaneAlignment.NotAxisAligned;
        
        // Если плоскость не оси или вертикальная, проверяем угол с вертикалью
        if (isNonAxisAligned || (!isVerticalAlignment && plane.normal != Vector3.zero))
        {
            return IsNearlyVertical(plane.normal);
        }
        
        return isVerticalAlignment;
    }
    
    /// <summary>
    /// Проверяет, близок ли вектор нормали к вертикальной плоскости (угол с вертикалью между 60° и 120°)
    /// </summary>
    private bool IsNearlyVertical(Vector3 normal)
    {
        if (normal == Vector3.zero) return false;
        
        // Вычисляем угол между нормалью и вектором вверх
        float angleWithUp = Vector3.Angle(normal, Vector3.up);
        
        // Считаем вертикальным, если угол с вертикалью большой (близко к 90°)
        // Обычно вертикальные плоскости имеют угол 90° ± 30°
        return angleWithUp > 60f && angleWithUp < 120f;
    }

    // Обновляет информацию AR Debug Manager о количестве стен
    private void UpdateARDebugInfo(int wallCount)
    {
        ARDebugManager debugManager = FindObjectOfType<ARDebugManager>();
        if (debugManager != null)
        {
            // Проверяем наличие метода для обновления информации о стенах
            var updateWallsMethod = debugManager.GetType().GetMethod("UpdateWallsDetected");
            if (updateWallsMethod != null)
            {
                updateWallsMethod.Invoke(debugManager, new object[] { wallCount });
            }
        }
    }
    
    /// <summary>
    /// Корректирует масштаб и позицию плоскостей AR после сегментации
    /// </summary>
    /// <param name="plane">Обрабатываемая плоскость</param>
    /// <param name="isWall">Является ли плоскость стеной</param>
    private void AdjustPlaneScaleAfterSegmentation(ARPlane plane, bool isWall)
    {
        if (plane == null) return;
        
        // Только для стен выполняем дополнительную коррекцию
        if (!isWall) return;
        
        // Получаем все визуализаторы плоскости
        ARPlaneVisualizer[] visualizers = plane.GetComponentsInChildren<ARPlaneVisualizer>();
        foreach (var visualizer in visualizers)
        {
            if (visualizer == null) continue;
            
            // Получаем текущий транформ
            Transform visTrans = visualizer.transform;
            
            // Для вертикальных стен корректируем масштаб
            if (IsVerticalPlane(plane))
            {
                // Проверяем возможные проблемы с масштабом
                if (visTrans.localScale.y < 1.0f)
                {
                    // Минимальная высота стены 2 метра
                    visTrans.localScale = new Vector3(
                        visTrans.localScale.x,
                        Mathf.Max(2.0f, visTrans.localScale.y),
                        visTrans.localScale.z
                    );
                    
                    if (enableDebugLogs)
                    {
                        Debug.Log($"WallSegmentation: Коррекция высоты стены для плоскости {plane.trackableId} до {visTrans.localScale.y}");
                    }
                }
                
                // Корректируем позицию стены, чтобы нижний край был на уровне пола
                Vector3 position = visTrans.position;
                
                // Вычисляем высоту стены относительно пола
                // Высчитываем расстояние от камеры до пола
                float cameraHeight = arCamera.transform.position.y;
                
                // Корректируем позицию, чтобы нижняя точка стены была на уровне пола
                position.y = cameraHeight - (visTrans.localScale.y / 2);
                
                // Применяем позицию с небольшим смещением от вертикали, чтобы не было пересечений с полом
                position.y += 0.05f; // 5 см над полом
                
                if (enableDebugLogs && debugPositioning)
                {
                    Debug.Log($"WallSegmentation: Коррекция позиции стены {plane.trackableId} - старая Y: {visTrans.position.y}, новая Y: {position.y}");
                }
                
                visTrans.position = position;
            }
        }
    }

    /// <summary>
    /// Обрабатывает уведомление о новых AR плоскостях от ARPlaneController
    /// </summary>
    /// <param name="planeCount">Количество новых плоскостей</param>
    public void HandleNewARPlanes(int planeCount)
    {
        Debug.Log($"WallSegmentation: Получено уведомление о {planeCount} новых AR плоскостях");
        
        // Запускаем обновление сегментации с задержкой
        StartCoroutine(DelayedSegmentationUpdate());
    }

    // Новый метод для принудительного включения отладочных настроек для всех визуализаторов плоскостей
    private void EnableDebugForAllVisualizers()
    {
        // Получаем ссылку на ARPlaneManager
        ARPlaneManager planeManager = FindObjectOfType<ARPlaneManager>();
        if (planeManager == null) return;
        
        if (enableDebugLogs)
        {
            Debug.Log("WallSegmentation: Принудительное включение отладочных настроек для всех визуализаторов плоскостей");
        }
        
        // Проходим по всем текущим плоскостям
        foreach (var plane in planeManager.trackables)
        {
            if (plane == null) continue;
            
            // Получаем все визуализаторы на плоскости
            ARPlaneVisualizer[] visualizers = plane.GetComponentsInChildren<ARPlaneVisualizer>();
            foreach (var visualizer in visualizers)
            {
                if (visualizer == null) continue;
                
                // Включаем отладочную визуализацию
                visualizer.SetDebugMode(true);
                
                // Для вертикальных плоскостей также включаем расширение стен
                if (IsVerticalPlane(plane))
                {
                    visualizer.SetExtendWalls(true);
                    
                    if (enableDebugLogs)
                    {
                        float angleWithUp = Vector3.Angle(plane.normal, Vector3.up);
                        Debug.Log($"WallSegmentation: Настройка визуализатора для плоскости {plane.trackableId} " + 
                                  $"(Нормаль: {plane.normal}, Угол с вертикалью: {angleWithUp}°)");
                    }
                }
                
                // Принудительно обновляем визуализацию
                visualizer.UpdateVisual();
            }
        }
    }

    // Метод для очистки текстуры сегментации
    private void ClearSegmentationTexture()
    {
        if (segmentationTexture != null)
        {
            for (int y = 0; y < segmentationTexture.height; y++)
            {
                for (int x = 0; x < segmentationTexture.width; x++)
                {
                    segmentationTexture.SetPixel(x, y, Color.clear);
                }
            }
            segmentationTexture.Apply();
        }
    }

    // Переключение в демо-режим из-за ошибки с моделью
    private void SwitchToDemoMode()
    {
        // Переключаемся в демо-режим и сбрасываем флаги
        useDemoMode = true;
        
        // Освобождаем ресурсы нейросети, если они существуют
        if (worker != null)
        {
            worker.Dispose();
            worker = null;
        }
        
        if (model != null)
        {
            model = null;
        }
        
        // Создаем демо-стены для визуализации
        if (demoWalls.Count == 0)
        {
            CreateDemoWalls();
        }
        
        Debug.LogWarning("WallSegmentation: Переключение в демо-режим из-за ошибки с нейросетью");
    }
    
    // Проверка доступности OpenCV для улучшения сегментации
    private bool CheckOpenCVAvailability()
    {
        OpenCVProcessor processor = GetComponent<OpenCVProcessor>();
        if (processor == null)
        {
            Debug.LogWarning("OpenCVProcessor не найден, некоторые функции сегментации будут недоступны");
            return false;
        }
        
        return processor.IsOpenCVAvailable();
    }

    /// <summary>
    /// Анализирует выходные данные модели для поиска наилучшего класса для стен
    /// </summary>
    private void AnalyzeOutputClasses(Tensor output, Texture2D originalImage)
    {
        if (output == null || output.shape[1] <= 1)
        {
            Debug.LogWarning("Невозможно проанализировать классы - выходной тензор некорректный.");
            return;
        }
        
        // Вывод информации о размерности выходного тензора
        Debug.Log($"Анализ классов модели: выходной тензор имеет {output.shape[1]} классов");
        
        // Для каждого класса вычисляем среднюю активацию
        float[] classActivations = new float[output.shape[1]];
        
        // Проходим по всему тензору
        for (int c = 0; c < output.shape[1]; c++)
        {
            float sum = 0;
            int count = 0;
            
            // Выбираем случайные точки для быстрого анализа
            for (int sample = 0; sample < 100; sample++)
            {
                int y = UnityEngine.Random.Range(0, output.shape[2]);
                int x = UnityEngine.Random.Range(0, output.shape[3]);
                
                sum += output[0, c, y, x];
                count++;
            }
            
            classActivations[c] = (count > 0) ? sum / count : 0;
        }
        
        // Находим класс с максимальной активацией
        int maxActivationClass = 0;
        float maxActivation = classActivations[0];
        
        // Находим класс с активацией около 0.5 (часто подходит для стен)
        int bestWallClass = 0;
        float bestWallScore = float.MaxValue;
        
        for (int c = 0; c < classActivations.Length; c++)
        {
            // Ищем максимальную активацию
            if (classActivations[c] > maxActivation)
            {
                maxActivation = classActivations[c];
                maxActivationClass = c;
            }
            
            // Ищем активацию ближе всего к 0.5 - часто это стены
            float distFromHalf = Mathf.Abs(classActivations[c] - 0.5f);
            if (distFromHalf < bestWallScore)
            {
                bestWallScore = distFromHalf;
                bestWallClass = c;
            }
        }
        
        Debug.Log($"Результаты анализа классов модели:");
        Debug.Log($"  Класс с максимальной активацией: {maxActivationClass} (значение: {maxActivation})");
        Debug.Log($"  Наиболее вероятный класс для стен: {bestWallClass} (значение: {classActivations[bestWallClass]})");
        Debug.Log($"  Текущий выбранный класс стены: {wallClassIndex}");
    }
}

/// <summary>
/// Класс для управления визуализацией стены
/// </summary>
public class WallVisualization : MonoBehaviour
{
    // Материал стены
    private Material wallMaterial;
    
    // Исходный цвет материала
    private Color originalColor;
    
    // Флаг, указывающий, выбрана ли стена
    private bool isSelected = false;
    
    /// <summary>
    /// Инициализация компонента
    /// </summary>
    private void Start()
    {
        // Получаем ссылку на MeshRenderer
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer != null && renderer.material != null)
        {
            // Сохраняем ссылку на материал
            wallMaterial = renderer.material;
            
            // Сохраняем исходный цвет
            originalColor = wallMaterial.color;
        }
    }
    
    /// <summary>
    /// Установка цвета стены
    /// </summary>
    public void SetColor(Color color)
    {
        // Получаем ссылку на MeshRenderer
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer != null && renderer.material != null)
        {
            // Устанавливаем новый цвет
            renderer.material.color = color;
            
            // Обновляем исходный цвет
            originalColor = color;
        }
    }
    
    /// <summary>
    /// Выделение стены
    /// </summary>
    public void Select()
    {
        // Получаем ссылку на MeshRenderer
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer != null && renderer.material != null)
        {
            // Сохраняем исходный цвет, если еще не сохранен
            if (!isSelected)
            {
                originalColor = renderer.material.color;
            }
            
            // Устанавливаем цвет выделения
            Color highlightColor = new Color(
                originalColor.r * 1.5f,
                originalColor.g * 1.5f,
                originalColor.b * 1.5f,
                originalColor.a
            );
            
            renderer.material.color = highlightColor;
            isSelected = true;
        }
    }
    
    /// <summary>
    /// Снятие выделения со стены
    /// </summary>
    public void Deselect()
    {
        // Получаем ссылку на MeshRenderer
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer != null && renderer.material != null && isSelected)
        {
            // Восстанавливаем исходный цвет
            renderer.material.color = originalColor;
            isSelected = false;
        }
    }
} 