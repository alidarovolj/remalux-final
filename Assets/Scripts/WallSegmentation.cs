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
    [SerializeField] private int inputWidth = 512; // Обновлено под размер входа SegFormer
    [SerializeField] private int inputHeight = 512; // Обновлено под размер входа SegFormer
    [SerializeField] private float threshold = 0.5f;
    [SerializeField] private int wallClassIndex = 1; // Обновлено на класс "wall" в наборе ADE20K (индекс 1)
    
    [Header("Debug & Performance")]
    [SerializeField] private bool forceDemoMode = false; // Принудительно использовать демо-режим вместо ML
    [SerializeField] private bool showDebugVisualisation = true;
    [SerializeField] private RawImage debugImage;
    [SerializeField] private float processingInterval = 0.3f;
    
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
        
        // Загружаем нужную модель в зависимости от выбранного режима
        LoadSelectedModel();
            
        // Подписка на событие обновления текстуры камеры
        cameraManager.frameReceived += OnCameraFrameReceived;
        
        // Создаем текстуру для сегментации
        segmentationTexture = new Texture2D(inputWidth, inputHeight, TextureFormat.RGBA32, false);
        
        // Инициализация обработки через интервал времени
        StartCoroutine(ProcessFrames());
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
        
        // Принудительно устанавливаем демо-режим, если он выбран
        if (currentMode == SegmentationMode.Demo || forceDemoMode)
        {
            useDemoMode = true;
            Debug.Log("Используется демо-режим сегментации стен");
            return;
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
                return;
            }
            
            // Используем метод ImportONNXModel для импорта модели
            bool success = ImportONNXModel(fullPath);
            
            if (!success)
            {
                Debug.LogWarning($"Не удалось импортировать ONNX модель. Переключаемся в демо-режим.");
                useDemoMode = true;
            }
        }
        
        // Инициализируем модель, если она доступна
        if (currentModelAsset != null || (currentMode == SegmentationMode.ExternalModel && model != null))
        {
            InitializeModel();
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
            // Если у нас уже есть загруженный объект модели (для внешней модели)
            if (model == null && currentModelAsset != null)
            {
                model = ModelLoader.Load(currentModelAsset);
            }
            
            if (model != null)
            {
                // Инспектируем модель для вывода информации о тензорах и операторах
                InspectModelOperators();
                
                // Получаем правильное имя выходного тензора
                outputName = GetCorrectOutputTensorName();
                Debug.Log($"Используется выходной тензор: {outputName}");
                
                worker = WorkerFactory.CreateWorker(WorkerFactory.Type.Auto, model);
                Debug.Log("Barracuda worker создан успешно");
            }
            else
            {
                Debug.LogError("Не удалось загрузить модель");
                useDemoMode = true;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Ошибка при инициализации модели: {e.Message}");
            useDemoMode = true;
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
            Texture2D segmentationResult = null;
            
            switch (currentMode)
            {
                case SegmentationMode.Demo:
                    // Демонстрационный режим без использования нейросети
                    segmentationResult = RunDemoSegmentation();
                    break;
                
                case SegmentationMode.EmbeddedModel:
                    // Используем встроенную модель
                    segmentationResult = RunModelSegmentation(cameraTexture);
                    break;
                
                case SegmentationMode.ExternalModel:
                    // Используем внешнюю модель
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
                Texture2D enhancedMask = openCVProcessor.EnhanceSegmentationMask(segmentationResult);
                if (enhancedMask != null)
                {
                    return enhancedMask;
                }
            }
            
            return segmentationResult;
                }
                catch (System.Exception e)
                {
            Debug.LogError($"Ошибка при сегментации: {e.Message}\n{e.StackTrace}");
            return null;
        }
    }
    
    /// <summary>
    /// Запускает демо-сегментацию без использования нейросети
    /// </summary>
    private Texture2D RunDemoSegmentation()
    {
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
                
                Color pixelColor = isWall
                    ? new Color(1, 1, 1, 1) // Белый для стены
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
        foreach (var wall in demoWalls)
        {
            if (wall != null)
            {
                Destroy(wall);
            }
        }
        demoWalls.Clear();
        
        // Создаем демо-стены
        
        // Левая стена
        CreateDemoWall("LeftWall", new Vector3(-2f, 0, 2f), Quaternion.Euler(0, 90, 0), 4f, 2.5f);
        
        // Правая стена
        CreateDemoWall("RightWall", new Vector3(2f, 0, 2f), Quaternion.Euler(0, -90, 0), 4f, 2.5f);
        
        // Задняя стена
        CreateDemoWall("BackWall", new Vector3(0, 0, 4f), Quaternion.Euler(0, 0, 0), 4f, 2.5f);
        
        Debug.Log($"Создано {demoWalls.Count} демонстрационных стен");
    }

    /// <summary>
    /// Создает одну демонстрационную стену
    /// </summary>
    private void CreateDemoWall(string name, Vector3 position, Quaternion rotation, float width, float height)
    {
        // Создаем объект стены
        GameObject wallObj = new GameObject($"DemoWall_{name}");
        wallObj.transform.SetParent(transform);
        wallObj.transform.position = position;
        wallObj.transform.rotation = rotation;
        
        // Добавляем компоненты для визуализации
        MeshFilter meshFilter = wallObj.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = wallObj.AddComponent<MeshRenderer>();
        
        // Создаем меш для стены
        Mesh wallMesh = CreateWallMesh(width, height);
        meshFilter.mesh = wallMesh;
        
        // Устанавливаем материал
        if (wallMaterial != null)
        {
            meshRenderer.material = wallMaterial;
        }
        else
        {
            // Если материал не задан, создаем временный
            Material tempMaterial = new Material(Shader.Find("Standard"));
            tempMaterial.color = new Color(0.8f, 0.8f, 0.8f, 0.7f);
            meshRenderer.material = tempMaterial;
        }
        
        // Добавляем ARPlaneVisualizer, чтобы использовать его функционал
        ARPlaneVisualizer visualizer = wallObj.AddComponent<ARPlaneVisualizer>();
        
        // Добавляем в список демо-стен
        demoWalls.Add(wallObj);
    }
    
    /// <summary>
    /// Запускает сегментацию с помощью встроенной модели
    /// </summary>
    private Texture2D RunModelSegmentation(Texture2D inputTexture)
    {
        if (worker == null || (currentModelAsset == null && model == null))
        {
            Debug.LogError("Модель ONNX не загружена или имеет неправильный формат");
            return RunDemoSegmentation();
        }
        
        try
        {
            // Подготавливаем входное изображение к размеру, требуемому BiseNet (960x720)
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
            
            // Запускаем инференс модели
            worker.Execute(inputTensor);
            
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
                    return RunDemoSegmentation();
                }
            }
            
            // Получаем результат
            Tensor output = worker.PeekOutput(outputName);
            Debug.Log($"Получен выходной тензор: {output.shape} (batch={output.shape[0]}, channels={output.shape[1]}, height={output.shape[2]}, width={output.shape[3]})");
            
            // Дополнительная отладка: вывод значений для стены
            if (output.shape[1] > wallClassIndex)
            {
                float minVal = float.MaxValue;
                float maxVal = float.MinValue;
                float avgVal = 0;
                int count = 0;
                
                // Анализ значений для класса стены
                for (int y = 0; y < output.shape[2]; y++)
                {
                    for (int x = 0; x < output.shape[3]; x++)
                    {
                        float val = output[0, wallClassIndex, y, x];
                        minVal = Mathf.Min(minVal, val);
                        maxVal = Mathf.Max(maxVal, val);
                        avgVal += val;
                        count++;
                    }
                }
                
                if (count > 0)
                {
                    avgVal /= count;
                    Debug.Log($"Анализ значений для класса стены: мин={minVal}, макс={maxVal}, среднее={avgVal}");
                }
            }
            
            // Создаем результирующую текстуру для сегментированного изображения
            Texture2D resultTexture = new Texture2D(inputWidth, inputHeight, TextureFormat.RGBA32, false);
            
            // Интерпретируем результат сегментации
            for (int y = 0; y < output.shape[2]; y++)
            {
                for (int x = 0; x < output.shape[3]; x++)
                {
                    // Для SegFormer обрабатываем только класс стены (индекс wallClassIndex)
                    float wallProb = output[0, wallClassIndex, y, x];
                    
                    // Определяем цвет пикселя на основе вероятности
                    Color pixelColor = Color.clear; // Прозрачный по умолчанию
                    
                    // Если вероятность стены выше порога
                    if (wallProb > threshold)
                    {
                        pixelColor = new Color(0.2f, 0.5f, 0.9f, 0.8f); // Синий для стен
                    }
                    
                    // Масштабируем координаты, так как выходная маска 128x128, а нам нужно 512x512
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
            
            // Очищаем ресурсы
            Destroy(resizedTexture);
            output.Dispose();
            
            Debug.Log("Сегментация успешно выполнена");
            return resultTexture;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Ошибка при сегментации изображения: {e.Message}\n{e.StackTrace}");
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
        
        // Перебираем все вершины меша
        for (int i = 0; i < vertices.Length; i++)
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
                
                // Для модели SegFormer проверяем только синий канал (наша маркировка стен)
                if (pixelColor.b > wallThreshold)
                {
                    maskedVertices++;
                }
            }
        }
        
        // Рассчитываем процент покрытия
        float coverage = totalVertices > 0 ? (float)maskedVertices / totalVertices : 0;
        
        // Отладка
        if (totalVertices > 0 && coverage > 0.1f)
        {
            Debug.Log($"Плоскость {plane.trackableId}: покрытие маской = {coverage:F2} ({maskedVertices}/{totalVertices})");
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
        
        // Перебираем все вершины меша
        for (int i = 0; i < vertices.Length; i++)
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
                
                // Для модели SegFormer проверяем только синий канал (наша маркировка стен)
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

    /// <summary>
    /// Обновляет статус всех AR плоскостей на основе результатов сегментации
    /// </summary>
    public void UpdatePlanesSegmentationStatus()
    {
        // Получаем ссылку на ARPlaneManager
        ARPlaneManager planeManager = FindObjectOfType<ARPlaneManager>();
        if (planeManager == null)
        {
            Debug.LogWarning("WallSegmentation: ARPlaneManager не найден в сцене");
            return;
        }
        
        // Получаем ссылку на ARPlaneController
        ARPlaneController planeController = FindObjectOfType<ARPlaneController>();
        
        int updatedCount = 0;
        
        // Перебираем все обнаруженные плоскости
        foreach (var plane in planeManager.trackables)
        {
            // Проверяем, является ли плоскость стеной по маске сегментации
            bool isWall = IsPlaneInSegmentationMask(plane, 0.3f); // Порог 30%
            
            // Получаем все визуализаторы на плоскости
            ARPlaneVisualizer[] visualizers = plane.GetComponentsInChildren<ARPlaneVisualizer>();
            
            // Если визуализаторы есть, обновляем их статус
            if (visualizers.Length > 0)
            {
                foreach (var visualizer in visualizers)
                {
                    visualizer.SetAsSegmentationPlane(isWall);
                    updatedCount++;
                }
            }
            // Если используем ARPlaneController, обновляем через него
            else if (planeController != null)
            {
                planeController.SetSegmentationFlagForPlane(plane, isWall);
                updatedCount++;
            }
        }
        
        Debug.Log($"WallSegmentation: Обновлен статус сегментации для {updatedCount} плоскостей");
    }
} 