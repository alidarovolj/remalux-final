using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if OPENCV_ENABLED
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.UnityUtils;
#endif

/// <summary>
/// Класс для обработки изображений с помощью OpenCV
/// </summary>
public class OpenCVProcessor : MonoBehaviour
{
    [Header("OpenCV Settings")]
    [SerializeField] private bool useOpenCV = true;
    [SerializeField] private bool showDebugVisuals = true;
    [SerializeField] private UnityEngine.UI.RawImage debugOutputImage;
    
    [Header("Processing Parameters")]
    [Range(0, 50)]
    [SerializeField] private int gaussianBlurSize = 5;
    [Range(0, 50)]
    [SerializeField] private int medianBlurSize = 7;
    [Range(0, 20)]
    [SerializeField] private int erosionSize = 3;
    [Range(0, 20)]
    [SerializeField] private int dilationSize = 5;
    [Range(0, 255)]
    [SerializeField] private int cannyThreshold1 = 50;
    [Range(0, 255)]
    [SerializeField] private int cannyThreshold2 = 150;
    
    // Ссылка на компонент сегментации
    private WallSegmentation wallSegmentation;
    
    // Текстуры для отладки
    private Texture2D processedTexture;
    private Texture2D debugTexture;
    
    // Флаг инициализации
    private bool isInitialized = false;
    
    void Start()
    {
        wallSegmentation = GetComponent<WallSegmentation>();
        if (wallSegmentation == null)
        {
            wallSegmentation = FindObjectOfType<WallSegmentation>();
        }
        
        CheckOpenCVAvailability();
    }
    
    /// <summary>
    /// Проверяет доступность OpenCV
    /// </summary>
    private void CheckOpenCVAvailability()
    {
#if OPENCV_ENABLED
        Debug.Log("OpenCV инициализирован и готов к использованию");
        isInitialized = true;
#else
        Debug.LogWarning("OpenCV не включен. Добавьте определение OPENCV_ENABLED в Player Settings > Scripting Define Symbols.");
        useOpenCV = false;
#endif
    }
    
    /// <summary>
    /// Обрабатывает маску сегментации с помощью OpenCV
    /// </summary>
    /// <param name="inputTexture">Входная текстура (маска сегментации)</param>
    /// <returns>Обработанная текстура</returns>
    public Texture2D ProcessSegmentationMask(Texture2D inputTexture)
    {
        if (!useOpenCV || inputTexture == null)
        {
            return inputTexture;
        }
        
#if OPENCV_ENABLED
        try
        {
            // Создаем выходную текстуру, если она еще не создана или имеет неправильный размер
            if (processedTexture == null || 
                processedTexture.width != inputTexture.width || 
                processedTexture.height != inputTexture.height)
            {
                if (processedTexture != null)
                {
                    Destroy(processedTexture);
                }
                processedTexture = new Texture2D(inputTexture.width, inputTexture.height, TextureFormat.RGBA32, false);
            }
            
            // Создаем текстуру для отладки
            if (showDebugVisuals && 
                (debugTexture == null || 
                debugTexture.width != inputTexture.width || 
                debugTexture.height != inputTexture.height))
            {
                if (debugTexture != null)
                {
                    Destroy(debugTexture);
                }
                debugTexture = new Texture2D(inputTexture.width, inputTexture.height, TextureFormat.RGBA32, false);
            }
            
            // Создаем матрицу из Unity текстуры
            Mat imageMat = new Mat(inputTexture.height, inputTexture.width, CvType.CV_8UC4);
            Utils.texture2DToMat(inputTexture, imageMat);
            
            // Преобразуем в градации серого для обработки
            Mat grayMat = new Mat();
            Imgproc.cvtColor(imageMat, grayMat, Imgproc.COLOR_RGBA2GRAY);
            
            // Размытие для удаления шума
            if (gaussianBlurSize > 0 && gaussianBlurSize % 2 == 1)
            {
                Imgproc.GaussianBlur(grayMat, grayMat, new Size(gaussianBlurSize, gaussianBlurSize), 0);
            }
            
            if (medianBlurSize > 0 && medianBlurSize % 2 == 1)
            {
                Imgproc.medianBlur(grayMat, grayMat, medianBlurSize);
            }
            
            // Бинаризация
            Mat binaryMat = new Mat();
            Imgproc.threshold(grayMat, binaryMat, 127, 255, Imgproc.THRESH_BINARY);
            
            // Морфологические операции
            if (erosionSize > 0)
            {
                Mat element = Imgproc.getStructuringElement(
                    Imgproc.MORPH_RECT, 
                    new Size(2 * erosionSize + 1, 2 * erosionSize + 1),
                    new Point(erosionSize, erosionSize));
                    
                Imgproc.erode(binaryMat, binaryMat, element);
                element.release();
            }
            
            if (dilationSize > 0)
            {
                Mat element = Imgproc.getStructuringElement(
                    Imgproc.MORPH_RECT, 
                    new Size(2 * dilationSize + 1, 2 * dilationSize + 1),
                    new Point(dilationSize, dilationSize));
                    
                Imgproc.dilate(binaryMat, binaryMat, element);
                element.release();
            }
            
            // Находим контуры для сглаживания краев
            Mat cannyMat = new Mat();
            Imgproc.Canny(binaryMat, cannyMat, cannyThreshold1, cannyThreshold2);
            
            // Находим контуры
            List<MatOfPoint> contours = new List<MatOfPoint>();
            Mat hierarchy = new Mat();
            Imgproc.findContours(cannyMat, contours, hierarchy, Imgproc.RETR_EXTERNAL, Imgproc.CHAIN_APPROX_SIMPLE);
            
            // Заполняем контуры
            Mat contourMat = Mat.zeros(cannyMat.size(), CvType.CV_8UC1);
            for (int i = 0; i < contours.Count; i++)
            {
                Imgproc.drawContours(contourMat, contours, i, new Scalar(255), -1);
                contours[i].release();
            }
            hierarchy.release();
            
            // Объединяем результат с бинарной маской
            Mat resultMat = new Mat();
            Core.bitwise_or(binaryMat, contourMat, resultMat);
            
            // Преобразуем обратно в RGBA
            Mat resultRGBA = new Mat();
            Imgproc.cvtColor(resultMat, resultRGBA, Imgproc.COLOR_GRAY2RGBA);
            
            // Обновляем текстуру результата
            Utils.matToTexture2D(resultRGBA, processedTexture);
            
            // Обновляем отладочную текстуру и UI
            if (showDebugVisuals && debugOutputImage != null)
            {
                // Для отладки выведем контуры
                Mat debugMat = new Mat();
                Imgproc.cvtColor(contourMat, debugMat, Imgproc.COLOR_GRAY2RGBA);
                Utils.matToTexture2D(debugMat, debugTexture);
                debugOutputImage.texture = debugTexture;
                debugMat.release();
            }
            
            // Очистка ресурсов OpenCV
            imageMat.release();
            grayMat.release();
            binaryMat.release();
            cannyMat.release();
            contourMat.release();
            resultMat.release();
            resultRGBA.release();
            
            if (showDebugVisuals)
            {
                Debug.Log("OpenCV: Обработка маски завершена");
            }
            
            return processedTexture;
        }
        catch (Exception e)
        {
            Debug.LogError($"Ошибка при обработке изображения OpenCV: {e.Message}\n{e.StackTrace}");
            return inputTexture;
        }
#else
        return inputTexture;
#endif
    }
    
    /// <summary>
    /// Улучшает маску сегментации для повышения качества краев
    /// </summary>
    /// <param name="segmentationMask">Исходная маска</param>
    /// <returns>Улучшенная маска</returns>
    public Texture2D EnhanceSegmentationMask(Texture2D segmentationMask)
    {
        if (!useOpenCV || segmentationMask == null)
        {
            return segmentationMask;
        }
        
        return ProcessSegmentationMask(segmentationMask);
    }
} 