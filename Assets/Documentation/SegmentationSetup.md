# Руководство по настройке сегментации стен

В этом документе описаны шаги по настройке и использованию сегментации стен в приложении.

## Подготовка модели

Для работы сегментации стен необходима модель машинного обучения в формате ONNX. Вы можете:

1. Использовать предварительно обученную модель DeepLabV3+ для сегментации стен
2. Обучить собственную модель на кастомном датасете стен
3. Настроить существующую модель для сегментации объектов помещения

### Шаги по экспорту модели в ONNX

#### PyTorch

```python
import torch
from your_model import YourModel

# Инициализация модели
model = YourModel()
model.load_state_dict(torch.load('your_model_weights.pth'))
model.eval()

# Создание примера входных данных
dummy_input = torch.randn(1, 3, 513, 513)

# Экспорт в ONNX
torch.onnx.export(
    model,
    dummy_input,
    "wall_segmentation_model.onnx",
    export_params=True,
    opset_version=11,
    do_constant_folding=True,
    input_names=["input"],
    output_names=["output"],
    dynamic_axes={
        "input": {0: "batch_size"},
        "output": {0: "batch_size"}
    }
)
```

#### TensorFlow

```python
import tensorflow as tf

# Загрузка модели
model = tf.keras.models.load_model('your_model.h5')

# Конвертация в ONNX
import tf2onnx
import onnx

spec = (tf.TensorSpec((1, 513, 513, 3), tf.float32, name="input"),)
output_path = "wall_segmentation_model.onnx"

model_proto, _ = tf2onnx.convert.from_keras(model, input_signature=spec, opset=11, output_path=output_path)
```

### Оптимизация модели

Для улучшения производительности модели можно использовать следующие инструменты:

```bash
# Установка onnx-simplifier
pip install onnx-simplifier

# Упрощение графа модели
python -m onnxsim wall_segmentation_model.onnx optimized_wall_segmentation_model.onnx
```

## Интеграция модели в Unity

1. Поместите оптимизированную ONNX-модель в папку `Assets/Models/`
2. В Unity, найдите объект с компонентом `WallSegmentation`
3. Укажите файл модели в поле `Model Asset`
4. Заполните параметры входа/выхода:
   - Input Name: обычно "input" или "ImageTensor"
   - Output Name: обычно "output" или "final_result"
5. Установите правильный индекс класса для стен (это зависит от обучения вашей модели)

## Настройка параметров сегментации

В компоненте `WallSegmentation` вы можете настроить:

1. **Input Width/Height**: размер входного изображения для модели (обычно 513x513 или 256x256)
2. **Threshold**: порог уверенности для классификации пикселя как стены (0.5 - хорошее начальное значение)
3. **Wall Class Index**: индекс класса стен в выходных данных модели (зависит от обучения)
4. **Debug Visualization**: включает визуализацию результатов сегментации на экране

## Настройка отображения маски

Для отладки сегментации важно настроить отображение маски:

1. Создайте UI-элемент типа RawImage
2. Назначьте его в поле `Debug Image` компонента WallSegmentation
3. Поместите RawImage в удобное место интерфейса (например, в углу экрана)

## Решение проблем

### Низкое качество сегментации

- Проверьте, правильно ли нормализованы входные данные для модели
- Увеличьте или уменьшите порог для классификации пикселей
- Попробуйте обучить модель на более релевантном датасете

### Низкая производительность

- Уменьшите размер входного изображения для модели
- Используйте квантование модели до FP16 или INT8
- Попробуйте более легкую архитектуру модели
- Уменьшите частоту инференса (не обрабатывать каждый кадр)

### Проблемы с памятью

- Освобождайте ресурсы тензоров после использования
- Уменьшите размер модели и входных данных
- Используйте асинхронный инференс 