using UnityEngine;
using UnityEditor;
using System.IO;

#if UNITY_EDITOR
/// <summary>
/// Editor utility to create AR Plane prefabs for testing
/// </summary>
public static class CreateARPlanePrefab
{
    [MenuItem("Tools/AR Wall Painting/2. Create AR Plane Prefab", false, 20)]
    public static void CreateTestARPlanePrefab()
    {
        // Create directory if it doesn't exist
        if (!Directory.Exists("Assets/Prefabs"))
        {
            Directory.CreateDirectory("Assets/Prefabs");
        }
        
        if (!Directory.Exists("Assets/Resources"))
        {
            Directory.CreateDirectory("Assets/Resources");
        }
        
        // Create a quad for the plane
        GameObject planeObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
        planeObject.name = "AR Plane";
        
        // Set default scale and orientation
        planeObject.transform.localScale = new Vector3(1f, 1f, 1f);
        planeObject.transform.rotation = Quaternion.Euler(0, 0, 0);
        
        // Create a material with transparency
        Material planeMaterial = new Material(Shader.Find("Standard"));
        planeMaterial.SetFloat("_Mode", 3); // Transparent mode
        planeMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        planeMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        planeMaterial.SetInt("_ZWrite", 0);
        planeMaterial.DisableKeyword("_ALPHATEST_ON");
        planeMaterial.EnableKeyword("_ALPHABLEND_ON");
        planeMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        planeMaterial.renderQueue = 3000;
        
        // Set default color (blue with transparency)
        planeMaterial.color = new Color(0.2f, 0.6f, 1.0f, 0.5f);
        
        // Apply material to the plane
        MeshRenderer renderer = planeObject.GetComponent<MeshRenderer>();
        renderer.material = planeMaterial;
        
        // Add AR Plane Mockup component
        ARPlaneMockup planeMockup = planeObject.AddComponent<ARPlaneMockup>();
        
        // Save material as asset
        AssetDatabase.CreateAsset(planeMaterial, "Assets/Resources/ARPlaneMaterial.mat");
        
        // Save the prefab
        string prefabPath = "Assets/Resources/AR Plane.prefab";
        
#if UNITY_2018_3_OR_NEWER
        // Modern prefab workflow
        PrefabUtility.SaveAsPrefabAsset(planeObject, prefabPath);
#else
        // Legacy prefab workflow
        PrefabUtility.CreatePrefab(prefabPath, planeObject);
#endif
        
        // Destroy the scene instance
        Object.DestroyImmediate(planeObject);
        
        // Refresh the asset database
        AssetDatabase.Refresh();
        
        // Log success
        Debug.Log("AR Plane prefab created and saved to: " + prefabPath);
        EditorUtility.DisplayDialog("Success", "AR Plane prefab created successfully!", "OK");
    }
}
#endif