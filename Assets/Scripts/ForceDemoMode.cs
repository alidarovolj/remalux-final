using UnityEngine;

/// <summary>
/// Временное решение для принудительного включения демо-режима сегментации стен
/// </summary>
public class ForceDemoMode : MonoBehaviour
{
    [SerializeField] private WallSegmentation wallSegmentation;
    [SerializeField] private bool enableOnAwake = true;
    [SerializeField] private bool enableOnStart = true;
    [SerializeField] private bool onlyIfModelLoadFailed = true; // Переключаем в демо-режим только при ошибке загрузки модели
    
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
            if (onlyIfModelLoadFailed && wallSegmentation.IsUsingDemoMode())
            {
                // Переключаем на демо-режим только если загрузка модели не удалась
                Debug.Log("Принудительное включение демо-режима сегментации стен (Awake) - модель не загружена");
                wallSegmentation.SwitchMode(WallSegmentation.SegmentationMode.Demo);
            }
            else if (!onlyIfModelLoadFailed)
            {
                Debug.Log("Принудительное включение демо-режима сегментации стен (Awake)");
                wallSegmentation.SwitchMode(WallSegmentation.SegmentationMode.Demo);
            }
        }
    }
    
    private void Start()
    {
        if (wallSegmentation == null)
        {
            Debug.LogWarning("Не удалось найти компонент WallSegmentation");
            return;
        }
        
        if (enableOnStart)
        {
            if (onlyIfModelLoadFailed && wallSegmentation.IsUsingDemoMode())
            {
                // Переключаем на демо-режим только если загрузка модели не удалась
                Debug.Log("Принудительное включение демо-режима сегментации стен (Start) - модель не загружена");
                wallSegmentation.SwitchMode(WallSegmentation.SegmentationMode.Demo);
            }
            else if (!onlyIfModelLoadFailed)
            {
                Debug.Log("Принудительное включение демо-режима сегментации стен (Start)");
                wallSegmentation.SwitchMode(WallSegmentation.SegmentationMode.Demo);
            }
        }
    }
    
    /// <summary>
    /// Принудительно включает демо-режим сегментации стен
    /// </summary>
    public void EnableDemoMode()
    {
        if (wallSegmentation == null)
        {
            wallSegmentation = FindObjectOfType<WallSegmentation>();
            
            if (wallSegmentation == null)
            {
                Debug.LogError("Не удалось найти компонент WallSegmentation для включения демо-режима");
                return;
            }
        }
        
        wallSegmentation.SwitchMode(WallSegmentation.SegmentationMode.Demo);
        Debug.Log("Демо-режим сегментации стен включен вручную");
    }
} 