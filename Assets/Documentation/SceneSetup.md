# Руководство по настройке сцены приложения AR Wall Painting

В этом документе описаны шаги по настройке базовой сцены для приложения покраски стен в AR.

## Шаг 1: Создание AR-сцены

1. В меню Unity выберите **Tools → AR → Create AR Wall Painting Scene**
2. Будет создана новая сцена с базовыми AR-компонентами:
   - AR Session
   - AR Session Origin с камерой
   - AR Plane Manager
   - AR Mesh Manager

3. Сохраните сцену в папке Assets/Scenes с именем "ARWallPainting"

## Шаг 2: Добавление главного контроллера приложения

1. В созданной сцене добавьте пустой GameObject с именем "AppController"
2. Добавьте компонент **ARWallPaintingApp** к этому объекту
3. Настройте ссылки в инспекторе:
   - AR Session: перетащите объект AR Session
   - AR Session Origin: перетащите объект AR Session Origin
   - AR Plane Manager: перетащите компонент AR Plane Manager
   - AR Raycast Manager: добавьте и перетащите компонент AR Raycast Manager

## Шаг 3: Настройка сегментации стен

1. Добавьте пустой GameObject с именем "WallSegmentationManager"
2. Добавьте компонент **WallSegmentation** к этому объекту
3. Настройте ссылки:
   - AR Camera Manager: перетащите компонент AR Camera Manager с камеры
   - AR Session Origin: перетащите объект AR Session Origin
   - Model Asset: укажите файл ONNX-модели из папки Models
4. Укажите параметры входной и выходной модели:
   - Input Name: обычно "input" или "ImageTensor"
   - Output Name: обычно "output" или "final_result" 
   - Input Width/Height: размеры входа модели (например, 256x256)
   - Wall Class Index: индекс класса стен (обычно 1)
   - Threshold: порог определения (обычно 0.5)

## Шаг 4: Настройка компонента покраски стен

1. Добавьте пустой GameObject с именем "WallPainterManager"
2. Добавьте компонент **WallPainter** к этому объекту
3. Настройте ссылки:
   - AR Raycast Manager: перетащите компонент AR Raycast Manager
   - AR Session Origin: перетащите объект AR Session Origin
   - AR Camera: перетащите камеру из AR Session Origin
   - Wall Segmentation: перетащите компонент WallSegmentation
   - Wall Prefab: перетащите префаб PaintedWall из папки Prefabs

## Шаг 5: Настройка пользовательского интерфейса

1. Создайте объект Canvas в сцене:
   - Убедитесь, что Render Mode установлен на "Screen Space - Overlay"
   - Добавьте компонент Canvas Scaler и настройте его для мобильных устройств

2. Добавьте компонент **UIManager** к объекту Canvas
3. Настройте ссылки:
   - Wall Painter: перетащите компонент WallPainter
   - Wall Segmentation: перетащите компонент WallSegmentation

4. Создайте базовые UI элементы:
   - Панель с 8 кнопками для выбора цвета
   - Два слайдера (для размера кисти и интенсивности)
   - Кнопку сброса
   - Кнопку для скрытия/показа палитры
   - Text элемент для статусных сообщений

5. Настройте ссылки в UIManager:
   - Color Palette: перетащите родительский объект кнопок цветов
   - Color Buttons: укажите массив кнопок выбора цвета
   - Brush Size Slider: перетащите слайдер размера кисти
   - Intensity Slider: перетащите слайдер интенсивности
   - Reset Button: перетащите кнопку сброса
   - Toggle Palette Button: перетащите кнопку переключения палитры
   - Status Text: перетащите текстовый элемент для статусных сообщений

## Шаг 6: Настройка отладочной визуализации сегментации

1. Добавьте UI элемент RawImage в углу экрана
2. Перетащите этот элемент в поле Debug Image компонента WallSegmentation
3. Настройте размер и положение этого элемента, чтобы он не мешал основному взаимодействию

## Шаг 7: Настройка материала для покраски

1. Создайте новый материал в папке Materials с именем "WallPaintMaterial"
2. Назначьте шейдер "Custom/WallPaint" для этого материала
3. Настройте параметры материала:
   - Color: начальный цвет (например, белый с альфа = 0.5)
   - Smoothness: 0.1-0.3 для матовой поверхности
   - Metallic: 0 для нематаллической поверхности

## Шаг 8: Подключение всех компонентов

1. Перетащите компоненты UIManager, WallPainter и WallSegmentation в соответствующие поля ARWallPaintingApp
2. Перетащите материал WallPaintMaterial в поле Wall Paint Material компонента ARWallPaintingApp

## Шаг 9: Настройка проекта для мобильных устройств

1. Откройте Player Settings (Edit → Project Settings → Player)
2. В разделе Other Settings:
   - Отметьте "Auto Graphics API"
   - Для Android: выберите "Multithreaded Rendering" и "ARM64"
   - Для iOS: настройте требуемые параметры для ARKit

3. В разделе XR Plug-in Management:
   - Включите "ARCore" для Android
   - Включите "ARKit" для iOS

## Шаг 10: Тестирование

1. Запустите сцену в редакторе для проверки базовой функциональности
2. Проверьте, что все компоненты находят друг друга через FindObjectOfType (если ссылки не указаны вручную)
3. Создайте сборку для мобильного устройства и протестируйте на реальном устройстве
4. Убедитесь, что все компоненты работают как ожидается 