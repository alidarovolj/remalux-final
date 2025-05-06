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
      private CaptureAndShare captureAndShare;

      // Photo visualizer components
      private PhotoVisualizerMode photoVisualizerMode;
      private Button modeToggleButton;

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
            creator.SetupCaptureAndShare();
            creator.SetupPhotoVisualizer();
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

      /// <summary>
      /// Sets up the capture and share functionality
      /// </summary>
      private void SetupCaptureAndShare()
      {
            // Create capture UI
            GameObject captureObject = new GameObject("Capture UI");
            captureObject.transform.parent = mainCanvas.transform;

            // Add capture & share component
            captureAndShare = captureObject.AddComponent<CaptureAndShare>();

            // Create capture button
            GameObject captureButtonObject = new GameObject("Capture Button");
            captureButtonObject.transform.parent = captureObject.transform;
            Button captureButton = captureButtonObject.AddComponent<Button>();
            Image captureButtonImage = captureButtonObject.AddComponent<Image>();
            captureButtonImage.color = Color.white;
            captureButton.targetGraphic = captureButtonImage;

            // Position capture button (bottom right corner)
            RectTransform captureButtonRect = captureButtonObject.GetComponent<RectTransform>();
            captureButtonRect.anchorMin = new Vector2(0.85f, 0.05f);
            captureButtonRect.anchorMax = new Vector2(0.95f, 0.15f);
            captureButtonRect.anchoredPosition = Vector2.zero;
            captureButtonRect.sizeDelta = Vector2.zero;

            // Add camera icon to capture button
            GameObject cameraIconObject = new GameObject("Camera Icon");
            cameraIconObject.transform.parent = captureButtonObject.transform;
            Image cameraIconImage = cameraIconObject.AddComponent<Image>();
            cameraIconImage.color = Color.black;
            // Position the icon within the button
            RectTransform cameraIconRect = cameraIconObject.GetComponent<RectTransform>();
            cameraIconRect.anchorMin = new Vector2(0.2f, 0.2f);
            cameraIconRect.anchorMax = new Vector2(0.8f, 0.8f);
            cameraIconRect.anchoredPosition = Vector2.zero;
            cameraIconRect.sizeDelta = Vector2.zero;

            // Create preview panel
            GameObject previewPanelObject = new GameObject("Preview Panel");
            previewPanelObject.transform.parent = captureObject.transform;
            Image previewPanelImage = previewPanelObject.AddComponent<Image>();
            previewPanelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);

            // Position the preview panel to cover most of the screen
            RectTransform previewPanelRect = previewPanelObject.GetComponent<RectTransform>();
            previewPanelRect.anchorMin = new Vector2(0.1f, 0.1f);
            previewPanelRect.anchorMax = new Vector2(0.9f, 0.9f);
            previewPanelRect.anchoredPosition = Vector2.zero;
            previewPanelRect.sizeDelta = Vector2.zero;

            // Create preview image
            GameObject previewImageObject = new GameObject("Preview Image");
            previewImageObject.transform.parent = previewPanelObject.transform;
            RawImage previewImage = previewImageObject.AddComponent<RawImage>();

            // Position the preview image
            RectTransform previewImageRect = previewImageObject.GetComponent<RectTransform>();
            previewImageRect.anchorMin = new Vector2(0.05f, 0.15f);
            previewImageRect.anchorMax = new Vector2(0.95f, 0.85f);
            previewImageRect.anchoredPosition = Vector2.zero;
            previewImageRect.sizeDelta = Vector2.zero;

            // Create share button
            GameObject shareButtonObject = new GameObject("Share Button");
            shareButtonObject.transform.parent = previewPanelObject.transform;
            Button shareButton = shareButtonObject.AddComponent<Button>();
            Image shareButtonImage = shareButtonObject.AddComponent<Image>();
            shareButtonImage.color = new Color(0.2f, 0.6f, 1.0f, 1.0f);
            shareButton.targetGraphic = shareButtonImage;

            // Position share button
            RectTransform shareButtonRect = shareButtonObject.GetComponent<RectTransform>();
            shareButtonRect.anchorMin = new Vector2(0.55f, 0.05f);
            shareButtonRect.anchorMax = new Vector2(0.95f, 0.12f);
            shareButtonRect.anchoredPosition = Vector2.zero;
            shareButtonRect.sizeDelta = Vector2.zero;

            // Add text to share button
            GameObject shareTextObject = new GameObject("Share Text");
            shareTextObject.transform.parent = shareButtonObject.transform;
            Text shareText = shareTextObject.AddComponent<Text>();
            shareText.text = "Share";
            shareText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            shareText.alignment = TextAnchor.MiddleCenter;
            shareText.color = Color.white;

            // Position text
            RectTransform shareTextRect = shareTextObject.GetComponent<RectTransform>();
            shareTextRect.anchorMin = Vector2.zero;
            shareTextRect.anchorMax = Vector2.one;
            shareTextRect.anchoredPosition = Vector2.zero;
            shareTextRect.sizeDelta = Vector2.zero;

            // Create close button
            GameObject closeButtonObject = new GameObject("Close Button");
            closeButtonObject.transform.parent = previewPanelObject.transform;
            Button closeButton = closeButtonObject.AddComponent<Button>();
            Image closeButtonImage = closeButtonObject.AddComponent<Image>();
            closeButtonImage.color = new Color(0.8f, 0.2f, 0.2f, 1.0f);
            closeButton.targetGraphic = closeButtonImage;

            // Position close button
            RectTransform closeButtonRect = closeButtonObject.GetComponent<RectTransform>();
            closeButtonRect.anchorMin = new Vector2(0.05f, 0.05f);
            closeButtonRect.anchorMax = new Vector2(0.45f, 0.12f);
            closeButtonRect.anchoredPosition = Vector2.zero;
            closeButtonRect.sizeDelta = Vector2.zero;

            // Add text to close button
            GameObject closeTextObject = new GameObject("Close Text");
            closeTextObject.transform.parent = closeButtonObject.transform;
            Text closeText = closeTextObject.AddComponent<Text>();
            closeText.text = "Close";
            closeText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            closeText.alignment = TextAnchor.MiddleCenter;
            closeText.color = Color.white;

            // Position text
            RectTransform closeTextRect = closeTextObject.GetComponent<RectTransform>();
            closeTextRect.anchorMin = Vector2.zero;
            closeTextRect.anchorMax = Vector2.one;
            closeTextRect.anchoredPosition = Vector2.zero;
            closeTextRect.sizeDelta = Vector2.zero;

            // Connect everything to the CaptureAndShare component
            captureAndShare.captureButton = captureButton;
            captureAndShare.shareButton = shareButton;
            captureAndShare.previewImage = previewImage;
            captureAndShare.previewPanel = previewPanelObject;

            // Add event listener for close button
            closeButton.onClick.AddListener(captureAndShare.ClosePreview);
      }

      /// <summary>
      /// Sets up the Photo Visualizer mode for non-AR devices
      /// </summary>
      private void SetupPhotoVisualizer()
      {
            // Create photo visualizer game object
            GameObject photoVisualizerObject = new GameObject("Photo Visualizer");
            photoVisualizerObject.transform.parent = transform;

            // Add photo visualizer component
            photoVisualizerMode = photoVisualizerObject.AddComponent<PhotoVisualizerMode>();

            // Create photo visualizer panel
            GameObject panelObject = new GameObject("Photo Visualizer Panel");
            panelObject.transform.parent = mainCanvas.transform;
            Image panelImage = panelObject.AddComponent<Image>();
            panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

            // Position panel to cover the full screen
            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = Vector2.zero;

            // Create photo display area
            GameObject displayObject = new GameObject("Photo Display");
            displayObject.transform.parent = panelObject.transform;
            RawImage displayImage = displayObject.AddComponent<RawImage>();
            displayImage.color = Color.white;

            // Position display in the center of the panel
            RectTransform displayRect = displayObject.GetComponent<RectTransform>();
            displayRect.anchorMin = new Vector2(0.1f, 0.2f);
            displayRect.anchorMax = new Vector2(0.9f, 0.8f);
            displayRect.anchoredPosition = Vector2.zero;
            displayRect.sizeDelta = Vector2.zero;

            // Create "Pick Image" button
            GameObject pickButtonObject = new GameObject("Pick Image Button");
            pickButtonObject.transform.parent = panelObject.transform;
            Button pickButton = pickButtonObject.AddComponent<Button>();
            Image pickButtonImage = pickButtonObject.AddComponent<Image>();
            pickButtonImage.color = new Color(0.2f, 0.6f, 1.0f, 1.0f);
            pickButton.targetGraphic = pickButtonImage;

            // Position pick button
            RectTransform pickButtonRect = pickButtonObject.GetComponent<RectTransform>();
            pickButtonRect.anchorMin = new Vector2(0.3f, 0.1f);
            pickButtonRect.anchorMax = new Vector2(0.7f, 0.15f);
            pickButtonRect.anchoredPosition = Vector2.zero;
            pickButtonRect.sizeDelta = Vector2.zero;

            // Add text to pick button
            GameObject pickTextObject = new GameObject("Pick Text");
            pickTextObject.transform.parent = pickButtonObject.transform;
            Text pickText = pickTextObject.AddComponent<Text>();
            pickText.text = "Pick Image";
            pickText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            pickText.alignment = TextAnchor.MiddleCenter;
            pickText.color = Color.white;

            // Position text
            RectTransform pickTextRect = pickTextObject.GetComponent<RectTransform>();
            pickTextRect.anchorMin = Vector2.zero;
            pickTextRect.anchorMax = Vector2.one;
            pickTextRect.anchoredPosition = Vector2.zero;
            pickTextRect.sizeDelta = Vector2.zero;

            // Create mode toggle button (AR/Photo)
            GameObject modeToggleObject = new GameObject("Mode Toggle Button");
            modeToggleObject.transform.parent = mainCanvas.transform;
            modeToggleButton = modeToggleObject.AddComponent<Button>();
            Image modeToggleImage = modeToggleObject.AddComponent<Image>();
            modeToggleImage.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);
            modeToggleButton.targetGraphic = modeToggleImage;

            // Position mode toggle button (top right)
            RectTransform modeToggleRect = modeToggleObject.GetComponent<RectTransform>();
            modeToggleRect.anchorMin = new Vector2(0.85f, 0.9f);
            modeToggleRect.anchorMax = new Vector2(0.95f, 0.95f);
            modeToggleRect.anchoredPosition = Vector2.zero;
            modeToggleRect.sizeDelta = Vector2.zero;

            // Add text to mode toggle button
            GameObject modeTextObject = new GameObject("Mode Text");
            modeTextObject.transform.parent = modeToggleObject.transform;
            Text modeText = modeTextObject.AddComponent<Text>();
            modeText.text = "Photo";
            modeText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            modeText.alignment = TextAnchor.MiddleCenter;
            modeText.color = Color.white;
            modeText.fontSize = 12;

            // Position text
            RectTransform modeTextRect = modeTextObject.GetComponent<RectTransform>();
            modeTextRect.anchorMin = Vector2.zero;
            modeTextRect.anchorMax = Vector2.one;
            modeTextRect.anchoredPosition = Vector2.zero;
            modeTextRect.sizeDelta = Vector2.zero;

            // Connect components
            photoVisualizerMode.pickImageButton = pickButton;
            photoVisualizerMode.photoDisplayImage = displayImage;
            photoVisualizerMode.photoVisualizerPanel = panelObject;
            photoVisualizerMode.wallSegmentation = wallSegmentation;
            photoVisualizerMode.wallPaintBlit = wallPaintBlit;

            // Initially hide the panel
            panelObject.SetActive(false);

            // Set up toggle button to switch between AR and Photo mode
            modeToggleButton.onClick.AddListener(ToggleVisualizerMode);
      }

      /// <summary>
      /// Toggles between AR and Photo Visualizer modes
      /// </summary>
      private void ToggleVisualizerMode()
      {
            if (photoVisualizerMode != null)
            {
                  // Get the current active state of the photo visualizer
                  bool isPhotoMode = photoVisualizerMode.photoVisualizerPanel.activeSelf;

                  if (isPhotoMode)
                  {
                        // Switch back to AR mode
                        EnableARMode();
                  }
                  else
                  {
                        // Switch to Photo mode
                        photoVisualizerMode.SwitchToPhotoMode();
                  }

                  // Update button text
                  Text buttonText = modeToggleButton.GetComponentInChildren<Text>();
                  if (buttonText != null)
                  {
                        buttonText.text = isPhotoMode ? "Photo" : "AR";
                  }
            }
      }

      /// <summary>
      /// Enables AR mode and disables Photo mode
      /// </summary>
      private void EnableARMode()
      {
            // Hide photo visualizer panel
            if (photoVisualizerMode != null && photoVisualizerMode.photoVisualizerPanel != null)
            {
                  photoVisualizerMode.photoVisualizerPanel.SetActive(false);
            }

            // Enable AR Session
            if (arSession != null)
            {
                  arSession.gameObject.SetActive(true);
            }

            // Enable AR Camera Background
            var cameraBackground = arCamera.GetComponent<ARCameraBackground>();
            if (cameraBackground != null)
            {
                  cameraBackground.enabled = true;
            }

            Debug.Log("Switched to AR Mode");
      }
}