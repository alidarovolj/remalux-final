using UnityEngine;
using UnityEngine.XR.ARFoundation;
using Unity.Barracuda;
using System.Collections.Generic;
using System.Collections;
using System;
using UnityEngine.XR.ARSubsystems;

namespace DuluxVisualizer
{
      /// <summary>
      /// Компонент для обработки данных с камеры AR и выполнения инференса модели сегментации
      /// </summary>
      [RequireComponent(typeof(ARCameraManager))]
      public class WallSegmentationProcessor : MonoBehaviour
      {
            [Header("Модель и параметры")]
            [SerializeField] private NNModel _modelAsset;
            [SerializeField] private RenderTexture _outputTexture;
            [SerializeField] private float _processingInterval = 0.1f;
            [SerializeField] private bool _useTemporalSmoothing = true;
            [SerializeField] private float _smoothingFactor = 0.75f;

            [Header("Отладка")]
            [SerializeField] private bool _debugMode = false;
            [SerializeField] private RenderTexture _debugInputTexture;
            [SerializeField] private RenderTexture _debugOutputTexture;

            // Внутренние переменные для работы
            private ModelLoader _modelLoader;
            private ARCameraManager _cameraManager;
            private Texture2D _cameraTexture;
            private bool _isProcessing = false;
            private Texture2D _previousMask;
            private float _lastProcessingTime;

            // События
            public delegate void SegmentationCompleteDelegate(RenderTexture mask);
            public event SegmentationCompleteDelegate OnSegmentationComplete;

            private void Awake()
            {
                  // Получаем компоненты
                  _cameraManager = GetComponent<ARCameraManager>();

                  // Создаем загрузчик модели
                  _modelLoader = gameObject.AddComponent<ModelLoader>();

                  // Инициализируем текстуру для обработки кадров с камеры
                  _cameraTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);

                  // Создаем выходную текстуру, если она не назначена
                  if (_outputTexture == null)
                  {
                        _outputTexture = new RenderTexture(256, 256, 0, RenderTextureFormat.R8);
                        _outputTexture.name = "WallSegmentationMask";
                        _outputTexture.Create();
                  }

                  // Создаем текстуру для хранения предыдущей маски для сглаживания
                  if (_useTemporalSmoothing)
                  {
                        _previousMask = new Texture2D(_outputTexture.width, _outputTexture.height, TextureFormat.R8, false);
                  }
            }

            private void OnEnable()
            {
                  // Подписываемся на события получения кадра с камеры
                  _cameraManager.frameReceived += OnCameraFrameReceived;

                  // Запускаем корутину обработки кадров
                  StartCoroutine(ProcessFramesRoutine());
            }

            private void OnDisable()
            {
                  // Отписываемся от событий
                  _cameraManager.frameReceived -= OnCameraFrameReceived;

                  // Останавливаем корутину
                  StopAllCoroutines();
            }

            /// <summary>
            /// Обработчик события получения кадра с камеры AR
            /// </summary>
            private void OnCameraFrameReceived(ARCameraFrameEventArgs args)
            {
                  // Пропускаем, если уже выполняется обработка или нет текстуры
                  if (_isProcessing || _cameraManager.TryAcquireLatestCpuImage(out var image) == false)
                        return;

                  try
                  {
                        // Конвертируем изображение с камеры в текстуру
                        _cameraTexture.Reinitialize(image.width, image.height);

                        // Конвертируем данные из формата камеры в текстуру
                        XRCpuImage.ConversionParams conversionParams = new XRCpuImage.ConversionParams
                        {
                              inputRect = new RectInt(0, 0, image.width, image.height),
                              outputDimensions = new Vector2Int(image.width, image.height),
                              outputFormat = TextureFormat.RGBA32,
                              transformation = XRCpuImage.Transformation.MirrorY
                        };

                        // Выделяем буфер для конвертации
                        var rawTextureData = _cameraTexture.GetRawTextureData<byte>();
                        image.Convert(conversionParams, rawTextureData);
                        _cameraTexture.Apply();

                        // Отладочная информация
                        if (_debugMode && _debugInputTexture != null)
                        {
                              Graphics.Blit(_cameraTexture, _debugInputTexture);
                        }
                  }
                  catch (Exception e)
                  {
                        Debug.LogError($"Ошибка обработки кадра камеры: {e.Message}");
                  }
                  finally
                  {
                        // Освобождаем ресурсы
                        image.Dispose();
                  }
            }

            /// <summary>
            /// Корутина для регулярной обработки кадров
            /// </summary>
            private IEnumerator ProcessFramesRoutine()
            {
                  while (true)
                  {
                        // Ждем готовности модели
                        yield return new WaitUntil(() => _modelLoader.IsModelReady);

                        // Проверяем интервал обработки
                        if (Time.time - _lastProcessingTime < _processingInterval)
                        {
                              yield return null;
                              continue;
                        }

                        // Отмечаем, что обработка началась
                        _isProcessing = true;
                        _lastProcessingTime = Time.time;

                        // Выполняем инференс
                        yield return ProcessCurrentFrame();

                        // Отмечаем, что обработка завершена
                        _isProcessing = false;

                        // Вызываем событие завершения сегментации
                        OnSegmentationComplete?.Invoke(_outputTexture);
                  }
            }

            /// <summary>
            /// Обработка текущего кадра и выполнение инференса
            /// </summary>
            private IEnumerator ProcessCurrentFrame()
            {
                  if (_cameraTexture == null || !_modelLoader.IsModelReady)
                        yield break;

                  try
                  {
                        // Создаем тензор из текстуры
                        Tensor inputTensor = _modelLoader.TextureToTensor(_cameraTexture);

                        // Выполняем инференс модели
                        Tensor outputTensor = _modelLoader.Execute(inputTensor);

                        if (outputTensor != null)
                        {
                              // Преобразуем результат в текстуру
                              Texture2D resultMask = _modelLoader.TensorToTexture(
                                  outputTensor,
                                  _outputTexture.width,
                                  _outputTexture.height);

                              // Применяем временное сглаживание, если включено
                              if (_useTemporalSmoothing && _previousMask != null)
                              {
                                    ApplyTemporalSmoothing(resultMask);
                              }

                              // Копируем результат в выходную текстуру
                              Graphics.Blit(resultMask, _outputTexture);

                              // Сохраняем текущий результат для следующего кадра
                              if (_useTemporalSmoothing)
                              {
                                    // Копируем данные для следующего кадра
                                    Color[] pixels = resultMask.GetPixels();
                                    _previousMask.SetPixels(pixels);
                                    _previousMask.Apply();
                              }

                              // Отладочная информация
                              if (_debugMode && _debugOutputTexture != null)
                              {
                                    Graphics.Blit(resultMask, _debugOutputTexture);
                              }

                              // Освобождаем ресурсы
                              Destroy(resultMask);
                              outputTensor.Dispose();
                        }

                        // Освобождаем входной тензор
                        inputTensor.Dispose();
                  }
                  catch (Exception e)
                  {
                        Debug.LogError($"Ошибка при обработке сегментации: {e.Message}");
                  }
            }

            /// <summary>
            /// Применяет временное сглаживание к текущей маске
            /// </summary>
            private void ApplyTemporalSmoothing(Texture2D currentMask)
            {
                  if (_previousMask == null || currentMask == null)
                        return;

                  // Получаем пиксели из текущей и предыдущей маски
                  Color[] currentPixels = currentMask.GetPixels();
                  Color[] previousPixels = _previousMask.GetPixels();

                  // Ленивая проверка размеров
                  if (currentPixels.Length != previousPixels.Length)
                        return;

                  // Смешиваем текущие и предыдущие пиксели
                  for (int i = 0; i < currentPixels.Length; i++)
                  {
                        currentPixels[i] = Color.Lerp(currentPixels[i], previousPixels[i], _smoothingFactor);
                  }

                  // Применяем смешанные пиксели к текущей маске
                  currentMask.SetPixels(currentPixels);
                  currentMask.Apply();
            }

            /// <summary>
            /// Задает модель для сегментации
            /// </summary>
            public void SetModel(NNModel model)
            {
                  if (model == null)
                        return;

                  _modelAsset = model;
                  _modelLoader.LoadModel(model);
            }

            /// <summary>
            /// Задает выходную текстуру для результата сегментации
            /// </summary>
            public void SetOutputTexture(RenderTexture texture)
            {
                  if (texture == null)
                        return;

                  _outputTexture = texture;
            }

            /// <summary>
            /// Настраивает интервал обработки кадров
            /// </summary>
            public void SetProcessingInterval(float interval)
            {
                  _processingInterval = Mathf.Max(0.05f, interval);
            }

            /// <summary>
            /// Включает или выключает временное сглаживание
            /// </summary>
            public void SetTemporalSmoothing(bool enabled, float factor = 0.75f)
            {
                  _useTemporalSmoothing = enabled;
                  _smoothingFactor = Mathf.Clamp01(factor);
            }

            private void OnDestroy()
            {
                  // Освобождаем ресурсы
                  if (_cameraTexture != null)
                        Destroy(_cameraTexture);

                  if (_previousMask != null)
                        Destroy(_previousMask);
            }
      }
}