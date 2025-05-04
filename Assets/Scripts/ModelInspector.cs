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
    [SerializeField] private NNModel modelAsset;
    [SerializeField] private string modelPath = "Assets/Models/model.onnx";

    [Header("Debug Options")]
    [SerializeField] private bool logOnStart = true; // Выводить информацию при запуске
    [SerializeField] private bool logTensorShapes = true; // Выводить формы тензоров

    void Start()
    {
        if (logOnStart)
        {
            InspectModel();
        }
    }

    public void InspectModel()
    {
        Model model = null;

        // Пробуем загрузить модель из пути напрямую
        try 
        {
            var converter = new Unity.Barracuda.ONNX.ONNXModelConverter(
                optimizeModel: true, 
                treatErrorsAsWarnings: true, 
                forceArbitraryBatchSize: true);
            
            byte[] onnxBytes = System.IO.File.ReadAllBytes(modelPath);
            model = converter.Convert(onnxBytes);
            Debug.Log("Модель загружена из файла: " + modelPath);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Ошибка при загрузке модели: " + e.Message);
            
            // Если не удалось загрузить из файла, пробуем использовать сериализованную модель
            if (modelAsset != null)
            {
                model = ModelLoader.Load(modelAsset);
                Debug.Log("Модель загружена из сериализованного ассета");
            }
            else
            {
                Debug.LogError("Не удалось загрузить модель ни из файла, ни из ассета");
                return;
            }
        }

        if (model == null)
        {
            Debug.LogError("Модель не загружена");
            return;
        }

        // Выводим общую информацию о модели
        Debug.Log("=== Информация о модели ===");
        Debug.Log($"Память (байт): {model.GetTensorByName("?").length * sizeof(float)}");
        Debug.Log($"Всего слоев: {model.layers.Count}");
        Debug.Log($"Всего входов: {model.inputs.Count}");
        Debug.Log($"Всего выходов: {model.outputs.Count}");

        // Выводим информацию о входах
        Debug.Log("\n=== Входы модели ===");
        foreach (var input in model.inputs)
        {
            Debug.Log($"Имя: {input.name}");
            Debug.Log($"Форма: {string.Join(",", input.shape)}");
        }

        // Выводим информацию о выходах
        Debug.Log("\n=== Выходы модели ===");
        foreach (var output in model.outputs)
        {
            Debug.Log($"Имя: {output}");
            
            // Ищем слой, соответствующий выходу
            foreach (var layer in model.layers)
            {
                if (layer.name == output)
                {
                    Debug.Log($"Тип выходного слоя: {layer.type}");
                    break;
                }
            }
        }

        // Выводим информацию о первых 10 слоях
        Debug.Log("\n=== Слои модели (первые 10) ===");
        for (int i = 0; i < Mathf.Min(10, model.layers.Count); i++)
        {
            var layer = model.layers[i];
            Debug.Log($"Слой {i}: {layer.name}, Тип: {layer.type}");
            Debug.Log($"  Входы: {string.Join(", ", layer.inputs)}");
            Debug.Log($"  Выходы: {(layer.outputs != null ? layer.outputs.Length.ToString() : "0")} тензоров");
        }

        if (model.layers.Count > 10)
        {
            Debug.Log($"...и еще {model.layers.Count - 10} слоев...");
        }
        
        Debug.Log("Инспекция модели завершена!");
    }

    /// <summary>
    /// Запускает инспекцию модели из кода
    /// </summary>
    public void InspectModelFromButton()
    {
        InspectModel();
    }
} 