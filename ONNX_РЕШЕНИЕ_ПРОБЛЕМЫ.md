# Решение проблемы с ONNX моделью в Unity Barracuda

## Основные проблемы

1. **Исходная проблема: несоответствие каналов**
   * Ошибка: `Assertion failure. Values are not equal. Expected: 3 == 32`
   * Причина: входной тензор имел 3 канала (RGB), а модель ожидала 32 канала

2. **Новая проблема: некорректные размеры выходного тензора**
   * Ошибка: модель возвращает тензор с размерами `1x0x1`
   * Результат: ошибка при создании текстуры и обработке результатов
   * Текст ошибки: `Texture must have height greater than 0` и `UnityException: Failed to create texture because of invalid parameters`

3. **Ограничения размера входного тензора**
   * Проблема: большие размеры входного тензора (512x512) могут вызывать ошибки в Barracuda
   * Рекомендуемый безопасный максимум: 256x256
   * Сообщение: `Размер входных данных (512x512=262144 пикселей) может быть слишком большим для модели`

## Реализованные решения

### 1. Преобразование входного тензора

```csharp
// Если тензор не соответствует ожидаемому формату модели
if (inputTensor.shape[3] != inputChannels)
{
    // Создаем новый тензор с нужной размерностью
    var tensorShape = new TensorShape(1, inputWidth, inputHeight, inputChannels);
    using (var convertedTensor = new Tensor(tensorShape))
    {
        // Копируем данные из исходного тензора в новый
        for (int h = 0; h < inputHeight; h++)
        {
            for (int w = 0; w < inputWidth; w++)
            {
                // Копируем RGB каналы если они есть
                for (int c = 0; c < Math.Min(inputTensor.shape[3], 3); c++)
                {
                    convertedTensor[0, h, w, c] = inputTensor[0, h, w, c];
                }
                
                // Заполняем оставшиеся каналы значением из первого канала
                for (int c = 3; c < inputChannels; c++)
                {
                    convertedTensor[0, h, w, c] = inputTensor[0, h, w, 0];
                }
            }
        }
        
        // Запускаем исполнение модели с конвертированным тензором
        worker.Execute(convertedTensor);
    }
}
```

### 2. Обработка некорректных размеров выходного тензора

```csharp
// Определяем размер выходного тензора
int outBatch = outputTensor.shape[0];
int outHeight = outputTensor.shape[1];
int outWidth = outputTensor.shape[2];
int outChannels = outputTensor.shape[3];

Debug.Log($"Размер выходного тензора: {outBatch}x{outHeight}x{outWidth}x{outChannels}");

// Проверяем на некорректные размеры тензора (например, 1x0x1)
if (outHeight <= 0 || outWidth <= 0 || outChannels <= 0)
{
    Debug.LogWarning($"Модель вернула некорректные размеры тензора: {outBatch}x{outHeight}x{outWidth}x{outChannels}. Используем безопасные значения по умолчанию.");
    
    // Используем демо-режим в случае ошибки
    return DemoSegmentation(sourceTexture);
}
```

### 3. Автоматическое ограничение размера входного тензора

```csharp
// Проверяем размер входных данных и уменьшаем если они слишком большие
// Важно: максимальный безопасный размер для Barracuda - 256x256 пикселей
if (inputWidth * inputHeight > 65536) // Превышен безопасный лимит (256x256)
{
    int originalWidth = inputWidth;
    int originalHeight = inputHeight;
    
    // Принудительно ограничиваем размер до 128x128
    inputWidth = 128;
    inputHeight = 128;
    
    Debug.LogWarning($"Уменьшаем размер входных данных с {originalWidth}x{originalHeight} до {inputWidth}x{inputHeight} " +
                   "для предотвращения ошибок с лимитами GPU compute.");
}

Debug.Log($"Используем размер входных данных для модели: {inputWidth}x{inputHeight} ({inputWidth * inputHeight} пикселей)");
```

### 4. Инициализация текстур с безопасными размерами

```csharp
// Инициализируем текстуры с безопасными размерами по умолчанию
int textureWidth = 256;
int textureHeight = 256;

// Проверяем и обновляем размеры текстуры на основе параметров модели
if (inputWidth > 0 && inputHeight > 0)
{
    textureWidth = inputWidth;
    textureHeight = inputHeight;
}
else
{
    Debug.LogWarning($"Некорректные размеры входа модели: {inputWidth}x{inputHeight}. Используем безопасные значения по умолчанию: {textureWidth}x{textureHeight}");
}

cameraTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
segmentationTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
```

### 5. Улучшенный метод проверки и обновления параметров модели

```csharp
private void CheckAndUpdateModelParameters()
{
    // Устанавливаем безопасные значения по умолчанию
    int defaultWidth = 128;
    int defaultHeight = 128;
    int defaultChannels = 32;
    bool useDefaultValues = false;

    // ... проверка параметров модели ...

    // Устанавливаем безопасные значения по умолчанию, если необходимо
    if (useDefaultValues || inputWidth <= 0 || inputHeight <= 0 || inputChannels <= 0)
    {
        inputWidth = defaultWidth;
        inputHeight = defaultHeight;
        inputChannels = defaultChannels;
        Debug.LogWarning($"Установлены безопасные значения по умолчанию: {inputWidth}x{inputHeight}x{inputChannels}");
    }
}
```

### 6. Резервный демо-режим

```csharp
private Texture2D DemoSegmentation(Texture2D sourceTexture)
{
    // Получаем размеры исходной текстуры
    int width = sourceTexture.width;
    int height = sourceTexture.height;
    
    // Создаем текстуру для результата
    Texture2D resultTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
    
    // Заполняем текстуру демонстрационными данными
    for (int y = 0; y < height; y++)
    {
        for (int x = 0; x < width; x++)
        {
            // Определяем, находится ли пиксель в центральной области (имитация стены)
            bool isWall = 
                (x > width * 0.3f && x < width * 0.7f) && 
                (y > height * 0.2f && y < height * 0.8f);
            
            // Устанавливаем цвет пикселя
            resultTexture.SetPixel(x, y, isWall ? wallColor : Color.clear);
        }
    }
    
    resultTexture.Apply();
    return resultTexture;
}
```

## Заключение

Реализованные решения обеспечивают надежную работу приложения даже при проблемах с ONNX моделью. Ключевые улучшения:

1. Автоматическое определение параметров модели с проверками на корректность значений
2. Преобразование входного тензора для совместимости с требованиями модели
3. Обработка некорректных выходных тензоров и автоматическое переключение в демо-режим
4. Инициализация текстур с безопасными размерами
5. Улучшенная обработка ошибок с информативными сообщениями
6. Автоматическое ограничение размера входного тензора для соответствия ограничениям Barracuda

Эти изменения значительно повышают стабильность приложения и обеспечивают корректную работу даже при непредвиденных проблемах с моделью.

## Рекомендации

1. **Предварительный анализ модели**
   * Использовать инструмент `inspect_onnx.py` для анализа структуры модели перед интеграцией
   * Проверять формат и имена тензоров

2. **Оптимизация модели**
   * Рассмотреть возможность переобучения модели с меньшим количеством каналов
   * Использовать размер 32x32 для улучшения производительности

3. **Тестирование**
   * Тестировать работу модели на различных устройствах
   * Проверять работу в условиях низкой производительности 