using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.IO;

/// <summary>
/// Provides a non-AR alternative for devices without AR support
/// Allows users to pick images from gallery and apply wall painting
/// </summary>
public class PhotoVisualizerMode : MonoBehaviour
{
      [Header("UI Components")]
      [SerializeField] public Button pickImageButton;
      [SerializeField] public RawImage photoDisplayImage;
      [SerializeField] public GameObject photoVisualizerPanel;

      [Header("Segmentation")]
      [SerializeField] public WallSegmentation wallSegmentation;
      [SerializeField] public WallPaintBlit wallPaintBlit;

      // Loaded photo texture
      private Texture2D loadedPhotoTexture;

      private void Start()
      {
            // Initially hide the photo visualizer panel if in AR mode
            if (photoVisualizerPanel != null)
            {
                  photoVisualizerPanel.SetActive(false);
            }

            // Add button event listener
            if (pickImageButton != null)
            {
                  pickImageButton.onClick.AddListener(PickImageFromGallery);
            }

            // Check if device supports AR
            CheckARSupport();
      }

      /// <summary>
      /// Checks if device supports AR, if not shows photo visualizer mode
      /// </summary>
      private void CheckARSupport()
      {
#if UNITY_EDITOR
            // In editor, we can toggle between modes for testing
            return;
#elif UNITY_IOS || UNITY_ANDROID
        // Check for AR support on mobile devices
        bool arSupported = IsARSupported();
        
        // If AR is not supported, automatically switch to photo visualizer mode
        if (!arSupported)
        {
            SwitchToPhotoMode();
        }
#endif
      }

      /// <summary>
      /// Determines if the device supports AR
      /// </summary>
      private bool IsARSupported()
      {
#if UNITY_ANDROID
        // For Android, check ARCore availability
        return IsARCoreSupported();
#elif UNITY_IOS
            // For iOS, check ARKit availability (requires iOS 11+)
            return IsARKitSupported();
#else
            // For other platforms, assume no AR support
            return false;
#endif
      }

#if UNITY_ANDROID
    /// <summary>
    /// Checks if ARCore is supported on this device
    /// </summary>
    private bool IsARCoreSupported()
    {
        // In a real app, use ARCore's compatibility check APIs
        // For now, use a simple API level check (ARCore requires Android 7.0+)
        using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
        {
            int sdkInt = version.GetStatic<int>("SDK_INT");
            return sdkInt >= 24; // Android 7.0 Nougat
        }
    }
#endif

#if UNITY_IOS
      /// <summary>
      /// Checks if ARKit is supported on this device
      /// </summary>
      private bool IsARKitSupported()
      {
            // ARKit requires iOS 11+
            // In a real app, use Unity's ARKit availability check
            // For now, assume supported on iOS
            return true;
      }
#endif

      /// <summary>
      /// Switches the app to photo visualizer mode
      /// </summary>
      public void SwitchToPhotoMode()
      {
            // Show photo visualizer UI
            if (photoVisualizerPanel != null)
            {
                  photoVisualizerPanel.SetActive(true);
            }

            // Disable AR components
            DisableARComponents();

            Debug.Log("Switched to Photo Visualizer Mode");
      }

      /// <summary>
      /// Disables AR-specific components
      /// </summary>
      private void DisableARComponents()
      {
            // Find and disable AR Session
            var arSession = FindObjectOfType<UnityEngine.XR.ARFoundation.ARSession>();
            if (arSession != null)
            {
                  arSession.gameObject.SetActive(false);
            }

            // Find and disable AR Camera background
            var cameraBackground = FindObjectOfType<UnityEngine.XR.ARFoundation.ARCameraBackground>();
            if (cameraBackground != null)
            {
                  cameraBackground.enabled = false;
            }
      }

      /// <summary>
      /// Opens native gallery picker to select a photo
      /// </summary>
      public void PickImageFromGallery()
      {
#if UNITY_ANDROID
        // On Android, use native intent system
        StartAndroidImagePicker();
#elif UNITY_IOS
            // On iOS, use native picker
            StartIOSImagePicker();
#else
            // For editor testing, load a test image
            LoadTestImage();
#endif
      }

#if UNITY_ANDROID
    /// <summary>
    /// Starts the native Android image picker
    /// </summary>
    private void StartAndroidImagePicker()
    {
        // Create Android intent to pick an image
        AndroidJavaClass intentClass = new AndroidJavaClass("android.content.Intent");
        AndroidJavaObject intent = new AndroidJavaObject("android.content.Intent", intentClass.GetStatic<string>("ACTION_PICK"));
        
        // Set MIME type for images
        AndroidJavaClass uriClass = new AndroidJavaClass("android.provider.MediaStore$Images$Media");
        intent.Call<AndroidJavaObject>("setType", "image/*");
        
        // Get the current activity
        AndroidJavaClass unityPlayerClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        AndroidJavaObject currentActivity = unityPlayerClass.GetStatic<AndroidJavaObject>("currentActivity");
        
        // Start the activity for result
        int requestCode = 100;
        currentActivity.Call("startActivityForResult", intent, requestCode);
        
        // To handle the result, implement OnActivityResult in a separate Android plugin
        // For simplicity in this example, we'll use a test image in the editor
    }
#endif

#if UNITY_IOS
      /// <summary>
      /// Starts the native iOS image picker
      /// </summary>
      private void StartIOSImagePicker()
      {
            // On iOS, you would typically use a native plugin like NativeGallery
            // For this example, use a test image in the editor
            Debug.Log("iOS image picker would be implemented here");
            LoadTestImage();
      }
#endif

      /// <summary>
      /// Loads a test image for the editor
      /// </summary>
      private void LoadTestImage()
      {
            // In a real app, this would come from the native gallery picker
            // For testing, load a built-in texture or resource
            Texture2D testTexture = Resources.Load<Texture2D>("TestWallImage");

            if (testTexture != null)
            {
                  ProcessLoadedImage(testTexture);
            }
            else
            {
                  // Create a simple placeholder texture
                  testTexture = new Texture2D(512, 512);
                  Color[] colors = new Color[512 * 512];
                  for (int i = 0; i < colors.Length; i++)
                  {
                        colors[i] = Color.gray;
                  }
                  testTexture.SetPixels(colors);
                  testTexture.Apply();

                  ProcessLoadedImage(testTexture);
                  Debug.LogWarning("Test wall image not found. Using placeholder.");
            }
      }

      /// <summary>
      /// Processes an image after it's been loaded
      /// </summary>
      private void ProcessLoadedImage(Texture2D texture)
      {
            // Store the loaded texture
            loadedPhotoTexture = texture;

            // Display the image
            if (photoDisplayImage != null)
            {
                  photoDisplayImage.texture = loadedPhotoTexture;
            }

            // Process the image with wall segmentation
            StartCoroutine(ProcessImageWithSegmentation(texture));
      }

      /// <summary>
      /// Processes the loaded image with wall segmentation
      /// </summary>
      private IEnumerator ProcessImageWithSegmentation(Texture2D texture)
      {
            yield return new WaitForEndOfFrame();

            // If wall segmentation is available, process the image
            if (wallSegmentation != null)
            {
                  // In a real implementation, you would feed the texture to the segmentation model
                  // For now, simulate a successful segmentation
                  Debug.Log("Processing image with wall segmentation");

                  // Create a simple mask (white in the middle, black around edges)
                  Texture2D mockMask = CreateMockWallMask(texture.width, texture.height);

                  // Apply the mask to the wall paint effect
                  if (wallPaintBlit != null && mockMask != null)
                  {
                        // Convert to render texture
                        RenderTexture maskRT = new RenderTexture(mockMask.width, mockMask.height, 0, RenderTextureFormat.R8);
                        Graphics.Blit(mockMask, maskRT);

                        // Assign to the paint effect
                        wallPaintBlit.maskTexture = maskRT;

                        Debug.Log("Mask applied to wall paint effect");
                  }
            }
      }

      /// <summary>
      /// Creates a mock wall mask for testing (white in center, black on edges)
      /// </summary>
      private Texture2D CreateMockWallMask(int width, int height)
      {
            Texture2D mask = new Texture2D(width, height, TextureFormat.R8, false);
            Color[] pixels = new Color[width * height];

            // Define a rectangular area in the center to be considered a wall
            float centerX = width * 0.5f;
            float centerY = height * 0.5f;
            float radiusX = width * 0.4f;
            float radiusY = height * 0.4f;

            for (int y = 0; y < height; y++)
            {
                  for (int x = 0; x < width; x++)
                  {
                        // Normalize distance from center
                        float dx = Mathf.Abs(x - centerX) / radiusX;
                        float dy = Mathf.Abs(y - centerY) / radiusY;

                        // Inside the rectangle = wall (white)
                        if (dx <= 1 && dy <= 1)
                        {
                              // Fade out near the edges
                              float edgeFactor = Mathf.Max(0, 1 - Mathf.Max(dx, dy));
                              pixels[y * width + x] = new Color(edgeFactor, edgeFactor, edgeFactor, 1);
                        }
                        else
                        {
                              // Outside = not wall (black)
                              pixels[y * width + x] = Color.black;
                        }
                  }
            }

            mask.SetPixels(pixels);
            mask.Apply();
            return mask;
      }
}