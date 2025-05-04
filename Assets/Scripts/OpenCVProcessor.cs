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
            
            // Применяем пороговую обработку для получения бинарного изображения
            // Это особенно важно для маски SegFormer, где нас интересует только класс "стена"
            Mat binaryMat = new Mat();
            Imgproc.threshold(grayMat, binaryMat, 100, 255, Imgproc.THRESH_BINARY);
            
            // Размытие для удаления шума
            if (gaussianBlurSize > 0 && gaussianBlurSize % 2 == 1)
            {
                Imgproc.GaussianBlur(binaryMat, binaryMat, new Size(gaussianBlurSize, gaussianBlurSize), 0);
            }
            
            if (medianBlurSize > 0 && medianBlurSize % 2 == 1)
            {
                Imgproc.medianBlur(binaryMat, binaryMat, medianBlurSize);
            }
            
            // Морфологические операции для улучшения маски
            // Сначала эрозия для удаления мелких артефактов
            if (erosionSize > 0)
            {
                Mat element = Imgproc.getStructuringElement(
                    Imgproc.MORPH_RECT, 
                    new Size(2 * erosionSize + 1, 2 * erosionSize + 1),
                    new Point(erosionSize, erosionSize));
                    
                Imgproc.erode(binaryMat, binaryMat, element);
                element.release();
            }
            
            // Затем дилатация для заполнения дыр и соединения близких областей
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
            
            // Фильтруем контуры по размеру - оставляем только большие контуры (стены)
            List<MatOfPoint> filteredContours = new List<MatOfPoint>();
            double minContourArea = inputTexture.width * inputTexture.height * 0.005; // Минимум 0.5% площади
            foreach (var contour in contours)
            {
                double area = Imgproc.contourArea(contour);
                if (area > minContourArea)
                {
                    filteredContours.Add(contour);
                }
                else
                {
                    contour.release();
                }
            }
            
            // Заполняем контуры
            Mat contourMat = Mat.zeros(cannyMat.size(), CvType.CV_8UC1);
            for (int i = 0; i < filteredContours.Count; i++)
            {
                Imgproc.drawContours(contourMat, filteredContours, i, new Scalar(255), -1);
                filteredContours[i].release();
            }
            
            hierarchy.release();
            
            // Объединяем результат с бинарной маской
            Mat resultMat = new Mat();
            Core.bitwise_or(binaryMat, contourMat, resultMat);
            
            // Создаем цветную маску для визуализации
            Mat colorMaskMat = new Mat();
            Imgproc.cvtColor(resultMat, colorMaskMat, Imgproc.COLOR_GRAY2RGBA);
            
            // Устанавливаем синий оттенок для стен в цветной маске
            for (int y = 0; y < colorMaskMat.rows(); y++)
            {
                for (int x = 0; x < colorMaskMat.cols(); x++)
                {
                    double[] pixel = colorMaskMat.get(y, x);
                    if (pixel[0] > 0) // Если пиксель "включен"
                    {
                        // Устанавливаем синий цвет
                        colorMaskMat.put(y, x, 0, 180, 255, 200); // R=0, G=180, B=255, A=200
                    }
                }
            }
            
            // Обновляем текстуру результата
            Utils.matToTexture2D(colorMaskMat, processedTexture);
            
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
            colorMaskMat.release();
            
            if (showDebugVisuals)
            {
                Debug.Log("OpenCV: Обработка маски завершена с улучшенными фильтрами");
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
    
    /// <summary>
    /// Проверяет доступность OpenCV и возвращает результат как булевое значение
    /// </summary>
    public bool IsOpenCVAvailable()
    {
#if OPENCV_ENABLED
        try
        {
            // Простая проверка OpenCV, создаем и уничтожаем тестовый Mat
            using (Mat testMat = new Mat(10, 10, CvType.CV_8UC3))
            {
                // Если мы дошли сюда без исключений, значит OpenCV работает
                return true;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Ошибка при проверке OpenCV: {ex.Message}");
            return false;
        }
#else
        Debug.LogWarning("OpenCV не включен. Добавьте символ OPENCV_ENABLED в Scripting Define Symbols.");
        return false;
#endif
    }
} 