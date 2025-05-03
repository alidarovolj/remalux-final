using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.UI;
using Unity.Barracuda;
using System.Linq;
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
    [SerializeField] private SegmentationMode currentMode = SegmentationMode.Demo;
    [SerializeField] private string externalModelPath = "model.onnx"; // Путь относительно StreamingAssets
    
    [Header("Barracuda Model")]
    [SerializeField] private NNModel embeddedModelAsset; // Модель, привязанная в Unity
    [SerializeField] private string inputName = "ImageTensor"; // Имя входного слоя в ONNX-модели
    [SerializeField] private string outputName = "final_result"; // Имя выходного слоя в ONNX-модели
    
    [Header("Segmentation Parameters")]
    [SerializeField] private int inputWidth = 513;
    [SerializeField] private int inputHeight = 513;
    [SerializeField] private float threshold = 0.5f;
    [SerializeField] private int wallClassIndex = 1; // Индекс класса "стена" (может отличаться в зависимости от модели)
    
    [Header("Debug & Performance")]
    [SerializeField] private bool forceDemoMode = false; // Принудительно использовать демо-режим вместо ML
    [SerializeField] private bool showDebugVisualisation = true;
    [SerializeField] private RawImage debugImage;
    [SerializeField] private float processingInterval = 0.2f; // Интервал между обработкой кадров для уменьшения нагрузки
    
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
            if (File.Exists(fullPath))
            {
                Debug.Log($"Загрузка внешней модели из: {fullPath}");
                
                // В рантайме невозможно напрямую создать NNModel из файла.
                // Здесь мы должны использовать ModelLoader для загрузки модели напрямую в объект Model
                try
                {
                    byte[] modelData = File.ReadAllBytes(fullPath);
                    model = ModelLoader.Load(modelData);
                    Debug.Log("Внешняя модель успешно загружена");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Ошибка загрузки внешней модели: {e.Message}");
                    useDemoMode = true;
                }
            }
            else
            {
                Debug.LogWarning($"Внешняя модель не найдена по пути: {fullPath}. Переключаемся на демо-режим");
                useDemoMode = true;
            }
        }
        
        // Инициализируем модель, если она доступна
        if (currentModelAsset != null || (currentMode == SegmentationMode.ExternalModel && model != null))
        {
            InitializeModel();
        }
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
        
        // Проверяем размер входного изображения
        if (inputTexture.width != inputWidth || inputTexture.height != inputHeight)
        {
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
            
            // Заменяем исходную текстуру
            inputTexture = resizedTexture;
        }
        
        // Создаем новый тензор
        try
        {
            // Используем корректный формат для входного тензора
            inputTensor = new Tensor(inputTexture, channels: 3);
            
            // Проверяем размерность входного тензора
            Debug.Log($"Входной тензор: {inputTensor.shape}");
            
            // Проверка на соответствие формата входных данных ожидаемому формату модели
            // 232960 / 3 = 77653.33, что не кратно 144, поэтому изменяем входной тензор
            
            // Выбираем размер, который должен быть кратен 144
            int modelInputSize = 144; // Размер, который ожидает модель
            int channelCount = 3; // RGB
            
            // Создаем новую текстуру с размерами, точно кратными 144
            int newWidth = 576; // 576 = 144 * 4
            int newHeight = 576; // 576 = 144 * 4
            // 576 * 576 * 3 = 995328, что делится на 144 (= 6912)
            
            // Ресайзим текстуру в соответствии с требованиями модели
            Texture2D modelInputTexture = new Texture2D(newWidth, newHeight, TextureFormat.RGB24, false);
            RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight, 24);
            Graphics.Blit(inputTexture, rt);
            RenderTexture prevRT = RenderTexture.active;
            RenderTexture.active = rt;
            modelInputTexture.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
            modelInputTexture.Apply();
            RenderTexture.active = prevRT;
            RenderTexture.ReleaseTemporary(rt);
            
            // Освобождаем предыдущий тензор для предотвращения утечек
            if (inputTensor != null)
            {
                inputTensor.Dispose();
                inputTensor = null;
            }
            
            // Создаем новый тензор с правильными размерами
            inputTensor = new Tensor(modelInputTexture, channels: channelCount);
            
            // Проверяем размерность нового тензора
            Debug.Log($"Исправленный тензор: {inputTensor.shape}");
            
            // Уничтожаем временную текстуру после создания тензора
            Destroy(modelInputTexture);
            
            // Проверка, что размер тензора соответствует ожиданиям модели
            int tensorSize = inputTensor.shape.length;
            if (tensorSize % modelInputSize != 0)
            {
                Debug.LogError($"Размер тензора {tensorSize} не кратен {modelInputSize}. Переключаемся на демо-режим.");
                inputTensor.Dispose();
                inputTensor = null;
                errorCount += 3; // Сразу увеличиваем счетчик ошибок для принудительного переключения в демо-режим
                return GenerateDemoSegmentation(inputTexture);
            }
            
            Tensor outputTensor = null;
            try
            {
                using (var output = worker.Execute(inputTensor).PeekOutput(outputName))
                {
                    outputTensor = output.DeepCopy();
                    Debug.Log($"Выходной тензор: {output.shape}");
                    
                    // Создаем результирующую текстуру для сегментированного изображения
                    Texture2D outputTexture = new Texture2D(inputWidth, inputHeight, TextureFormat.RGBA32, false);
                    
                    try
                    {
                        // Размеры выходного тензора могут отличаться от ожидаемых
                        // Получаем фактические размеры выходного тензора
                        int outHeight = output.shape.height;
                        int outWidth = output.shape.width;
                        
                        // Обрабатываем результат сегментации с учетом фактических размеров
                        for (int y = 0; y < inputHeight; y++)
                        {
                            for (int x = 0; x < inputWidth; x++)
                            {
                                // Пересчитываем координаты для выходного тензора
                                int tensorY = Mathf.Min(y * outHeight / inputHeight, outHeight - 1);
                                int tensorX = Mathf.Min(x * outWidth / inputWidth, outWidth - 1);
                                
                                float value = 0;
                                
                                // Получаем значение из тензора с проверкой на корректность индексов и размерности
                                if (output.shape.channels > 1)
                                {
                                    // Для многоканального выхода берем канал wallClassIndex
                                    if (wallClassIndex < output.shape.channels)
                                    {
                                        value = output[0, tensorY, tensorX, wallClassIndex];
                                    }
                                }
                                else
                                {
                                    // Для одноканального выхода берем единственный канал
                                    value = output[0, tensorY, tensorX, 0];
                                }
                                
                                // Применяем порог для определения стены
                                Color pixelColor = value > threshold 
                                    ? new Color(1, 1, 1, 1) // Белый для стены
                                    : new Color(0, 0, 0, 0); // Прозрачный для не-стены
                                
                                outputTexture.SetPixel(x, y, pixelColor);
                            }
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"Ошибка при обработке выходного тензора: {e.Message}");
                        
                        // Заполняем текстуру случайными значениями для демонстрационных целей
                        for (int y = 0; y < inputHeight; y++)
                        {
                            for (int x = 0; x < inputWidth; x++)
                            {
                                // Генерируем простой паттерн для отладки
                                bool isWall = (x / 50 + y / 50) % 2 == 0;
                                Color pixelColor = isWall
                                    ? new Color(1, 1, 1, 1) // Белый для стены
                                    : new Color(0, 0, 0, 0); // Прозрачный для не-стены
                                
                                outputTexture.SetPixel(x, y, pixelColor);
                            }
                        }
                    }
                    
                    outputTexture.Apply();
                    
                    // Освобождаем тензор после использования
                    if (outputTensor != null)
                    {
                        outputTensor.Dispose();
                        outputTensor = null;
                    }
                    
                    return outputTexture;
                }
            }
            catch (System.Exception e)
            {
                // Освобождаем тензоры в случае ошибки
                if (outputTensor != null)
                {
                    outputTensor.Dispose();
                    outputTensor = null;
                }
                
                if (inputTensor != null)
                {
                    inputTensor.Dispose();
                    inputTensor = null;
                }
                
                Debug.LogError($"Ошибка при обработке изображения через нейросеть: {e.Message}\n{e.StackTrace}");
                errorCount++;
                
                // Если произошло несколько ошибок подряд, переключаемся на демо-режим
                if (errorCount > 3)
                {
                    Debug.LogWarning("Слишком много ошибок нейросети. Переключаемся на демо-режим сегментации.");
                    useDemoMode = true;
                    return GenerateDemoSegmentation(inputTexture);
                }
                
                return null;
            }
        }
        catch (System.Exception e)
        {
            // Здесь обрабатываются ошибки подготовительного этапа (не связанные с выполнением модели)
            Debug.LogError($"Ошибка подготовки входных данных: {e.Message}\n{e.StackTrace}");
            
            if (inputTensor != null)
            {
                inputTensor.Dispose();
                inputTensor = null;
            }
            
            errorCount++;
            
            // Если произошло несколько ошибок подряд, переключаемся на демо-режим
            if (errorCount > 3)
            {
                Debug.LogWarning("Слишком много ошибок при подготовке данных. Переключаемся на демо-режим сегментации.");
                useDemoMode = true;
                return GenerateDemoSegmentation(inputTexture);
            }
            
            return null;
        }
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
        return demoTexture;
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

    // Добавляем публичный метод для переключения режима
    public void SwitchMode(SegmentationMode newMode)
    {
        // Если режим уже такой же, ничего не делаем
        if (currentMode == newMode)
            return;
            
        // Запоминаем новый режим
        currentMode = newMode;
        
        // Сбрасываем флаги ошибок
        useDemoMode = false;
        errorCount = 0;
        
        // Освобождаем ресурсы текущей модели
        if (worker != null)
        {
            worker.Dispose();
            worker = null;
        }
        
        if (model != null)
        {
            model = null;
        }
        
        // Загружаем новую модель
        LoadSelectedModel();
        
        Debug.Log($"Режим сегментации переключен на: {newMode}");
    }

    // Добавляем публичный метод для получения текущего режима
    public SegmentationMode GetCurrentMode()
    {
        if (forceDemoMode || useDemoMode)
            return SegmentationMode.Demo;
            
        return currentMode;
    }
    
    // Добавляем публичный метод для проверки, использует ли компонент демо-режим
    public bool IsUsingDemoMode()
    {
        return forceDemoMode || useDemoMode || currentMode == SegmentationMode.Demo;
    }
} 