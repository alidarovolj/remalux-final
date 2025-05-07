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
      /// Creates a full AR scene with AR Foundation components using reflection
      /// to avoid compile-time dependencies
      /// </summary>
      private static void CreateARScene()
      {
            Debug.Log("Creating AR scene with AR Foundation components...");

            try
            {
                  // 1. AR Session
                  GameObject sessionGO = new GameObject("AR Session");
                  var sessionType = GetTypeFromName("UnityEngine.XR.ARFoundation.ARSession");
                  if (sessionType != null)
                        sessionGO.AddComponent(sessionType);
                  else
                        Debug.LogWarning("ARSession type not found");

                  // 2. AR Session Origin/XR Origin
                  GameObject originGO = new GameObject("AR Session Origin");

                  // Try to find the XROrigin type first (newer AR Foundation)
                  var xrOriginType = GetTypeFromName("Unity.XR.CoreUtils.XROrigin");
                  var originType = xrOriginType;
                  bool usingXROrigin = xrOriginType != null;

                  // Fallback to ARSessionOrigin if XROrigin not found
                  if (!usingXROrigin)
                  {
                        originType = GetTypeFromName("UnityEngine.XR.ARFoundation.ARSessionOrigin");
                  }

                  Component origin = null;
                  if (originType != null)
                  {
                        origin = originGO.AddComponent(originType);
                  }
                  else
                  {
                        Debug.LogWarning("Neither XROrigin nor ARSessionOrigin type found");
                  }

                  // 3. Camera Floor Offset for XR Origin
                  GameObject cameraFloorOffsetGO = new GameObject("Camera Floor Offset");
                  cameraFloorOffsetGO.transform.SetParent(originGO.transform);

                  // 4. AR Camera
                  GameObject cameraGO = new GameObject("AR Camera");
                  cameraGO.transform.SetParent(cameraFloorOffsetGO.transform);

                  Camera mainCamera = cameraGO.AddComponent<Camera>();
                  mainCamera.clearFlags = CameraClearFlags.SolidColor;
                  mainCamera.backgroundColor = Color.black;
                  mainCamera.tag = "MainCamera";

                  // Configure XR Origin
                  if (origin != null)
                  {
                        // Set camera floor offset object
                        PropertyInfo cameraFloorOffsetProperty = null;

                        if (usingXROrigin)
                        {
                              cameraFloorOffsetProperty = originType.GetProperty("CameraFloorOffsetObject");
                        }
                        else
                        {
                              cameraFloorOffsetProperty = originType.GetProperty("cameraFloorOffsetObject");
                        }

                        if (cameraFloorOffsetProperty != null)
                        {
                              // Pass the Transform, not the GameObject
                              Debug.Log($"Setting Camera Floor Offset (expected type: {cameraFloorOffsetProperty.PropertyType.Name})");
                              if (cameraFloorOffsetProperty.PropertyType == typeof(Transform))
                              {
                                    cameraFloorOffsetProperty.SetValue(origin, cameraFloorOffsetGO.transform);
                              }
                              else if (cameraFloorOffsetProperty.PropertyType == typeof(GameObject))
                              {
                                    cameraFloorOffsetProperty.SetValue(origin, cameraFloorOffsetGO);
                              }
                              Debug.Log("Camera Floor Offset set for Origin");
                        }

                        // Set camera reference
                        var cameraProperty = originType.GetProperty("Camera");
                        if (cameraProperty == null)
                        {
                              cameraProperty = originType.GetProperty("camera");
                        }

                        if (cameraProperty != null)
                        {
                              // Check expected type of camera property and pass the correct type
                              Type propertyType = cameraProperty.PropertyType;
                              Debug.Log($"Setting Camera reference (expected type: {propertyType.Name})");
                              if (propertyType == typeof(Camera))
                              {
                                    cameraProperty.SetValue(origin, mainCamera);
                              }
                              else if (propertyType == typeof(GameObject))
                              {
                                    cameraProperty.SetValue(origin, cameraGO);
                              }
                              else if (propertyType == typeof(Transform))
                              {
                                    cameraProperty.SetValue(origin, cameraGO.transform);
                              }
                              Debug.Log("Camera reference set for Origin");
                        }
                  }

                  // 5. Add TrackedPoseDriver for camera motion tracking
                  // Try Input System Tracked Pose Driver first (newer)
                  var inputSysTPDType = GetTypeFromName("UnityEngine.InputSystem.XR.TrackedPoseDriver");
                  bool tpdAdded = false;

                  if (inputSysTPDType != null)
                  {
                        try
                        {
                              cameraGO.AddComponent(inputSysTPDType);
                              tpdAdded = true;
                              Debug.Log("Added Input System TrackedPoseDriver");
                        }
                        catch (Exception ex)
                        {
                              Debug.LogWarning($"Failed to add Input System TrackedPoseDriver: {ex.Message}");
                        }
                  }

                  // Fallback to AR Foundation TrackedPoseDriver if needed
                  if (!tpdAdded)
                  {
                        var arTPDType = GetTypeFromName("UnityEngine.XR.ARFoundation.ARTrackedPoseDriver");
                        if (arTPDType != null)
                        {
                              try
                              {
                                    cameraGO.AddComponent(arTPDType);
                                    Debug.Log("Added ARTrackedPoseDriver");
                              }
                              catch (Exception ex)
                              {
                                    Debug.LogWarning($"Failed to add ARTrackedPoseDriver: {ex.Message}");
                              }
                        }
                        else
                        {
                              Debug.LogWarning("Could not find any TrackedPoseDriver type");
                        }
                  }

                  // 6. Add ARCameraManager and ARCameraBackground
                  var cameraManagerType = GetTypeFromName("UnityEngine.XR.ARFoundation.ARCameraManager");
                  Component arCameraManager = null;
                  if (cameraManagerType != null)
                  {
                        arCameraManager = cameraGO.AddComponent(cameraManagerType);
                        Debug.Log("Added ARCameraManager to camera");
                  }

                  var cameraBackgroundType = GetTypeFromName("UnityEngine.XR.ARFoundation.ARCameraBackground");
                  if (cameraBackgroundType != null)
                  {
                        cameraGO.AddComponent(cameraBackgroundType);
                        Debug.Log("Added ARCameraBackground to camera");
                  }

                  // 7. Add ARPlaneManager and ARRaycastManager
                  var planeManagerType = GetTypeFromName("UnityEngine.XR.ARFoundation.ARPlaneManager");
                  if (planeManagerType != null)
                  {
                        var planeManager = originGO.AddComponent(planeManagerType);
                        Debug.Log("Added ARPlaneManager to Origin");

                        // Set to detect vertical planes (walls)
                        var planeDetectionModeType = GetTypeFromName("UnityEngine.XR.ARSubsystems.PlaneDetectionMode");
                        if (planeDetectionModeType != null)
                        {
                              // Vertical mode is typically value 2
                              var requestedDetectionModeProperty = planeManagerType.GetProperty("requestedDetectionMode");
                              if (requestedDetectionModeProperty != null)
                              {
                                    var verticalMode = Enum.ToObject(planeDetectionModeType, 2);
                                    requestedDetectionModeProperty.SetValue(planeManager, verticalMode);
                                    Debug.Log("Set ARPlaneManager to detect vertical planes");
                              }
                        }
                  }

                  var raycastManagerType = GetTypeFromName("UnityEngine.XR.ARFoundation.ARRaycastManager");
                  if (raycastManagerType != null)
                  {
                        originGO.AddComponent(raycastManagerType);
                        Debug.Log("Added ARRaycastManager to Origin");
                  }

                  // 8. Add WallSegmentation and WallPaintBlit components
                  var wallSegmentation = cameraGO.AddComponent<WallSegmentation>();
                  if (wallSegmentation != null && arCameraManager != null)
                  {
                        // Set references
                        var cameraManagerField = typeof(WallSegmentation).GetField("cameraManager",
                            BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                        if (cameraManagerField != null)
                              cameraManagerField.SetValue(wallSegmentation, arCameraManager);

                        var arCameraField = typeof(WallSegmentation).GetField("arCamera",
                            BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                        if (arCameraField != null)
                              arCameraField.SetValue(wallSegmentation, mainCamera);

                        // Configure model parameters to avoid dimension errors
                        var inputWidthField = typeof(WallSegmentation).GetField("inputWidth",
                            BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                        var inputHeightField = typeof(WallSegmentation).GetField("inputHeight",
                            BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                        var inputChannelsField = typeof(WallSegmentation).GetField("inputChannels",
                            BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);

                        if (inputWidthField != null) inputWidthField.SetValue(wallSegmentation, 128);
                        if (inputHeightField != null) inputHeightField.SetValue(wallSegmentation, 128);
                        if (inputChannelsField != null) inputChannelsField.SetValue(wallSegmentation, 3);

                        Debug.Log("Added and configured WallSegmentation");
                  }

                  var wallPaintBlit = cameraGO.AddComponent<WallPaintBlit>();
                  if (wallPaintBlit != null && wallSegmentation != null)
                  {
                        wallPaintBlit.maskTexture = wallSegmentation.outputRenderTexture;
                        wallPaintBlit.paintColor = Color.red;
                        wallPaintBlit.opacity = 0.7f;
                        Debug.Log("Added and configured WallPaintBlit");
                  }
            }
            catch (Exception ex)
            {
                  Debug.LogError($"Error creating AR scene: {ex.Message}\n{ex.StackTrace}");
                  CreateDemoScene(); // Fallback to basic scene
            }
      }

      /// <summary>
      /// Creates a basic demo scene without AR Foundation
      /// </summary>
      private static void CreateDemoScene()
      {
            Debug.Log("Creating basic scene without AR Foundation");

            // Main Camera
            GameObject cameraObj = new GameObject("Main Camera");
            Camera camera = cameraObj.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.tag = "MainCamera";

            // Demo walls parent
            GameObject demoWallsParent = new GameObject("Demo Walls");

            // Create demo walls
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

            // Add DemoWallSegmentation if available
            try
            {
                  var demoSegType = GetTypeFromName("DemoWallSegmentation");
                  if (demoSegType != null)
                        cameraObj.AddComponent(demoSegType);
                  else if (typeof(DemoWallSegmentation) != null)
                        cameraObj.AddComponent<DemoWallSegmentation>();
            }
            catch
            {
                  Debug.LogWarning("DemoWallSegmentation component not available");
            }

            // Try to add WallSegmentation in demo mode
            try
            {
                  var wallSeg = cameraObj.AddComponent<WallSegmentation>();
                  if (wallSeg != null)
                  {
                        // Set to demo mode
                        var modeField = typeof(WallSegmentation).GetField("currentMode",
                            BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                        if (modeField != null)
                        {
                              // Get the enum value for "Demo" mode (typically 0)
                              var modeType = modeField.FieldType;
                              var demoMode = Enum.GetValues(modeType).GetValue(0);
                              modeField.SetValue(wallSeg, demoMode);
                        }

                        Debug.Log("Added WallSegmentation in demo mode");
                  }
            }
            catch (Exception ex)
            {
                  Debug.LogWarning($"Could not add WallSegmentation: {ex.Message}");
            }
      }

      /// <summary>
      /// Creates UI canvas with debug visualization
      /// </summary>
      private static void CreateUI()
      {
            try
            {
                  // Create canvas
                  GameObject canvasObj = new GameObject("UI Canvas");
                  var canvasType = GetTypeFromName("UnityEngine.Canvas");
                  var canvas = canvasObj.AddComponent(canvasType);

                  // Set render mode to screen space
                  var renderModeProperty = canvasType.GetProperty("renderMode");
                  if (renderModeProperty != null)
                        renderModeProperty.SetValue(canvas, 1); // ScreenSpaceOverlay

                  // Add canvas scaler
                  var canvasScalerType = GetTypeFromName("UnityEngine.UI.CanvasScaler");
                  if (canvasScalerType != null)
                  {
                        var scaler = canvasObj.AddComponent(canvasScalerType);
                        var uiScaleModeProperty = canvasScalerType.GetProperty("uiScaleMode");
                        if (uiScaleModeProperty != null)
                              uiScaleModeProperty.SetValue(scaler, 1); // ScaleWithScreenSize
                  }

                  // Add graphic raycaster
                  var graphicRaycasterType = GetTypeFromName("UnityEngine.UI.GraphicRaycaster");
                  if (graphicRaycasterType != null)
                        canvasObj.AddComponent(graphicRaycasterType);

                  // Create debug visualization panel
                  GameObject debugPanel = new GameObject("Segmentation Debug View");
                  debugPanel.transform.SetParent(canvasObj.transform, false);

                  // Add RectTransform
                  var rectTransformType = GetTypeFromName("UnityEngine.RectTransform");
                  var rectTransform = debugPanel.AddComponent(rectTransformType);

                  // Configure position (top right corner)
                  var anchorMinProperty = rectTransformType.GetProperty("anchorMin");
                  if (anchorMinProperty != null)
                        anchorMinProperty.SetValue(rectTransform, new Vector2(0.7f, 0.7f));

                  var anchorMaxProperty = rectTransformType.GetProperty("anchorMax");
                  if (anchorMaxProperty != null)
                        anchorMaxProperty.SetValue(rectTransform, new Vector2(0.98f, 0.98f));

                  var offsetMinProperty = rectTransformType.GetProperty("offsetMin");
                  if (offsetMinProperty != null)
                        offsetMinProperty.SetValue(rectTransform, Vector2.zero);

                  var offsetMaxProperty = rectTransformType.GetProperty("offsetMax");
                  if (offsetMaxProperty != null)
                        offsetMaxProperty.SetValue(rectTransform, Vector2.zero);

                  // Add RawImage
                  var rawImageType = GetTypeFromName("UnityEngine.UI.RawImage");
                  if (rawImageType != null)
                  {
                        debugPanel.AddComponent(rawImageType);
                        Debug.Log("Added RawImage for segmentation visualization");

                        // Try to connect to WallSegmentation if available
                        var wallSeg = GameObject.FindFirstObjectByType<WallSegmentation>();
                        if (wallSeg != null)
                        {
                              // Find debugImage field in WallSegmentation
                              var debugImageField = typeof(WallSegmentation).GetField("debugImage",
                                  BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                              if (debugImageField != null)
                              {
                                    debugImageField.SetValue(wallSeg, debugPanel.GetComponent(rawImageType));
                                    Debug.Log("Connected RawImage to WallSegmentation.debugImage");
                              }
                        }
                  }

                  // Create a color picker panel
                  GameObject colorPanel = new GameObject("Color Panel");
                  colorPanel.transform.SetParent(canvasObj.transform, false);

                  // Add RectTransform
                  var panelRect = colorPanel.AddComponent(rectTransformType);

                  // Configure position (bottom of screen)
                  if (anchorMinProperty != null)
                        anchorMinProperty.SetValue(panelRect, new Vector2(0, 0));

                  if (anchorMaxProperty != null)
                        anchorMaxProperty.SetValue(panelRect, new Vector2(1, 0.2f));

                  if (offsetMinProperty != null)
                        offsetMinProperty.SetValue(panelRect, Vector2.zero);

                  if (offsetMaxProperty != null)
                        offsetMaxProperty.SetValue(panelRect, Vector2.zero);

                  Debug.Log("UI canvas setup complete");
            }
            catch (Exception ex)
            {
                  Debug.LogError($"Error creating UI: {ex.Message}\n{ex.StackTrace}");
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

                  // Try looking in all loaded assemblies
                  foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                  {
                        type = assembly.GetType(typeName);
                        if (type != null) return type;

                        // Try without assembly qualifier
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