# Исправление ошибки с загрузкой ONNX моделей

## Проблема

При загрузке ONNX модели в Unity с использованием Barracuda может возникать ошибка:
```
Ошибка загрузки внешней модели: Format version not supported: 246
```

Эта ошибка возникает, когда вы пытаетесь загрузить ONNX файл напрямую через `ModelLoader.Load()` - такой подход работает только для нативного формата Barracuda (`.nn`), но не для исходных ONNX файлов.

## Решение

Для правильной загрузки ONNX моделей используйте класс `ONNXModelConverter`:

```csharp
using Unity.Barracuda.ONNX;

// ...

string fullPath = Path.Combine(Application.streamingAssetsPath, "model.onnx");
byte[] onnxBytes = File.ReadAllBytes(fullPath);

// Создаем конвертер ONNX моделей
ONNXModelConverter converter = new ONNXModelConverter(
    optimizeModel: true,
    treatErrorsAsWarnings: false,
    forceArbitraryBatchSize: true
);

// Конвертируем ONNX в модель Barracuda Model
Model model = converter.Convert(onnxBytes);

// Создаем worker для инференса
IWorker worker = WorkerFactory.CreateWorker(WorkerFactory.Type.Auto, model);
```

## Рекомендации по экспорту моделей

При экспорте моделей в ONNX для использования в Barracuda:

1. Используйте Pytorch, TensorFlow 1.x или Keras
2. Задавайте `opset_version=9` при экспорте в ONNX
3. При экспорте из TensorFlow/Keras используйте флаг `inputs_as_nchw` для преобразования из NHWC в NCHW
4. Для PyTorch: `torch.onnx.export(model, dummy_input, "model.onnx", opset_version=9)`
5. Для TensorFlow: `tf2onnx.convert --saved-model path/to/model --output model.onnx --opset 9`

## Дополнительные материалы

- [Unity Barracuda Documentation](https://docs.unity3d.com/Packages/com.unity.barracuda@1.3/manual/)
- [ONNX Supported Operators](https://docs.unity3d.com/Packages/com.unity.barracuda@1.3/manual/SupportedOperators.html)
- [Unity Forum - ONNX Import Issues](https://forum.unity.com/threads/onnx-import-issues.1125944/) 