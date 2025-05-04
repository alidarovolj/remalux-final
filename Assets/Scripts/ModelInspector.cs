using System.IO;
using UnityEngine;
using Unity.Barracuda;
using Unity.Barracuda.ONNX;
using System.Linq;

/// <summary>
/// Инструмент для анализа ONNX-моделей и инспекции их входных и выходных тензоров
/// </summary>
public class ModelInspector : MonoBehaviour
{
    [Header("Model Source")]
    [SerializeField] private NNModel embeddedModel; // Модель в редакторе Unity
    [SerializeField] private string externalModelPath = "BiseNet.onnx"; // Путь к модели в StreamingAssets

    [Header("Debug Options")]
    [SerializeField] private bool logOnStart = true; // Выводить информацию при запуске
    [SerializeField] private bool logTensorShapes = true; // Выводить формы тензоров

    void Start()
    {
        if (logOnStart)
        {
            if (embeddedModel != null)
            {
                InspectEmbeddedModel();
            }
            else if (!string.IsNullOrEmpty(externalModelPath))
            {
                InspectExternalModel();
            }
        }
    }

    /// <summary>
    /// Инспектирует модель, встроенную через Unity Editor
    /// </summary>
    public void InspectEmbeddedModel()
    {
        if (embeddedModel == null)
        {
            Debug.LogError("Не назначена встроенная модель для инспекции");
            return;
        }

        try
        {
            Model model = ModelLoader.Load(embeddedModel);
            InspectModelDetails(model, "Встроенная модель");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Ошибка загрузки встроенной модели: {e.Message}");
        }
    }

    /// <summary>
    /// Инспектирует модель из StreamingAssets
    /// </summary>
    public void InspectExternalModel()
    {
        string fullPath = Path.Combine(Application.streamingAssetsPath, externalModelPath);
        if (!File.Exists(fullPath))
        {
            Debug.LogError($"Модель не найдена по пути: {fullPath}");
            return;
        }

        try
        {
            // Загружаем ONNX-файл
            byte[] onnxBytes = File.ReadAllBytes(fullPath);
            
            // Конвертируем ONNX в Barracuda-модель
            ONNXModelConverter converter = new ONNXModelConverter(
                optimizeModel: true, 
                treatErrorsAsWarnings: false, 
                forceArbitraryBatchSize: true);
            
            Model model = converter.Convert(onnxBytes);
            InspectModelDetails(model, $"Внешняя модель: {externalModelPath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Ошибка загрузки внешней модели: {e.Message}");
        }
    }

    /// <summary>
    /// Анализирует и выводит информацию о модели
    /// </summary>
    private void InspectModelDetails(Model model, string modelName)
    {
        Debug.Log($"=== Информация о модели: {modelName} ===");
        Debug.Log($"Входные тензоры: {string.Join(", ", model.inputs)}");
        Debug.Log($"Выходные тензоры: {string.Join(", ", model.outputs)}");
        Debug.Log($"Количество слоев: {model.layers.Count}");

        if (logTensorShapes)
        {
            // Упрощённый подход: просто выводим имена тензоров без форм
            Debug.Log("=== Список входных тензоров ===");
            foreach (var inputName in model.inputs)
            {
                Debug.Log($"Вход: '{inputName}'");
            }

            Debug.Log("=== Список выходных тензоров ===");
            foreach (var outputName in model.outputs)
            {
                Debug.Log($"Выход: '{outputName}'");
            }

            // Выводим информацию о всех слоях модели
            Debug.Log("=== Слои модели ===");
            foreach (var layer in model.layers)
            {
                Debug.Log($"Слой: '{layer.name}', Тип: {layer.type}");
                Debug.Log($"  Входы: {string.Join(", ", layer.inputs)}");
                
                // Проверяем, имеет ли layer выходы и выводим их имена
                if (layer.outputs != null && layer.outputs.Length > 0)
                {
                    Debug.Log($"  Выходы: {string.Join(", ", layer.outputs)}");
                    
                    // Просто выводим информацию о количестве выходных тензоров
                    Debug.Log($"    Количество выходных тензоров: {layer.outputs.Length}");
                }
            }
        }
    }

    /// <summary>
    /// Запускает инспекцию модели из кода
    /// </summary>
    public void InspectModelFromButton()
    {
        if (embeddedModel != null)
        {
            InspectEmbeddedModel();
        }
        else if (!string.IsNullOrEmpty(externalModelPath))
        {
            InspectExternalModel();
        }
        else
        {
            Debug.LogWarning("Не назначена модель для инспекции");
        }
    }
} 