using UnityEngine;

/// <summary>
/// Временное решение для принудительного включения демо-режима сегментации стен
/// </summary>
public class ForceDemoMode : MonoBehaviour
{
    [SerializeField] private WallSegmentation wallSegmentation;
    [SerializeField] private bool enableOnAwake = true;
    [SerializeField] private bool enableOnStart = true;
    
    private void Awake()
    {
        // Найти компонент WallSegmentation если он не назначен
        if (wallSegmentation == null)
        {
            wallSegmentation = FindObjectOfType<WallSegmentation>();
            
            if (wallSegmentation == null)
            {
                wallSegmentation = GetComponent<WallSegmentation>();
            }
        }
        
        if (enableOnAwake && wallSegmentation != null)
        {
            Debug.Log("Принудительное включение демо-режима сегментации стен (Awake)");
            wallSegmentation.SwitchMode(WallSegmentation.SegmentationMode.Demo);
        }
    }
    
    private void Start()
    {
        if (enableOnStart && wallSegmentation != null)
        {
            Debug.Log("Принудительное включение демо-режима сегментации стен (Start)");
            wallSegmentation.SwitchMode(WallSegmentation.SegmentationMode.Demo);
        }
        else if (wallSegmentation == null)
        {
            Debug.LogWarning("Не удалось найти компонент WallSegmentation");
        }
    }
    
    /// <summary>
    /// Принудительно включает демо-режим сегментации стен
    /// </summary>
    public void EnableDemoMode()
    {
        if (wallSegmentation != null)
        {
            Debug.Log("Принудительное включение демо-режима сегментации стен (вручную)");
            wallSegmentation.SwitchMode(WallSegmentation.SegmentationMode.Demo);
        }
        else
        {
            Debug.LogWarning("Не удалось найти компонент WallSegmentation");
        }
    }
} 