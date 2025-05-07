using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.UI;
using Unity.XR.CoreUtils;
using System.Collections;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Скрипт для создания демо-сцены с SentisWallSegmentation
/// </summary>
public class DemoSceneCreator : MonoBehaviour
{
      /// <summary>
      /// Создает новую демо-сцену AR с компонентами для сегментации стен
      /// </summary>
      public static void CreateDemoScene()
      {
#if UNITY_EDITOR
        // Создаем новую пустую сцену
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Создаем AR-сессию
        GameObject sessionObject = new GameObject("AR Session");
        sessionObject.AddComponent<ARSession>();
        sessionObject.AddComponent<ARInputManager>();

        // Создаем XR Origin
        GameObject originObject = new GameObject("XR Origin");
        XROrigin origin = originObject.AddComponent<XROrigin>();

        // Создаем AR камеру
        GameObject cameraObject = new GameObject("AR Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.tag = "MainCamera";

        // Добавляем компоненты AR к камере
        cameraObject.AddComponent<ARCameraManager>();
        cameraObject.AddComponent<ARCameraBackground>();
        cameraObject.AddComponent<ARPoseDriver>();

        // Устанавливаем камеру как дочерний объект XR Origin
        cameraObject.transform.SetParent(originObject.transform);
        origin.Camera = camera;

        // Создаем Trackables объект для AR-плоскостей
        GameObject trackablesObject = new GameObject("Trackables");
        trackablesObject.transform.SetParent(originObject.transform);

        // Добавляем AR Plane Manager
        GameObject planeManagerObject = new GameObject("AR Plane Manager");
        planeManagerObject.transform.SetParent(originObject.transform);
        ARPlaneManager planeManager = planeManagerObject.AddComponent<ARPlaneManager>();

        // Добавляем AR Raycast Manager для взаимодействия
        originObject.AddComponent<ARRaycastManager>();

        // Добавляем компонент SentisWallSegmentation к камере
        SentisWallSegmentation wallSegmentation = cameraObject.AddComponent<SentisWallSegmentation>();
        wallSegmentation.cameraManager = cameraObject.GetComponent<ARCameraManager>();
        wallSegmentation.arCamera = camera;

        // Создаем RenderTexture для маски сегментации
        RenderTexture maskRT = new RenderTexture(256, 256, 0, RenderTextureFormat.R8);
        maskRT.name = "SegmentationMask";
        wallSegmentation.outputRenderTexture = maskRT;

        // Добавляем WallPaintBlit для эффекта перекраски
        WallPaintBlit wallPaintBlit = cameraObject.AddComponent<WallPaintBlit>();
        wallPaintBlit.maskTexture = maskRT;
        wallPaintBlit.paintColor = new Color(0.2f, 0.5f, 0.9f, 0.8f);
        wallPaintBlit.opacity = 0.7f;

        // Создаем Canvas для отладочного интерфейса
        GameObject canvasObject = new GameObject("Debug Canvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // Создаем отладочное изображение
        GameObject debugImageObject = new GameObject("Debug Image");
        debugImageObject.transform.SetParent(canvasObject.transform, false);
        RawImage debugImage = debugImageObject.AddComponent<RawImage>();
        RectTransform debugRect = debugImageObject.GetComponent<RectTransform>();
        debugRect.anchorMin = new Vector2(0, 0.7f);
        debugRect.anchorMax = new Vector2(0.3f, 1f);
        debugRect.offsetMin = new Vector2(10, 10);
        debugRect.offsetMax = new Vector2(-10, -10);

        // Создаем текст с информацией
        GameObject infoTextObject = new GameObject("Info Text");
        infoTextObject.transform.SetParent(canvasObject.transform, false);
        Text infoText = infoTextObject.AddComponent<Text>();
        infoText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        infoText.fontSize = 14;
        infoText.color = Color.white;
        infoText.text = "Wall Segmentation Demo\nTouch screen to change color";
        RectTransform infoRect = infoTextObject.GetComponent<RectTransform>();
        infoRect.anchorMin = new Vector2(0, 0);
        infoRect.anchorMax = new Vector2(1, 0.1f);
        infoRect.offsetMin = new Vector2(10, 10);
        infoRect.offsetMax = new Vector2(-10, -10);

        // Добавляем демо-контроллер
        GameObject controllerObject = new GameObject("Demo Controller");
        SentisWallSegmentationDemo demo = controllerObject.AddComponent<SentisWallSegmentationDemo>();
        demo.arSession = sessionObject.GetComponent<ARSession>();
        demo.xrOrigin = origin;
        demo.cameraManager = cameraObject.GetComponent<ARCameraManager>();
        demo.planeManager = planeManager;
        demo.wallSegmentation = wallSegmentation;
        demo.segmentationMask = maskRT;
        demo.wallPaintBlit = wallPaintBlit;
        demo.debugCanvas = canvas;
        demo.debugImage = debugImage;
        demo.infoText = infoText;

        // Опционально - добавляем источник света для лучшей визуализации
        GameObject lightObject = new GameObject("Directional Light");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1f;
        light.color = Color.white;
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // Сохраняем сцену
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Demo/SentisWallSegmentationDemo.unity");
        
        Debug.Log("Демо-сцена успешно создана и сохранена по пути: Assets/Scenes/Demo/SentisWallSegmentationDemo.unity");
#else
            Debug.LogWarning("Создание демо-сцены доступно только в редакторе Unity");
#endif
      }

#if UNITY_EDITOR
    [MenuItem("DuluxVisualizer/Create Sentis Demo Scene")]
    private static void CreateDemoSceneMenuItem()
    {
        CreateDemoScene();
    }
#endif
}