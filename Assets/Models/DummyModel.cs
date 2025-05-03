using UnityEngine;
using Unity.Barracuda;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;

[ExecuteInEditMode]
public class DummyModel : MonoBehaviour
{
    [Header("Export Settings")]
    [SerializeField] private string modelName = "dummy_wall_segmentation";
    [SerializeField] private int inputWidth = 256;
    [SerializeField] private int inputHeight = 256;
    [SerializeField] private int numClasses = 3; // фон, стены, другие объекты
    
    [Header("Output")]
    [SerializeField] private NNModel generatedModel;
    [SerializeField] private bool useExistingOnnxModel = true;
    [SerializeField] private string existingModelPath = "Assets/StreamingAssets/model.onnx";
    
    [ContextMenu("Generate Dummy ONNX Model")]
    public void GenerateDummyModel()
    {
        #if UNITY_EDITOR
        if (useExistingOnnxModel)
        {
            // Используем существующую модель
            if (File.Exists(existingModelPath))
            {
                // Создаем ассет NNModel из существующего файла ONNX
                string assetPath = "Assets/Models/" + modelName + ".asset";
                
                // Проверяем, существует ли уже ассет
                NNModel existingAsset = AssetDatabase.LoadAssetAtPath<NNModel>(assetPath);
                if (existingAsset == null)
                {
                    // Копируем существующий ONNX файл в папку Assets/Models
                    string targetOnnxPath = "Assets/Models/" + modelName + ".onnx";
                    FileUtil.CopyFileOrDirectory(existingModelPath, targetOnnxPath);
                    AssetDatabase.Refresh();
                    
                    // Импортируем ONNX как NNModel
                    AssetDatabase.ImportAsset(targetOnnxPath);
                    
                    // Создаем ассет NNModel
                    generatedModel = AssetDatabase.LoadAssetAtPath<NNModel>(targetOnnxPath);
                }
                else
                {
                    generatedModel = existingAsset;
                }
                
                Debug.Log("ONNX model imported from: " + existingModelPath);
            }
            else
            {
                Debug.LogError("Existing ONNX model not found at: " + existingModelPath);
            }
        }
        else
        {
            // Создаем базовую структуру тензоров для простой модели
            // Эта модель просто применяет фиксированную маску и не делает реальной сегментации
            // Для демонстрационных целей
            
            var modelBuilder = new ModelBuilder();
            
            // Входной тензор
            string inputName = "input";
            var inputShape = new TensorShape(1, inputHeight, inputWidth, 3);
            
            // Выходной тензор
            string outputName = "output";
            var outputShape = new TensorShape(1, inputHeight, inputWidth, numClasses);
            
            // Создаем константный тензор с некоторыми весами
            var weightsShape = new TensorShape(1, 1, 1, numClasses);
            float[] weightsArray = new float[numClasses];
            for (int i = 0; i < numClasses; i++)
            {
                weightsArray[i] = 0.1f * (i + 1);
            }
            
            // Создаем полносвязный слой
            modelBuilder.Input(inputName, inputShape);
            var weights = new Tensor(weightsShape, weightsArray);
            modelBuilder.Dense(inputName, "dense1", weights, null);
            modelBuilder.Reshape("dense1", outputName, outputShape);
            
            // Строим модель
            var model = modelBuilder.model;
            
            // Экспортируем модель как ONNX файл
            // В Unity 2020+ мы не можем напрямую сохранить модель как ONNX,
            // но можем создать NNModel из модели Barracuda
            
            // Создаем директорию для моделей, если ее нет
            if (!Directory.Exists("Assets/Models"))
            {
                Directory.CreateDirectory("Assets/Models");
            }
            
            // Создаем файл .onnx (это симуляция, настоящий ONNX файл требует дополнительных инструментов)
            string dummyOnnxPath = "Assets/Models/" + modelName + ".bytes";
            File.WriteAllText(dummyOnnxPath, "Dummy ONNX content");
            AssetDatabase.Refresh();
            
            // Создаем ассет NNModel и отмечаем его как модель Barracuda
            string assetPath = "Assets/Models/" + modelName + ".asset";
            NNModel nnModel = ScriptableObject.CreateInstance<NNModel>();
            AssetDatabase.CreateAsset(nnModel, assetPath);
            
            SerializedObject serializedModel = new SerializedObject(nnModel);
            SerializedProperty modelProp = serializedModel.FindProperty("m_Model");
            
            // Устанавливаем ссылку на байтовый файл
            UnityEngine.Object bytesAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(dummyOnnxPath);
            if (bytesAsset != null)
            {
                modelProp.objectReferenceValue = bytesAsset;
                serializedModel.ApplyModifiedProperties();
            }
            
            AssetDatabase.SaveAssets();
            generatedModel = nnModel;
            
            Debug.Log("Dummy model generated at: " + assetPath);
        }
        #endif
    }
}
#endif 