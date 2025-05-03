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
} 