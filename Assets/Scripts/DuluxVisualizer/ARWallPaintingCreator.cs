using UnityEngine;
using UnityEngine.XR.ARFoundation;
using Unity.XR.CoreUtils;
using UnityEngine.UI;
using Unity.Barracuda;
using System.Collections;

/// <summary>
/// Creates and sets up an AR scene for wall painting using wall segmentation
/// </summary>
public class ARWallPaintingCreator : MonoBehaviour
{
      // Singleton reference
      private static ARWallPaintingCreator instance;

      // AR Components
      private ARSession arSession;
      private XROrigin xrOrigin;
      private ARCameraManager arCameraManager;
      private Camera arCamera;

      // Segmentation components
      private WallSegmentation wallSegmentation;
      private RenderTexture maskRT;

      // Wall painting components
      private WallPaintBlit wallPaintBlit;

      // UI components
      private Canvas mainCanvas;
      private Slider opacitySlider;
      private GameObject colorPickerPanel;

      void Awake()
      {
            if (instance == null)
            {
                  instance = this;
                  DontDestroyOnLoad(gameObject);
            }
            else
            {
                  Destroy(gameObject);
            }
      }

      /// <summary>
      /// Creates a complete AR Wall Painting scene
      /// </summary>
      public static void CreateScene()
      {
            GameObject sceneRoot = new GameObject("AR Wall Painting Scene");

            // Add the scene creator component
            ARWallPaintingCreator creator = sceneRoot.AddComponent<ARWallPaintingCreator>();

            // Create and set up the AR scene
            creator.SetupARSession();
            creator.SetupAROrigin();
            creator.SetupWallSegmentation();
            creator.SetupWallPaintBlit();
            creator.SetupUI();
      }

      /// <summary>
      /// Sets up the AR Session for tracking
      /// </summary>
      private void SetupARSession()
      {
            GameObject sessionObject = new GameObject("AR Session");
            sessionObject.transform.parent = transform;

            // Add AR Session component
            arSession = sessionObject.AddComponent<ARSession>();
      }

      /// <summary>
      /// Sets up the XR Origin with AR Camera
      /// </summary>
      private void SetupAROrigin()
      {
            GameObject originObject = new GameObject("XR Origin");
            originObject.transform.parent = transform;

            // Add XR Origin component
            xrOrigin = originObject.AddComponent<XROrigin>();

            // Create camera offset
            GameObject cameraOffset = new GameObject("Camera Offset");
            cameraOffset.transform.parent = originObject.transform;
            xrOrigin.CameraFloorOffsetObject = cameraOffset;

            // Create AR Camera
            GameObject cameraObject = new GameObject("AR Camera");
            cameraObject.transform.parent = cameraOffset.transform;

            // Add camera component
            arCamera = cameraObject.AddComponent<Camera>();
            arCamera.clearFlags = CameraClearFlags.SolidColor;
            arCamera.backgroundColor = Color.black;
            arCamera.nearClipPlane = 0.1f;
            arCamera.farClipPlane = 20f;

            // Add AR Camera components
            arCameraManager = cameraObject.AddComponent<ARCameraManager>();
            cameraObject.AddComponent<ARCameraBackground>();

            // Set camera in XR Origin
            xrOrigin.Camera = arCamera;

            // Create trackables parent for detected planes
            GameObject trackablesParent = new GameObject("Trackables");
            trackablesParent.transform.parent = originObject.transform;

            // Add AR Plane Manager
            GameObject planeManagerObject = new GameObject("AR Plane Manager");
            planeManagerObject.transform.parent = originObject.transform;
            ARPlaneManager planeManager = planeManagerObject.AddComponent<ARPlaneManager>();
            planeManager.planePrefab = Resources.Load<GameObject>("AR Plane");
      }

      /// <summary>
      /// Sets up wall segmentation component
      /// </summary>
      private void SetupWallSegmentation()
      {
            // Add wall segmentation to the camera
            wallSegmentation = arCamera.gameObject.AddComponent<WallSegmentation>();

            // Configure segmentation
            wallSegmentation.cameraManager = arCameraManager;

            // Load the segmentation model
            NNModel wallSegmentationModel = Resources.Load<NNModel>("Models/wall_segmentation_model");
            if (wallSegmentationModel != null)
            {
                  wallSegmentation.modelAsset = wallSegmentationModel;
            }

            // Create mask render texture
            maskRT = new RenderTexture(256, 256, 0, RenderTextureFormat.R8);
            maskRT.Create();
            wallSegmentation.outputRenderTexture = maskRT;
      }

      /// <summary>
      /// Sets up the Wall Paint post-processing
      /// </summary>
      private void SetupWallPaintBlit()
      {
            // Add WallPaintBlit component to camera
            wallPaintBlit = arCamera.gameObject.AddComponent<WallPaintBlit>();

            // Assign the mask texture
            wallPaintBlit.maskTexture = maskRT;

            // Set default color and opacity
            wallPaintBlit.paintColor = new Color(0.8f, 0.2f, 0.3f, 1.0f);
            wallPaintBlit.opacity = 0.7f;
      }

      /// <summary>
      /// Sets up UI for color picking and opacity controls
      /// </summary>
      private void SetupUI()
      {
            // Create main canvas
            GameObject canvasObject = new GameObject("Main Canvas");
            canvasObject.transform.parent = transform;

            // Add canvas components
            mainCanvas = canvasObject.AddComponent<Canvas>();
            mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();

            // Create opacity slider
            GameObject sliderObject = new GameObject("Opacity Slider");
            sliderObject.transform.parent = canvasObject.transform;
            opacitySlider = sliderObject.AddComponent<Slider>();
            opacitySlider.minValue = 0f;
            opacitySlider.maxValue = 1f;
            opacitySlider.value = 0.7f;

            // Add event listener for slider
            opacitySlider.onValueChanged.AddListener(SetOpacity);

            // Create color picker panel
            colorPickerPanel = new GameObject("Color Picker Panel");
            colorPickerPanel.transform.parent = canvasObject.transform;

            // Add color buttons
            AddColorButton(colorPickerPanel, "Red", Color.red);
            AddColorButton(colorPickerPanel, "Green", Color.green);
            AddColorButton(colorPickerPanel, "Blue", Color.blue);

            // Position UI elements
            RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0.1f, 0.9f);
            sliderRect.anchorMax = new Vector2(0.4f, 0.95f);
            sliderRect.anchoredPosition = Vector2.zero;
            sliderRect.sizeDelta = Vector2.zero;

            RectTransform panelRect = colorPickerPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.1f, 0.1f);
            panelRect.anchorMax = new Vector2(0.9f, 0.3f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = Vector2.zero;
      }

      /// <summary>
      /// Adds a color selection button to the panel
      /// </summary>
      private void AddColorButton(GameObject parent, string name, Color color)
      {
            GameObject buttonObject = new GameObject(name + " Button");
            buttonObject.transform.parent = parent.transform;

            // Add button component
            Button button = buttonObject.AddComponent<Button>();

            // Add image for button background
            Image buttonImage = buttonObject.AddComponent<Image>();
            buttonImage.color = color;

            // Set button target graphic
            button.targetGraphic = buttonImage;

            // Add event listener
            button.onClick.AddListener(() => SetPaintColor(color));

            // Position button
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.1f, 0.1f);
            buttonRect.anchorMax = new Vector2(0.2f, 0.9f);

            // Adjust position based on name to create a row of buttons
            float xPos = 0;
            if (name == "Red") xPos = 0.1f;
            else if (name == "Green") xPos = 0.3f;
            else if (name == "Blue") xPos = 0.5f;

            buttonRect.anchorMin = new Vector2(xPos, 0.1f);
            buttonRect.anchorMax = new Vector2(xPos + 0.1f, 0.9f);
            buttonRect.anchoredPosition = Vector2.zero;
            buttonRect.sizeDelta = Vector2.zero;
      }

      /// <summary>
      /// Sets the opacity of the paint effect
      /// </summary>
      private void SetOpacity(float value)
      {
            if (wallPaintBlit != null)
            {
                  wallPaintBlit.opacity = value;
            }
      }

      /// <summary>
      /// Sets the color of the paint effect
      /// </summary>
      private void SetPaintColor(Color color)
      {
            if (wallPaintBlit != null)
            {
                  wallPaintBlit.paintColor = color;
            }
      }
}