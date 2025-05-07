using UnityEngine;
using Unity.Barracuda;
using System.Collections.Generic;

namespace DuluxVisualizer
{
      /// <summary>
      /// Компонент для загрузки и подготовки моделей Barracuda ONNX
      /// </summary>
      public class ModelLoader : MonoBehaviour
      {
            [SerializeField] private NNModel _modelAsset;
            [SerializeField] private string _modelInputName = "input";
            [SerializeField] private string _modelOutputName = "output";
            [SerializeField] private bool _useNCHW = true;
            [SerializeField] private int _inputWidth = 128;
            [SerializeField] private int _inputHeight = 128;

            private Model _runtimeModel;
            private IWorker _engine;
            private string _inferenceDevice = "CPU";
            private bool _isModelReady = false;

            public bool IsModelReady => _isModelReady;

            // Событие для оповещения о готовности модели
            public delegate void ModelReadyDelegate(bool success);
            public event ModelReadyDelegate OnModelReady;

            private void Awake()
            {
                  // Пытаемся загрузить модель автоматически при старте
                  if (_modelAsset != null)
                  {
                        LoadModel(_modelAsset);
                  }
                  else
                  {
                        // Пытаемся найти модель в Resources
                        NNModel model = Resources.Load<NNModel>("Models/model");
                        if (model != null)
                        {
                              LoadModel(model);
                        }
                        else
                        {
                              Debug.LogWarning("Модель сегментации не найдена в Resources/Models.");
                        }
                  }
            }

            /// <summary>
            /// Загружает ONNX модель и подготавливает движок для инференса
            /// </summary>
            public bool LoadModel(NNModel modelAsset)
            {
                  if (modelAsset == null)
                  {
                        Debug.LogError("ModelLoader: Не указана модель для загрузки.");
                        return false;
                  }

                  try
                  {
                        // Создаем рантайм-модель из ассета, используя класс Unity.Barracuda.ModelLoader
                        _runtimeModel = Unity.Barracuda.ModelLoader.Load(modelAsset);

                        // Создаем воркер для инференса
                        WorkerFactory.Type workerType = WorkerFactory.Type.CSharpBurst;

                        // Если доступно GPU, используем его
                        if (SystemInfo.supportsComputeShaders && _inferenceDevice == "GPU")
                        {
                              workerType = WorkerFactory.Type.ComputePrecompiled;
                              Debug.Log("ModelLoader: Используем GPU для инференса.");
                        }
                        else
                        {
                              Debug.Log("ModelLoader: Используем CPU для инференса.");
                        }

                        // Освобождаем предыдущий движок, если он был
                        if (_engine != null)
                        {
                              _engine.Dispose();
                        }

                        // Создаем новый движок
                        _engine = WorkerFactory.CreateWorker(workerType, _runtimeModel);

                        // Анализируем входы-выходы модели
                        AnalyzeModel();

                        _isModelReady = true;
                        OnModelReady?.Invoke(true);
                        return true;
                  }
                  catch (System.Exception e)
                  {
                        Debug.LogError($"ModelLoader: Ошибка при загрузке модели: {e.Message}");
                        _isModelReady = false;
                        OnModelReady?.Invoke(false);
                        return false;
                  }
            }

            /// <summary>
            /// Анализирует модель и выводит информацию о входах и выходах
            /// </summary>
            private void AnalyzeModel()
            {
                  if (_runtimeModel == null) return;

                  Debug.Log($"ModelLoader: Загружена модель");
                  Debug.Log($"  Входы ({_runtimeModel.inputs.Count}):");
                  foreach (var input in _runtimeModel.inputs)
                  {
                        Debug.Log($"    {input.name}: shape={input.shape}");
                  }

                  Debug.Log($"  Выходы ({_runtimeModel.outputs.Count}):");
                  foreach (var output in _runtimeModel.outputs)
                  {
                        Debug.Log($"    {output}");
                  }
            }

            /// <summary>
            /// Выполняет инференс модели с заданным тензором входа
            /// </summary>
            public Tensor Execute(Tensor inputTensor)
            {
                  if (!_isModelReady || _engine == null)
                  {
                        Debug.LogWarning("ModelLoader: Модель не готова для инференса.");
                        return null;
                  }

                  try
                  {
                        // Выполняем инференс
                        _engine.Execute(inputTensor);

                        // Получаем результат
                        Tensor outputTensor = _engine.PeekOutput(_modelOutputName);
                        return outputTensor;
                  }
                  catch (System.Exception e)
                  {
                        Debug.LogError($"ModelLoader: Ошибка при инференсе: {e.Message}");
                        return null;
                  }
            }

            /// <summary>
            /// Создает входной тензор из текстуры
            /// </summary>
            public Tensor TextureToTensor(Texture2D texture)
            {
                  if (texture == null) return null;

                  // Приводим текстуру к нужному размеру
                  Texture2D resizedTexture = texture;
                  if (texture.width != _inputWidth || texture.height != _inputHeight)
                  {
                        resizedTexture = Resize(texture, _inputWidth, _inputHeight);
                  }

                  Tensor tensor;
                  if (_useNCHW)
                  {
                        // Формат NCHW (батч, каналы, высота, ширина)
                        tensor = new Tensor(resizedTexture, channels: 3);
                  }
                  else
                  {
                        // Формат NHWC (батч, высота, ширина, каналы)
                        tensor = new Tensor(resizedTexture);
                  }

                  // Если временная текстура была создана, освобождаем её
                  if (resizedTexture != texture)
                  {
                        Destroy(resizedTexture);
                  }

                  return tensor;
            }

            /// <summary>
            /// Изменяет размер текстуры
            /// </summary>
            private Texture2D Resize(Texture2D source, int width, int height)
            {
                  RenderTexture rt = RenderTexture.GetTemporary(width, height, 0);
                  Graphics.Blit(source, rt);

                  RenderTexture prevRT = RenderTexture.active;
                  RenderTexture.active = rt;

                  Texture2D result = new Texture2D(width, height);
                  result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                  result.Apply();

                  RenderTexture.active = prevRT;
                  RenderTexture.ReleaseTemporary(rt);

                  return result;
            }

            /// <summary>
            /// Преобразует тензор в текстуру
            /// </summary>
            public Texture2D TensorToTexture(Tensor tensor, int targetWidth, int targetHeight)
            {
                  if (tensor == null) return null;

                  Texture2D texture = new Texture2D(targetWidth, targetHeight, TextureFormat.R8, false);
                  float[] data = tensor.AsFloats();

                  // Предполагаем, что тензор имеет shape [1, height, width, 1] или [1, 1, height, width]
                  int dataLength = data.Length;
                  Color[] colors = new Color[targetWidth * targetHeight];

                  // Нормализуем и конвертируем в цвета
                  for (int i = 0; i < colors.Length && i < dataLength; i++)
                  {
                        float value = Mathf.Clamp01(data[i]);
                        colors[i] = new Color(value, value, value, 1f);
                  }

                  texture.SetPixels(colors);
                  texture.Apply();

                  return texture;
            }

            private void OnDestroy()
            {
                  // Освобождаем ресурсы
                  if (_engine != null)
                  {
                        _engine.Dispose();
                        _engine = null;
                  }
            }
      }
}