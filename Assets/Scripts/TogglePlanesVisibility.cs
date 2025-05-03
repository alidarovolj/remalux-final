using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Компонент для управления видимостью AR плоскостей и режимами их отображения
/// </summary>
public class TogglePlanesVisibility : MonoBehaviour
{
    [Header("AR Components")]
    [SerializeField] private ARPlaneManager planeManager;
    
    [Header("UI Elements")]
    [SerializeField] private Button toggleVisibilityButton;
    [SerializeField] private Button toggleExactPlacementButton;
    [SerializeField] private Button toggleExtendWallsButton;
    [SerializeField] private Text statusText;
    
    private bool planesVisible = true;
    private bool usingExactPlacement = true;
    private bool extendingWalls = false;
    
    private void Awake()
    {
        // Автоматически находим компоненты, если они не назначены
        if (planeManager == null)
            planeManager = FindObjectOfType<ARPlaneManager>();
            
        if (toggleVisibilityButton == null && GetComponent<Button>() != null)
            toggleVisibilityButton = GetComponent<Button>();
    }
    
    private void Start()
    {
        if (planeManager == null)
        {
            planeManager = FindObjectOfType<ARPlaneManager>();
        }
        
        // Начальное состояние - плоскости видимы
        SetPlanesVisibility(true);
        planesVisible = true;
        
        // Настраиваем кнопку переключения видимости
        if (toggleVisibilityButton != null)
        {
            toggleVisibilityButton.onClick.AddListener(ToggleVisibility);
        }
        
        // Настраиваем кнопку переключения режима размещения
        if (toggleExactPlacementButton != null)
        {
            toggleExactPlacementButton.onClick.AddListener(ToggleExactPlacement);
        }
        
        // Настраиваем кнопку переключения расширения стен
        if (toggleExtendWallsButton != null)
        {
            toggleExtendWallsButton.onClick.AddListener(ToggleExtendWalls);
        }
        
        UpdateStatusText();
        
        Debug.Log("TogglePlanesVisibility: установлена начальная видимость плоскостей: ВИДИМЫЕ");
    }
    
    /// <summary>
    /// Переключает видимость AR плоскостей
    /// </summary>
    public void ToggleVisibility()
    {
        if (planeManager == null) return;
        
        planesVisible = !planesVisible;
        
        foreach (var plane in planeManager.trackables)
        {
            foreach (var visualizer in plane.GetComponentsInChildren<MeshRenderer>())
            {
                visualizer.enabled = planesVisible;
            }
        }
        
        UpdateStatusText();
    }
    
    /// <summary>
    /// Переключает режим точного размещения AR плоскостей
    /// </summary>
    public void ToggleExactPlacement()
    {
        if (planeManager == null) return;
        
        usingExactPlacement = !usingExactPlacement;
        
        foreach (var plane in planeManager.trackables)
        {
            foreach (var visualizer in plane.GetComponentsInChildren<ARPlaneVisualizer>())
            {
                visualizer.ToggleExactPlacement();
            }
        }
        
        UpdateStatusText();
    }
    
    /// <summary>
    /// Переключает режим расширения стен
    /// </summary>
    public void ToggleExtendWalls()
    {
        if (planeManager == null) return;
        
        extendingWalls = !extendingWalls;
        
        foreach (var plane in planeManager.trackables)
        {
            foreach (var visualizer in plane.GetComponentsInChildren<ARPlaneVisualizer>())
            {
                visualizer.ToggleExtendWalls();
            }
        }
        
        UpdateStatusText();
    }
    
    /// <summary>
    /// Устанавливает видимость плоскостей
    /// </summary>
    public void SetPlanesVisibility(bool visible)
    {
        if (planeManager == null) return;
        
        planesVisible = visible;
        
        // Обновляем видимость всех существующих плоскостей
        foreach (var plane in planeManager.trackables)
        {
            // Включаем/выключаем визуализатор
            foreach (var visualizer in plane.GetComponentsInChildren<MeshRenderer>())
            {
                visualizer.enabled = visible;
            }
        }
        
        // Также настраиваем обработчик событий для новых плоскостей
        planeManager.planesChanged -= OnPlanesChanged;
        if (visible)
        {
            planeManager.planesChanged += OnPlanesChanged;
        }
        
        Debug.Log($"Видимость AR плоскостей: {(visible ? "включена" : "выключена")}");
    }
    
    private void OnPlanesChanged(ARPlanesChangedEventArgs args)
    {
        // Устанавливаем видимость для новых плоскостей
        foreach (var plane in args.added)
        {
            foreach (var visualizer in plane.GetComponentsInChildren<MeshRenderer>())
            {
                visualizer.enabled = planesVisible;
            }
        }
    }
    
    /// <summary>
    /// Обновляет текст статуса
    /// </summary>
    private void UpdateStatusText()
    {
        if (statusText != null)
        {
            string placementMode = usingExactPlacement ? "Точное" : "Смещенное";
            string wallMode = extendingWalls ? "Расширенные" : "Реальные";
            
            statusText.text = $"Плоскости: {(planesVisible ? "Видимы" : "Скрыты")}\n" +
                             $"Размещение: {placementMode}\n" +
                             $"Стены: {wallMode}";
        }
    }
    
    private void OnDestroy()
    {
        if (planeManager != null)
        {
            planeManager.planesChanged -= OnPlanesChanged;
        }
        
        if (toggleVisibilityButton != null)
        {
            toggleVisibilityButton.onClick.RemoveListener(ToggleVisibility);
        }
        
        if (toggleExactPlacementButton != null)
        {
            toggleExactPlacementButton.onClick.RemoveListener(ToggleExactPlacement);
        }
        
        if (toggleExtendWallsButton != null)
        {
            toggleExtendWallsButton.onClick.RemoveListener(ToggleExtendWalls);
        }
    }
} 