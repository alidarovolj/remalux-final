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
    [SerializeField] private string externalModelPath = "BiseNet.onnx"; // Изменено на новую модель BiseNet
    
    [Header("Barracuda Model")]
    [SerializeField] private NNModel embeddedModelAsset; // Модель, привязанная в Unity
    [SerializeField] private string inputName = "input"; // Обновлено под BiseNet
    [SerializeField] private string outputName = "output"; // Обновлено под BiseNet
    
    [Header("Segmentation Parameters")]
    [SerializeField] private int inputWidth = 960; // Обновлено под размер входа BiseNet
    [SerializeField] private int inputHeight = 720; // Обновлено под размер входа BiseNet
    [SerializeField] private float threshold = 0.5f;
    [SerializeField] private int wallClassIndex = 12; // Обновлено на класс "wall" в наборе Cityscapes
    
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
        // Загружаем внешнюю модель из StreamingAssets
        else if (currentMode == SegmentationMode.ExternalModel)
        {
            string fullPath = Path.Combine(Application.streamingAssetsPath, externalModelPath);
            
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
        }
        
        yield return null;
        isProcessing = false;
    }
    
    // Выполнение сегментации с помощью Barracuda или демо-режима
    private Texture2D RunSegmentation(Texture2D inputTexture)
    {
        // Если демо-режим принудительно включен или установлен из-за ошибок
        if (forceDemoMode || useDemoMode || currentMode == SegmentationMode.Demo)
        {
            return GenerateDemoSegmentation(inputTexture);
        }
        
        // Проверка модели
        if ((currentMode == SegmentationMode.EmbeddedModel && currentModelAsset == null) || 
            (currentMode == SegmentationMode.ExternalModel && model == null))
        {
            Debug.LogError("Модель ONNX не загружена или имеет неправильный формат");
            useDemoMode = true;
            return GenerateDemoSegmentation(inputTexture);
        }
        
        if (worker == null)
        {
            try
            {
                InitializeModel();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Ошибка при создании Barracuda worker: {e.Message}");
                useDemoMode = true;
                return GenerateDemoSegmentation(inputTexture);
            }
        }
        
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
        
        // Создаем входной тензор - BiseNet ожидает формат [1,3,720,960]
        try
        {
            // Освобождаем предыдущий тензор, если он существует
            if (inputTensor != null)
            {
                inputTensor.Dispose();
                inputTensor = null;
            }
            
            // Создаем тензор в формате NCHW (batch, channels, height, width)
            inputTensor = new Tensor(resizedTexture, channels: 3);
            
            Debug.Log($"Создан входной тензор для BiseNet: {inputTensor.shape}");
            
            Tensor outputTensor = null;
            try
            {
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
                        useDemoMode = true;
                        return GenerateDemoSegmentation(inputTexture);
                    }
                }
                
                try
                {
                    var output = worker.PeekOutput(outputName);
                    Debug.Log($"Получен выходной тензор: {output.shape}");
                
                    // Создаем результирующую текстуру для сегментированного изображения
                    Texture2D resultTexture = new Texture2D(inputWidth, inputHeight, TextureFormat.RGBA32, false);
                
                    // Интерпретируем результат для BiseNet (маска классов)
                    int numClasses = output.shape[1]; // Количество классов в выходном слое
                
                    // Обрабатываем результат сегментации
                    for (int y = 0; y < inputHeight; y++)
                    {
                        for (int x = 0; x < inputWidth; x++)
                        {
                            // Находим класс с максимальной вероятностью в данной точке
                            float maxVal = float.MinValue;
                            int bestClass = 0;
                        
                            for (int c = 0; c < numClasses; c++)
                            {
                                // BiseNet возвращает scores для каждого класса в данной точке
                                float value = output[0, c, y, x];
                                if (value > maxVal)
                                {
                                    maxVal = value;
                                    bestClass = c;
                                }
                            }
                        
                            // Цветовая схема для классов Cityscapes
                            Color pixelColor = Color.black;
                        
                            // Если этот пиксель распознан как стена (wallClassIndex)
                            if (bestClass == wallClassIndex)
                            {
                                pixelColor = new Color(0.2f, 0.5f, 0.9f, 0.8f); // Синий для стен
                            }
                            else
                            {
                                // Разные цвета для других классов для отладки
                                float hue = (float)bestClass / numClasses;
                                pixelColor = Color.HSVToRGB(hue, 0.7f, 0.3f);
                                pixelColor.a = 0.3f; // Полупрозрачный для не-стен
                            }
                        
                            resultTexture.SetPixel(x, y, pixelColor);
                        }
                    }
                
                    resultTexture.Apply();
                
                    // Очищаем ресурсы
                    Destroy(resizedTexture);
                
                    Debug.Log("BiseNet сегментация успешно выполнена");
                    return resultTexture;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Ошибка при доступе к тензору {outputName}: {e.Message}");
                    
                    // Пробуем найти доступные выходные тензоры
                    Debug.Log("Доступные выходы модели:");
                    // Вместо worker.GetOutputs() используем model.outputs
                    if (model != null && model.outputs != null)
                    {
                        foreach (var output in model.outputs)
                        {
                            Debug.Log($"- {output}");
                        }
                    }
                    else
                    {
                        Debug.LogError("Не удалось получить список выходных тензоров модели");
                    }
                    
                    errorCount++;
                    // Если превышено количество ошибок, переключаемся на демо-режим
                    if (errorCount > 3)
                    {
                        Debug.LogWarning("Превышено количество ошибок сегментации. Переключение в демо-режим.");
                        useDemoMode = true;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Ошибка при выполнении инференса: {e.Message}");
                errorCount++;
                
                // Если превышено количество ошибок, переключаемся на демо-режим
                if (errorCount > 3)
                {
                    Debug.LogWarning("Превышено количество ошибок сегментации. Переключение в демо-режим.");
                    useDemoMode = true;
                }
            }
            finally
            {
                if (outputTensor != null)
                {
                    outputTensor.Dispose();
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Ошибка при подготовке тензоров: {e.Message}");
            errorCount++;
            
            // Если превышено количество ошибок, переключаемся на демо-режим
            if (errorCount > 3)
            {
                Debug.LogWarning("Превышено количество ошибок сегментации. Переключение в демо-режим.");
                useDemoMode = true;
            }
        }
        finally
        {
            if (inputTensor != null)
            {
                inputTensor.Dispose();
                inputTensor = null;
            }
        }
        
        // В случае ошибки возвращаем демо-сегментацию
        return GenerateDemoSegmentation(inputTexture);
    }
    
    // Генерация демонстрационной сегментации без использования ML
    private Texture2D GenerateDemoSegmentation(Texture2D source)
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
    
    // Обновляем метод OnDisable для надежного освобождения тензоров
    private void OnDisable()
    {
        // Освобождаем ресурсы Barracuda
        if (inputTensor != null)
        {
            inputTensor.Dispose();
            inputTensor = null;
        }
        
        if (worker != null)
        {
            worker.Dispose();
            worker = null;
        }
    }
    
    // Добавляем метод OnApplicationQuit для освобождения ресурсов
    private void OnApplicationQuit()
    {
        // Дублируем освобождение ресурсов для предотвращения утечек
        if (inputTensor != null)
        {
            inputTensor.Dispose();
            inputTensor = null;
        }
        
        if (worker != null)
        {
            worker.Dispose();
            worker = null;
        }
        
        // Очищаем текстуры
        if (segmentationTexture != null)
        {
            Destroy(segmentationTexture);
            segmentationTexture = null;
        }
        
        if (cameraTexture != null)
        {
            Destroy(cameraTexture);
            cameraTexture = null;
        }
    }

    // Освобождение ресурсов
    private void OnDestroy()
    {
        // Отписываемся от событий
        if (cameraManager != null)
        {
            cameraManager.frameReceived -= OnCameraFrameReceived;
        }
        
        // Освобождаем ресурсы Barracuda при уничтожении объекта
        if (inputTensor != null)
        {
            inputTensor.Dispose();
            inputTensor = null;
        }
        
        if (worker != null)
        {
            worker.Dispose();
            worker = null;
        }
        
        // Освобождаем объекты Unity
        if (segmentationTexture != null)
        {
            Destroy(segmentationTexture);
            segmentationTexture = null;
        }
        
        if (cameraTexture != null)
        {
            Destroy(cameraTexture);
            cameraTexture = null;
        }
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
    public bool IsPlaneInSegmentationMask(ARPlane plane, float minCoverage = 0.5f)
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
                
                // В зависимости от типа маски можем проверять разные условия
                // Для бинарной маски: белый цвет (или высокое значение канала) = стена
                if (pixelColor.r > threshold || pixelColor.g > threshold || pixelColor.b > threshold)
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
                
                // В зависимости от типа маски можем проверять разные условия
                if (pixelColor.r > threshold || pixelColor.g > threshold || pixelColor.b > threshold)
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
    /// Создает визуализацию обнаруженных стен
    /// </summary>
    /// <param name="prediction">Информация о сегментации</param>
    private void CreateVisualization(WallPrediction prediction)
    {
        if (prediction == null || prediction.walls == null || prediction.walls.Count == 0)
        {
            Debug.LogWarning("Нет стен для визуализации");
            return;
        }
        
        // Очищаем предыдущие стены, если они есть
        ClearCurrentWalls();
        
        // Создаем новые объекты для каждой стены
        foreach (var wallData in prediction.walls)
        {
            // Создаем GameObject для стены
            GameObject wallObj = new GameObject($"SegmentedWall_{wallData.id}");
            
            // Устанавливаем родителя
            wallObj.transform.SetParent(transform);
            
            // Устанавливаем позицию и поворот
            wallObj.transform.position = wallData.position;
            wallObj.transform.rotation = wallData.rotation;
            
            // Добавляем компоненты
            MeshFilter meshFilter = wallObj.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = wallObj.AddComponent<MeshRenderer>();
            
            // Создаем меш для стены
            Mesh wallMesh = CreateWallMesh(wallData.width, wallData.height);
            meshFilter.mesh = wallMesh;
            
            // Устанавливаем материал
            meshRenderer.material = wallMaterial;
            
            // Добавляем ARPlaneVisualizer
            ARPlaneVisualizer visualizer = wallObj.AddComponent<ARPlaneVisualizer>();
            
            // Помечаем как плоскость сегментации
            visualizer.SetAsSegmentationPlane(true);
            
            // Добавляем в список текущих стен
            currentWalls.Add(wallObj);
        }
        
        Debug.Log($"Создано {currentWalls.Count} сегментированных стен");
    }

    /// <summary>
    /// Очищает текущие объекты стен
    /// </summary>
    private void ClearCurrentWalls()
    {
        if (currentWalls != null)
        {
            foreach (var wall in currentWalls)
            {
                if (wall != null)
                {
                    Destroy(wall);
                }
            }
            
            currentWalls.Clear();
        }
        else
        {
            currentWalls = new List<GameObject>();
        }
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
} 