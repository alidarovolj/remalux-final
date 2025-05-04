# OpenCV для проекта AR Wall Painting

Данный проект использует OpenCV для улучшения качества сегментации стен. Для корректной работы приложения необходимо установить и настроить плагин OpenCV в Unity.

## Инструкция по установке OpenCV

### Вариант 1: OpenCV for Unity (рекомендуется)

1. Приобретите и импортируйте пакет [OpenCV for Unity](https://assetstore.unity.com/packages/tools/integration/opencv-for-unity-21088) из Asset Store.
2. После импорта, перейдите в меню **Tools > AR Wall Painting > Setup OpenCV** для автоматической настройки проекта.
3. Убедитесь, что в консоли появилось сообщение "OpenCV успешно включен!".

### Вариант 2: OpenCvSharp (бесплатный вариант)

1. Загрузите [OpenCvSharp](https://github.com/shimat/opencvsharp) и распакуйте плагин в директорию `Assets/Plugins/OpenCvSharp`.
2. Скопируйте соответствующие DLL файлы для целевых платформ (Windows, Android, iOS).
3. Перейдите в **Tools > AR Wall Painting > Setup OpenCV** для настройки проекта.

### Вариант 3: Альтернативные плагины

Также можно использовать другие плагины для OpenCV:
- [OpenCV plus Unity](https://assetstore.unity.com/packages/tools/integration/opencv-plus-unity-85928)
- [Emgu CV](https://assetstore.unity.com/packages/tools/integration/emgu-cv-25925)

## Проверка настройки

1. После установки OpenCV, компонент **OpenCVProcessor** должен работать без ошибок.
2. В редакторе Unity можно настроить параметры обработки в инспекторе компонента **OpenCVProcessor**.
3. Убедитесь, что в **Player Settings > Scripting Define Symbols** присутствует символ `OPENCV_ENABLED`.

## Особенности использования

- OpenCV используется для пост-обработки маски сегментации стен, что улучшает точность и качество краев.
- Можно отключить использование OpenCV через инспектор компонента **OpenCVProcessor**, установив флаг `useOpenCV` в false.
- Настройте параметры блюра, эрозии и дилатации для достижения наилучшего результата в вашем окружении.

## Устранение проблем

- Если возникают ошибки компиляции, убедитесь, что DLL файлы OpenCV совместимы с целевой платформой.
- При ошибках времени выполнения, проверьте логи на наличие сообщений об ошибках инициализации OpenCV.
- При необходимости, обновите версию OpenCV до последней стабильной. 