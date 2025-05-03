using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.UI;
using Unity.Barracuda;
using System.Linq;

public class WallSegmentation : MonoBehaviour
{
    [Header("AR Components")]
    [SerializeField] private ARCameraManager cameraManager;
    [SerializeField] private ARSessionOrigin sessionOrigin;
    
    [Header("Barracuda Model")]
    [SerializeField] private NNModel modelAsset;
    [SerializeField] private string inputName = "ImageTensor";
    [SerializeField] private string outputName = "final_result";
    
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
    
    // Инициализация
    private void Start()
    {
        if (cameraManager == null)
            cameraManager = FindObjectOfType<ARCameraManager>();
            
        if (sessionOrigin == null)
            sessionOrigin = FindObjectOfType<ARSessionOrigin>();
            
        // Подписка на событие обновления текстуры камеры
        cameraManager.frameReceived += OnCameraFrameReceived;
        
        // Инициализация барракуды
        InitializeModel();
        
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
        RunSegmentation(cameraTexture);
        
        isProcessing = false;
    }
    
    // Выполнение сегментации с помощью Barracuda
    private void RunSegmentation(Texture2D inputTexture)
    {
        // Изменение размера и нормализация входных данных
        Tensor input = PrepareInput(inputTexture);
        
        // Выполнение инференса
        worker.Execute(input);
        
        // Получение результатов
        Tensor output = worker.PeekOutput(outputName);
        
        // Обработка результатов сегментации
        ProcessSegmentationResults(output);
        
        // Освобождение ресурсов
        input.Dispose();
    }
    
    // Подготовка входных данных для модели
    private Tensor PrepareInput(Texture2D texture)
    {
        // Преобразуем текстуру к нужному размеру
        Texture2D resizedTexture = ResizeTexture(texture, inputWidth, inputHeight);
        
        // Преобразуем в тензор с нормализацией
        float[] inputData = new float[inputWidth * inputHeight * 3];
        
        Color[] pixels = resizedTexture.GetPixels();
        for (int i = 0; i < pixels.Length; i++)
        {
            // Нормализация к диапазону [-1, 1] или [0, 1] в зависимости от модели
            inputData[i * 3 + 0] = (pixels[i].r - 0.5f) * 2.0f;
            inputData[i * 3 + 1] = (pixels[i].g - 0.5f) * 2.0f;
            inputData[i * 3 + 2] = (pixels[i].b - 0.5f) * 2.0f;
        }
        
        return new Tensor(1, inputHeight, inputWidth, 3, inputData);
    }
    
    // Изменение размера текстуры
    private Texture2D ResizeTexture(Texture2D source, int targetWidth, int targetHeight)
    {
        RenderTexture rt = RenderTexture.GetTemporary(targetWidth, targetHeight);
        Graphics.Blit(source, rt);
        
        RenderTexture prevActive = RenderTexture.active;
        RenderTexture.active = rt;
        
        Texture2D result = new Texture2D(targetWidth, targetHeight);
        result.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
        result.Apply();
        
        RenderTexture.active = prevActive;
        RenderTexture.ReleaseTemporary(rt);
        
        return result;
    }
    
    // Обработка результатов сегментации
    private void ProcessSegmentationResults(Tensor output)
    {
        // Обработка зависит от формата выходных данных вашей модели
        // Это пример для модели с многоклассовой сегментацией
        
        int outputWidth = output.width;
        int outputHeight = output.height;
        int numClasses = output.channels;
        
        Color[] segmentationColors = new Color[outputWidth * outputHeight];
        
        // Для каждого пикселя выходного изображения
        for (int y = 0; y < outputHeight; y++) 
        {
            for (int x = 0; x < outputWidth; x++) 
            {
                int maxClassIndex = 0;
                float maxConfidence = 0;
                
                // Находим класс с максимальной уверенностью
                for (int c = 0; c < numClasses; c++) 
                {
                    float confidence = output[0, y, x, c];
                    if (confidence > maxConfidence) 
                    {
                        maxConfidence = confidence;
                        maxClassIndex = c;
                    }
                }
                
                // Если это класс "стена" и уверенность > порога
                if (maxClassIndex == wallClassIndex && maxConfidence > threshold) 
                {
                    // Отмечаем как стену (красный цвет для отладки)
                    segmentationColors[y * outputWidth + x] = new Color(1, 0, 0, 0.5f);
                } 
                else 
                {
                    // Прозрачный цвет для не-стен
                    segmentationColors[y * outputWidth + x] = new Color(0, 0, 0, 0);
                }
            }
        }
        
        // Обновляем текстуру сегментации
        segmentationTexture.SetPixels(0, 0, outputWidth, outputHeight, segmentationColors);
        segmentationTexture.Apply();
        
        // Отображаем результат для отладки
        if (showDebugVisualisation && debugImage != null) 
        {
            debugImage.texture = segmentationTexture;
        }
    }
    
    // Освобождение ресурсов
    private void OnDestroy()
    {
        if (worker != null)
        {
            worker.Dispose();
        }
        
        cameraManager.frameReceived -= OnCameraFrameReceived;
    }
} 