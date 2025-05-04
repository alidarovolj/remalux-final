using UnityEngine;
using UnityEditor;
using Unity.Barracuda;
using System.IO;

public class ModelInspectorEditor : EditorWindow
{
    private string modelPath = "Assets/Models/model.onnx";
    private NNModel modelAsset;
    private Vector2 scrollPosition;
    private string inspectionResults = "";

    [MenuItem("Tools/ONNX Model Inspector")]
    public static void ShowWindow()
    {
        GetWindow<ModelInspectorEditor>("ONNX Model Inspector");
    }

    private void OnGUI()
    {
        GUILayout.Label("ONNX Model Inspector", EditorStyles.boldLabel);
        
        modelPath = EditorGUILayout.TextField("Model Path:", modelPath);
        modelAsset = (NNModel)EditorGUILayout.ObjectField("Model Asset:", modelAsset, typeof(NNModel), false);
        
        if (GUILayout.Button("Inspect Model"))
        {
            inspectionResults = InspectModel();
        }
        
        // Отображение результатов в прокручиваемой области
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));
        EditorGUILayout.TextArea(inspectionResults, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private string InspectModel()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("=== Инспекция ONNX модели ===");
        
        Model model = null;
        
        // Пробуем загрузить модель из файла
        if (!string.IsNullOrEmpty(modelPath) && File.Exists(modelPath))
        {
            try
            {
                sb.AppendLine($"Загрузка модели из файла: {modelPath}");
                
                var converter = new Unity.Barracuda.ONNX.ONNXModelConverter(
                    optimizeModel: true,
                    treatErrorsAsWarnings: true,
                    forceArbitraryBatchSize: true);
                
                byte[] onnxBytes = File.ReadAllBytes(modelPath);
                model = converter.Convert(onnxBytes);
                sb.AppendLine("Модель успешно загружена из файла");
            }
            catch (System.Exception e)
            {
                sb.AppendLine($"Ошибка при загрузке модели из файла: {e.Message}");
            }
        }
        
        // Если не удалось загрузить из файла, пробуем использовать ассет
        if (model == null && modelAsset != null)
        {
            try
            {
                sb.AppendLine("Загрузка модели из ассета Unity");
                model = ModelLoader.Load(modelAsset);
                sb.AppendLine("Модель успешно загружена из ассета");
            }
            catch (System.Exception e)
            {
                sb.AppendLine($"Ошибка при загрузке модели из ассета: {e.Message}");
            }
        }
        
        if (model == null)
        {
            sb.AppendLine("Не удалось загрузить модель ни из файла, ни из ассета");
            return sb.ToString();
        }
        
        // Выводим общую информацию о модели
        sb.AppendLine("\n=== Общая информация о модели ===");
        sb.AppendLine($"Всего слоев: {model.layers.Count}");
        sb.AppendLine($"Всего входов: {model.inputs.Count}");
        sb.AppendLine($"Всего выходов: {model.outputs.Count}");
        
        // Выводим информацию о входах
        sb.AppendLine("\n=== Входы модели ===");
        foreach (var input in model.inputs)
        {
            sb.AppendLine($"Имя: {input.name}");
            sb.AppendLine($"Форма: {string.Join(",", input.shape)}");
        }
        
        // Выводим информацию о выходах
        sb.AppendLine("\n=== Выходы модели ===");
        foreach (var output in model.outputs)
        {
            sb.AppendLine($"Имя: {output}");
            
            // Ищем слой, соответствующий выходу
            foreach (var layer in model.layers)
            {
                if (layer.name == output)
                {
                    sb.AppendLine($"Тип выходного слоя: {layer.type}");
                    break;
                }
            }
        }
        
        // Выводим информацию о первых 10 слоях
        sb.AppendLine("\n=== Слои модели (первые 10) ===");
        for (int i = 0; i < Mathf.Min(10, model.layers.Count); i++)
        {
            var layer = model.layers[i];
            sb.AppendLine($"Слой {i}: {layer.name}, Тип: {layer.type}");
            sb.AppendLine($"  Входы: {string.Join(", ", layer.inputs)}");
            sb.AppendLine($"  Выходы: {(layer.outputs != null ? layer.outputs.Length.ToString() : "0")} тензоров");
        }
        
        if (model.layers.Count > 10)
        {
            sb.AppendLine($"...и еще {model.layers.Count - 10} слоев...");
        }
        
        sb.AppendLine("\nИнспекция модели завершена!");
        return sb.ToString();
    }
} 