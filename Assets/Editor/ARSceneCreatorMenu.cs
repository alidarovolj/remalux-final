using UnityEngine;
using UnityEditor;
using System.Reflection;
using System;

#if UNITY_EDITOR
/// <summary>
/// Provides a unified menu for all AR Wall Painting scene creation options
/// </summary>
public static class ARSceneCreatorMenu
{
      [MenuItem("Tools/AR Wall Painting/🆕 Create All-in-One AR Scene", false, 0)]
      public static void CreateCompleteARScene()
      {
            // First try to look in DuluxVisualizer namespace
            try
            {
                  // Call ARSceneBuilder.CreateScene
                  var arSceneBuilderType = GetTypeByExactName("ARSceneBuilder");
                  if (arSceneBuilderType != null)
                  {
                        var createSceneMethod = arSceneBuilderType.GetMethod("CreateScene",
                            BindingFlags.Public | BindingFlags.Static);

                        if (createSceneMethod != null)
                        {
                              try
                              {
                                    createSceneMethod.Invoke(null, null);
                                    Debug.Log("Successfully created AR scene using ARSceneBuilder");
                                    return;
                              }
                              catch (Exception ex)
                              {
                                    Debug.LogError($"Error creating scene with ARSceneBuilder: {ex.Message}");
                              }
                        }
                  }

                  // Fallback to CreateSceneWithFixedSegmentation
                  var fixedSegType = GetTypeByExactName("CreateSceneWithFixedSegmentation");
                  if (fixedSegType != null)
                  {
                        var createFixedMethod = fixedSegType.GetMethod("CreateARSceneWithFixedSegmentation",
                            BindingFlags.Public | BindingFlags.Static);

                        if (createFixedMethod != null)
                        {
                              try
                              {
                                    createFixedMethod.Invoke(null, null);
                                    Debug.Log("Successfully created AR scene with fixed segmentation");
                                    return;
                              }
                              catch (Exception ex)
                              {
                                    Debug.LogError($"Error creating scene with CreateSceneWithFixedSegmentation: {ex.Message}");
                              }
                        }
                  }
            }
            catch (Exception ex)
            {
                  Debug.LogError($"Error searching for scene creator classes: {ex.Message}");
            }

            // If both methods fail, show an error
            EditorUtility.DisplayDialog(
                "AR Scene Creation Failed",
                "Failed to create AR scene using either ARSceneBuilder or CreateSceneWithFixedSegmentation.\n\n" +
                "Please check the console for error details.\n\n" +
                "Make sure ARSceneBuilder.cs and CreateSceneWithFixedSegmentation.cs exist in the project.",
                "OK");
      }

      [MenuItem("Tools/AR Wall Painting/⚙️ Settings and Documentation...", false, 100)]
      public static void ShowDocumentation()
      {
            EditorUtility.DisplayDialog(
                "AR Wall Painting Documentation",
                "AR Wall Painting Scene Creation Options:\n\n" +
                "1. 🆕 Create All-in-One AR Scene\n" +
                "   Creates a complete AR scene with wall segmentation, automatic AR package detection, and UI setup.\n\n" +
                "2. Create AR Wall Painting Scene\n" +
                "   Uses ARSceneBuilder to create an AR scene optimized for newer AR Foundation versions.\n\n" +
                "3. Create Scene With Fixed Segmentation\n" +
                "   Uses CreateSceneWithFixedSegmentation which has additional compatibility fixes.\n\n" +
                "All scene creators attempt to work with or without AR Foundation packages installed.",
                "Close"
            );
      }

      // Helper method to find a type by exact name
      private static Type GetTypeByExactName(string typeName)
      {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                  foreach (var type in assembly.GetTypes())
                  {
                        if (type.Name == typeName)
                        {
                              Debug.Log($"Found {typeName} in assembly {assembly.GetName().Name}");
                              return type;
                        }
                  }
            }
            return null;
      }
}
#endif