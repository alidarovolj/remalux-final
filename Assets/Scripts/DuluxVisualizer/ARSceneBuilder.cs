using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System;
using System.Reflection;
#if UNITY_2019_1_OR_NEWER
using UnityEngine.UI;
#endif

#if UNITY_EDITOR
/// <summary>
/// Editor utility to build a ready-to-run AR Wall Painting scene
/// with ARFoundation, Barracuda segmentation and paint-blit setup.
/// </summary>
public static class ARSceneBuilder
{
      private const string ScenePath = "Assets/Scenes/ARWallPainting.unity";

      [MenuItem("Tools/AR Wall Painting/Create AR Wall Painting Scene", false, 10)]
      public static void CreateScene()
      {
            // Check if required packages are available
            bool arFoundationAvailable = IsTypeAvailable("UnityEngine.XR.ARFoundation.ARSession");
            // AR Subsystems is now part of AR Foundation, so we don't need to check it separately
            bool uiPackageAvailable = IsTypeAvailable("UnityEngine.UI.Image");

            if (!arFoundationAvailable)
            {
                  bool createAnyway = EditorUtility.DisplayDialog(
                      "Missing AR Packages",
                      "AR Foundation package is not installed or available. " +
                      "The scene will be created with limited functionality.\n\n" +
                      "Would you like to continue anyway?",
                      "Create Limited Scene", "Cancel"
                  );

                  if (!createAnyway)
                  {
                        Debug.LogWarning("Scene creation canceled due to missing AR packages. Install AR Foundation and try again.");
                        return;
                  }
            }

            // Create a new empty scene
            var newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            newScene.name = "ARWallPainting";

            // Create scene objects based on available packages
            if (arFoundationAvailable)
            {
                  CreateARScene();
            }
            else
            {
                  CreateDemoScene();
            }

            // Create UI if package is available
            if (uiPackageAvailable)
            {
                  CreateUI();
            }
            else
            {
                  Debug.LogWarning("Unity UI package not detected. UI elements will not be created.");
                  // Create a simple canvas without UI components
                  var canvasGO = new GameObject("UI Canvas (Limited)");
                  canvasGO.AddComponent<Canvas>();
            }

            // Save scene
            EditorSceneManager.SaveScene(newScene, ScenePath, true);
            Debug.Log("AR Wall Painting scene created at " + ScenePath);
      }

      /// <summary>
      /// Creates a full AR scene with AR Foundation components
      /// </summary>
      private static void CreateARScene()
      {
            Debug.Log("Creating AR scene with AR Foundation components...");

            // Use reflection to create AR components
            try
            {
                  // Get AR Foundation types
                  Type arSessionType = GetTypeFromName("UnityEngine.XR.ARFoundation.ARSession");
                  Type arSessionOriginType = GetTypeFromName("UnityEngine.XR.ARFoundation.ARSessionOrigin");
                  Type arPoseDriverType = GetTypeFromName("UnityEngine.XR.ARFoundation.ARPoseDriver");
                  Type arCameraManagerType = GetTypeFromName("UnityEngine.XR.ARFoundation.ARCameraManager");
                  Type arCameraBackgroundType = GetTypeFromName("UnityEngine.XR.ARFoundation.ARCameraBackground");
                  Type arPlaneManagerType = GetTypeFromName("UnityEngine.XR.ARFoundation.ARPlaneManager");
                  Type planeDetectionModeType = GetTypeFromName("UnityEngine.XR.ARSubsystems.PlaneDetectionMode");

                  if (arSessionType == null || arSessionOriginType == null)
                  {
                        Debug.LogError("Critical AR Foundation types not found. Falling back to demo scene.");
                        CreateDemoScene();
                        return;
                  }

                  // 1. AR Session
                  var sessionGO = new GameObject("AR Session");
                  sessionGO.AddComponent(arSessionType);

                  // 2. AR Session Origin
                  var originGO = new GameObject("AR Session Origin");
                  var origin = originGO.AddComponent(arSessionOriginType);

                  // Get trackablesParent property using reflection
                  var trackablesParentProperty = arSessionOriginType.GetProperty("trackablesParent",
                      BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

                  // Only try to set trackablesParent if it's not a read-only property
                  if (trackablesParentProperty != null && trackablesParentProperty.CanWrite)
                  {
                        trackablesParentProperty.SetValue(origin, null);
                  }
                  else
                  {
                        Debug.Log("trackablesParent is read-only or not found, skipping assignment");
                  }

                  // 3. AR Camera under Origin
                  var camGO = new GameObject("AR Camera");
                  camGO.transform.SetParent(originGO.transform);
                  var cam = camGO.AddComponent<Camera>();
                  cam.clearFlags = CameraClearFlags.SolidColor;
                  cam.backgroundColor = Color.black;
                  cam.tag = "MainCamera";

                  // Pose tracking via ARPoseDriver 
                  if (arPoseDriverType != null)
                  {
                        try
                        {
                              camGO.AddComponent(arPoseDriverType);
                        }
                        catch (Exception ex)
                        {
                              Debug.LogWarning($"Couldn't add ARPoseDriver: {ex.Message}. You may need to update AR Foundation package.");
                        }
                  }

                  // AR Camera components
                  Component arCameraManager = null;
                  if (arCameraManagerType != null)
                  {
                        arCameraManager = camGO.AddComponent(arCameraManagerType);
                  }

                  if (arCameraBackgroundType != null)
                  {
                        camGO.AddComponent(arCameraBackgroundType);
                  }

                  // Assign camera to origin
                  var cameraProperty = arSessionOriginType.GetProperty("camera",
                      BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                  if (cameraProperty != null)
                  {
                        cameraProperty.SetValue(origin, cam);
                  }

                  // 4. Add AR Planes for plane detection
                  if (arPlaneManagerType != null && planeDetectionModeType != null)
                  {
                        try
                        {
                              var planeManager = originGO.AddComponent(arPlaneManagerType);

                              // Set vertical detection mode
                              var requestedDetectionModeProperty = arPlaneManagerType.GetProperty("requestedDetectionMode",
                                  BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                              if (requestedDetectionModeProperty != null)
                              {
                                    // Vertical mode is typically value 2
                                    var verticalMode = Enum.ToObject(planeDetectionModeType, 2);
                                    requestedDetectionModeProperty.SetValue(planeManager, verticalMode);
                              }
                        }
                        catch (Exception ex)
                        {
                              Debug.LogWarning($"Couldn't add ARPlaneManager: {ex.Message}");
                        }
                  }

                  // 5. Wall Segmentation component
                  var seg = camGO.AddComponent<WallSegmentation>();
                  if (seg != null && arCameraManager != null)
                  {
                        // Use reflection to set cameraManager field to avoid direct type casting
                        var cameraManagerField = typeof(WallSegmentation).GetField("cameraManager",
                            BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                        if (cameraManagerField != null)
                        {
                              cameraManagerField.SetValue(seg, arCameraManager);
                        }

                        // Set arCamera reference
                        var arCameraField = typeof(WallSegmentation).GetField("arCamera",
                            BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                        if (arCameraField != null)
                        {
                              arCameraField.SetValue(seg, cam);
                        }

                        // Set correct input dimensions to avoid assertion errors
                        // Fix for "Assertion failure. Values are not equal. Expected: 3 == 128"
                        try
                        {
                              var inputWidthField = typeof(WallSegmentation).GetField("inputWidth",
                                  BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                              var inputHeightField = typeof(WallSegmentation).GetField("inputHeight",
                                  BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                              var inputChannelsField = typeof(WallSegmentation).GetField("inputChannels",
                                  BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);

                              if (inputWidthField != null) inputWidthField.SetValue(seg, 128);
                              if (inputHeightField != null) inputHeightField.SetValue(seg, 128);
                              if (inputChannelsField != null) inputChannelsField.SetValue(seg, 3);

                              Debug.Log("WallSegmentation parameters configured for model compatibility");
                        }
                        catch (Exception ex)
                        {
                              Debug.LogWarning($"Failed to configure WallSegmentation parameters: {ex.Message}");
                        }
                  }

                  // 6. Paint Blit component
                  var blit = camGO.AddComponent<WallPaintBlit>();
                  if (blit != null && seg != null)
                  {
                        blit.maskTexture = seg.outputRenderTexture;
                        blit.paintColor = Color.red;
                        blit.opacity = 0.7f;
                  }
            }
            catch (Exception ex)
            {
                  Debug.LogError($"Error creating AR scene: {ex.Message}\n{ex.StackTrace}");
                  CreateDemoScene();
            }
      }

      /// <summary>
      /// Creates a simple demo scene without AR Foundation for testing
      /// </summary>
      private static void CreateDemoScene()
      {
            Debug.Log("Creating demo scene without AR Foundation...");

            // 1. Main Camera
            var camGO = new GameObject("Main Camera");
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.tag = "MainCamera";

            // 2. Add demo wall segmentation
            try
            {
                  var demoSeg = camGO.AddComponent<DemoWallSegmentation>();
            }
            catch (System.Exception ex)
            {
                  Debug.LogWarning($"Couldn't add DemoWallSegmentation: {ex.Message}");
            }

            // 3. Create demo walls
            CreateDemoWalls();
      }

      /// <summary>
      /// Creates demo walls for testing without AR
      /// </summary>
      private static void CreateDemoWalls()
      {
            var wallsParent = new GameObject("Demo Walls");

            // Create demo walls
            for (int i = 0; i < 3; i++)
            {
                  var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                  wall.name = $"Demo Wall {i + 1}";
                  wall.transform.parent = wallsParent.transform;
                  wall.transform.position = new Vector3(i * 2 - 2, 0, 5);
                  wall.transform.localScale = new Vector3(1.5f, 3, 0.1f);

                  // Add a material with a light color
                  var renderer = wall.GetComponent<Renderer>();
                  if (renderer != null && renderer.sharedMaterial != null)
                  {
                        renderer.sharedMaterial.color = new Color(0.9f, 0.9f, 0.85f);
                  }
            }
      }

      /// <summary>
      /// Creates the UI canvas and elements
      /// </summary>
      private static void CreateUI()
      {
            // We only run this code when UI package is available
            // This check is now done in the main CreateScene method
            // and this method is only called if UI package is present
            try
            {
                  // Using dynamic type to avoid compile-time dependencies
                  // Create canvas GameObject
                  var canvasGO = new GameObject("UI Canvas");

                  // Try to set UI layer if it exists
                  try
                  {
                        canvasGO.layer = LayerMask.NameToLayer("UI");
                  }
                  catch
                  {
                        // Fallback to default layer if UI layer doesn't exist
                  }

                  var canvasType = System.Type.GetType("UnityEngine.Canvas, UnityEngine");
                  var canvas = canvasGO.AddComponent(canvasType);

                  var renderModeProperty = canvasType.GetProperty("renderMode",
                      BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                  renderModeProperty?.SetValue(canvas, 1); // ScreenSpaceOverlay

                  System.Type canvasScalerType = System.Type.GetType("UnityEngine.UI.CanvasScaler, UnityEngine.UI");
                  if (canvasScalerType != null)
                  {
                        var scaler = canvasGO.AddComponent(canvasScalerType);
                        var uiScaleModeProperty = canvasScalerType.GetProperty("uiScaleMode",
                            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                        uiScaleModeProperty?.SetValue(scaler, 1); // ScaleWithScreenSize
                  }

                  System.Type graphicRaycasterType = System.Type.GetType("UnityEngine.UI.GraphicRaycaster, UnityEngine.UI");
                  if (graphicRaycasterType != null)
                  {
                        canvasGO.AddComponent(graphicRaycasterType);
                  }

                  // Create a simple panel for color buttons
                  var panelGO = new GameObject("Color Panel");
                  panelGO.transform.SetParent(canvasGO.transform);

                  // Add RectTransform
                  var rectTransformType = System.Type.GetType("UnityEngine.RectTransform, UnityEngine");
                  var rect = panelGO.AddComponent(rectTransformType);

                  // Set properties
                  var anchorMinProperty = rectTransformType.GetProperty("anchorMin",
                      BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                  anchorMinProperty?.SetValue(rect, new Vector2(0, 0));

                  var anchorMaxProperty = rectTransformType.GetProperty("anchorMax",
                      BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                  anchorMaxProperty?.SetValue(rect, new Vector2(1, 0.2f));

                  Debug.Log("UI elements created successfully.");
            }
            catch (System.Exception ex)
            {
                  Debug.LogError($"Error creating UI elements: {ex.Message}\n{ex.StackTrace}");
            }
      }

      /// <summary>
      /// Gets a Type by name across all loaded assemblies
      /// </summary>
      private static Type GetTypeFromName(string typeName)
      {
            // Try direct lookup
            Type type = Type.GetType(typeName);
            if (type != null) return type;

            // Try more comprehensive search
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                  // Try full name
                  type = assembly.GetType(typeName);
                  if (type != null) return type;

                  // Try without assembly specification
                  if (typeName.Contains(","))
                  {
                        string shortName = typeName.Substring(0, typeName.IndexOf(",")).Trim();
                        type = assembly.GetType(shortName);
                        if (type != null) return type;
                  }

                  // Try just the class name (last part of namespace)
                  if (typeName.Contains("."))
                  {
                        string className = typeName.Substring(typeName.LastIndexOf(".") + 1);
                        foreach (var t in assembly.GetTypes())
                        {
                              if (t.Name == className)
                                    return t;
                        }
                  }
            }

            return null;
      }

      /// <summary>
      /// Checks if a type is available (used to test for package availability)
      /// </summary>
      private static bool IsTypeAvailable(string typeName)
      {
            return GetTypeFromName(typeName) != null;
      }
}
#endif