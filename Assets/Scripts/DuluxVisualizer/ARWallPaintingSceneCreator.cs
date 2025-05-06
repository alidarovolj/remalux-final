using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Static utility class to create an AR Wall Painting scene
/// </summary>
public static class ARWallPaintingSceneCreator
{
      /// <summary>
      /// Creates a new AR Wall Painting scene with all necessary components
      /// </summary>
      public static void CreateARWallPaintingScene()
      {
            // Create a new empty scene
            Scene newScene = SceneManager.CreateScene("ARWallPainting");
            SceneManager.SetActiveScene(newScene);

            // Create the AR scene
            ARWallPaintingCreator.CreateScene();

            Debug.Log("AR Wall Painting scene created successfully!");
      }
}