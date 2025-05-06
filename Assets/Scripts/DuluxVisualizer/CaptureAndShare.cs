using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.IO;
using System;

/// <summary>
/// Component for capturing screenshots and sharing them
/// </summary>
public class CaptureAndShare : MonoBehaviour
{
      [Header("UI Components")]
      [SerializeField] public Button captureButton;
      [SerializeField] public Button shareButton;
      [SerializeField] public RawImage previewImage;
      [SerializeField] public GameObject previewPanel;

      [Header("Settings")]
      [SerializeField] private string fileNamePrefix = "DuluxVisualizer_";
      [SerializeField] private string fileExtension = ".png";

      // Captured screenshot
      private Texture2D capturedScreenshot;
      private string capturedImagePath;

      private void Start()
      {
            // Initially hide the preview panel
            if (previewPanel != null)
            {
                  previewPanel.SetActive(false);
            }

            // Add button event listeners
            if (captureButton != null)
            {
                  captureButton.onClick.AddListener(CaptureScreenshot);
            }

            if (shareButton != null)
            {
                  shareButton.onClick.AddListener(ShareScreenshot);
                  // Initially disable share button until we have a screenshot
                  shareButton.interactable = false;
            }
      }

      /// <summary>
      /// Captures a screenshot of the current AR view
      /// </summary>
      public void CaptureScreenshot()
      {
            StartCoroutine(CaptureScreenshotCoroutine());
      }

      private IEnumerator CaptureScreenshotCoroutine()
      {
            // Wait for the end of the frame so we can capture everything that was rendered
            yield return new WaitForEndOfFrame();

            // Create a Texture2D with the size of the screen
            capturedScreenshot = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);

            // Read the pixels from the screen
            capturedScreenshot.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            capturedScreenshot.Apply();

            // Show the preview panel with the captured image
            if (previewPanel != null)
            {
                  previewPanel.SetActive(true);
            }

            // Display the captured image in the preview
            if (previewImage != null)
            {
                  previewImage.texture = capturedScreenshot;
            }

            // Enable the share button
            if (shareButton != null)
            {
                  shareButton.interactable = true;
            }

            // Save the screenshot to a temporary file
            SaveScreenshotToFile();

            Debug.Log("Screenshot captured and ready to share!");
      }

      /// <summary>
      /// Saves the captured screenshot to a file
      /// </summary>
      private void SaveScreenshotToFile()
      {
            if (capturedScreenshot == null)
            {
                  Debug.LogError("No screenshot has been captured yet!");
                  return;
            }

            // Generate unique filename
            string fileName = fileNamePrefix + DateTime.Now.ToString("yyyyMMdd_HHmmss") + fileExtension;

            // Path for saving the file
            string path = Path.Combine(Application.temporaryCachePath, fileName);

            // Convert texture to PNG
            byte[] pngBytes = capturedScreenshot.EncodeToPNG();

            // Write to file
            File.WriteAllBytes(path, pngBytes);

            // Store the path for sharing
            capturedImagePath = path;

            Debug.Log("Screenshot saved to: " + path);
      }

      /// <summary>
      /// Shares the captured screenshot using the native sharing dialog
      /// </summary>
      public void ShareScreenshot()
      {
            if (string.IsNullOrEmpty(capturedImagePath))
            {
                  Debug.LogError("No screenshot available to share!");
                  return;
            }

#if UNITY_ANDROID
        // For Android, we need to use Android's native sharing
        AndroidJavaClass intentClass = new AndroidJavaClass("android.content.Intent");
        AndroidJavaObject intentObject = new AndroidJavaObject("android.content.Intent");

        intentObject.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND"));
        AndroidJavaClass uriClass = new AndroidJavaClass("android.net.Uri");
        AndroidJavaObject uriObject = uriClass.CallStatic<AndroidJavaObject>("parse", "file://" + capturedImagePath);
        intentObject.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_STREAM"), uriObject);
        intentObject.Call<AndroidJavaObject>("setType", "image/png");

        AndroidJavaClass unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        AndroidJavaObject currentActivity = unity.GetStatic<AndroidJavaObject>("currentActivity");
        AndroidJavaObject chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intentObject, "Share your wall visualization");
        currentActivity.Call("startActivity", chooser);
#elif UNITY_IOS
            // For iOS, use NativeShare plugin or the native iOS sharing
            Debug.Log("Sharing on iOS...");
            // In a real implementation, you would use the NativeShare plugin or similar
#else
            // For other platforms, just show a message
            Debug.Log("Sharing not supported on this platform. Screenshot saved at: " + capturedImagePath);
#endif

            Debug.Log("Screenshot shared!");

            // Hide the preview panel after sharing
            if (previewPanel != null)
            {
                  previewPanel.SetActive(false);
            }
      }

      /// <summary>
      /// Closes the preview panel without sharing
      /// </summary>
      public void ClosePreview()
      {
            if (previewPanel != null)
            {
                  previewPanel.SetActive(false);
            }
      }

      private void OnDestroy()
      {
            // Cleanup
            if (capturedScreenshot != null)
            {
                  Destroy(capturedScreenshot);
            }
      }
}