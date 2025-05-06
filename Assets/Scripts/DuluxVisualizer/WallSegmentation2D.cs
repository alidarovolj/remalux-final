using UnityEngine;
using UnityEngine.XR.ARFoundation;
using Unity.Barracuda;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.XR.ARSubsystems;
using System.IO;
using Unity.Collections;
using System.Linq;

/// <summary>
/// Структура для хранения 4D вектора с целочисленными значениями
/// </summary>
[System.Serializable]
public struct Vector4Int
{
      public int x;
      public int y;
      public int z;
      public int w;

      public Vector4Int(int x, int y, int z, int w)
      {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
      }

      public override string ToString()
      {
            return $"({x}, {y}, {z}, {w})";
      }
}

/// <summary>
/// Компонент для сегментации стен в 2D изображении с камеры
/// Использует ML модель Barracuda для создания маски "стена/не стена"
/// </summary>
public class WallSegmentation2D : MonoBehaviour
{
      [Header("Модель сегментации")]
      [SerializeField] private NNModel segmentationModel;
      [SerializeField] private int inputResolution = 32; // 32x32 - размер, требуемый моделью
      [SerializeField] private float inferenceInterval = 0.3f; // интервал между запусками сегментации
      [SerializeField] private bool enableDebug = true;

      [Tooltip("Имя входного тензора модели")]
      [SerializeField] private string inputName = "pixel_values";

      [Tooltip("Имя выходного тензора модели")]
      [SerializeField] private string outputName = "logits";

      [Tooltip("Формат тензора. NHWC - Unity формат, NCHW - ONNX формат")]
      [SerializeField] private bool useNCHW = true;

      [Tooltip("Индекс класса для стен")]
      [SerializeField] private int wallClassIndex = 0;

      [Header("AR компоненты")]
      [SerializeField] private ARCameraManager cameraManager;

      [Header("Параметры инференса")]
      [SerializeField] private Vector2Int inputSize = new Vector2Int(32, 32);

      // Форма выходного тензора для проверки и отладки
      [SerializeField] private Vector4Int expectedOutputShape = new Vector4Int(1, 2, 32, 32);

      // Публичное свойство для доступа к маске из других скриптов
      public RenderTexture MaskTexture { get; private set; }
      public Texture2D DebugTexture { get; private set; }

      // Доступ к текстуре камеры для других компонентов
      public Texture2D CameraTexture => latestCameraFrame;

      // Добавляем событие для оповещения об обновлении сегментации
      public delegate void SegmentationUpdatedDelegate(Texture segmentationMask);
      public event SegmentationUpdatedDelegate onSegmentationUpdated;

      // Параметры маски
      [Range(0, 1)]
      [SerializeField] private float maskThreshold = 0.5f;

      // Размеры экрана для маски
      private int screenWidth = 0;
      private int screenHeight = 0;

      // Маска сегментации в формате Texture2D
      private Texture2D segmentationMask;
      private bool segmentationUpdated = false;

      // Материал для окрашивания стен
      public Material paintMaterial;

      // UI элемент для отображения отладочной информации
      public UnityEngine.UI.RawImage debugImage;

      private IWorker modelWorker;
      private bool isProcessing = false;
      private Model runtimeModel;
      private Material blitMaterial;
      private bool usingTemporaryMask = false;

      private void Start()
      {
            // Инициализируем размеры экрана
            screenWidth = Screen.width;
            screenHeight = Screen.height;

            // Создаем или инициализируем маску сегментации
            if (segmentationMask == null)
            {
                  segmentationMask = new Texture2D(screenWidth, screenHeight, TextureFormat.R8, false);
                  Debug.Log($"WallSegmentation2D: Создана маска сегментации размером {screenWidth}x{screenHeight}");
            }

            // Проверка наличия необходимых компонентов
            if (cameraManager == null)
            {
                  cameraManager = FindObjectOfType<ARCameraManager>();
                  if (cameraManager == null)
                  {
                        Debug.LogError("WallSegmentation2D: ARCameraManager не найден!");
                        enabled = false;
                        return;
                  }
            }

            // Инициализация для отладки
            if (enableDebug)
            {
                  DebugTexture = new Texture2D(inputResolution, inputResolution, TextureFormat.RGBA32, false);
                  blitMaterial = new Material(Shader.Find("Hidden/BlitCopy"));
            }

            // Пытаемся автоматически найти модель, если не назначена
            if (segmentationModel == null)
            {
                  TryLoadModelFromResources();
            }

            if (segmentationModel == null)
            {
                  Debug.LogWarning("WallSegmentation2D: ML модель не назначена! Используем временную маску.");
                  // Создаем временную маску для тестирования
                  CreateTemporaryMask();
                  usingTemporaryMask = true;
            }
            else
            {
                  // Читаем информацию из модели
                  ReadModelInfo();

                  // Инициализация модели
                  try
                  {
                        // Логируем информацию о входном тензоре для отладки
                        Debug.Log($"WallSegmentation2D: Попытка загрузки модели с размером входа {inputResolution}x{inputResolution}x3");
                        string tensorFormat = useNCHW ? "NCHW" : "NHWC";
                        Debug.Log($"WallSegmentation2D: Используем формат тензора: {tensorFormat}");

                        runtimeModel = ModelLoader.Load(segmentationModel);

                        // Проверяем имена входных тензоров
                        Debug.Log($"WallSegmentation2D: Входной тензор: '{inputName}'");
                        Debug.Log($"WallSegmentation2D: Выходной тензор: '{outputName}'");

                        // Выводим информацию о входах и выходах модели
                        Debug.Log("WallSegmentation2D: Доступные входы модели:");
                        foreach (var input in runtimeModel.inputs)
                        {
                              Debug.Log($" - {input.name}: {input.shape}");
                        }

                        Debug.Log("WallSegmentation2D: Доступные выходы модели:");
                        foreach (var output in runtimeModel.outputs)
                        {
                              Debug.Log($" - {output}");
                        }

                        // Создаем рабочего с автоматическим выбором бэкенда
                        modelWorker = WorkerFactory.CreateWorker(WorkerFactory.Type.Auto, runtimeModel);

                        Debug.Log("WallSegmentation2D: Модель ML успешно загружена");
                  }
                  catch (System.Exception e)
                  {
                        Debug.LogError($"WallSegmentation2D: Ошибка загрузки модели: {e.Message}");
                        Debug.LogError($"Stack trace: {e.StackTrace}");
                        CreateTemporaryMask();
                        usingTemporaryMask = true;
                  }
            }

            // Создаем RenderTexture для маски (одноканальный)
            MaskTexture = new RenderTexture(inputResolution, inputResolution, 0, RenderTextureFormat.R8);
            MaskTexture.enableRandomWrite = true;
            MaskTexture.Create();

            // Подписываемся на события камеры
            cameraManager.frameReceived += OnCameraFrameReceived;

            // Запускаем периодическую сегментацию
            StartCoroutine(PeriodicSegmentation());

            Debug.Log("WallSegmentation2D: Инициализация завершена");
      }

      /// <summary>
      /// Пытается загрузить модель из папки "Assets/Models"
      /// </summary>
      private void TryLoadModelFromResources()
      {
            Debug.Log("WallSegmentation2D: Попытка автоматического поиска модели...");

            // Пытаемся загрузить ONNX модель из Resources
            NNModel model = Resources.Load<NNModel>("Models/model") ?? Resources.Load<NNModel>("Models/BiseNet");

            if (model != null)
            {
                  segmentationModel = model;
                  Debug.Log($"WallSegmentation2D: Модель автоматически загружена из Resources: {model.name}");
                  return;
            }

#if UNITY_EDITOR
            // Если мы в редакторе, пробуем напрямую загрузить .onnx файл из папки Assets/Models
            string[] modelPaths = { "Assets/Models/model.onnx", "Assets/Models/BiseNet.onnx" };

            foreach (string path in modelPaths)
            {
                  if (System.IO.File.Exists(path))
                  {
                        // Если файл существует, создаем временную NNModel из этого файла
                        UnityEngine.Object asset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                        if (asset != null && asset is NNModel)
                        {
                              segmentationModel = asset as NNModel;
                              Debug.Log($"WallSegmentation2D: Модель автоматически загружена из {path}");
                              return;
                        }
                  }
            }
#endif

            Debug.LogWarning("WallSegmentation2D: Не удалось автоматически найти модель.");
      }

      private void ReadModelInfo()
      {
            // Пытаемся прочитать информацию о модели
            if (segmentationModel == null)
            {
                  Debug.LogError("WallSegmentation2D: Модель не загружена");
                  return;
            }

            // Определяем тип модели по имени
            string modelName = segmentationModel.name;

            if (modelName.Contains("deeplabv3") || modelName.Contains("unet") || modelName.Contains("segmentation"))
            {
                  Debug.Log("WallSegmentation2D: Обнаружена базовая модель, настроены соответствующие параметры");
                  useNCHW = true;
                  inputName = "pixel_values";
                  outputName = "logits";

                  // Устанавливаем стандартный размер входа для моделей сегментации
                  inputSize = new Vector2Int(32, 32);
                  Debug.Log($"WallSegmentation2D: Установлен стандартный размер входа: {inputSize.x}x{inputSize.y}");
            }
      }

      private void OnDestroy()
      {
            if (cameraManager != null)
            {
                  cameraManager.frameReceived -= OnCameraFrameReceived;
            }

            modelWorker?.Dispose();
            MaskTexture?.Release();

            if (DebugTexture != null)
            {
                  Destroy(DebugTexture);
            }

            if (blitMaterial != null)
            {
                  Destroy(blitMaterial);
            }
      }

      private Texture2D latestCameraFrame;

      private void OnCameraFrameReceived(ARCameraFrameEventArgs args)
      {
            // Пропускаем обработку, если уже обрабатываем кадр
            if (isProcessing) return;

            try
            {
                  // Получаем XRCpuImage из камеры
                  if (cameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
                  {
                        try
                        {
                              // Получаем размеры изображения
                              int width = image.width;
                              int height = image.height;

                              // Создаем текстуру подходящего размера (или переиспользуем)
                              if (latestCameraFrame == null || latestCameraFrame.width != inputSize.x || latestCameraFrame.height != inputSize.y)
                              {
                                    if (latestCameraFrame != null)
                                          Destroy(latestCameraFrame);

                                    latestCameraFrame = new Texture2D(inputSize.x, inputSize.y, TextureFormat.RGBA32, false);
                              }

                              // Конвертируем XRCpuImage в Texture2D
                              var conversionParams = new XRCpuImage.ConversionParams
                              {
                                    inputRect = new RectInt(0, 0, width, height),
                                    outputDimensions = new Vector2Int(inputSize.x, inputSize.y),
                                    outputFormat = TextureFormat.RGBA32,
                                    transformation = XRCpuImage.Transformation.MirrorY
                              };

                              // Буфер для данных изображения
                              int size = image.GetConvertedDataSize(conversionParams);
                              using (var buffer = new Unity.Collections.NativeArray<byte>(size, Unity.Collections.Allocator.Temp))
                              {
                                    // Конвертируем изображение
                                    image.Convert(conversionParams, buffer);

                                    // Загружаем данные в текстуру
                                    latestCameraFrame.LoadRawTextureData(buffer);
                                    latestCameraFrame.Apply();
                              }
                        }
                        catch (System.Exception e)
                        {
                              Debug.LogError($"WallSegmentation2D: Ошибка при обработке кадра камеры: {e.Message}");
                              FallbackCameraFrameAcquisition(args);
                        }
                        finally
                        {
                              // Всегда освобождаем XRCpuImage
                              image.Dispose();
                        }
                  }
                  else
                  {
                        // Если не удалось получить XRCpuImage, используем запасной метод
                        FallbackCameraFrameAcquisition(args);
                  }
            }
            catch (System.Exception e)
            {
                  Debug.LogError($"WallSegmentation2D: Ошибка в OnCameraFrameReceived: {e.Message}");
                  FallbackCameraFrameAcquisition(args);
            }
      }

      /// <summary>
      /// Запасной метод получения текстуры камеры, если не удалось через XRCpuImage
      /// </summary>
      private void FallbackCameraFrameAcquisition(ARCameraFrameEventArgs args)
      {
            try
            {
                  // Попытка 1: Проверяем наличие текстуры в ARCameraManager
                  Camera arCamera = cameraManager.GetComponent<Camera>();
                  if (arCamera != null && arCamera.targetTexture != null)
                  {
                        CaptureFromRenderTexture(arCamera.targetTexture);
                        return;
                  }

                  // Попытка 2: Используем возможную текстуру из ARCameraBackground
                  var cameraBackground = cameraManager.GetComponent<ARCameraBackground>();
                  if (cameraBackground != null && cameraBackground.material != null)
                  {
                        if (cameraBackground.material.HasProperty("_MainTex"))
                        {
                              Texture camTex = cameraBackground.material.GetTexture("_MainTex");
                              if (camTex != null)
                              {
                                    CaptureFromTexture(camTex);
                                    return;
                              }
                        }

                        if (cameraBackground.material.HasProperty("_textureY"))
                        {
                              Texture camTex = cameraBackground.material.GetTexture("_textureY");
                              if (camTex != null)
                              {
                                    CaptureFromTexture(camTex);
                                    return;
                              }
                        }
                  }

                  // Попытка 3: Создаем пустую текстуру, если ничего не помогло
                  if (latestCameraFrame == null)
                  {
                        latestCameraFrame = new Texture2D(inputSize.x, inputSize.y, TextureFormat.RGBA32, false);
                        Color[] pixels = new Color[inputSize.x * inputSize.y];
                        for (int i = 0; i < pixels.Length; i++)
                              pixels[i] = Color.gray;
                        latestCameraFrame.SetPixels(pixels);
                        latestCameraFrame.Apply();

                        Debug.LogWarning("WallSegmentation2D: Не удалось получить текстуру камеры, использована пустая текстура");
                  }
            }
            catch (System.Exception e)
            {
                  Debug.LogError($"WallSegmentation2D: Ошибка в FallbackCameraFrameAcquisition: {e.Message}");
            }
      }

      /// <summary>
      /// Копирует содержимое RenderTexture в latestCameraFrame
      /// </summary>
      private void CaptureFromRenderTexture(RenderTexture source)
      {
            if (source == null) return;

            // Создаем Texture2D подходящего размера
            if (latestCameraFrame == null || latestCameraFrame.width != inputSize.x || latestCameraFrame.height != inputSize.y)
            {
                  if (latestCameraFrame != null)
                        Destroy(latestCameraFrame);

                  latestCameraFrame = new Texture2D(inputSize.x, inputSize.y, TextureFormat.RGBA32, false);
            }

            // Создаем временный RenderTexture нужного размера
            RenderTexture tempRT = RenderTexture.GetTemporary(inputSize.x, inputSize.y);
            Graphics.Blit(source, tempRT);

            // Сохраняем активную RenderTexture
            RenderTexture prevRT = RenderTexture.active;
            RenderTexture.active = tempRT;

            // Копируем данные из RenderTexture в Texture2D
            latestCameraFrame.ReadPixels(new Rect(0, 0, inputSize.x, inputSize.y), 0, 0);
            latestCameraFrame.Apply();

            // Восстанавливаем активную RenderTexture
            RenderTexture.active = prevRT;
            RenderTexture.ReleaseTemporary(tempRT);

            Debug.Log("WallSegmentation2D: Получена текстура из камеры через RenderTexture");
      }

      /// <summary>
      /// Копирует содержимое Texture в latestCameraFrame
      /// </summary>
      private void CaptureFromTexture(Texture source)
      {
            if (source == null) return;

            // Создаем Texture2D подходящего размера
            if (latestCameraFrame == null || latestCameraFrame.width != inputSize.x || latestCameraFrame.height != inputSize.y)
            {
                  if (latestCameraFrame != null)
                        Destroy(latestCameraFrame);

                  latestCameraFrame = new Texture2D(inputSize.x, inputSize.y, TextureFormat.RGBA32, false);
            }

            // Создаем временный RenderTexture нужного размера
            RenderTexture tempRT = RenderTexture.GetTemporary(inputSize.x, inputSize.y);
            Graphics.Blit(source, tempRT);

            // Сохраняем активную RenderTexture
            RenderTexture prevRT = RenderTexture.active;
            RenderTexture.active = tempRT;

            // Копируем данные из RenderTexture в Texture2D
            latestCameraFrame.ReadPixels(new Rect(0, 0, inputSize.x, inputSize.y), 0, 0);
            latestCameraFrame.Apply();

            // Восстанавливаем активную RenderTexture
            RenderTexture.active = prevRT;
            RenderTexture.ReleaseTemporary(tempRT);

            Debug.Log("WallSegmentation2D: Получена текстура из камеры через Texture");
      }

      private IEnumerator PeriodicSegmentation()
      {
            // Ждем инициализации
            yield return new WaitForSeconds(1.0f);

            while (true)
            {
                  // Запускаем сегментацию, если еще не обрабатываем и есть доступное изображение
                  if (!isProcessing && latestCameraFrame != null)
                  {
                        if (!usingTemporaryMask)
                        {
                              StartCoroutine(RunSegmentation());
                        }
                        else
                        {
                              // Используем временную маску, если модели нет
                              UpdateTemporaryMask();
                        }
                  }

                  // Ждем указанный интервал перед следующим запуском
                  yield return new WaitForSeconds(inferenceInterval);
            }
      }

      private IEnumerator RunSegmentation()
      {
            if (modelWorker == null || latestCameraFrame == null)
            {
                  Debug.LogError("WallSegmentation2D: Невозможно провести сегментацию, не инициализирована модель или текстура камеры");
                  UpdateTemporaryMask();
                  yield break;
            }

            isProcessing = true;

            yield return new WaitForEndOfFrame();

            // Создаем текстуру нужного размера с содержимым изображения камеры
            RenderTexture resizedTexture = null;
            Tensor inputTensor = null;

            try
            {
                  // Создаем временную RenderTexture нужного размера для масштабирования
                  resizedTexture = RenderTexture.GetTemporary(inputSize.x, inputSize.y, 0, RenderTextureFormat.ARGB32);
                  resizedTexture.enableRandomWrite = true;
                  resizedTexture.Create();

                  // Масштабируем изображение на RenderTexture
                  Graphics.Blit(latestCameraFrame, resizedTexture);

                  Debug.Log($"WallSegmentation2D: Подготовлена текстура для входа размером {inputSize.x}x{inputSize.y}");

                  try
                  {
                        // Проверяем входные параметры модели и фиксируем несоответствия
                        ValidateModelInputs();

                        // Создаем тензор из текстуры с правильным форматом - с явным указанием размерности
                        float[] pixelValues = ConvertTextureToFloatArray(resizedTexture, inputSize.x, inputSize.y);

                        // Логируем размер тензора для отладки
                        Debug.Log($"WallSegmentation2D: Размер массива пикселей: {pixelValues.Length}");

                        // Проверяем, возможно, модель ожидает 32 канала (как в ошибке о несоответствии 3 == 32)
                        if (IsModelExpecting32Channels())
                        {
                              Debug.Log("WallSegmentation2D: Модель, вероятно, ожидает 32 канала. Пробуем создать соответствующий тензор.");
                              try
                              {
                                    // Расширяем данные до 32 каналов путем повторения существующих данных
                                    float[] expandedPixelValues = ExpandTo32Channels(pixelValues, inputSize.x, inputSize.y);

                                    // Создаем тензор с 32 каналами
                                    inputTensor = new Tensor(1, 32, inputSize.y, inputSize.x, expandedPixelValues);

                                    Debug.Log($"WallSegmentation2D: Создан тензор с 32 каналами: [{inputTensor.shape.batch}, {inputTensor.shape.channels}, {inputTensor.shape.height}, {inputTensor.shape.width}]");
                              }
                              catch (System.Exception e)
                              {
                                    Debug.LogError($"WallSegmentation2D: Ошибка при создании тензора с 32 каналами: {e.Message}");
                                    // Если не удалось, возвращаемся к стандартному тензору с 3 каналами
                                    inputTensor = new Tensor(1, 3, inputSize.y, inputSize.x, pixelValues);
                              }
                        }
                        else
                        {
                              // Стандартный подход - 3 канала (RGB)
                              // Правильная размерность: [1, 3, 32, 32] - batch, channels, height, width
                              inputTensor = new Tensor(1, 3, inputSize.y, inputSize.x, pixelValues);
                        }

                        // Логируем форму тензора для отладки
                        Debug.Log($"WallSegmentation2D: Форма входного тензора: [{inputTensor.shape.batch}, {inputTensor.shape.channels}, {inputTensor.shape.height}, {inputTensor.shape.width}]");

                        try
                        {
                              // Так как тензор может быть неправильного размера, добавляем дополнительное управление исключениями
                              try
                              {
                                    // Запускаем инференс
                                    modelWorker.Execute(inputTensor);

                                    // Получаем результат (маску)
                                    Tensor outputTensor = modelWorker.PeekOutput(outputName);

                                    // Обрабатываем результат - извлекаем конкретный класс (стена) из всех классов сегментации
                                    ProcessOutputTensor(outputTensor);

                                    // Освобождаем ресурсы
                                    outputTensor.Dispose();
                              }
                              catch (System.Exception e)
                              {
                                    // Проверяем конкретные ошибки по строке сообщения
                                    string errorMsg = e.Message;
                                    if (errorMsg.Contains("Cannot reshape array") ||
                                        errorMsg.Contains("Assertion failure") ||
                                        errorMsg.Contains("Values are not equal") ||
                                        errorMsg.Contains("3 == 32") ||
                                        errorMsg.Contains("ExitCode=3221225477"))
                                    {
                                          Debug.LogError($"WallSegmentation2D: Ошибка размерности тензора: {e.Message}");
                                          // Пробуем альтернативную размерность
                                          TryAlternativeTensorShape(pixelValues);
                                    }
                                    else
                                    {
                                          // Пробрасываем другие ошибки
                                          throw;
                                    }
                              }
                        }
                        catch (System.Exception e)
                        {
                              Debug.LogError($"WallSegmentation2D: Ошибка при сегментации: {e.Message}");
                              Debug.LogError($"Stack trace: {e.StackTrace}");
                              UpdateTemporaryMask();
                        }
                  }
                  finally
                  {
                        // Освобождаем ресурсы
                        if (inputTensor != null)
                              inputTensor.Dispose();
                  }
            }
            catch (System.Exception e)
            {
                  Debug.LogError($"WallSegmentation2D: Общая ошибка сегментации: {e.Message}");
                  UpdateTemporaryMask();
            }
            finally
            {
                  // Освобождаем временную RenderTexture
                  if (resizedTexture != null)
                        RenderTexture.ReleaseTemporary(resizedTexture);

                  isProcessing = false;
            }
      }

      /// <summary>
      /// Пытается выполнить инференс с альтернативной размерностью тензора
      /// </summary>
      private void TryAlternativeTensorShape(float[] pixelValues)
      {
            Debug.Log("WallSegmentation2D: Пробуем альтернативную размерность тензора");

            // Размерности для проверки
            int[][] shapesToTry = new int[][]
            {
                  new int[] { 1, 3, inputSize.y, inputSize.x }, // NCHW стандарт
                  new int[] { 1, inputSize.y, inputSize.x, 3 }, // NHWC формат
                  new int[] { 1, 3, inputSize.x, inputSize.y }, // NCHW с инвертированными размерами
                  new int[] { 1, 1, 3, inputSize.y * inputSize.x }, // Развернутый формат
                  new int[] { 1, 3, 1, inputSize.y * inputSize.x }, // Еще один вариант
                  new int[] { 1, inputSize.y, 8, 8 }  // Специальный формат для модели из ошибки
            };

            bool successFound = false;

            foreach (int[] shape in shapesToTry)
            {
                  if (successFound) break;

                  try
                  {
                        Debug.Log($"WallSegmentation2D: Пробуем форму [{shape[0]}, {shape[1]}, {shape[2]}, {shape[3]}]");

                        // Создаем тензор с заданной формой
                        using (var tensor = new Tensor(shape[0], shape[1], shape[2], shape[3], pixelValues))
                        {
                              // Проверяем, что размерность создалась правильно
                              Debug.Log($"WallSegmentation2D: Создан тензор с формой [{tensor.shape.batch}, {tensor.shape.channels}, {tensor.shape.height}, {tensor.shape.width}]");

                              modelWorker.Execute(tensor);
                              using (var outputTensor = modelWorker.PeekOutput(outputName))
                              {
                                    Debug.Log($"WallSegmentation2D: Инференс успешен! Форма выходного тензора: [{outputTensor.shape.batch}, {outputTensor.shape.channels}, {outputTensor.shape.height}, {outputTensor.shape.width}]");

                                    ProcessOutputTensor(outputTensor);
                                    successFound = true;

                                    // Запоминаем правильную форму для следующих запусков
                                    inputSize = new Vector2Int(shape[3], shape[2]);
                                    useNCHW = shape[1] == 3;

                                    Debug.Log($"WallSegmentation2D: Найдена правильная форма тензора: [{shape[0]}, {shape[1]}, {shape[2]}, {shape[3]}]. Сохраняем для будущих запусков.");
                              }
                        }
                  }
                  catch (System.Exception e)
                  {
                        Debug.LogWarning($"WallSegmentation2D: Форма [{shape[0]}, {shape[1]}, {shape[2]}, {shape[3]}] не подходит: {e.Message}");
                  }
            }

            // Если все попытки не удались, пробуем переупорядочить данные
            if (!successFound)
            {
                  Debug.Log("WallSegmentation2D: Пробуем переупорядочить данные в тензоре...");
                  TryReorderTensorData(pixelValues);
            }

            // Если все равно ничего не помогло, используем временную маску
            if (!successFound)
            {
                  Debug.LogError("WallSegmentation2D: Не удалось найти подходящую размерность тензора, используем временную маску");
                  UpdateTemporaryMask();
            }
      }

      /// <summary>
      /// Пробует различные способы упорядочивания данных в тензоре
      /// </summary>
      private void TryReorderTensorData(float[] originalValues)
      {
            int pixelCount = inputSize.x * inputSize.y;

            try
            {
                  // Создаем новый массив с другим порядком каналов (BGR вместо RGB)
                  float[] reorderedValues = new float[originalValues.Length];

                  for (int i = 0; i < pixelCount; i++)
                  {
                        reorderedValues[i] = originalValues[i + 2 * pixelCount]; // B канал
                        reorderedValues[i + pixelCount] = originalValues[i + pixelCount]; // G канал
                        reorderedValues[i + 2 * pixelCount] = originalValues[i]; // R канал
                  }

                  using (var tensor = new Tensor(1, 3, inputSize.y, inputSize.x, reorderedValues))
                  {
                        Debug.Log("WallSegmentation2D: Пробуем BGR порядок каналов");
                        modelWorker.Execute(tensor);
                        using (var outputTensor = modelWorker.PeekOutput(outputName))
                        {
                              ProcessOutputTensor(outputTensor);
                              Debug.Log("WallSegmentation2D: BGR порядок каналов работает!");
                              return;
                        }
                  }
            }
            catch (System.Exception e)
            {
                  Debug.LogWarning($"WallSegmentation2D: BGR порядок каналов не подходит: {e.Message}");
            }

            // Если ничего не помогло, используем временную маску
            UpdateTemporaryMask();
      }

      /// <summary>
      /// Преобразует RenderTexture в массив float[] для создания тензора
      /// </summary>
      private float[] ConvertTextureToFloatArray(RenderTexture texture, int width, int height)
      {
            if (texture == null)
            {
                  Debug.LogError("WallSegmentation2D: Текстура равна null в ConvertTextureToFloatArray");
                  return new float[width * height * 3];
            }

            try
            {
                  // Создаем временную текстуру для чтения пикселей
                  Texture2D tempTexture = new Texture2D(width, height, TextureFormat.RGB24, false);

                  // Сохраняем текущую активную RenderTexture
                  RenderTexture prevRT = RenderTexture.active;
                  RenderTexture.active = texture;

                  // Копируем пиксели из RenderTexture в Texture2D
                  tempTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                  tempTexture.Apply();

                  // Восстанавливаем предыдущую активную RenderTexture
                  RenderTexture.active = prevRT;

                  // Получаем массив пикселей
                  Color32[] pixels = tempTexture.GetPixels32();

                  // Преобразуем в массив float[] для тензора в формате NCHW [batch, channels, height, width]
                  // Для NCHW формата необходимо сгруппировать все пиксели канала R, затем все G, затем все B
                  float[] result = new float[width * height * 3];
                  int pixelCount = width * height;

                  for (int i = 0; i < pixels.Length; i++)
                  {
                        // Индекс в массиве результата для каждого канала
                        // R канал начинается с 0
                        // G канал начинается с pixelCount
                        // B канал начинается с 2*pixelCount
                        result[i] = pixels[i].r / 255.0f;                   // R канал
                        result[i + pixelCount] = pixels[i].g / 255.0f;      // G канал
                        result[i + 2 * pixelCount] = pixels[i].b / 255.0f;  // B канал
                  }

                  // Уничтожаем временную текстуру
                  Destroy(tempTexture);

                  return result;
            }
            catch (System.Exception e)
            {
                  Debug.LogError($"WallSegmentation2D: Ошибка в ConvertTextureToFloatArray: {e.Message}");
                  return new float[width * height * 3];
            }
      }

      private void ProcessOutputTensor(Tensor outputTensor)
      {
            // Логируем форму выходного тензора для отладки
            Debug.Log($"WallSegmentation2D: Форма выходного тензора: [{outputTensor.shape.batch}, {outputTensor.shape.channels}, {outputTensor.shape.height}, {outputTensor.shape.width}]");

            // Убедимся, что у нас есть доступ к данным тензора
            if (outputTensor == null)
            {
                  Debug.LogError("WallSegmentation2D: Выходной тензор равен null");
                  return;
            }

            // Получаем размеры выходного тензора
            int width = outputTensor.shape.width;
            int height = outputTensor.shape.height;
            int classes = outputTensor.shape.channels;

            Debug.Log($"WallSegmentation2D: Размеры выходного тензора: {width}x{height}, классов: {classes}");

            // Создаем маску, если она еще не существует
            if (segmentationMask == null || segmentationMask.width != screenWidth || segmentationMask.height != screenHeight)
            {
                  if (segmentationMask != null)
                        Destroy(segmentationMask);

                  segmentationMask = new Texture2D(screenWidth, screenHeight, TextureFormat.R8, false);
                  Debug.Log($"WallSegmentation2D: Создана новая маска размером {screenWidth}x{screenHeight}");
            }

            // Готовим временный буфер для хранения данных маски в размере выходного тензора
            Texture2D tempMask = new Texture2D(width, height, TextureFormat.R8, false);
            Color[] maskPixels = new Color[width * height];

            // Индекс класса "стены" в данных сегментации
            int wallClassIndex = 0; // Индекс класса стены (зависит от модели)

            // Извлекаем данные сегментации для класса "стена"
            for (int y = 0; y < height; y++)
            {
                  for (int x = 0; x < width; x++)
                  {
                        float wallProb = 0;

                        // Проверяем, есть ли у нас несколько классов или только один канал
                        if (classes > 1)
                        {
                              // Если несколько классов, берем вероятность для класса "стена"
                              wallProb = outputTensor[0, wallClassIndex, y, x];

                              // Если у нас multi-class segmentation, применяем softmax
                              float sum = 0;
                              for (int c = 0; c < classes; c++)
                              {
                                    sum += Mathf.Exp(outputTensor[0, c, y, x]);
                              }

                              wallProb = Mathf.Exp(wallProb) / sum;
                        }
                        else
                        {
                              // Если только один канал, предполагаем, что это и есть маска стен
                              wallProb = outputTensor[0, 0, y, x];
                        }

                        // Применяем порог для бинаризации
                        float value = wallProb > maskThreshold ? 1f : 0f;
                        maskPixels[y * width + x] = new Color(value, value, value, 1);
                  }
            }

            // Устанавливаем пиксели во временную маску
            tempMask.SetPixels(maskPixels);
            tempMask.Apply();

            // Масштабируем временную маску до размера экрана
            RenderTexture rt = RenderTexture.GetTemporary(screenWidth, screenHeight);
            Graphics.Blit(tempMask, rt);

            // Копируем данные из rt в финальную маску
            RenderTexture.active = rt;
            segmentationMask.ReadPixels(new Rect(0, 0, screenWidth, screenHeight), 0, 0);
            segmentationMask.Apply();

            // Освобождаем ресурсы
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            Destroy(tempMask);

            // Обновляем материал шейдера
            UpdateShaderMaterial();

            // Устанавливаем флаг, что маска обновлена
            segmentationUpdated = true;
      }

      private Texture2D ResizeTexture(Texture2D source, int width, int height)
      {
            // Создаем временную RenderTexture для ресайза
            RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            rt.filterMode = FilterMode.Bilinear;

            try
            {
                  // Делаем временную RenderTexture активной и копируем исходную текстуру
                  RenderTexture.active = rt;
                  Graphics.Blit(source, rt);

                  // Создаем результирующую текстуру и копируем из RenderTexture
                  Texture2D resizedTexture = new Texture2D(width, height, TextureFormat.RGB24, false);
                  resizedTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                  resizedTexture.Apply();

                  return resizedTexture;
            }
            finally
            {
                  // Освобождаем ресурсы
                  RenderTexture.active = null;
                  RenderTexture.ReleaseTemporary(rt);
            }
      }

      public void CreateTemporaryMask()
      {
            Debug.Log("WallSegmentation2D: Создание временной маски для тестирования");

            // Используем размер 32x32 для быстрой работы
            int resolution = inputResolution;
            Texture2D tempMaskTexture = new Texture2D(resolution, resolution, TextureFormat.R8, false);

            try
            {
                  Color[] pixels = new Color[resolution * resolution];

                  for (int y = 0; y < resolution; y++)
                  {
                        for (int x = 0; x < resolution; x++)
                        {
                              // Создаем простую маску: левая половина изображения - "стена"
                              float value = (x < resolution / 2) ? 1.0f : 0.0f;
                              pixels[y * resolution + x] = new Color(value, value, value, 1.0f);
                        }
                  }

                  tempMaskTexture.SetPixels(pixels);
                  tempMaskTexture.Apply();

                  // Копируем в RenderTexture
                  if (MaskTexture != null)
                  {
                        Graphics.Blit(tempMaskTexture, MaskTexture);
                  }

                  // Сохраняем для отладки
                  if (DebugTexture != null)
                  {
                        DebugTexture.SetPixels(pixels);
                        DebugTexture.Apply();
                  }
            }
            finally
            {
                  // Освобождаем ресурсы
                  Destroy(tempMaskTexture);
            }
      }

      private void UpdateTemporaryMask()
      {
            Debug.Log("WallSegmentation2D: Создаем временную маску для тестирования");

            // Создаем маску, если нужно
            if (segmentationMask == null || segmentationMask.width != screenWidth || segmentationMask.height != screenHeight)
            {
                  if (segmentationMask != null)
                        Destroy(segmentationMask);

                  segmentationMask = new Texture2D(screenWidth, screenHeight, TextureFormat.R8, false);
                  Debug.Log($"WallSegmentation2D: Создана новая маска размером {screenWidth}x{screenHeight}");
            }

            // Заполняем маску тестовым паттерном
            Color[] pixels = new Color[screenWidth * screenHeight];
            for (int y = 0; y < screenHeight; y++)
            {
                  for (int x = 0; x < screenWidth; x++)
                  {
                        // Создаем простой тестовый паттерн - центральный прямоугольник
                        float centerX = screenWidth / 2f;
                        float centerY = screenHeight / 2f;
                        float distanceX = Mathf.Abs(x - centerX) / (screenWidth / 2f);
                        float distanceY = Mathf.Abs(y - centerY) / (screenHeight / 2f);

                        // Если точка находится в центральной области экрана (прямоугольник)
                        float value = (distanceX < 0.5f && distanceY < 0.5f) ? 1f : 0f;

                        pixels[y * screenWidth + x] = new Color(value, value, value, 1);
                  }
            }

            segmentationMask.SetPixels(pixels);
            segmentationMask.Apply();

            // Создаем материал, если его нет
            if (paintMaterial == null)
            {
                  // Ищем шейдер для покраски стен
                  Shader wallPaintShader = Shader.Find("Custom/WallPaint");
                  if (wallPaintShader == null)
                  {
                        wallPaintShader = Shader.Find("Unlit/Texture");
                        Debug.LogWarning("WallSegmentation2D: Шейдер Custom/WallPaint не найден, используем Unlit/Texture");
                  }

                  // Создаем материал
                  paintMaterial = new Material(wallPaintShader);
                  Debug.Log("WallSegmentation2D: Создан новый материал для покраски стен");
            }

            // Обновляем шейдер
            UpdateShaderMaterial();

            // Устанавливаем флаг, что маска обновлена
            segmentationUpdated = true;
      }

      // Обновляет материал шейдера с новой маской сегментации
      private void UpdateShaderMaterial()
      {
            if (segmentationMask == null)
            {
                  Debug.LogWarning("WallSegmentation2D: Не удалось обновить материал - отсутствует маска");
                  return;
            }

            // Если материал не создан, пытаемся создать его
            if (paintMaterial == null)
            {
                  // Ищем объект WallPaintingTextureUpdater для получения материала
                  WallPaintingTextureUpdater wallPainting = FindObjectOfType<WallPaintingTextureUpdater>();
                  if (wallPainting != null)
                  {
                        // Используем публичный метод для получения материала
                        Material updaterMaterial = wallPainting.GetMaterial();
                        if (updaterMaterial != null)
                        {
                              paintMaterial = updaterMaterial;
                              Debug.Log("WallSegmentation2D: Получен материал из WallPaintingTextureUpdater");
                        }
                        else
                        {
                              Debug.LogWarning("WallSegmentation2D: Не удалось получить материал из WallPaintingTextureUpdater");
                        }
                  }

                  // Если материал все еще не получен, создаем новый
                  if (paintMaterial == null)
                  {
                        // Ищем шейдер для покраски стен
                        Shader wallPaintShader = Shader.Find("Custom/WallPaint");
                        if (wallPaintShader == null)
                        {
                              wallPaintShader = Shader.Find("Unlit/Texture");
                              Debug.LogWarning("WallSegmentation2D: Шейдер Custom/WallPaint не найден, используем Unlit/Texture");
                        }

                        // Создаем материал
                        paintMaterial = new Material(wallPaintShader);
                        Debug.Log("WallSegmentation2D: Создан новый материал для покраски стен");
                  }
            }

            // Теперь, когда у нас гарантированно есть материал и маска, устанавливаем текстуру
            if (paintMaterial.HasProperty("_MaskTex"))
            {
                  paintMaterial.SetTexture("_MaskTex", segmentationMask);
                  Debug.Log("WallSegmentation2D: Маска установлена в материал шейдера");
            }
            else
            {
                  Debug.LogWarning("WallSegmentation2D: Материал не содержит свойства _MaskTex");

                  // Если это Unlit/Texture материал, используем _MainTex
                  if (paintMaterial.HasProperty("_MainTex"))
                  {
                        paintMaterial.SetTexture("_MainTex", segmentationMask);
                        Debug.Log("WallSegmentation2D: Используем _MainTex вместо _MaskTex");
                  }
            }

            // Оповещаем о событии обновления маски
            if (onSegmentationUpdated != null)
            {
                  onSegmentationUpdated.Invoke(segmentationMask);
                  Debug.Log("WallSegmentation2D: Отправлено событие onSegmentationUpdated");
            }

            // Обновляем отладочное изображение, если оно есть
            if (enableDebug && debugImage != null)
            {
                  debugImage.texture = segmentationMask;
                  Debug.Log("WallSegmentation2D: Обновлено отладочное изображение");
            }
      }

      // Для отладки
      private void OnGUI()
      {
            if (enableDebug && DebugTexture != null)
            {
                  // Отображаем маску в правом верхнем углу экрана
                  GUI.DrawTexture(new Rect(Screen.width - 150, 10, 140, 140), DebugTexture);
                  GUI.Label(new Rect(Screen.width - 150, 155, 140, 20), "Маска сегментации");
            }
      }

      /// <summary>
      /// Проверяет параметры модели и исправляет несоответствия
      /// </summary>
      private void ValidateModelInputs()
      {
            if (runtimeModel == null)
            {
                  Debug.LogWarning("WallSegmentation2D: RuntimeModel не инициализирована");
                  return;
            }

            // Проверяем размер входного тензора
            foreach (var input in runtimeModel.inputs)
            {
                  if (input.name == inputName && input.shape.Length >= 4)
                  {
                        // Получаем размерности из входного тензора
                        var tensorDims = input.shape;

                        // Для NCHW: индексы 2 и 3 содержат высоту и ширину
                        // Для NHWC: индексы 1 и 2 содержат высоту и ширину
                        int heightIndex = useNCHW ? 2 : 1;
                        int widthIndex = useNCHW ? 3 : 2;

                        // Если размерности заданы как -1 (динамические), используем наш размер
                        if (tensorDims[heightIndex] > 0 && tensorDims[widthIndex] > 0)
                        {
                              // Обновляем inputSize, если модель ожидает другой размер
                              Vector2Int modelSize = new Vector2Int(tensorDims[widthIndex], tensorDims[heightIndex]);

                              if (inputSize != modelSize)
                              {
                                    Debug.LogWarning($"WallSegmentation2D: Размер входа не соответствует ожидаемому моделью. " +
                                                    $"Текущий: {inputSize.x}x{inputSize.y}, ожидаемый: {modelSize.x}x{modelSize.y}. " +
                                                    $"Обновляем до {modelSize.x}x{modelSize.y}");
                                    inputSize = modelSize;
                              }
                        }

                        // Проверяем соотношение сторон
                        float aspectRatio = (float)inputSize.x / inputSize.y;
                        if (aspectRatio < 0.9f || aspectRatio > 1.1f)
                        {
                              Debug.LogWarning($"WallSegmentation2D: Необычное соотношение сторон входа {aspectRatio}. " +
                                              $"Рекомендуется использовать квадратный вход. Корректируем до квадрата.");
                              // Используем наименьшую сторону для создания квадрата
                              int minDim = Mathf.Min(inputSize.x, inputSize.y);
                              inputSize = new Vector2Int(minDim, minDim);
                        }

                        Debug.Log($"WallSegmentation2D: Проверка входных данных завершена, используем размер {inputSize.x}x{inputSize.y}");
                        return; // Нашли входной тензор, прекращаем поиск
                  }
            }

            Debug.LogWarning($"WallSegmentation2D: Не найден входной тензор с именем '{inputName}'. Используем стандартный размер {inputSize.x}x{inputSize.y}");
      }

      /// <summary>
      /// Проверяет, нужны ли модели 32 канала на входе (исходя из предыдущих ошибок)
      /// </summary>
      private bool IsModelExpecting32Channels()
      {
            // Если ранее была ошибка о несоответствии "3 == 32", предполагаем, что модель ожидает 32 канала
            return true; // Временно всегда возвращаем true для тестирования
      }

      /// <summary>
      /// Расширяет данные RGB до 32 каналов, повторяя или дополняя существующие данные
      /// </summary>
      private float[] ExpandTo32Channels(float[] rgbData, int width, int height)
      {
            int pixelCount = width * height;
            float[] result = new float[pixelCount * 32];

            // Копируем RGB каналы в первые 3 канала результата
            for (int i = 0; i < pixelCount; i++)
            {
                  result[i] = rgbData[i]; // R
                  result[i + pixelCount] = rgbData[i + pixelCount]; // G
                  result[i + 2 * pixelCount] = rgbData[i + 2 * pixelCount]; // B
            }

            // Заполняем оставшиеся 29 каналов нулями или средними значениями
            for (int c = 3; c < 32; c++)
            {
                  for (int i = 0; i < pixelCount; i++)
                  {
                        // Заполняем остальные каналы "прокси-данными" или нулями
                        if (c % 3 == 0)
                              result[i + c * pixelCount] = rgbData[i]; // повторяем R
                        else if (c % 3 == 1)
                              result[i + c * pixelCount] = rgbData[i + pixelCount]; // повторяем G
                        else
                              result[i + c * pixelCount] = rgbData[i + 2 * pixelCount]; // повторяем B
                  }
            }

            return result;
      }
}