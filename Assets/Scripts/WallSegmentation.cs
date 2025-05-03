using System.Collections;
using System.Collections.Generic;
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
    [Header("AR Components")]
    [SerializeField] private ARCameraManager cameraManager;
    [SerializeField] private Camera arCamera;
    
    [Header("Barracuda Model")]
    [SerializeField] private NNModel modelAsset;
    [SerializeField] private string inputName = "ImageTensor"; // Имя входного слоя в ONNX-модели
    [SerializeField] private string outputName = "final_result"; // Имя выходного слоя в ONNX-модели
    
    [Header("Segmentation Parameters")]
    [SerializeField] private int inputWidth = 513;
    [SerializeField] private int inputHeight = 513;
    [SerializeField] private float threshold = 0.5f;
    [SerializeField] private int wallClassIndex = 1; // Индекс класса "стена" (может отличаться в зависимости от модели)
    
    [Header("Debug")]
    [SerializeField] private bool showDebugVisualisation = true;
    [SerializeField] private RawImage debugImage;
    
    // Приватные переменные
    private Texture2D cameraTexture;
    private Texture2D segmentationTexture;
    private Model model;
    private IWorker worker;
    private bool isProcessing;
    private Tensor inputTensor;
    
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
            
        // Подписка на событие обновления текстуры камеры
        cameraManager.frameReceived += OnCameraFrameReceived;
        
        // Создаем текстуру для сегментации
        segmentationTexture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGBA32, false);
    }
    
    // Инициализация модели Barracuda
    private void InitializeModel()
    {
        model = ModelLoader.Load(modelAsset);
        worker = WorkerFactory.CreateWorker(WorkerFactory.Type.Auto, model);
    }
    
    // Обработка кадра камеры
    private void OnCameraFrameReceived(ARCameraFrameEventArgs args)
    {
        if (isProcessing)
            return;
            
        StartCoroutine(ProcessCameraImage());
    }
    
    // Обработка изображения с камеры
    private IEnumerator ProcessCameraImage()
    {
        isProcessing = true;
        
        // Захват текстуры камеры
        if (cameraTexture == null || cameraTexture.width != Screen.width || cameraTexture.height != Screen.height)
        {
            cameraTexture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGBA32, false);
        }
        
        // Считываем текущее изображение с камеры
        yield return new WaitForEndOfFrame();
        cameraTexture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
        cameraTexture.Apply();
        
        // Выполняем сегментацию
        Texture2D result = RunSegmentation(cameraTexture);
        
        if (result != null)
        {
            // Обновляем текстуру сегментации
            segmentationTexture.SetPixels(0, 0, inputWidth, inputHeight, result.GetPixels());
            segmentationTexture.Apply();
            
            // Отображаем результат для отладки
            if (showDebugVisualisation && debugImage != null) 
            {
                debugImage.texture = segmentationTexture;
            }
        }
        
        isProcessing = false;
    }
    
    // Выполнение сегментации с помощью Barracuda
    private Texture2D RunSegmentation(Texture2D inputTexture)
    {
        if (modelAsset == null)
        {
            Debug.LogError("Модель ONNX не загружена или имеет неправильный формат");
            return null;
        }
        
        if (worker == null)
        {
            try
            {
                model = ModelLoader.Load(modelAsset);
                worker = WorkerFactory.CreateWorker(WorkerFactory.Type.Auto, model);
                Debug.Log("Barracuda worker создан успешно");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Ошибка при создании Barracuda worker: {e.Message}");
                return null;
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
        
        // Освобождаем предыдущий тензор, если он существует
        if (inputTensor != null)
        {
            inputTensor.Dispose();
            inputTensor = null;
        }
        
        // Создаем новый тензор
        try
        {
            // Используем корректный формат для входного тензора
            inputTensor = new Tensor(inputTexture, channels: 3);
            
            // Проверяем размерность входного тензора
            Debug.Log($"Входной тензор: {inputTensor.shape}");
            
            using (var output = worker.Execute(inputTensor).PeekOutput(outputName))
            {
                Debug.Log($"Выходной тензор: {output.shape}");
                
                // Создаем результирующую текстуру для сегментированного изображения
                Texture2D outputTexture = new Texture2D(inputWidth, inputHeight, TextureFormat.RGBA32, false);
                
                // Обрабатываем результат сегментации
                for (int y = 0; y < inputHeight; y++)
                {
                    for (int x = 0; x < inputWidth; x++)
                    {
                        float value = output[0, y, x, 0]; // Предполагается, что выход - одноканальная карта вероятностей
                        
                        // Применяем порог для определения стены
                        Color pixelColor = value > threshold 
                            ? new Color(1, 1, 1, 1) // Белый для стены
                            : new Color(0, 0, 0, 0); // Прозрачный для не-стены
                        
                        outputTexture.SetPixel(x, y, pixelColor);
                    }
                }
                
                outputTexture.Apply();
                return outputTexture;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Ошибка при обработке изображения через нейросеть: {e.Message}\n{e.StackTrace}");
            return null;
        }
    }
    
    // Освобождение ресурсов
    private void OnDestroy()
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
        
        // Освобождаем остальные ресурсы
        if (segmentationTexture != null)
        {
            Destroy(segmentationTexture);
            segmentationTexture = null;
        }
        
        if (cameraManager != null)
        {
            cameraManager.frameReceived -= OnCameraFrameReceived;
        }
    }

    // Добавляем метод OnDisable для надежного освобождения ресурсов
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
} 