using UnityEngine;
using UnityEditor;
using UnityEngine.XR.ARFoundation;
using Unity.XR.CoreUtils;
using UnityEngine.UI;
using System.IO;

/// <summary>
/// Инструмент для создания и настройки AR сцены для покраски стен
/// </summary>
#if UNITY_EDITOR
public class ARSceneCreator : EditorWindow
{
    // Параметры для создания сцены
    private string sceneName = "ARWallPainting";
    private bool createARSession = true;
    private bool createUIControls = true;
    private bool createDebugVisuals = true;
    private WallSegmentation.SegmentationMode segmentationMode = WallSegmentation.SegmentationMode.Demo;
    private Object onnxModelAsset;
    
    // Опции для внешней модели
    private bool useExternalModel = false;
    private string externalModelPath = "model.onnx"; // Путь относительно StreamingAssets
    
    // Открываем окно инструмента
    [MenuItem("AR/Wall Painting/Open Scene Creator", false, 10)]
    public static void ShowWindow()
    {
        GetWindow<ARSceneCreator>("AR Wall Painting Setup");
    }
    
    [MenuItem("AR/Wall Painting/Generate New Scene", false, 11)]
    public static void GenerateARWallPaintingScene()
    {
        // Создаем новую сцену через ARWallPaintingSceneCreator
        ARWallPaintingSceneCreator.CreateARWallPaintingScene();
    }
    
    // Отрисовка UI инструмента
    private void OnGUI()
    {
        GUILayout.Label("AR Wall Painting Scene Creator", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Этот инструмент создаст сцену для AR приложения покраски стен.", MessageType.Info);
        
        GUILayout.Space(10);
        
        // Базовые настройки
        sceneName = EditorGUILayout.TextField("Название сцены:", sceneName);
        createARSession = EditorGUILayout.Toggle("Создать AR Session", createARSession);
        createUIControls = EditorGUILayout.Toggle("Создать UI элементы", createUIControls);
        createDebugVisuals = EditorGUILayout.Toggle("Создать отладочную визуализацию", createDebugVisuals);
        
        GUILayout.Space(10);
        GUILayout.Label("Настройки сегментации стен", EditorStyles.boldLabel);
        
        // Выбор режима сегментации
        segmentationMode = (WallSegmentation.SegmentationMode)EditorGUILayout.EnumPopup("Режим сегментации:", segmentationMode);
        
        // Показываем дополнительные опции в зависимости от выбранного режима
        if (segmentationMode == WallSegmentation.SegmentationMode.EmbeddedModel)
        {
            onnxModelAsset = EditorGUILayout.ObjectField("ONNX модель:", onnxModelAsset, typeof(Unity.Barracuda.NNModel), false);
        }
        else if (segmentationMode == WallSegmentation.SegmentationMode.ExternalModel)
        {
            EditorGUILayout.HelpBox("Внешняя модель должна находиться в папке StreamingAssets.", MessageType.Info);
            externalModelPath = EditorGUILayout.TextField("Путь к модели:", externalModelPath);
            
            EditorGUILayout.BeginHorizontal();
            
            // Проверяем наличие StreamingAssets и создаем папку при необходимости
            if (GUILayout.Button("Создать папку StreamingAssets"))
            {
                if (!Directory.Exists(Application.streamingAssetsPath))
                {
                    Directory.CreateDirectory(Application.streamingAssetsPath);
                    AssetDatabase.Refresh();
                    EditorUtility.DisplayDialog("Информация", "Папка StreamingAssets создана", "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog("Информация", "Папка StreamingAssets уже существует", "OK");
                }
            }
            
            // Добавляем кнопку для выбора файла ONNX модели
            if (GUILayout.Button("Выбрать ONNX файл"))
            {
                string filepath = EditorUtility.OpenFilePanel("Выберите ONNX модель", "", "onnx");
                if (!string.IsNullOrEmpty(filepath))
                {
                    // Получаем только имя файла (без пути)
                    string filename = Path.GetFileName(filepath);
                    externalModelPath = filename;
                    
                    // Проверяем, нужно ли копировать файл в StreamingAssets
                    bool shouldCopy = EditorUtility.DisplayDialog(
                        "Копировать файл?", 
                        $"Скопировать {filename} в папку StreamingAssets?", 
                        "Да", "Нет");
                    
                    if (shouldCopy)
                    {
                        CopyModelToStreamingAssets(filepath);
                    }
                }
            }
            
            EditorGUILayout.EndHorizontal();
            
            // Проверяем, существует ли файл модели в StreamingAssets
            string fullModelPath = Path.Combine(Application.streamingAssetsPath, externalModelPath);
            bool modelExists = File.Exists(fullModelPath);
            
            EditorGUILayout.BeginHorizontal();
            
            // Показываем статус модели
            if (modelExists)
            {
                EditorGUILayout.HelpBox($"Модель найдена: {fullModelPath}", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox($"Модель не найдена в StreamingAssets!", MessageType.Warning);
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        GUILayout.Space(20);
        
        // Кнопка создания сцены
        if (GUILayout.Button("Создать AR сцену"))
        {
            ARWallPaintingSceneCreator.CreateARWallPaintingScene();
            EditorUtility.DisplayDialog("Готово", "AR сцена создана успешно!", "OK");
        }
    }
    
    // Копирование модели в StreamingAssets
    private void CopyModelToStreamingAssets(string sourcePath)
    {
        // Проверяем, что папка StreamingAssets существует
        if (!Directory.Exists(Application.streamingAssetsPath))
        {
            Directory.CreateDirectory(Application.streamingAssetsPath);
        }
        
        // Получаем имя файла
        string fileName = Path.GetFileName(sourcePath);
        string destinationPath = Path.Combine(Application.streamingAssetsPath, fileName);
        
        try
        {
            // Копируем файл
            File.Copy(sourcePath, destinationPath, true);
            Debug.Log($"Файл скопирован в: {destinationPath}");
            
            // Обновляем AssetDatabase
            AssetDatabase.Refresh();
            
            EditorUtility.DisplayDialog("Успех", $"Модель {fileName} скопирована в StreamingAssets", "OK");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Ошибка при копировании файла: {e.Message}");
            EditorUtility.DisplayDialog("Ошибка", $"Не удалось скопировать файл: {e.Message}", "OK");
        }
    }
}
#endif