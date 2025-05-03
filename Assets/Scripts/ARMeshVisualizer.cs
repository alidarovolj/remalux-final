using UnityEngine;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// Компонент для визуализации AR меша
/// </summary>
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class ARMeshVisualizer : MonoBehaviour
{
    [SerializeField] private Color meshColor = new Color(0.5f, 1f, 0.5f, 0.3f); // Зеленый полупрозрачный
    
    private ARMeshManager meshManager;
    private MeshRenderer meshRenderer;
    
    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        
        // Ищем ARMeshManager в сцене
        meshManager = FindObjectOfType<ARMeshManager>();
    }
    
    private void Start()
    {
        // Настраиваем материал для визуализации меша
        if (meshRenderer != null && meshRenderer.material != null)
        {
            meshRenderer.material.color = meshColor;
            
            // Настраиваем свойства для полупрозрачности
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }
    }
} 