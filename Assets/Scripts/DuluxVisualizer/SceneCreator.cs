using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

#if UNITY_EDITOR

#if USING_AR_FOUNDATION
using UnityEngine.XR.ARFoundation;
#endif

#if USING_XR_CORE_UTILS
using Unity.XR.CoreUtils;
#endif

namespace DuluxVisualizer
{
    /// <summary>
    /// Централизованный класс для создания AR сцены с функционалом окрашивания стен
    /// </summary>
    public static class SceneCreator
    {
        /// <summary>
        /// Создает полностью настроенную AR сцену с сегментацией стен
        /// </summary>
        public static void CreateScene()
        {
#if USING_AR_FOUNDATION && USING_XR_CORE_UTILS
            Debug.Log("Создание AR сцены для визуализатора Dulux...");
            
            // 1. Создание базовых AR объектов
            GameObject arSession = CreateARSession();
            GameObject xrOrigin = CreateXROrigin();
            
            // 2. Настройка AR камеры
            GameObject arCamera = SetupARCamera(xrOrigin);
            
            // 3. Настройка сегментации стен
            SetupWallSegmentation(arCamera);
            
            // 4. Настройка UI
            SetupUI();
            
            Debug.Log("AR сцена успешно создана!");
#else
            Debug.LogError("Для создания AR сцены необходимы пакеты AR Foundation и XR Core Utils. Пожалуйста, установите их через Package Manager.");
#endif
        }

#if USING_AR_FOUNDATION && USING_XR_CORE_UTILS
        /// <summary>
        /// Создает и настраивает AR Session
        /// </summary>
        private static GameObject CreateARSession()
        {
            GameObject arSession = new GameObject("AR Session");
            arSession.AddComponent<ARSession>();
            return arSession;
        }

        /// <summary>
        /// Создает и настраивает XR Origin
        /// </summary>
        private static GameObject CreateXROrigin()
        {
            GameObject xrOrigin = new GameObject("XR Origin");
            xrOrigin.AddComponent<XROrigin>();
            return xrOrigin;
        }

        /// <summary>
        /// Настраивает AR камеру с необходимыми компонентами
        /// </summary>
        private static GameObject SetupARCamera(GameObject xrOrigin)
        {
            // Создаем объект камеры
            GameObject arCamera = new GameObject("AR Camera");
            arCamera.tag = "MainCamera";
            arCamera.transform.SetParent(xrOrigin.transform);
            
            // Добавляем компоненты камеры
            Camera camera = arCamera.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            
            // Добавляем AR компоненты
            arCamera.AddComponent<ARCameraManager>();
            arCamera.AddComponent<ARCameraBackground>();
            
            return arCamera;
        }

        /// <summary>
        /// Настраивает сегментацию стен и компоненты визуализации
        /// </summary>
        private static void SetupWallSegmentation(GameObject arCamera)
        {
            // Добавляем компонент сегментации
            WallSegmentation segmentation = arCamera.AddComponent<WallSegmentation>();
            
            // Загружаем модель ONNX
            NNModel model = Resources.Load<NNModel>("Models/model");
            if (model != null)
            {
                segmentation.modelAsset = model;
            }
            else
            {
                Debug.LogWarning("ONNX модель не найдена. Пожалуйста, импортируйте модель в папку Resources/Models.");
            }
            
            // Настраиваем RT для маски сегментации
            RenderTexture maskRT = new RenderTexture(256, 256, 0, RenderTextureFormat.R8);
            maskRT.name = "WallMaskRT";
            
            // Добавляем компонент для рендеринга стен
            WallPaintBlit paintBlit = arCamera.AddComponent<WallPaintBlit>();
            paintBlit.maskTexture = maskRT;
            
            // Настраиваем связь между компонентами
            segmentation.outputTexture = maskRT;
            
            // Добавляем оптимизатор
            WallSegmentationOptimizer optimizer = arCamera.AddComponent<WallSegmentationOptimizer>();
            optimizer.segmentation = segmentation;
        }

        /// <summary>
        /// Настраивает UI компоненты для управления окрашиванием
        /// </summary>
        private static void SetupUI()
        {
            // Создаем канвас
            GameObject canvas = new GameObject("UI Canvas");
            Canvas canvasComponent = canvas.AddComponent<Canvas>();
            canvasComponent.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvas.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            
            // Добавляем компонент селектора цветов
            GameObject colorPicker = new GameObject("Color Picker");
            colorPicker.transform.SetParent(canvas.transform, false);
            colorPicker.AddComponent<ColorPickerUI>();
            
            // Это место для добавления дополнительных UI элементов
        }
#endif
    }
}

#endif