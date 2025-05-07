using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Инструкции и утилиты для подготовки модели YOLOv8n-seg для SentisWallSegmentation
/// </summary>
public class YOLOv8ModelPreparation : MonoBehaviour
{
      /// <summary>
      /// Руководство по подготовке модели YOLOv8n-seg для Unity Sentis
      /// </summary>
      [TextArea(20, 50)]
      public string preparationGuide = @"# Руководство по подготовке модели YOLOv8n-seg для Unity Sentis

## Шаг 1: Установка необходимых пакетов в Python

```bash
pip install ultralytics
pip install onnx
pip install onnxruntime
```

## Шаг 2: Экспортировать модель YOLOv8n-seg в формат ONNX

```python
from ultralytics import YOLO

# Загрузить предобученную модель YOLOv8n-seg
model = YOLO('yolov8n-seg.pt')  

# Экспортировать модель в ONNX формат 
# Используем opset 15 для совместимости с Unity Sentis
model.export(format='onnx', opset=15)
```

## Шаг 3: Проверка и тестирование ONNX модели

```python
import onnx
import numpy as np
import onnxruntime as ort

# Загружаем ONNX модель и проверяем ее
onnx_model = onnx.load('yolov8n-seg.onnx')
onnx.checker.check_model(onnx_model)

# Получаем информацию о входах и выходах модели
model_inputs = [input.name for input in onnx_model.graph.input]
model_outputs = [output.name for output in onnx_model.graph.output]

print(f'Входы модели: {model_inputs}')
print(f'Выходы модели: {model_outputs}')

# Создаем сессию для инференса
session = ort.InferenceSession('yolov8n-seg.onnx')

# Вывод информации о форме входных и выходных тензоров
input_name = session.get_inputs()[0].name
input_shape = session.get_inputs()[0].shape
print(f'Форма входного тензора: {input_name} - {input_shape}')

for output in session.get_outputs():
    print(f'Форма выходного тензора: {output.name} - {output.shape}')
```

## Шаг 4: Импортирование модели в Unity Sentis

1. Поместите ONNX модель в папку Assets/Models/
2. Импортируйте модель через Sentis Model Importer
3. Убедитесь, что формат ONNX поддерживается (opset 15)
4. Настройте ModelAsset в SentisWallSegmentation

## Шаг 5: Настройка параметров SentisWallSegmentation для YOLOv8n-seg

```csharp
// Предварительные настройки для YOLOv8n-seg
wallSegmentation.inputName = 'images';  // Стандартное имя входа для YOLOv8
wallSegmentation.outputName = 'output0'; // Стандартное имя выхода для YOLOv8
wallSegmentation.wallClassIndex = 0;    // Индекс класса стены (зависит от модели)
wallSegmentation.inputWidth = 640;      // Стандартная ширина входа YOLOv8
wallSegmentation.inputHeight = 640;     // Стандартная высота входа YOLOv8
wallSegmentation.useNCHW = true;        // YOLOv8 использует формат NCHW
```

## Замечания по обработке выходного тензора YOLOv8n-seg

YOLOv8n-seg обычно выдает несколько выходных тензоров:
1. Тензор детекций (bounding boxes, scores, class ids)
2. Тензор масок сегментации (proto)
3. Тензор масок для каждого обнаруженного объекта

В SentisWallSegmentation нам нужно правильно обрабатывать маску сегментации класса 'wall'. 
В CreateSegmentationTexture() необходимо учитывать особенности формата выходных данных YOLOv8n-seg.

## Полезные ссылки

- https://github.com/wojciechp6/YOLOv8Unity - Пример интеграции YOLOv8 в Unity
- https://huggingface.co/unity/sentis-YOLOv8n - Готовая модель YOLOv8n для Sentis
- https://github.com/teamunitlab/yolo8-segmentation-deploy - Пример развертывания YOLOv8-seg
- https://docs.unity3d.com/Packages/com.unity.sentis@1.4/manual/export-convert-onnx.html - Документация Unity по импорту ONNX
";

      /// <summary>
      /// Вспомогательный метод для создания простого шаблона Python-скрипта для экспорта модели
      /// </summary>
      public string GenerateExportScript()
      {
            return @"
# export_yolov8_seg.py
from ultralytics import YOLO

# Загрузить предобученную модель YOLOv8n-seg или свою собственную
model = YOLO('yolov8n-seg.pt')  # или путь к обученной модели

# Экспортировать модель в ONNX формат с opset 15 для совместимости с Unity Sentis
model.export(format='onnx', opset=15, dynamic=True, simplify=True)

print('Модель успешно экспортирована в формат ONNX!')
";
      }

      /// <summary>
      /// Вспомогательный метод для создания шаблона C# кода для использования YOLOv8-seg
      /// </summary>
      public string GenerateUnityImplementationExample()
      {
            return @"
using System;
using System.Collections;
using UnityEngine;
using Unity.Sentis;

// Пример использования YOLOv8n-seg с Unity Sentis
public class YOLOv8SegmentationExample : MonoBehaviour
{
    public ModelAsset modelAsset; // Ссылка на .onnx или .sentis файл модели
    public Texture2D inputImage;  // Входное изображение
    public RenderTexture outputMask; // Выходная маска сегментации
    
    private Model runtimeModel;
    private IWorker worker;
    private TensorFloat inputTensor;
    
    void Start()
    {
        // Загружаем модель
        runtimeModel = ModelLoader.Load(modelAsset);
        
        // Создаем worker для выполнения инференса
        worker = WorkerFactory.CreateWorker(BackendType.GPUCompute, runtimeModel);
        
        // Вызываем сегментацию для тестового изображения
        StartCoroutine(ProcessSegmentation());
    }
    
    IEnumerator ProcessSegmentation()
    {
        // Преобразуем входное изображение в тензор
        TensorShape inputShape = new TensorShape(1, 3, 640, 640); // Формат NCHW для YOLOv8
        inputTensor = new TensorFloat(inputShape);
        
        // Используем TextureConverter для заполнения тензора из текстуры
        TextureConverter.ToTensor(inputImage, inputTensor, 
            new TextureTransform(640, 640, 3).SetStandardImageNetNormalization());
        
        // Выполняем инференс
        worker.Execute(inputTensor);
        
        // Получаем результаты сегментации
        // Примечание: имена выходных тензоров зависят от модели
        var detectionsTensor = worker.PeekOutput('output0') as TensorFloat;
        var maskProtoTensor = worker.PeekOutput('output1') as TensorFloat;
        
        // Здесь выполняется обработка выходных тензоров для получения масок сегментации
        // ...
        
        // Освобождаем ресурсы
        if (inputTensor != null)
            inputTensor.Dispose();
            
        yield return null;
    }
    
    void OnDestroy()
    {
        if (worker != null)
        {
            worker.Dispose();
            worker = null;
        }
        
        if (inputTensor != null)
        {
            inputTensor.Dispose();
            inputTensor = null;
        }
    }
}
";
      }
}