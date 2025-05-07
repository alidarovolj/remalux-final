using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.Reflection;
using System;

#if UNITY_EDITOR
/// <summary>
/// Tool for creating AR scene with fixed WallSegmentation component
/// </summary>
public class CreateSceneWithFixedSegmentation : Editor
{
      // Path to save the scene
      private const string ScenePath = "Assets/Scenes/ARWallPainting.unity";

      /// <summary>
      /// Creates a new AR scene with fixed WallSegmentation component
      /// </summary>
      [MenuItem("Tools/AR Wall Painting/Create Scene With Fixed Segmentation", false, 1)]
      public static void CreateARSceneWithFixedSegmentation()
      {
            Debug.Log("Creating AR Wall Painting Scene...");

            // Check for AR Foundation dynamically
            bool arFoundationAvailable = IsTypeAvailable("UnityEngine.XR.ARFoundation.ARSession");

            // If AR Foundation is not available, offer to create a limited scene
            if (!arFoundationAvailable)
            {
                  bool createAnyway = EditorUtility.DisplayDialog(
                      "Missing AR Foundation",
                      "AR Foundation package is not detected in this project. " +
                      "A basic scene with demo walls will be created instead.\n\n" +
                      "Would you like to continue?",
                      "Create Basic Scene", "Cancel");

                  if (!createAnyway)
                  {
                        Debug.Log("Scene creation canceled.");
                        return;
                  }
            }

            // Create a new scene
            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            newScene.name = "ARWallPainting";

            if (arFoundationAvailable)
            {
                  // Create scene with AR components using reflection
                  CreateARSceneWithReflection();
            }
            else
            {
                  // Create basic scene without AR components
                  CreateBasicScene();
            }

            // Create UI canvas
            CreateUICanvas();

            // Save scene
            EditorSceneManager.SaveScene(newScene, ScenePath, true);
            Debug.Log("AR Wall Painting scene created successfully!");
      }

      /// <summary>
      /// Creates AR scene components using reflection to avoid compile-time dependencies
      /// </summary>
      private static void CreateARSceneWithReflection()
      {
            Debug.Log("Creating AR scene with AR Foundation...");

            try
            {
                  // 1. AR Session
                  GameObject sessionGO = new GameObject("AR Session");
                  var sessionType = GetTypeFromName("UnityEngine.XR.ARFoundation.ARSession, Unity.XR.ARFoundation");
                  if (sessionType != null)
                        sessionGO.AddComponent(sessionType);
                  else
                        Debug.LogWarning("ARSession type not found");

                  // 2. AR Session Origin
                  GameObject originGO = new GameObject("AR Session Origin");
                  var originType = GetTypeFromName("UnityEngine.XR.ARFoundation.ARSessionOrigin, Unity.XR.ARFoundation");
                  Component origin = null;
                  if (originType != null)
                  {
                        origin = originGO.AddComponent(originType);
                  }
                  else
                        Debug.LogWarning("ARSessionOrigin type not found");

                  // 3. AR Camera
                  var mainCamera = UnityEngine.Object.FindObjectOfType<Camera>();
                  if (mainCamera == null || mainCamera.gameObject == null)
                  {
                        // Create a new camera if none exists
                        GameObject cameraGO = new GameObject("AR Camera");
                        cameraGO.transform.SetParent(originGO.transform);
                        mainCamera = cameraGO.AddComponent<Camera>();
                        mainCamera.clearFlags = CameraClearFlags.SolidColor;
                        mainCamera.backgroundColor = Color.black;
                        mainCamera.tag = "MainCamera";
                  }
                  else
                  {
                        // Use existing camera
                        mainCamera.transform.SetParent(originGO.transform);
                  }

                  // Add ARCameraManager
                  var cameraManagerType = GetTypeFromName("UnityEngine.XR.ARFoundation.ARCameraManager, Unity.XR.ARFoundation");
                  Component arCameraManager = null;
                  if (cameraManagerType != null)
                  {
                        arCameraManager = mainCamera.gameObject.AddComponent(cameraManagerType);
                  }
                  else
                        Debug.LogWarning("ARCameraManager type not found");

                  // Add ARCameraBackground
                  var cameraBackgroundType = GetTypeFromName("UnityEngine.XR.ARFoundation.ARCameraBackground, Unity.XR.ARFoundation");
                  if (cameraBackgroundType != null)
                  {
                        mainCamera.gameObject.AddComponent(cameraBackgroundType);
                  }
                  else
                        Debug.LogWarning("ARCameraBackground type not found");

                  // Assign camera to origin using reflection
                  if (originType != null && origin != null)
                  {
                        var cameraProperty = originType.GetProperty("camera",
                            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                        if (cameraProperty != null)
                              cameraProperty.SetValue(origin, mainCamera);
                  }

                  // 4. AR Plane Manager
                  var planeManagerType = GetTypeFromName("UnityEngine.XR.ARFoundation.ARPlaneManager, Unity.XR.ARFoundation");
                  if (planeManagerType != null)
                  {
                        // Add plane manager
                        var planeManager = originGO.AddComponent(planeManagerType);

                        // Set vertical detection mode
                        var planeDetectionModeType = GetTypeFromName("UnityEngine.XR.ARSubsystems.PlaneDetectionMode, Unity.XR.ARSubsystems");
                        if (planeDetectionModeType != null)
                        {
                              var requestedDetectionModeProperty = planeManagerType.GetProperty("requestedDetectionMode",
                                  BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                              if (requestedDetectionModeProperty != null)
                              {
                                    // Vertical mode is typically value 2
                                    var verticalMode = Enum.ToObject(planeDetectionModeType, 2);
                                    requestedDetectionModeProperty.SetValue(planeManager, verticalMode);
                              }
                        }
                  }
                  else
                        Debug.LogWarning("ARPlaneManager type not found");

                  // 5. Wall Segmentation component
                  var wallSegmentation = mainCamera.gameObject.AddComponent<WallSegmentation>();
                  if (wallSegmentation != null && arCameraManager != null)
                  {
                        // Set cameraManager using reflection
                        var cameraManagerField = typeof(WallSegmentation).GetField("cameraManager",
                            BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                        if (cameraManagerField != null)
                              cameraManagerField.SetValue(wallSegmentation, arCameraManager);

                        // Set arCamera reference
                        var arCameraField = typeof(WallSegmentation).GetField("arCamera",
                            BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                        if (arCameraField != null)
                              arCameraField.SetValue(wallSegmentation, mainCamera);

                        // Настройка параметров модели для устранения ошибки размерности
                        try
                        {
                              // Установка правильных параметров для модели
                              var inputWidthField = typeof(WallSegmentation).GetField("inputWidth",
                                  BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                              var inputHeightField = typeof(WallSegmentation).GetField("inputHeight",
                                  BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                              var inputChannelsField = typeof(WallSegmentation).GetField("inputChannels",
                                  BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);

                              if (inputWidthField != null) inputWidthField.SetValue(wallSegmentation, 128);
                              if (inputHeightField != null) inputHeightField.SetValue(wallSegmentation, 128);
                              if (inputChannelsField != null) inputChannelsField.SetValue(wallSegmentation, 3);

                              Debug.Log("Настроены параметры модели WallSegmentation: 128x128x3");
                        }
                        catch (System.Exception ex)
                        {
                              Debug.LogWarning($"Не удалось настроить параметры модели: {ex.Message}");
                        }
                  }

                  // 6. Paint Blit component
                  var paintBlit = mainCamera.gameObject.AddComponent<WallPaintBlit>();
                  if (paintBlit != null && wallSegmentation != null)
                  {
                        // Get outputRenderTexture
                        var outputRenderTextureProperty = typeof(WallSegmentation).GetProperty("outputRenderTexture",
                            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                        if (outputRenderTextureProperty != null)
                        {
                              var outputTexture = outputRenderTextureProperty.GetValue(wallSegmentation) as RenderTexture;
                              if (outputTexture != null)
                                    paintBlit.maskTexture = outputTexture;
                        }

                        // Set initial color
                        paintBlit.paintColor = Color.red;
                        paintBlit.opacity = 0.7f;
                  }
            }
            catch (System.Exception ex)
            {
                  Debug.LogError($"Error creating AR scene: {ex.Message}\n{ex.StackTrace}");
                  CreateBasicScene(); // Fallback to basic scene
            }
      }

      /// <summary>
      /// Creates a basic scene without AR components
      /// </summary>
      private static void CreateBasicScene()
      {
            Debug.Log("Creating basic scene without AR Foundation");

            // 1. Create a parent for demo walls
            GameObject demoWallsParent = new GameObject("Demo Walls");

            // 2. Create Main Camera
            GameObject cameraObj = new GameObject("Main Camera");
            Camera camera = cameraObj.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.tag = "MainCamera";

            // 3. Add demo wall segmentation component if possible
            try
            {
                  var demoSegmentationType = GetTypeFromName("DemoWallSegmentation, Assembly-CSharp");
                  if (demoSegmentationType != null)
                        cameraObj.AddComponent(demoSegmentationType);
                  else
                        cameraObj.AddComponent<DemoWallSegmentation>();
            }
            catch (System.Exception ex)
            {
                  Debug.LogWarning($"DemoWallSegmentation component could not be added: {ex.Message}");
            }

            // 4. Create demo walls
            for (int i = 0; i < 3; i++)
            {
                  GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                  wall.transform.SetParent(demoWallsParent.transform);
                  wall.transform.position = new Vector3(i * 2 - 2, 0, 5);
                  wall.transform.localScale = new Vector3(1.5f, 3, 0.1f);
                  wall.name = "Demo Wall " + (i + 1);

                  // Use shared material to avoid leaks in edit mode
                  var renderer = wall.GetComponent<Renderer>();
                  if (renderer != null && renderer.sharedMaterial != null)
                  {
                        renderer.sharedMaterial.color = new Color(0.9f, 0.9f, 0.85f);
                  }
            }
      }

      /// <summary>
      /// Creates a UI canvas for controls
      /// </summary>
      private static void CreateUICanvas()
      {
            try
            {
                  // 1. Create canvas
                  GameObject canvasObj = new GameObject("UI Canvas");
                  canvasObj.AddComponent<Canvas>();

                  // Try to set UI properties if UI package available
                  bool uiPackageAvailable = IsTypeAvailable("UnityEngine.UI.CanvasScaler");
                  if (uiPackageAvailable)
                  {
                        // Using reflection to avoid compile errors if UI package is missing
                        var canvas = canvasObj.GetComponent<Canvas>();
                        if (canvas != null)
                        {
                              canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                        }

                        var canvasScalerType = GetTypeFromName("UnityEngine.UI.CanvasScaler, UnityEngine.UI");
                        if (canvasScalerType != null)
                        {
                              var scaler = canvasObj.AddComponent(canvasScalerType);
                              var uiScaleModeProperty = canvasScalerType.GetProperty("uiScaleMode",
                                  BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                              if (uiScaleModeProperty != null)
                                    uiScaleModeProperty.SetValue(scaler, 1); // ScaleWithScreenSize
                        }

                        var graphicRaycasterType = GetTypeFromName("UnityEngine.UI.GraphicRaycaster, UnityEngine.UI");
                        if (graphicRaycasterType != null)
                              canvasObj.AddComponent(graphicRaycasterType);

                        // 2. Create a simple color panel
                        GameObject colorPanel = new GameObject("Color Panel");
                        colorPanel.transform.SetParent(canvasObj.transform, false);

                        // Add and configure RectTransform
                        var rect = colorPanel.AddComponent<RectTransform>();
                        if (rect != null)
                        {
                              rect.anchorMin = new Vector2(0, 0);
                              rect.anchorMax = new Vector2(1, 0.2f);
                              rect.offsetMin = rect.offsetMax = Vector2.zero;
                        }
                  }
            }
            catch (System.Exception ex)
            {
                  Debug.LogWarning($"Could not create UI Canvas: {ex.Message}");
            }
      }

      /// <summary>
      /// Gets a Type by name with proper error handling
      /// </summary>
      private static System.Type GetTypeFromName(string typeName)
      {
            try
            {
                  // Try direct type lookup
                  var type = System.Type.GetType(typeName);
                  if (type != null) return type;

                  // Try searching in assemblies
                  foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                  {
                        type = assembly.GetType(typeName);
                        if (type != null) return type;

                        // Try with shortened name
                        if (typeName.Contains(","))
                        {
                              string shortName = typeName.Substring(0, typeName.IndexOf(','));
                              type = assembly.GetType(shortName);
                              if (type != null) return type;
                        }

                        // Try without namespace
                        if (typeName.Contains("."))
                        {
                              string className = typeName.Substring(typeName.LastIndexOf('.') + 1);
                              foreach (var t in assembly.GetTypes())
                              {
                                    if (t.Name == className)
                                          return t;
                              }
                        }
                  }

                  return null;
            }
            catch (System.Exception ex)
            {
                  Debug.LogError($"Error getting type '{typeName}': {ex.Message}");
                  return null;
            }
      }

      /// <summary>
      /// Checks if a type is available in the current app domain
      /// </summary>
      private static bool IsTypeAvailable(string typeName)
      {
            return GetTypeFromName(typeName) != null;
      }
}
#endif